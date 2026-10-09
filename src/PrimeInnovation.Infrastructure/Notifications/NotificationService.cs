using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Application.Options;

namespace PrimeInnovation.Infrastructure.Notifications;

public class NotificationService(
    IEmailService email,
    ISmsService sms,
    EmailSettings emailSettings,
    IConfiguration config,
    ILogger<NotificationService> logger) : INotificationService
{
    private string FrontendUrl => (config["FrontendUrl"] ?? "http://localhost:5173").TrimEnd('/');

    public async Task SendWelcomeAsync(string fullName, string emailAddress, string? phoneNumber, CancellationToken ct = default)
    {
        var subject = "Welcome to Prime Digital Academy";
        var html = $"""
            <h2>Welcome, {Escape(fullName)}!</h2>
            <p>You are now part of <strong>Prime Digital Academy</strong>, powered by Prime Innovation Network.</p>
            <p><a href="{FrontendUrl}/courses">Browse courses</a> and start learning today.</p>
            """;
        await SafeEmail(emailAddress, subject, html, ct);

        await SafeSms(phoneNumber,
            $"Welcome to Prime Digital Academy, {fullName}! Start learning: {FrontendUrl}/courses",
            ct);
    }

    public async Task SendPasswordResetAsync(string fullName, string emailAddress, string? phoneNumber, string resetToken, CancellationToken ct = default)
    {
        var resetUrl = $"{FrontendUrl}/login?reset=1&email={Uri.EscapeDataString(emailAddress)}&token={Uri.EscapeDataString(resetToken)}";
        var subject = "Password reset — Prime Innovation Network";
        var html = $"""
            <h2>Password reset</h2>
            <p>Hi {Escape(fullName)},</p>
            <p>Use this token to reset your password (valid for 1 hour):</p>
            <p><code>{Escape(resetToken)}</code></p>
            <p>Or open: <a href="{resetUrl}">{resetUrl}</a></p>
            <p>If you did not request this, ignore this email.</p>
            """;
        await SafeEmail(emailAddress, subject, html, ct);

        await SafeSms(phoneNumber,
            $"PIN reset code: {resetToken}. Valid 1 hour. Or visit {FrontendUrl}/login",
            ct);
    }

    public async Task SendEnrollmentConfirmationAsync(string fullName, string emailAddress, string? phoneNumber, string courseTitle, CancellationToken ct = default)
    {
        var subject = $"Enrolled: {courseTitle}";
        var html = $"""
            <h2>Enrollment confirmed</h2>
            <p>Hi {Escape(fullName)},</p>
            <p>You are enrolled in <strong>{Escape(courseTitle)}</strong>.</p>
            <p><a href="{FrontendUrl}/dashboard">Open your dashboard</a></p>
            """;
        await SafeEmail(emailAddress, subject, html, ct);

        await SafeSms(phoneNumber,
            $"You're enrolled in {courseTitle}. Continue at {FrontendUrl}/dashboard — Prime Digital Academy",
            ct);
    }

    public async Task NotifyAdminContactAsync(string fullName, string fromEmail, string topic, string message, CancellationToken ct = default)
    {
        var admin = emailSettings.AdminEmail ?? emailSettings.FromEmail;
        if (string.IsNullOrWhiteSpace(admin)) return;

        var subject = $"[Contact] {topic} — {fullName}";
        var html = $"""
            <p><strong>From:</strong> {Escape(fullName)} ({Escape(fromEmail)})</p>
            <p><strong>Topic:</strong> {Escape(topic)}</p>
            <p>{Escape(message)}</p>
            """;
        await SafeEmail(admin, subject, html, ct);
    }

    private async Task SafeEmail(string to, string subject, string html, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(new EmailMessage(to, subject, html), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}", to);
        }
    }

    private async Task SafeSms(string? phone, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        try
        {
            await sms.SendAsync(new SmsMessage(phone, body), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send SMS to {Phone}", phone);
        }
    }

    private static string Escape(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
