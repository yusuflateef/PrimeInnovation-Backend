using System.Security.Claims;
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
public class PaymentsController(AppDbContext db, IConfiguration config, IPaystackService paystack, INotificationService notifications) : ControllerBase
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
        if (string.IsNullOrWhiteSpace(request.PaymentReference))
            return BadRequest(new { message = "Payment reference is required." });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Slug == slug && c.Published);
        if (course is null) return NotFound(new { message = "Course not found." });

        var existing = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == course.Id);
        if (existing is not null)
            return Ok(ToDto(existing, course));

        if (course.Price is null or 0)
        {
            var free = new Enrollment
            {
                UserId = userId,
                CourseId = course.Id,
                PaymentProvider = "Free",
                PaymentReference = request.PaymentReference,
                PaymentStatus = "Paid"
            };
            db.Enrollments.Add(free);
            course.StudentsCount += 1;
            await db.SaveChangesAsync();
            var freeUser = await db.Users.FindAsync(userId);
            if (freeUser is not null)
                await notifications.SendEnrollmentConfirmationAsync(freeUser.FullName, freeUser.Email, freeUser.PhoneNumber, course.Title);
            return Ok(ToDto(free, course));
        }

        if (!paystack.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Paystack is not configured." });

        var verify = await paystack.VerifyAsync(request.PaymentReference);
        if (!verify.Success)
            return BadRequest(new { message = verify.Message ?? "Payment not successful.", status = verify.Status, gateway = verify.GatewayResponse });

        var expectedKobo = (int)Math.Round(course.Price.Value * 100m, MidpointRounding.AwayFromZero);
        var paidKobo = (int)Math.Round(verify.AmountNgn * 100m, MidpointRounding.AwayFromZero);
        if (paidKobo < expectedKobo)
            return BadRequest(new { message = $"Paid amount ({verify.AmountNgn:N0} NGN) is less than course price ({course.Price.Value:N0} NGN)." });

        // Prevent reuse of the same successful reference across accounts.
        if (await db.Enrollments.AnyAsync(e => e.PaymentReference == verify.Reference))
            return Conflict(new { message = "This payment reference has already been used." });

        var enrollment = new Enrollment
        {
            UserId = userId,
            CourseId = course.Id,
            PaymentProvider = "Paystack",
            PaymentReference = verify.Reference,
            PaymentStatus = "Paid"
        };
        db.Enrollments.Add(enrollment);
        course.StudentsCount += 1;
        await db.SaveChangesAsync();

        var paidUser = await db.Users.FindAsync(userId);
        if (paidUser is not null)
            await notifications.SendEnrollmentConfirmationAsync(paidUser.FullName, paidUser.Email, paidUser.PhoneNumber, course.Title);

        return Ok(ToDto(enrollment, course));
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

