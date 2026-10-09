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
public class CoursesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<CourseListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CourseListItemDto>>> GetCourses(
        [FromQuery] string? level,
        [FromQuery] string? category,
        [FromQuery] bool? featured)
    {
        var query = db.Courses.AsNoTracking().Where(c => c.Published);

        if (!string.IsNullOrWhiteSpace(level) && !level.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(c => c.Level == level);
        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(c => c.Category == category);
        if (featured == true)
            query = query.Where(c => c.Featured);

        var courses = await query.OrderByDescending(c => c.Featured).ThenBy(c => c.Title).ToListAsync();
        return Ok(courses.Select(CourseMapper.ToListItem).ToList());
    }

    [HttpGet("categories")]
    public ActionResult<List<string>> Categories() => Ok(new List<string>
    {
        "AI & Machine Learning", "Data Analysis", "Web Development", "UI/UX",
        "Generative AI", "Data Science", "AI Automation"
    });

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CourseDetailDto>> GetBySlug(string slug)
    {
        var course = await db.Courses.AsNoTracking()
            .Include(c => c.Modules).ThenInclude(m => m.Lessons)
            .Include(c => c.Materials)
            .Include(c => c.Assignments)
            .Include(c => c.QuizQuestions)
            .FirstOrDefaultAsync(c => c.Slug == slug && c.Published);

        return course is null ? NotFound(new { message = "Course not found." }) : Ok(CourseMapper.ToDetail(course));
    }
}

