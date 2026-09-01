using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace dotnet.core.utils.server.Helpers
{
    public static class TokenHelper
    {
        // Le secret doit être exactement le même que celui lu dans ConfigureService.AddJwtService,
        // sinon les jetons délivrés ici ne seront jamais reconnus comme valides par l'API.
        public static string GenerateToken(string userId, string displayName, string role = "user")
        {
            var secret = Environment.GetEnvironmentVariable("GOLDENFAN_JWT_SECRET") ?? "goldenfan-dev-secret-change-me";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, displayName ?? string.Empty),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
