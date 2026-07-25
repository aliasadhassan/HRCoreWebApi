using Azure.Core;
using HR.Identity.API.Configuration;
using HR.Identity.API.Data;
using HR.Identity.API.Models;
using HR.Identity.API.Services;
using HR.Shared.Library.Events;
using HR.Shared.Library.Helpers;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;

namespace HR.Identity.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(
        AppDbContext context,
        JwtTokenHelper jwt,
        IEmailService emailService,
        EmailTemplatesHelper emailTemplatesHelper,
        IOptions<AuthSettings> authSettingsConfig,
        IPublishEndpoint publishEndpoint,
        MicrosoftGraphService _graphService,
        ILogger<AuthController> logger) : ControllerBase
    {

        [HttpGet]
        public IActionResult Get()
        {
            var userName = User.Identity?.Name;
            return Ok($"JWT working for {userName}");
        }
        // 🛠️ Helper Method: HttpOnly Cookie set karne ke liye
        private void SetRefreshTokenCookie(string refreshToken, DateTime expires)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,                          // Hacker ka JavaScript isko read nahi kar sakega
                Secure = true,                            // Sirf HTTPS par chalega
                SameSite = SameSiteMode.Strict,           // CSRF attack se bachayega
                Expires = expires,                        // Cookie ki expiry date (7 days)
                IsEssential = true
            };
            Response.Cookies.Append("X-Refresh-Token", refreshToken, cookieOptions);
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Models.RegisterRequest model)
        {
            if (model == null || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
            {
                return BadRequest(new { message = "Email and Password are required." });
            }

            logger.LogInformation("Registration attempt for email: {Email}", model.Email);

            try
            {
                var normalizedEmail = model.Email.Trim().ToLower();

                var exists = await context.Users.AnyAsync(x => x.Email == normalizedEmail);
                if (exists)
                {
                    logger.LogWarning("Registration failed: User {Email} already exists", normalizedEmail);
                    return Conflict(new { message = "User with this email already exists" });
                }

                var user = new User
                {
                    Username = model.Username,
                    Email = normalizedEmail,
                    PasswordHash = PasswordHelper.Hash(model.Password),
                    CreatedDate = DateTime.UtcNow
                };

                context.Users.Add(user);
                await context.SaveChangesAsync();

                // RabbitMQ ko message phenkein
                await publishEndpoint.Publish(new UserCreatedEvent(user.Id, user.Username, user.Email));

                logger.LogInformation("User {Email} registered successfully with ID: {UserId}", user.Email, user.Id);

                return StatusCode(201, new { message = "User registered successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while registering user: {Email}", model.Email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [AllowAnonymous]
        [HttpPost("sso/callback")]
        public async Task<IActionResult> SsoCallback([FromBody] SsoLoginRequest request)
        {
            try
            {
                var msUser = await _graphService.GetUserFromTokenAsync(request.AccessToken);
                if (msUser == null)
                {
                    return Unauthorized(new { message = "Invalid Microsoft token" });
                }

                var normalizedEmail = msUser.Email.Trim().ToLower();
                var user = await context.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail);

                if (user == null)
                {
                    user = new User
                    {
                        Username = msUser.DisplayName,
                        Email = normalizedEmail,
                        // SSO users password se login nahi karte, is liye random unusable hash
                        PasswordHash = PasswordHelper.Hash(Guid.NewGuid().ToString()),
                        CreatedDate = DateTime.UtcNow
                    };

                    context.Users.Add(user);
                    await context.SaveChangesAsync();

                    await publishEndpoint.Publish(new UserCreatedEvent(user.Id, user.Username, user.Email));

                    logger.LogInformation("New user auto-provisioned via SSO: {Email}", user.Email);
                }

                var accessToken = jwt.GenerateToken(user.Email);
                var refreshTokenObj = jwt.GenerateRefreshToken();

                var expiredTokens = context.RefreshTokenConfiguration
                    .Where(t => t.UserId == user.Id && t.RefreshTokenExpiryDate < DateTime.UtcNow);
                context.RefreshTokenConfiguration.RemoveRange(expiredTokens);

                context.RefreshTokenConfiguration.Add(new RefreshTokenConfiguration
                {
                    UserId = user.Id,
                    AccessToken = accessToken,
                    RefreshToken = refreshTokenObj.RefreshToken,
                    RefreshTokenExpiryDate = refreshTokenObj.RefreshTokenExpiryDate,
                    RefreshTokenCreatedDate = DateTime.UtcNow,
                    IsRevoked = false
                });

                await context.SaveChangesAsync();

                SetRefreshTokenCookie(refreshTokenObj.RefreshToken, refreshTokenObj.RefreshTokenExpiryDate);

                logger.LogInformation("SSO login successful for {Email}", user.Email);

                return Ok(new { accessToken });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SSO login error occurred");
                return StatusCode(500, new { message = "An internal error occurred during SSO login" });
            }
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            try
            {
                var user = await context.Users.FirstOrDefaultAsync(x => x.Email == model.Email);

                if (user == null || !PasswordHelper.Verify(model.Password, user.PasswordHash))
                {
                    return Unauthorized(new { message = "Invalid Email or Password" });
                }

                var accessToken = jwt.GenerateToken(user.Email);
                var refreshTokenObj = jwt.GenerateRefreshToken();

                var expiredTokens = context.RefreshTokenConfiguration
                                    .Where(t => t.UserId == user.Id && t.RefreshTokenExpiryDate < DateTime.UtcNow);
                context.RefreshTokenConfiguration.RemoveRange(expiredTokens);

                // Save new refresh token in DB
                context.RefreshTokenConfiguration.Add(new RefreshTokenConfiguration
                {
                    UserId = user.Id,
                    AccessToken = accessToken,
                    RefreshToken = refreshTokenObj.RefreshToken,
                    RefreshTokenExpiryDate = refreshTokenObj.RefreshTokenExpiryDate,
                    RefreshTokenCreatedDate = DateTime.UtcNow,
                    IsRevoked = false
                });

                await context.SaveChangesAsync();

                // ✅ FIX 1: Refresh Token ko secure HttpOnly Cookie me daal diya
                SetRefreshTokenCookie(refreshTokenObj.RefreshToken, refreshTokenObj.RefreshTokenExpiryDate);

                // ✅ FIX 2: Response body se refresh token permanent hata diya, ab sirf access token jayega
                return Ok(new { accessToken });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login error occurred");
                return StatusCode(500, new { message = "An internal error occurred" });
            }
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // 1. Refresh Token Headers ki bajaye Cookie se read hoga
                if (!Request.Cookies.TryGetValue("X-Refresh-Token", out var refreshToken))
                {
                    return StatusCode(400, new { message = "Refresh token cookie missing." });
                }

                // 2. DB mein us token ko dhoondein aur remove karein
                var storedToken = await context.RefreshTokenConfiguration
                    .FirstOrDefaultAsync(x => x.RefreshToken == refreshToken);

                if (storedToken != null)
                {
                    context.RefreshTokenConfiguration.Remove(storedToken);
                    await context.SaveChangesAsync();
                }

                // 3. Strict Cookie Options ke sath browser se cookie delete karein (Safari/Chrome fully compatible)
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    IsEssential = true
                };
                Response.Cookies.Delete("X-Refresh-Token", cookieOptions);

                logger.LogInformation("User logged out and refresh token cookie cleared safely.");

                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during logout execution");
                return StatusCode(500, new { message = "Internal server error during logout" });
            }
        }

        [AllowAnonymous]
        [HttpPost("refreshToken")]
        public async Task<IActionResult> RefreshToken()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
                return StatusCode(403, new { message = "Authorization header missing." });

            if (!Request.Cookies.TryGetValue("X-Refresh-Token", out var existingRefreshToken))
                return StatusCode(403, new { message = "Refresh token cookie missing." });

            string accessToken = authHeader.ToString().Replace("Bearer", "", StringComparison.OrdinalIgnoreCase).Trim();

            try
            {
                var principal = jwt.GetPrincipalFromExpiredToken(accessToken);
                var email = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(email))
                    return Unauthorized("Invalid Token Claims");

                var storedToken = await context.RefreshTokenConfiguration
                    .Include(u => u.User)
                    .FirstOrDefaultAsync(x => x.RefreshToken == existingRefreshToken && x.User.Email == email);

                if (storedToken == null || storedToken.IsRevoked || storedToken.RefreshTokenExpiryDate < DateTime.UtcNow)
                    return StatusCode(403, new { message = "Invalid or expired refresh token." });

                storedToken.IsRevoked = true;
                context.Update(storedToken);

                var newAccessToken = jwt.GenerateToken(email);
                var newRefreshTokenObj = jwt.GenerateRefreshToken();

                context.RefreshTokenConfiguration.Add(new RefreshTokenConfiguration
                {
                    UserId = storedToken.UserId,
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshTokenObj.RefreshToken,
                    RefreshTokenExpiryDate = newRefreshTokenObj.RefreshTokenExpiryDate,
                    RefreshTokenCreatedDate = DateTime.UtcNow,
                    IsRevoked = false
                });

                await context.SaveChangesAsync();

                // 🟢 FIX HERE: Naya rotated token browser ki cookie me overwrite karein
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = newRefreshTokenObj.RefreshTokenExpiryDate,
                    IsEssential = true
                };
                Response.Cookies.Append("X-Refresh-Token", newRefreshTokenObj.RefreshToken, cookieOptions);

                // 🟢 FIX HERE: Body me ab refresh token bilkul nahi bhejna, sirf access token jayega
                return Ok(new { accessToken = newAccessToken });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during token refresh: {Message}", ex.Message);
                return StatusCode(500, new { message = "Internal server error during token refresh" });
            }
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1. User find karein (Direct DbContext se)
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                // Security: Generic response
                return Ok(new { message = "If your email is registered, you will receive a reset link." });
            }

            // 2. Custom Token Generate Karein
            // Guid ya Random byte array generate kar sakte hain
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));

            // 3. Database mein Token aur Expiry save karein
            var expiryHours = 1; // Aapki logic ke mutabiq 1 ghanta
            user.PasswordResetToken = token;
            user.ResetTokenExpires = DateTime.UtcNow.AddHours(expiryHours);

            await context.SaveChangesAsync();

            // 4. Create Reset Link 
            var frontendUrl = authSettingsConfig.Value.ResetPasswordUrl; // Frontend URL config سے لیں

            // Token ko URL encode lazmi karein
            var resetLink = $"{frontendUrl}?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(user.Email)}";

            // 5. Send Email
            try
            {
                string subject = "Reset Your Password";

                // PURANI LINES KO HATA KAR NAYA HELPER METHOD USE KAREIN:
                string message = emailTemplatesHelper.GetPasswordResetEmail(resetLink, $"{expiryHours} Hour(s)");

                await emailService.SendEmailAsync(user.Email, subject, message);
            }
            catch (Exception)
            {
                // Error log karein yahan
                return StatusCode(500, new { message = "Error sending email." });
            }

            return Ok(new { message = "Reset link has been sent to your email." });
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1. User ko email se dhoondein
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            // 2. Token validation (Security check)
            // Humne PasswordResetToken check kiya aur Expiry bhi
            if (user == null || user.PasswordResetToken != model.Token || user.ResetTokenExpires < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Invalid or expired token." });
            }

            // 3. Password Hash (BCrypt)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            // 4. Token ko Clear karein (Taake ek token dobara use na ho sakay)
            user.PasswordResetToken = null;
            user.ResetTokenExpires = null;

            await context.SaveChangesAsync();

            return Ok(new { message = "Password has been reset successfully." });
        }


    }
}
