using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Application.Options;

namespace PrimeInnovation.Infrastructure.Notifications;

/// <summary>
/// Nigerian SMS via Termii (https://developers.termii.com/messaging-api).
/// POST {BaseUrl}/api/sms/send
/// </summary>
public class TermiiSmsService(HttpClient http, TermiiSettings settings, ILogger<TermiiSmsService> logger) : ISmsService
{
    public bool IsConfigured => settings.Enabled;

    public async Task SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("SMS skipped (Termii not configured). To={To}", message.To);
            return;
        }

        var to = NormalizeNigerianPhone(message.To);
        if (to is null)
        {
            logger.LogWarning("SMS skipped — invalid Nigerian phone number: {Phone}", message.To);
            return;
        }

        var payload = new
        {
            api_key = settings.ApiKey,
            to,
            from = settings.SenderId,
            sms = message.Body,
            type = "plain",
            channel = string.IsNullOrWhiteSpace(settings.Channel) ? "generic" : settings.Channel
        };

        using var response = await http.PostAsJsonAsync("api/sms/send", payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Termii SMS failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Termii SMS failed: {body}");
        }

        var parsed = System.Text.Json.JsonSerializer.Deserialize<TermiiResponse>(body, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (parsed is not null &&
            !string.IsNullOrWhiteSpace(parsed.Code) &&
            !parsed.Code.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("Termii SMS rejected: {Message}", parsed.Message);
            throw new InvalidOperationException(parsed.Message ?? "Termii SMS rejected.");
        }

        logger.LogInformation("Termii SMS sent to {To}. MessageId={MessageId}", to, parsed?.MessageId);
    }

    /// <summary>
    /// Accepts 0803..., +234803..., 234803... and returns 234803...
    /// </summary>
    public static string? NormalizeNigerianPhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = Regex.Replace(raw, @"[^\d]", "");
        if (digits.StartsWith("234") && digits.Length >= 13)
            return digits[..13];
        if (digits.StartsWith("0") && digits.Length == 11)
            return "234" + digits[1..];
        if (digits.Length == 10 && (digits.StartsWith('7') || digits.StartsWith('8') || digits.StartsWith('9')))
            return "234" + digits;
        return digits.Length >= 11 ? digits : null;
    }

    private sealed class TermiiResponse
    {
        public string? Code { get; set; }
        [JsonPropertyName("message_id")]
        public string? MessageId { get; set; }
        public string? Message { get; set; }
    }
}
