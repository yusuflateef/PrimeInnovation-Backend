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
public class CertificatesController(AppDbContext db) : ControllerBase
{
    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<CertificateDto>>> Mine()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var certs = await db.Certificates.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.IssueDate)
            .ToListAsync();
        return Ok(certs.Select(ToDto).ToList());
    }

    [Authorize]
    [HttpPost("generate/{slug}")]
    public async Task<ActionResult<CertificateDto>> Generate(string slug)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var enrollment = await db.Enrollments
            .Include(e => e.Course)
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Course.Slug == slug);

        if (enrollment is null) return NotFound(new { message = "Not enrolled." });
        if (!enrollment.Completed && enrollment.ProgressPercent < 100)
            return BadRequest(new { message = "Complete the course before generating a certificate." });

        enrollment.Completed = true;
        enrollment.CompletedAt ??= DateTime.UtcNow;
        enrollment.ProgressPercent = 100;

        var existing = await db.Certificates.FirstOrDefaultAsync(c => c.UserId == userId && c.CourseId == enrollment.CourseId);
        if (existing is not null) return Ok(ToDto(existing));

        var course = enrollment.Course;
        var year = DateTime.UtcNow.Year;
        var seq = await db.Certificates.CountAsync() + 1;
        var cert = new Certificate
        {
            CertificateId = $"PIN-PDA-{year}-{seq:D5}",
            UserId = userId,
            CourseId = course.Id,
            StudentName = enrollment.User.FullName,
            CourseTitle = course.Title,
            Level = course.Level,
            FieldOfStudy = course.Category,
            Duration = course.Duration,
            Mode = course.Mode,
            Skills = string.Join(", ", CourseMapper.ParseSkills(course.SkillsJson)),
            Grade = "Distinction / 88%",
            ProjectTitle = "Capstone Project",
            IssueDate = DateTime.UtcNow
        };
        db.Certificates.Add(cert);
        await db.SaveChangesAsync();
        return Ok(ToDto(cert));
    }

    [HttpGet("verify/{certificateId}")]
    public async Task<ActionResult<CertificateDto>> Verify(string certificateId)
    {
        var cert = await db.Certificates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId);
        return cert is null ? NotFound(new { message = "Certificate not found." }) : Ok(ToDto(cert));
    }

    private static CertificateDto ToDto(Certificate c)
    {
        var verify = $"{c.VerificationUrl}?id={Uri.EscapeDataString(c.CertificateId)}";
        var qr = $"https://api.qrserver.com/v1/create-qr-code/?size=160x160&data={Uri.EscapeDataString(verify)}";
        return new(
            c.CertificateId,
            c.StudentName,
            c.CourseTitle,
            "Prime Innovation Network",
            c.Level,
            c.FieldOfStudy,
            c.Duration,
            c.Mode,
            c.Skills,
            c.Grade,
            c.ProjectTitle,
            c.IssueDate,
            verify,
            c.IssueDate,
            qr);
    }
}

