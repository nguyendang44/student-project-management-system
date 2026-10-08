using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StudentProjects.Domain.Entities;

namespace StudentProjects.Api.Security;

public sealed class TokenService(IConfiguration config)
{
    public (string Token, DateTimeOffset ExpiresAt) Create(User user)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured.");
        var issuer = config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT issuer is not configured.");
        var audience = config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT audience is not configured.");
        var minutes = config.GetValue("Jwt:AccessTokenMinutes", 30);
        if (minutes < 1 || minutes > 60) throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 1 and 60.");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(minutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.Name),
            new Claim("token_version", user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: issuer, audience: audience, claims: claims,
            notBefore: DateTime.UtcNow, expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
