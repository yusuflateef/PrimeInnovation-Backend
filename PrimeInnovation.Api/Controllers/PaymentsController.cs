using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeInnovation.Application.Common;
using PrimeInnovation.Application.DTOs;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Domain.Entities;
using PrimeInnovation.Infrastructure.Persistence;

namespace PrimeInnovation.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController(
    AppDbContext db,
    IConfiguration config,
    IPaystackService paystack,
    INotificationService notifications,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpPost("initialize/{slug}")]
    [ProducesResponseType(typeof(PaymentInitResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentInitResponse>> Initialize(string slug, PaymentInitRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(userId);
        if (user is null) return Unauthorized();

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Slug == slug && c.Published);
        if (course is null) return NotFound(new { message = "Course not found." });
        if (course.Price is null or 0)
            return Ok(new PaymentInitResponse("Free", $"FREE-{Guid.NewGuid():N}"[..16], $"/courses/{slug}", 0, "NGN"));

        var existing = await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == course.Id);
        if (existing) return Conflict(new { message = "You are already enrolled in this course." });

        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "Paystack" : request.Provider;
        if (!provider.Equals("Paystack", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only Paystack is supported. Use provider: Paystack." });

        if (!paystack.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Paystack is not configured. Add your test/live secret key under Paystack:SecretKey in appsettings.json or environment variables."
            });

        var reference = $"PIN-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        var amount = course.Price.Value;
        var callback = request.CallbackUrl
            ?? $"{config["FrontendUrl"]?.TrimEnd('/')}/courses/{slug}?provider=paystack";

        var init = await paystack.InitializeAsync(
            user.Email,
            amount,
            reference,
            callback,
            new
            {
                course_slug = course.Slug,
                course_title = course.Title,
                user_id = user.Id,
                custom_fields = new[]
                {
                    new { display_name = "Course", variable_name = "course", value = course.Title }
                }
            });

        if (!init.Success || string.IsNullOrWhiteSpace(init.AuthorizationUrl) || string.IsNullOrWhiteSpace(init.Reference))
            return BadRequest(new { message = init.Message ?? "Unable to start Paystack checkout." });

        return Ok(new PaymentInitResponse(
            "Paystack",
            init.Reference,
            init.AuthorizationUrl,
            amount,
            "NGN",
            paystack.PublicKey,
            !paystack.PublicKey.StartsWith("pk_test", StringComparison.OrdinalIgnoreCase)));
    }

    [HttpPost("confirm/{slug}")]
    public async Task<ActionResult<EnrollmentDto>> Confirm(string slug, [FromBody] EnrollRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Slug == slug && c.Published);
        if (course is null) return NotFound(new { message = "Course not found." });

        var existing = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == course.Id);
        if (existing is not null)
            return Ok(ToDto(existing, course));

        if (course.Price is null or 0)
        {
            var (free, freeError) = await EnrollVerifiedAsync(userId, course, "Free", $"FREE-{Guid.NewGuid():N}"[..20]);
            return free is null ? Conflict(new { message = freeError }) : Ok(ToDto(free, course));
        }

        if (string.IsNullOrWhiteSpace(request.PaymentReference))
            return BadRequest(new { message = "Payment reference is required." });

        if (!paystack.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Paystack is not configured." });

        var verify = await paystack.VerifyAsync(request.PaymentReference);
        if (!verify.Success)
            return BadRequest(new { message = verify.Message ?? "Payment not successful.", status = verify.Status, gateway = verify.GatewayResponse });

        // The transaction must have been initialized by this user for this course (metadata set in Initialize).
        if (!string.Equals(verify.UserId, userId.ToString(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(verify.CourseSlug, course.Slug, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "This payment does not belong to your account or this course." });

        var amountError = CheckAmount(course, verify);
        if (amountError is not null)
            return BadRequest(new { message = amountError });

        var (enrollment, error) = await EnrollVerifiedAsync(userId, course, "Paystack", verify.Reference);
        return enrollment is null ? Conflict(new { message = error }) : Ok(ToDto(enrollment, course));
    }

    /// <summary>
    /// Paystack server-to-server notification. Enrolls the buyer even if they never return to the callback URL.
    /// Configure in the Paystack dashboard as https://&lt;api-host&gt;/api/payments/paystack/webhook.
    /// </summary>
    [HttpPost("paystack/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PaystackWebhook()
    {
        if (!paystack.IsConfigured) return NotFound();

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        if (!paystack.IsValidWebhookSignature(body, Request.Headers["x-paystack-signature"]))
            return Unauthorized();

        string? eventName, reference;
        try
        {
            using var doc = JsonDocument.Parse(body);
            eventName = doc.RootElement.TryGetProperty("event", out var ev) ? ev.GetString() : null;
            reference = doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("reference", out var r)
                ? r.GetString()
                : null;
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        // Acknowledge every other event so Paystack does not retry it.
        if (eventName != "charge.success" || string.IsNullOrWhiteSpace(reference))
            return Ok();

        // Never trust the webhook payload's amount/metadata: re-verify with Paystack.
        var verify = await paystack.VerifyAsync(reference);
        if (!verify.Success || !Guid.TryParse(verify.UserId, out var userId) || string.IsNullOrWhiteSpace(verify.CourseSlug))
        {
            logger.LogWarning("Paystack webhook {Reference} could not be verified or lacks metadata ({Status}).", reference, verify.Status);
            return Ok();
        }

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Slug == verify.CourseSlug);
        if (course is null || !await db.Users.AnyAsync(u => u.Id == userId))
        {
            logger.LogWarning("Paystack webhook {Reference}: unknown course {Slug} or user {UserId}.", reference, verify.CourseSlug, userId);
            return Ok();
        }

        var amountError = CheckAmount(course, verify);
        if (amountError is not null)
        {
            logger.LogWarning("Paystack webhook {Reference}: {Error}", reference, amountError);
            return Ok();
        }

        var (enrollment, error) = await EnrollVerifiedAsync(userId, course, "Paystack", verify.Reference);
        if (enrollment is null)
            logger.LogWarning("Paystack webhook {Reference}: {Error}", reference, error);
        return Ok();
    }

    private static string? CheckAmount(Course course, PaystackVerifyResult verify)
    {
        var price = course.Price ?? 0;
        var expectedKobo = (int)Math.Round(price * 100m, MidpointRounding.AwayFromZero);
        var paidKobo = (int)Math.Round(verify.AmountNgn * 100m, MidpointRounding.AwayFromZero);
        return paidKobo < expectedKobo
            ? $"Paid amount ({verify.AmountNgn:N0} NGN) is less than course price ({price:N0} NGN)."
            : null;
    }

    /// <summary>
    /// Creates the enrollment for an already-verified payment. Idempotent: shared by the browser
    /// confirm call and the webhook, which can race each other for the same reference.
    /// </summary>
    private async Task<(Enrollment? Enrollment, string? Error)> EnrollVerifiedAsync(Guid userId, Course course, string provider, string reference)
    {
        var existing = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == course.Id);
        if (existing is not null) return (existing, null);

        if (await db.Enrollments.AnyAsync(e => e.PaymentReference == reference))
            return (null, "This payment reference has already been used.");

        var enrollment = new Enrollment
        {
            UserId = userId,
            CourseId = course.Id,
            PaymentProvider = provider,
            PaymentReference = reference,
            PaymentStatus = "Paid"
        };
        db.Enrollments.Add(enrollment);
        course.StudentsCount += 1;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race on the (UserId, CourseId) or PaymentReference unique index.
            db.ChangeTracker.Clear();
            var winner = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == course.Id);
            return winner is not null ? (winner, null) : (null, "This payment reference has already been used.");
        }

        var user = await db.Users.FindAsync(userId);
        if (user is not null)
            await notifications.SendEnrollmentConfirmationAsync(user.FullName, user.Email, user.PhoneNumber, course.Title);

        return (enrollment, null);
    }

    [HttpGet("paystack/config")]
    [AllowAnonymous]
    public ActionResult<object> PaystackConfig() => Ok(new
    {
        configured = paystack.IsConfigured,
        publicKey = paystack.IsConfigured ? paystack.PublicKey : null,
        provider = "Paystack"
    });

    private static EnrollmentDto ToDto(Enrollment e, Course c) =>
        new(e.Id, c.Id, c.Slug, c.Title, c.InstructorName, e.ProgressPercent, e.Completed, e.EnrolledAt);
}

