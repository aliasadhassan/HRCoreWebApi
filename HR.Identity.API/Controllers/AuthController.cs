using HR.Identity.API.Configuration;
using HR.Identity.API.Data;
using HR.Identity.API.Helpers;
using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using HR.Identity.API.Services;
using HR.Shared.Library.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;

namespace HR.Identity.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(
        AppDbContext context,
        AccessTokenFactory accessTokens,
        RefreshTokenService refreshTokens,
        IEmailService emailService,
        EmailTemplatesHelper emailTemplatesHelper,
        IOptions<AuthSettings> authSettingsConfig,
        MicrosoftGraphService graphService,
        ILogger<AuthController> logger) : ControllerBase
    {
        private const string RefreshCookie = "X-Refresh-Token";
        private const string MicrosoftProvider = "Microsoft";
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private const string GenericLoginError = "Invalid Email or Password";
        private const string GenericResetMessage = "If your email is registered, you will receive a reset link.";
        private const string NotInvitedMessage = "Sign-up is by invitation only. Ask your HR administrator to invite you.";

        private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? UserAgent
        {
            get
            {
                var ua = Request.Headers.UserAgent.ToString();
                return string.IsNullOrEmpty(ua) ? null : ua.Length > 300 ? ua[..300] : ua;
            }
        }

        [HttpGet]
        public IActionResult Get() => Ok($"JWT working for {User.Identity?.Name}");

        // ─────────────────────────────── REGISTER ───────────────────────────────
        // Open signup band (audit C2): pehle har koi "hr-cloud" tenant mein EMPLOYEE ban jata tha.
        // Ab sirf invite: Admin Users > Invite → email link → reset-password (Invite token) se password set.
        // Route rakha hai taake purana client clear error dikhaye.
        [AllowAnonymous]
        [HttpPost("register")]
        public IActionResult Register() =>
            StatusCode(403, new { code = "REGISTRATION_DISABLED", message = NotInvitedMessage });

        // ──────────────────────────────── LOGIN ─────────────────────────────────
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            var normalizedEmail = model.Email?.Trim().ToUpperInvariant() ?? string.Empty;

            try
            {
                var user = await context.Users
                    .Include(u => u.Tenant).ThenInclude(t => t.Settings)
                    .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

                if (user is null)
                {
                    await AuditFailureAsync(null, model.Email, LoginMethod.Password, "UserNotFound");
                    return Unauthorized(new { code = "INVALID_CREDENTIALS", message = GenericLoginError });
                }

                if (user.LockoutEnd > DateTime.UtcNow)
                {
                    await AuditFailureAsync(user, model.Email, LoginMethod.Password, "LockedOut");
                    return LockedOut(user.LockoutEnd.Value);
                }

                if (user.PasswordHash is null)
                {
                    await AuditFailureAsync(user, model.Email, LoginMethod.Password, "SsoOnlyAccount");
                    return Unauthorized(new { code = "SSO_ONLY", message = "This account uses Microsoft sign-in." });
                }

                if (!PasswordHelper.Verify(model.Password, user.PasswordHash))
                {
                    user.AccessFailedCount++;
                    // Company settings se (Settings > Security), warna default
                    var maxFailed = user.Tenant.Settings?.MaxFailedLoginAttempts ?? MaxFailedAttempts;
                    if (user.AccessFailedCount >= maxFailed)
                    {
                        // Isi attempt pe lock — user ko foran bata do, agli koshish ka intezar nahi
                        user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                        user.AccessFailedCount = 0;
                        await AuditFailureAsync(user, model.Email, LoginMethod.Password, "LockedOut");
                        return LockedOut(user.LockoutEnd.Value);
                    }
                    await AuditFailureAsync(user, model.Email, LoginMethod.Password, "InvalidPassword");
                    return Unauthorized(new { code = "INVALID_CREDENTIALS", message = GenericLoginError });
                }

                if (!user.IsActive || user.Tenant.Status != TenantStatus.Active)
                {
                    await AuditFailureAsync(user, model.Email, LoginMethod.Password, "Inactive");
                    return StatusCode(403, new { code = "ACCOUNT_DISABLED", message = "Your account is disabled. Contact your administrator." });
                }

                return await SignInAsync(user, LoginMethod.Password);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login error occurred");
                return StatusCode(500, new { message = "An internal error occurred" });
            }
        }

        // ─────────────────────────────── SSO (Entra) ────────────────────────────
        [AllowAnonymous]
        [HttpPost("sso/callback")]
        public async Task<IActionResult> SsoCallback([FromBody] SsoLoginRequest request)
        {
            try
            {
                var msUser = await graphService.GetUserFromTokenAsync(request.AccessToken);
                if (msUser == null || string.IsNullOrWhiteSpace(msUser.Id) || string.IsNullOrWhiteSpace(msUser.Email))
                    return Unauthorized(new { message = "Invalid Microsoft token" });

                var normalizedEmail = msUser.Email.Trim().ToUpperInvariant();

                // 1) Pehle Entra object id (oid) se linked user dhoondo — ye kabhi change nahi hota
                var link = await context.UserExternalLogins
                    .Include(l => l.User).ThenInclude(u => u.Tenant)
                    .FirstOrDefaultAsync(l => l.Provider == MicrosoftProvider && l.ProviderKey == msUser.Id);

                var user = link?.User;

                if (link is not null)
                {
                    link.LastUsedAt = DateTime.UtcNow;
                }
                else
                {
                    // 2) Link nahi mila: email se existing (invited) user — pehli dafa SSO
                    //    NOTE: email-based linking tab hi safe hai jab Entra app registration single-tenant ho
                    user = await context.Users
                        .Include(u => u.Tenant)
                        .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

                    // Auto-provision band (audit C2): anjaan email kisi tenant mein khud nahi jayega
                    if (user is null)
                    {
                        await AuditFailureAsync(null, msUser.Email, LoginMethod.Microsoft, "NotInvited");
                        return StatusCode(403, new { code = "NOT_INVITED", message = NotInvitedMessage });
                    }

                    user.ExternalLogins.Add(new UserExternalLogin
                    {
                        Provider = MicrosoftProvider,
                        ProviderKey = msUser.Id,
                        LastUsedAt = DateTime.UtcNow
                    });
                }

                if (!user!.IsActive || user.Tenant.Status != TenantStatus.Active)
                {
                    await AuditFailureAsync(user, msUser.Email, LoginMethod.Microsoft, "Inactive");
                    return StatusCode(403, new { message = "Your account is disabled. Contact your administrator." });
                }

                return await SignInAsync(user, LoginMethod.Microsoft);   // yahan save ho jata hai
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SSO login error occurred");
                return StatusCode(500, new { message = "An internal error occurred during SSO login" });
            }
        }

        // ──────────────────────────────── REFRESH ───────────────────────────────
        [AllowAnonymous]
        [HttpPost("refreshToken")]
        public async Task<IActionResult> RefreshTokenAsync()
        {
            if (!Request.Cookies.TryGetValue(RefreshCookie, out var rawToken) || string.IsNullOrEmpty(rawToken))
                return StatusCode(403, new { message = "Refresh token cookie missing." });

            try
            {
                var result = await refreshTokens.RotateAsync(rawToken, ClientIp, UserAgent);

                switch (result.Status)
                {
                    case RefreshStatus.Success:
                        SetRefreshTokenCookie(result.Token!, result.ExpiresAt);
                        // Har refresh pe taaza roles/permissions — role badla to yahan asar
                        return Ok(new { accessToken = await accessTokens.CreateAsync(result.User!) });

                    case RefreshStatus.Superseded:
                        // Doosri parallel request ne abhi rotate kiya — browser mein nayi cookie aa chuki, retry karo
                        return StatusCode(409, new { message = "Token already refreshed. Retry the request." });

                    default: // Invalid / Reused
                        DeleteRefreshTokenCookie();
                        return StatusCode(403, new { message = "Invalid or expired refresh token." });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, new { message = "Internal server error during token refresh" });
            }
        }

        // ──────────────────────────────── LOGOUT ────────────────────────────────
        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                if (Request.Cookies.TryGetValue(RefreshCookie, out var rawToken) && !string.IsNullOrEmpty(rawToken))
                    await refreshTokens.RevokeAsync(rawToken, ClientIp, "Logout");

                DeleteRefreshTokenCookie();
                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { message = "Internal server error during logout" });
            }
        }

        // ─────────────────────────── FORGOT / RESET PASSWORD ────────────────────
        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var normalizedEmail = model.Email.Trim().ToUpperInvariant();
            var user = await context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && u.IsActive);

            // SSO-only users ka password hi nahi — same generic response (email enumeration se bachao)
            if (user is null || user.PasswordHash is null)
                return Ok(new { message = GenericResetMessage });

            var now = DateTime.UtcNow;
            const int expiryHours = 1;

            // Pehle se bheje gaye unused reset links invalid
            await context.UserTokens
                .Where(t => t.UserId == user.Id && t.Purpose == TokenPurpose.PasswordReset && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now));

            var token = TokenHasher.NewToken();
            context.UserTokens.Add(new UserToken
            {
                UserId = user.Id,
                Purpose = TokenPurpose.PasswordReset,
                TokenHash = TokenHasher.Hash(token),
                ExpiresAt = now.AddHours(expiryHours)
            });
            await context.SaveChangesAsync();

            var resetLink = $"{authSettingsConfig.Value.ResetPasswordUrl}?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(user.Email)}";

            try
            {
                var message = emailTemplatesHelper.GetPasswordResetEmail(resetLink, $"{expiryHours} Hour(s)");
                await emailService.SendEmailAsync(user.Email, "Reset Your Password", message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send reset email to {Email}", user.Email);
                return StatusCode(500, new { message = "Error sending email." });
            }

            return Ok(new { message = GenericResetMessage });
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var now = DateTime.UtcNow;
            var normalizedEmail = model.Email.Trim().ToUpperInvariant();
            var hash = TokenHasher.Hash(model.Token);

            var resetToken = await context.UserTokens
                .Include(t => t.User).ThenInclude(u => u.Tenant).ThenInclude(t => t.Settings)
                .FirstOrDefaultAsync(t => t.TokenHash == hash
                                       && (t.Purpose == TokenPurpose.PasswordReset || t.Purpose == TokenPurpose.Invite)
                                       && t.UsedAt == null
                                       && t.ExpiresAt > now
                                       && t.User.NormalizedEmail == normalizedEmail);

            if (resetToken is null)
                return BadRequest(new { message = "Invalid or expired token." });

            var user = resetToken.User;

            // Company ki password policy (Settings > Security)
            var minLength = user.Tenant.Settings?.PasswordMinLength ?? 8;
            if (model.NewPassword.Length < minLength)
                return BadRequest(new { message = $"Password must be at least {minLength} characters." });

            resetToken.UsedAt = now;                       // one-time use
            user.PasswordHash = PasswordHelper.Hash(model.NewPassword);
            user.SecurityStamp = Guid.NewGuid();
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            user.EmailConfirmed = true;                    // link email pe aaya tha — email sahi hai

            await context.SaveChangesAsync();

            // Password badla => har device se logout
            await refreshTokens.RevokeAllForUserAsync(user.Id, "PasswordReset");

            return Ok(new { message = "Password has been reset successfully." });
        }

        // ──────────────────────────────── HELPERS ───────────────────────────────
        private async Task<IActionResult> SignInAsync(User user, LoginMethod method)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            AddAudit(user, user.Email, method, succeeded: true);

            // Pehle save (IssueAsync) — naya SSO user bhi DB mein aa jaye, phir uske roles se token
            var (refreshToken, expiresAt) = await refreshTokens.IssueAsync(user.Id, ClientIp, UserAgent); // saves all
            var accessToken = await accessTokens.CreateAsync(user);

            SetRefreshTokenCookie(refreshToken, expiresAt);
            logger.LogInformation("{Method} login successful for {Email}", method, user.Email);

            return Ok(new { accessToken });
        }

        // Seconds bhejte hain, DateTime nahi — timezone ka jhanjhat hi khatam
        private IActionResult LockedOut(DateTime lockoutEndUtc)
        {
            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((lockoutEndUtc - DateTime.UtcNow).TotalSeconds));
            return Unauthorized(new
            {
                code = "LOCKED_OUT",
                message = "Too many failed attempts. Your account is temporarily locked.",
                retryAfterSeconds
            });
        }

        private void AddAudit(User? user, string? email, LoginMethod method, bool succeeded, string? reason = null)
        {
            email = string.IsNullOrEmpty(email) ? "(empty)" : email.Length > 256 ? email[..256] : email;
            context.LoginAudits.Add(new LoginAudit
            {
                TenantId = user?.TenantId,
                UserId = user?.Id,
                EmailAttempted = email,
                Method = method,
                Succeeded = succeeded,
                FailureReason = reason,
                IpAddress = ClientIp,
                UserAgent = UserAgent
            });
        }

        private async Task AuditFailureAsync(User? user, string? email, LoginMethod method, string reason)
        {
            AddAudit(user, email, method, succeeded: false, reason);
            await context.SaveChangesAsync();   // lockout counters bhi saath save
        }

        private static CookieOptions RefreshCookieOptions(DateTime? expires = null) => new()
        {
            HttpOnly = true,                  // JS read nahi kar sakta
            Secure = true,
            SameSite = SameSiteMode.None,     // dev: None | prod (same site): Strict
            Expires = expires,
            IsEssential = true
        };

        private void SetRefreshTokenCookie(string token, DateTime expires) =>
            Response.Cookies.Append(RefreshCookie, token, RefreshCookieOptions(expires));

        private void DeleteRefreshTokenCookie() =>
            Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions());
    }
}