namespace PrimeInnovation.Application.Interfaces;

public record PaystackInitializeResult(bool Success, string? AuthorizationUrl, string? Reference, string? Message);
public record PaystackVerifyResult(bool Success, string Status, string Reference, decimal AmountNgn, string? Email, string? Message, string? GatewayResponse);

public interface IPaystackService
{
    bool IsConfigured { get; }
    string PublicKey { get; }
    Task<PaystackInitializeResult> InitializeAsync(string email, decimal amountNgn, string reference, string callbackUrl, object? metadata = null, CancellationToken ct = default);
    Task<PaystackVerifyResult> VerifyAsync(string reference, CancellationToken ct = default);
}
