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
        ILogger<AuthController> logger) : ControllerBase
    {

        [HttpGet]
        public IActionResult Get()
        {
            var userName = User.Identity?.Name;
            return Ok($"JWT working for {userName}");
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

                // *** CHANGE 1: Return Refresh Token in the response body (not cookie) ***
                return Ok(new { accessToken, refreshToken = refreshTokenObj.RefreshToken });
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
                // *** CHANGE 2: Read Refresh Token from Header, not Cookie ***
                if (!Request.Headers.TryGetValue("X-Refresh-Token", out var refreshTokenHeaderValue))
                {
                    return StatusCode(403, new { message = "Refresh token header missing." });
                }
                var refreshToken = refreshTokenHeaderValue.FirstOrDefault();

                // 2. DB mein us token ko dhoondein aur revoke/delete karein
                var storedToken = await context.RefreshTokenConfiguration
                    .FirstOrDefaultAsync(x => x.RefreshToken == refreshToken);

                if (storedToken != null)
                {
                    context.RefreshTokenConfiguration.Remove(storedToken);
                    await context.SaveChangesAsync();
                }

                logger.LogInformation("User logged out and refresh token revoked.");

                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during logout");
                return StatusCode(500, "Internal server error");
            }
        }

        [AllowAnonymous]
        [HttpPost("refreshToken")]
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
                var principal = jwt.GetPrincipalFromExpiredToken(accessToken);
                var email = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(email))
                    return Unauthorized("Invalid Token Claims");

                // 3. DB se Refresh Token verify karna
                var storedToken = await context.RefreshTokenConfiguration
                    .Include(u => u.User)
                    .FirstOrDefaultAsync(x => x.RefreshToken == existingRefreshTokenHeaderValue.ToString()
                                         && x.User.Email == email);

                if (storedToken == null || storedToken.IsRevoked || storedToken.RefreshTokenExpiryDate < DateTime.UtcNow)
                    return StatusCode(403, new { message = "Invalid or expired refresh token." });

                // 4. Purane tokens ko revoke ya delete karein (Multiple devices ke liye sirf isko revoke karein)
                storedToken.IsRevoked = true;
                context.Update(storedToken);

                // 5. Token Rotation (Generate new access and refresh tokens)
                var newAccessToken = jwt.GenerateToken(email);
                var newRefreshTokenObj = jwt.GenerateRefreshToken();

                // 6. Database mein Naya Token Record Add Karein
                context.RefreshTokenConfiguration.Add(new RefreshTokenConfiguration
                {
                    UserId = storedToken.UserId, // Purane user ki ID use karein
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshTokenObj.RefreshToken,
                    RefreshTokenExpiryDate = newRefreshTokenObj.RefreshTokenExpiryDate,
                    RefreshTokenCreatedDate = DateTime.UtcNow,
                    IsRevoked = false // Naya token revoke nahi hai
                });

                await context.SaveChangesAsync();

                // *** CHANGE 4: Return new Access and Refresh tokens in the body ***
                return Ok(new { accessToken = newAccessToken, refreshToken = newRefreshTokenObj.RefreshToken });
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
