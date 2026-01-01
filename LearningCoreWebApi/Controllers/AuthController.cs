using LearningCoreWebApi.Data;
using LearningCoreWebApi.Helpers;
using LearningCoreWebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LearningCoreWebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtTokenHelper _jwt;
        private readonly ILogger<AuthController> _logger;
        public AuthController(AppDbContext context, JwtTokenHelper jwt, ILogger<AuthController> logger)
        {
            _context = context;
            _jwt = jwt;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var userName = User.Identity?.Name;
            return Ok($"JWT working for {userName}");
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Models.RegisterRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Email and Password are required." });
            }

            _logger.LogInformation("Registration attempt for email: {Email}", request.Email);

            try
            {
                var normalizedEmail = request.Email.Trim().ToLower();

                var exists = await _context.Users.AnyAsync(x => x.Email == normalizedEmail);
                if (exists)
                {
                    _logger.LogWarning("Registration failed: User {Email} already exists", normalizedEmail);
                    return Conflict(new { message = "User with this email already exists" });
                }

                var user = new User
                {
                    Username = request.Username,
                    Email = normalizedEmail,
                    PasswordHash = PasswordHelper.Hash(request.Password),
                    CreatedDate = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {Email} registered successfully with ID: {UserId}", user.Email, user.Id);

                return StatusCode(201, new { message = "User registered successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while registering user: {Email}", request.Email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == request.Email);

                if (user == null || !PasswordHelper.Verify(request.Password, user.PasswordHash))
                {
                    return Unauthorized(new { message = "Invalid Email or Password" });
                }

                var accessToken = _jwt.GenerateToken(user.Email);
                var refreshTokenObj = _jwt.GenerateRefreshToken();

                var expiredTokens = _context.RefreshTokenConfiguration
                                    .Where(t => t.UserId == user.Id && t.RefreshTokenExpiryDate < DateTime.UtcNow);
                _context.RefreshTokenConfiguration.RemoveRange(expiredTokens);

                // Save new refresh token in DB
                _context.RefreshTokenConfiguration.Add(new RefreshTokenConfiguration
                {
                    UserId = user.Id,
                    AccessToken = accessToken,
                    RefreshToken = refreshTokenObj.RefreshToken,
                    RefreshTokenExpiryDate = refreshTokenObj.RefreshTokenExpiryDate,
                    RefreshTokenCreatedDate = DateTime.UtcNow,
                    IsRevoked = false
                });

                await _context.SaveChangesAsync();

                // *** CHANGE 1: Return Refresh Token in the response body (not cookie) ***
                return Ok(new { accessToken, refreshToken = refreshTokenObj.RefreshToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error occurred");
                return StatusCode(500, new { message = "An internal error occurred" });
            }
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // *** CHANGE 2: Read Refresh Token from Header, not Cookie ***
                if (!Request.Headers.TryGetValue("X-Refresh-Token", out var refreshTokenHeaderValue))
                {
                    return StatusCode(403, new { message = "Refresh token header missing." });
                }
                var refreshToken = refreshTokenHeaderValue.FirstOrDefault();

                // 2. DB mein us token ko dhoondein aur revoke/delete karein
                var storedToken = await _context.RefreshTokenConfiguration
                    .FirstOrDefaultAsync(x => x.RefreshToken == refreshToken);

                if (storedToken != null)
                {
                    _context.RefreshTokenConfiguration.Remove(storedToken);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("User logged out and refresh token revoked.");

                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("refreshToken")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken()
        {
            // 1. Headers se tokens nikalna
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                return StatusCode(403, new { message = "Authorization header missing." });

            // *** CHANGE 3: Read Refresh Token from Header, not Cookie/mixed logic ***
            if (!Request.Headers.TryGetValue("X-Refresh-Token", out var existingRefreshTokenHeaderValue))
                return StatusCode(403, new { message = "Refresh token header missing." });

            string accessToken = authHeader.ToString().Replace("Bearer", "", StringComparison.OrdinalIgnoreCase).Trim();

            try
            {
                // 2. Expired Access Token se User ki details nikalna
                var principal = _jwt.GetPrincipalFromExpiredToken(accessToken);
                var email = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(email))
                    return Unauthorized("Invalid Token Claims");

                // 3. DB se Refresh Token verify karna
                var storedToken = await _context.RefreshTokenConfiguration
                    .Include(u => u.User)
                    .FirstOrDefaultAsync(x => x.RefreshToken == existingRefreshTokenHeaderValue.ToString()
                                         && x.User.Email == email);

                if (storedToken == null || storedToken.IsRevoked || storedToken.RefreshTokenExpiryDate < DateTime.UtcNow)
                    return StatusCode(403, new { message = "Invalid or expired refresh token." });

                // 4. Purane tokens ko revoke ya delete karein (Multiple devices ke liye sirf isko revoke karein)
                storedToken.IsRevoked = true;
                _context.Update(storedToken);

                // 5. Token Rotation (Generate new access and refresh tokens)
                var newAccessToken = _jwt.GenerateToken(email);
                var newRefreshTokenObj = _jwt.GenerateRefreshToken();

                // 6. Database mein Naya Token Record Add Karein
                _context.RefreshTokenConfiguration.Add(new RefreshTokenConfiguration
                {
                    UserId = storedToken.UserId, // Purane user ki ID use karein
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshTokenObj.RefreshToken,
                    RefreshTokenExpiryDate = newRefreshTokenObj.RefreshTokenExpiryDate,
                    RefreshTokenCreatedDate = DateTime.UtcNow,
                    IsRevoked = false // Naya token revoke nahi hai
                });

                await _context.SaveChangesAsync();

                // *** CHANGE 4: Return new Access and Refresh tokens in the body ***
                return Ok(new { accessToken = newAccessToken, refreshToken = newRefreshTokenObj.RefreshToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh: {Message}", ex.Message);
                return StatusCode(500, new { message = "Internal server error during token refresh" });
            }
        }
    }
}
