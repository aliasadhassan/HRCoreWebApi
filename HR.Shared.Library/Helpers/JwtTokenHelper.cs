using HR.Shared.Library.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace HR.Shared.Library.Helpers
{
    /// <summary>Token mein kya jayega — Identity API isse bharti hai.</summary>
    public sealed record TokenSubject(
        Guid UserId,
        Guid TenantId,
        string Email,
        string DisplayName,
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> Permissions);

    public class JwtTokenHelper
    {
        private readonly IConfiguration _config;

        public const string TenantClaim = "tenant_id";
        /// <summary>Har permission ek alag "perm" claim — APIs [HasPermission] se isi ko check karti hain.</summary>
        public const string PermissionClaim = "perm";
        /// <summary>Insaan ka naam (UI ke liye). ClaimTypes.Name email hi rehta hai, taake purana code na toote.</summary>
        public const string DisplayNameClaim = JwtRegisteredClaimNames.Name;

        public JwtTokenHelper(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(TokenSubject subject)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, subject.UserId.ToString()),
                new(ClaimTypes.Name, subject.Email),
                new(ClaimTypes.Email, subject.Email),
                new(DisplayNameClaim, subject.DisplayName),
                new(TenantClaim, subject.TenantId.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            claims.AddRange(subject.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
            claims.AddRange(subject.Permissions.Select(p => new Claim(PermissionClaim, p)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:ExpireMinutes"])),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [Obsolete("Roles/permissions ke baghair token. GenerateToken(TokenSubject) use karein.")]
        public string GenerateToken(string email, Guid userId, Guid tenantId)
            => GenerateToken(new TokenSubject(userId, tenantId, email, email, [], []));

        public RefreshTokenConfiguration GenerateRefreshToken()
        {
            return new RefreshTokenConfiguration
            {
                RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                RefreshTokenExpiryDate = DateTime.UtcNow.AddDays(
                    Convert.ToDouble(_config["Jwt:RefreshTokenExpireDays"]))
            };
        }
    }
}
