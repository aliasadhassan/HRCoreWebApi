using HR.Shared.Library.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace HR.Shared.Library.Helpers
{
    public class JwtTokenHelper
    {
        private readonly IConfiguration _config;
        public const string TenantClaim = "tenant_id";
        public JwtTokenHelper(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(string email, Guid userId, Guid tenantId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email),
                new Claim(TenantClaim, tenantId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(_config["Jwt:ExpireMinutes"])
                ),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public RefreshTokenConfiguration GenerateRefreshToken()
        {
            return new RefreshTokenConfiguration
            {
                RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                RefreshTokenExpiryDate = DateTime.UtcNow.AddDays(
                    Convert.ToDouble(_config["Jwt:RefreshTokenExpireDays"])
                )
            };
        }
    }
}
