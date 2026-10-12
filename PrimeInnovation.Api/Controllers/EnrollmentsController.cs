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
public class EnrollmentsController(AppDbContext db, INotificationService notifications) : ControllerBase
{
    [HttpPost("courses/{slug}")]
    public async Task<ActionResult<EnrollmentDto>> Enroll(string slug, EnrollRequest request)
    {
        var userId = GetUserId();
        var user = await db.Users.FindAsync(userId);
        var course = await db.Courses.Include(c => c.Modules).ThenInclude(m => m.Lessons)
            .FirstOrDefaultAsync(c => c.Slug == slug && c.Published);
        if (course is null) return NotFound(new { message = "Course not found." });

        var existing = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == course.Id);
        if (existing is not null)
            return Ok(ToDto(existing, course));

        // Paid courses are only enrolled after Paystack verification (PaymentsController confirm/webhook).
        // A client-supplied reference here is never trusted.
        if (course.Price is not (null or 0))
            return BadRequest(new { message = "Payment required. Start checkout via /api/payments/initialize." });

        var enrollment = new Enrollment
        {
            UserId = userId,
            CourseId = course.Id,
            PaymentProvider = "Free",
            PaymentReference = $"FREE-{Guid.NewGuid():N}"[..20],
            PaymentStatus = "Paid"
        };
        db.Enrollments.Add(enrollment);
        course.StudentsCount += 1;
        await db.SaveChangesAsync();

        if (user is not null)
            await notifications.SendEnrollmentConfirmationAsync(user.FullName, user.Email, user.PhoneNumber, course.Title);

        return Ok(ToDto(enrollment, course));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<EnrollmentDto>>> Mine()
    {
        var userId = GetUserId();
        var items = await db.Enrollments.AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
        return Ok(items.Select(e => ToDto(e, e.Course)).ToList());
    }

    [HttpGet("courses/{slug}")]
    public async Task<ActionResult<object>> GetEnrollment(string slug)
    {
        var userId = GetUserId();
        var enrollment = await db.Enrollments
            .Include(e => e.Course).ThenInclude(c => c.Modules).ThenInclude(m => m.Lessons)
            .Include(e => e.Course).ThenInclude(c => c.Materials)
            .Include(e => e.Course).ThenInclude(c => c.Assignments)
            .Include(e => e.Course).ThenInclude(c => c.QuizQuestions)
            .Include(e => e.LessonProgresses)
            .Include(e => e.Notes)
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Course.Slug == slug);

        if (enrollment is null) return NotFound(new { message = "Not enrolled in this course." });

        return Ok(new
        {
            enrollment = ToDto(enrollment, enrollment.Course),
            course = CourseMapper.ToDetail(enrollment.Course),
            completedLessonIds = enrollment.LessonProgresses.Where(p => p.Completed).Select(p => p.LessonId).ToList(),
            notes = enrollment.Notes.Select(n => new NoteDto(n.Id, n.Content, n.LessonId, n.UpdatedAt)).ToList()
        });
    }

    private static EnrollmentDto ToDto(Enrollment e, Course c) => new(
        e.Id, c.Id, c.Slug, c.Title, c.InstructorName, e.ProgressPercent, e.Completed, e.EnrolledAt);

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

