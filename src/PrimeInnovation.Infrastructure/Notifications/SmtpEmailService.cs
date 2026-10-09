using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Application.Options;

namespace PrimeInnovation.Infrastructure.Notifications;

public class SmtpEmailService(EmailSettings settings, ILogger<SmtpEmailService> logger) : IEmailService
{
    public bool IsConfigured =>
        settings.Enabled &&
        !string.IsNullOrWhiteSpace(settings.Host) &&
        !string.IsNullOrWhiteSpace(settings.FromEmail);

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Email skipped (SMTP not configured). To={To} Subject={Subject}", message.To, message.Subject);
            return;
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(message.To);
        if (!string.IsNullOrWhiteSpace(message.PlainText))
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.PlainText, null, "text/plain"));

        await client.SendMailAsync(mail, ct);
        logger.LogInformation("Email sent to {To}", message.To);
    }
}
