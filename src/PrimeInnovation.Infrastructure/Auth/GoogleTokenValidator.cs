using Google.Apis.Auth;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Application.Options;

namespace PrimeInnovation.Infrastructure.Auth;

public class GoogleTokenValidator(GoogleSettings settings) : IGoogleTokenValidator
{
    public bool IsConfigured => settings.Enabled;

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (!settings.Enabled) return null;

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [settings.ClientId]
            });

            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
                return null;

            return new GoogleIdentity(payload.Subject, payload.Email, payload.Name);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
