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
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(userId);
        if (user is null) return Unauthorized();

        var enrollments = await db.Enrollments.AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        var certCount = await db.Certificates.CountAsync(c => c.UserId == userId);
        var avg = enrollments.Count == 0 ? 0 : Math.Round(enrollments.Average(e => e.ProgressPercent), 1);

        return Ok(new DashboardDto(
            user.FullName,
            enrollments.Count(e => !e.Completed),
            avg,
            certCount,
            enrollments.Select(e => new EnrollmentDto(
                e.Id, e.CourseId, e.Course.Slug, e.Course.Title, e.Course.InstructorName,
                e.ProgressPercent, e.Completed, e.EnrolledAt)).ToList()));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(ProfileUpdateRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(userId);
        if (user is null) return Unauthorized();

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != userId))
            return Conflict(new { message = "Email already in use." });

        user.FullName = request.FullName.Trim();
        user.Email = email;
        await db.SaveChangesAsync();
        return Ok(new { user.FullName, user.Email });
    }
}

