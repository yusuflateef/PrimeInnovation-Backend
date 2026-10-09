using PrimeInnovation.Application.DTOs;
using PrimeInnovation.Domain.Entities;

namespace PrimeInnovation.Application.Interfaces;

public interface ITokenService
{
    AuthResponse CreateToken(User user);
    string GenerateResetToken();
}
