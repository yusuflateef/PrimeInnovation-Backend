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
[Route("api/learn")]
[Authorize]
public class LearnController(AppDbContext db) : ControllerBase
{
    [HttpPost("{slug}/progress")]
    public async Task<ActionResult<object>> UpdateProgress(string slug, ProgressUpdateRequest request)
    {
        var enrollment = await GetEnrollment(slug);
        if (enrollment is null) return NotFound(new { message = "Not enrolled." });

        var lessonExists = enrollment.Course.Modules.SelectMany(m => m.Lessons).Any(l => l.Id == request.LessonId);
        if (!lessonExists) return BadRequest(new { message = "Lesson not found in course." });

        var progress = await db.LessonProgresses
            .FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.Id && p.LessonId == request.LessonId);

        if (progress is null)
        {
            progress = new LessonProgress
            {
                EnrollmentId = enrollment.Id,
                LessonId = request.LessonId,
                Completed = request.Completed,
                CompletedAt = request.Completed ? DateTime.UtcNow : null
            };
            db.LessonProgresses.Add(progress);
        }
        else
        {
            progress.Completed = request.Completed;
            progress.CompletedAt = request.Completed ? DateTime.UtcNow : null;
        }

        await db.SaveChangesAsync();

        var totalLessons = enrollment.Course.Modules.SelectMany(m => m.Lessons).Count();
        var completedCount = await db.LessonProgresses.CountAsync(p => p.EnrollmentId == enrollment.Id && p.Completed);
        enrollment.ProgressPercent = totalLessons == 0 ? 0 : Math.Round((decimal)completedCount / totalLessons * 100, 1);
        if (enrollment.ProgressPercent >= 100 && !enrollment.Completed)
        {
            enrollment.Completed = true;
            enrollment.CompletedAt = DateTime.UtcNow;
            enrollment.ProgressPercent = 100;
            await EnsureCertificate(enrollment);
        }

        await db.SaveChangesAsync();
        return Ok(new { enrollment.ProgressPercent, enrollment.Completed, completedLessons = completedCount, totalLessons });
    }

    [HttpGet("{slug}/notes")]
    public async Task<ActionResult<List<NoteDto>>> GetNotes(string slug)
    {
        var enrollment = await GetEnrollment(slug);
        if (enrollment is null) return NotFound(new { message = "Not enrolled." });
        var notes = await db.LessonNotes.Where(n => n.EnrollmentId == enrollment.Id)
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NoteDto(n.Id, n.Content, n.LessonId, n.UpdatedAt))
            .ToListAsync();
        return Ok(notes);
    }

    [HttpPost("{slug}/notes")]
    public async Task<ActionResult<NoteDto>> SaveNote(string slug, NoteRequest request)
    {
        var enrollment = await GetEnrollment(slug);
        if (enrollment is null) return NotFound(new { message = "Not enrolled." });

        var note = await db.LessonNotes.FirstOrDefaultAsync(n =>
            n.EnrollmentId == enrollment.Id && n.LessonId == request.LessonId);

        if (note is null)
        {
            note = new LessonNote
            {
                EnrollmentId = enrollment.Id,
                LessonId = request.LessonId,
                Content = request.Content
            };
            db.LessonNotes.Add(note);
        }
        else
        {
            note.Content = request.Content;
            note.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new NoteDto(note.Id, note.Content, note.LessonId, note.UpdatedAt));
    }

    [HttpPost("{slug}/assignments/{assignmentId:guid}")]
    public async Task<IActionResult> SubmitAssignment(string slug, Guid assignmentId, AssignmentSubmitRequest request)
    {
        var enrollment = await GetEnrollment(slug);
        if (enrollment is null) return NotFound(new { message = "Not enrolled." });

        var assignment = await db.Assignments.FirstOrDefaultAsync(a => a.Id == assignmentId && a.CourseId == enrollment.CourseId);
        if (assignment is null) return NotFound(new { message = "Assignment not found." });

        db.AssignmentSubmissions.Add(new AssignmentSubmission
        {
            AssignmentId = assignmentId,
            UserId = enrollment.UserId,
            Content = request.Content
        });
        await db.SaveChangesAsync();
        return Ok(new { message = "Assignment submitted." });
    }

    [HttpPost("{slug}/quiz")]
    public async Task<ActionResult<QuizResultDto>> SubmitQuiz(string slug, QuizSubmitRequest request)
    {
        var enrollment = await GetEnrollment(slug);
        if (enrollment is null) return NotFound(new { message = "Not enrolled." });

        var question = await db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == request.QuestionId && q.CourseId == enrollment.CourseId);
        if (question is null) return NotFound(new { message = "Question not found." });

        var correct = question.CorrectOptionIndex == request.SelectedOptionIndex;
        return Ok(new QuizResultDto(correct, correct ? "Correct â€” well done!" : "Not quite. Review the lesson and try again."));
    }

    private async Task<Enrollment?> GetEnrollment(string slug)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db.Enrollments
            .Include(e => e.Course).ThenInclude(c => c.Modules).ThenInclude(m => m.Lessons)
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Course.Slug == slug);
    }

    private async Task EnsureCertificate(Enrollment enrollment)
    {
        if (await db.Certificates.AnyAsync(c => c.UserId == enrollment.UserId && c.CourseId == enrollment.CourseId))
            return;

        var user = await db.Users.FindAsync(enrollment.UserId);
        var course = enrollment.Course;
        var skills = string.Join(", ", CourseMapper.ParseSkills(course.SkillsJson));
        var year = DateTime.UtcNow.Year;
        var seq = await db.Certificates.CountAsync() + 1;

        db.Certificates.Add(new Certificate
        {
            CertificateId = $"PIN-PDA-{year}-{seq:D5}",
            UserId = enrollment.UserId,
            CourseId = course.Id,
            StudentName = user?.FullName ?? "Student",
            CourseTitle = course.Title,
            Level = course.Level,
            FieldOfStudy = course.Category,
            Duration = course.Duration,
            Mode = course.Mode,
            Skills = skills,
            Grade = "Distinction / 88%",
            ProjectTitle = "Capstone Project",
            IssueDate = DateTime.UtcNow
        });
    }
}

