namespace PrimeInnovation.Application.Interfaces;

public record GoogleIdentity(string Subject, string Email, string? FullName);

public interface IGoogleTokenValidator
{
    bool IsConfigured { get; }

    /// <summary>Validates a Google ID token server-side. Returns null when the token is invalid or the email is unverified.</summary>
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct = default);
}
