using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PrimeInnovation.Application.DTOs;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Application.Options;
using PrimeInnovation.Domain.Entities;

namespace PrimeInnovation.Infrastructure.Auth;

public class TokenService(JwtSettings settings) : ITokenService
{
    public AuthResponse CreateToken(User user)
    {
        var expires = DateTime.UtcNow.AddHours(settings.ExpiryHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires,
            user.Id,
            user.FullName,
            user.Email,
            user.Role.ToString());
    }

    public string GenerateResetToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
