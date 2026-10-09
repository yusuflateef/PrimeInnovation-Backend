namespace PrimeInnovation.Application.Options;

public class JwtSettings
{
    public string Key { get; set; } = "PrimeInnovationNetwork_SuperSecret_Key_ChangeInProduction_2026!";
    public string Issuer { get; set; } = "PrimeInnovation.Api";
    public string Audience { get; set; } = "PrimeInnovation.Web";
    public int ExpiryHours { get; set; } = 24;
}

public class PaystackSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paystack.co";
    public bool Enabled => !string.IsNullOrWhiteSpace(SecretKey) &&
                           !SecretKey.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
}

public class EmailSettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@primeinnovation.network";
    public string FromName { get; set; } = "Prime Innovation Network";
    public string? AdminEmail { get; set; }
}

/// <summary>Termii — Nigerian SMS provider (https://termii.com)</summary>
public class TermiiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string SenderId { get; set; } = "PrimePIN";
    public string Channel { get; set; } = "generic";
    public string BaseUrl { get; set; } = "https://api.ng.termii.com";
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey) &&
                           !ApiKey.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
}
