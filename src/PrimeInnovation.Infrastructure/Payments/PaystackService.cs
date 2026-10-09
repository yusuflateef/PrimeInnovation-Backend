using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Application.Options;

namespace PrimeInnovation.Infrastructure.Payments;

public class PaystackService(HttpClient http, PaystackSettings settings) : IPaystackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsConfigured => settings.Enabled;
    public string PublicKey => settings.PublicKey;

    public async Task<PaystackInitializeResult> InitializeAsync(
        string email,
        decimal amountNgn,
        string reference,
        string callbackUrl,
        object? metadata = null,
        CancellationToken ct = default)
    {
        if (!settings.Enabled)
            return new PaystackInitializeResult(false, null, null, "Paystack is not configured. Set Paystack:SecretKey in appsettings or environment.");

        EnsureAuth();

        var amountKobo = (int)Math.Round(amountNgn * 100m, MidpointRounding.AwayFromZero);
        var payload = new
        {
            email,
            amount = amountKobo,
            currency = "NGN",
            reference,
            callback_url = callbackUrl,
            metadata
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await http.PostAsync("transaction/initialize", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<PaystackApiResponse<PaystackInitData>>(body, JsonOptions);

        if (!response.IsSuccessStatusCode || parsed is null || !parsed.Status || parsed.Data is null)
        {
            return new PaystackInitializeResult(false, null, null, parsed?.Message ?? $"Paystack initialize failed ({(int)response.StatusCode}).");
        }

        return new PaystackInitializeResult(true, parsed.Data.AuthorizationUrl, parsed.Data.Reference, parsed.Message);
    }

    public async Task<PaystackVerifyResult> VerifyAsync(string reference, CancellationToken ct = default)
    {
        if (!settings.Enabled)
            return new PaystackVerifyResult(false, "unconfigured", reference, 0, null, "Paystack is not configured.", null);

        EnsureAuth();

        using var response = await http.GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<PaystackApiResponse<PaystackVerifyData>>(body, JsonOptions);

        if (!response.IsSuccessStatusCode || parsed is null || !parsed.Status || parsed.Data is null)
        {
            return new PaystackVerifyResult(false, "failed", reference, 0, null, parsed?.Message ?? "Paystack verification failed.", null);
        }

        var amountNgn = parsed.Data.Amount / 100m;
        var ok = string.Equals(parsed.Data.Status, "success", StringComparison.OrdinalIgnoreCase);
        return new PaystackVerifyResult(
            ok,
            parsed.Data.Status ?? "unknown",
            parsed.Data.Reference ?? reference,
            amountNgn,
            parsed.Data.Customer?.Email,
            parsed.Message,
            parsed.Data.GatewayResponse);
    }

    private void EnsureAuth()
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.SecretKey);
        if (!http.DefaultRequestHeaders.Accept.Any())
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private sealed class PaystackApiResponse<T>
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    private sealed class PaystackInitData
    {
        [JsonPropertyName("authorization_url")]
        public string? AuthorizationUrl { get; set; }
        public string? Reference { get; set; }
        [JsonPropertyName("access_code")]
        public string? AccessCode { get; set; }
    }

    private sealed class PaystackVerifyData
    {
        public string? Status { get; set; }
        public string? Reference { get; set; }
        public long Amount { get; set; }
        [JsonPropertyName("gateway_response")]
        public string? GatewayResponse { get; set; }
        public PaystackCustomer? Customer { get; set; }
    }

    private sealed class PaystackCustomer
    {
        public string? Email { get; set; }
    }
}
