namespace PrimeInnovation.Application.Interfaces;

public record EmailMessage(string To, string Subject, string HtmlBody, string? PlainText = null);
public record SmsMessage(string To, string Body);

public interface IEmailService
{
    bool IsConfigured { get; }
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public interface ISmsService
{
    bool IsConfigured { get; }
    /// <summary>Sends SMS via Termii. Phone should be Nigerian local or international format.</summary>
    Task SendAsync(SmsMessage message, CancellationToken ct = default);
}

public interface INotificationService
{
    Task SendWelcomeAsync(string fullName, string email, string? phoneNumber, CancellationToken ct = default);
    Task SendPasswordResetAsync(string fullName, string email, string? phoneNumber, string resetToken, CancellationToken ct = default);
    Task SendEnrollmentConfirmationAsync(string fullName, string email, string? phoneNumber, string courseTitle, CancellationToken ct = default);
    Task NotifyAdminContactAsync(string fullName, string email, string topic, string message, CancellationToken ct = default);
}
