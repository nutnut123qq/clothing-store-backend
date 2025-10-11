using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ClothingStore.API.Models;

namespace ClothingStore.API.Services
{
    public class JwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(User user)
        {
            var secret = _config["JWT_SECRET"] ?? _config.GetValue<string>("Jwt:Secret");
            
            // Use default secret for testing if not configured
            if (string.IsNullOrEmpty(secret))
            {
                Console.WriteLine("[JwtService WARNING] JWT_SECRET not configured, using default (INSECURE for production!)");
                secret = "temp-secret-key-for-testing-minimum-32-characters-long-12345";
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("uid", user.Id.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "clothingstore",
                audience: _config["Jwt:Audience"] ?? "clothingstore",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
