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

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace LearningCoreWebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtTokenHelper _jwt;
        private readonly ILogger<ValuesController> _logger;
        public ValuesController(AppDbContext context, JwtTokenHelper jwt, ILogger<ValuesController> logger)
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
            // 1. Basic Validation Check
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Email and Password are required." });
            }

            _logger.LogInformation("Registration attempt for email: {Email}", request.Email);

            try
            {
                // 2. Email ko normalize karein (Trim aur Lowercase)
                var normalizedEmail = request.Email.Trim().ToLower();

                // 3. User existence check
                var exists = await _context.Users.AnyAsync(x => x.Email == normalizedEmail);
                if (exists)
                {
                    _logger.LogWarning("Registration failed: User {Email} already exists", normalizedEmail);
                    return Conflict(new { message = "User with this email already exists" }); // Conflict (409) behtar status code hai
                }

                // 4. Create User object
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

                // 5. Success Response
                return StatusCode(201, new { message = "User registered successfully" }); // 201 Created status code
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

                // 1. Generic message for security
                if (user == null || !PasswordHelper.Verify(request.Password, user.PasswordHash))
                {
                    return Unauthorized(new { message = "Invalid Email or Password" });
                }

                var accessToken = _jwt.GenerateToken(user.Email);
                var refreshTokenObj = _jwt.GenerateRefreshToken();

                // 3. Cleanup: Purane expired tokens remove karein
                var expiredTokens = _context.RefreshTokenConfiguration
                                    .Where(t => t.UserId == user.Id && t.RefreshTokenExpiryDate < DateTime.UtcNow);
                _context.RefreshTokenConfiguration.RemoveRange(expiredTokens);

                // 4. Save new refresh token
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

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true, // Production mein true rakhein
                    SameSite = SameSiteMode.None,
                    Expires = refreshTokenObj.RefreshTokenExpiryDate,
                    Path = "/" // Yeh line add karein taake har API call mein cookie jaye
                };
                Response.Cookies.Append("refreshToken", refreshTokenObj.RefreshToken, cookieOptions);

                return Ok(new { accessToken });
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
                // 1. Cookie se Refresh Token nikalein
                if (Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
                {
                    // 2. DB mein us token ko dhoondein aur revoke/delete karein
                    var storedToken = await _context.RefreshTokenConfiguration
                        .FirstOrDefaultAsync(x => x.RefreshToken == refreshToken);

                    if (storedToken != null)
                    {
                        // Option A: Token delete kar dein (Cleanest way)
                        _context.RefreshTokenConfiguration.Remove(storedToken);

                        // Option B: Sirf Revoke mark karein (Agar record rakhna ho)
                        // storedToken.IsRevoked = true;
                        // _context.Update(storedToken);

                        await _context.SaveChangesAsync();
                    }
                }

                // 3. Browser se Refresh Token ki Cookie remove karein
                Response.Cookies.Delete("refreshToken", new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Path = "/" // Yeh line add karein taake har API call mein cookie jaye
                });

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
                return StatusCode(403, "Authorization header missing.");

            if (!Request.Cookies.TryGetValue("refreshToken", out var existingRefreshToken))
                return StatusCode(403, "Refresh token cookie missing.");

            string accessToken = authHeader.ToString().Replace("Bearer", "", StringComparison.OrdinalIgnoreCase).Trim();

            try
            {
                // 2. Expired Access Token se User ki details nikalna (Helper use karte hue)
                var principal = _jwt.GetPrincipalFromExpiredToken(accessToken);
                var email = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(email))
                    return Unauthorized("Invalid Token Claims");

                // 3. DB se Refresh Token verify karna
                var storedToken = await _context.RefreshTokenConfiguration
                    .Include(u => u.User)
                    .FirstOrDefaultAsync(x => x.RefreshToken == existingRefreshToken.ToString()
                                         && x.User.Email == email);

                if (storedToken == null || storedToken.IsRevoked)
                    return StatusCode(403, "Invalid Request");

                if (storedToken.RefreshTokenExpiryDate < DateTime.UtcNow)
                    return StatusCode(403, "Token expired.");

                // 4. Purane tokens ko revoke ya delete karein (Multiple devices ke liye sirf isko revoke karein)
                storedToken.IsRevoked = true;
                _context.Update(storedToken);

                // 5. Naye Tokens Generate karein
                var newAccessToken = _jwt.GenerateToken(email);
                var newRefreshTokenObj = _jwt.GenerateRefreshToken();

                // 6. DB mein naya record save karein
                _context.RefreshTokenConfiguration.Add(new RefreshTokenConfiguration
                {
                    UserId = storedToken.UserId,
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshTokenObj.RefreshToken,
                    RefreshTokenExpiryDate = newRefreshTokenObj.RefreshTokenExpiryDate,
                    RefreshTokenCreatedDate = DateTime.UtcNow,
                    IsRevoked = false
                });

                await _context.SaveChangesAsync();

                // 7. Security: Naya refresh token cookie mein update karein
                Response.Cookies.Append("refreshToken", newRefreshTokenObj.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = newRefreshTokenObj.RefreshTokenExpiryDate,
                    Path = "/" // Yeh line add karein taake har API call mein cookie jaye
                });

                return Ok(new { accessToken = newAccessToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh failed");
                return StatusCode(403, "Invalid Credentials");
            }
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("admin")]
        public IActionResult AdminOnly()
        {
            return Ok("Admin access granted");
        }

    }
}
