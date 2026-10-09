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
public class ContentController(AppDbContext db, INotificationService notifications) : ControllerBase
{
    [HttpGet("testimonials")]
    public async Task<ActionResult<List<TestimonialDto>>> Testimonials() =>
        Ok(await db.Testimonials.AsNoTracking()
            .Select(t => new TestimonialDto(t.Name, t.Role, t.Quote))
            .ToListAsync());

    [HttpGet("blog")]
    public async Task<ActionResult<List<BlogPostDto>>> Blog() =>
        Ok(await db.BlogPosts.AsNoTracking()
            .OrderByDescending(b => b.PublishedAt)
            .Select(b => new BlogPostDto(b.Slug, b.Title, b.Excerpt, b.Category, b.PublishedAt, null))
            .ToListAsync());

    [HttpGet("blog/{slug}")]
    public async Task<ActionResult<BlogPostDto>> BlogPost(string slug)
    {
        var post = await db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(b => b.Slug == slug);
        return post is null
            ? NotFound()
            : Ok(new BlogPostDto(post.Slug, post.Title, post.Excerpt, post.Category, post.PublishedAt, post.Content));
    }

    [HttpGet("faqs")]
    public async Task<ActionResult<List<FaqDto>>> Faqs() =>
        Ok(await db.Faqs.AsNoTracking().OrderBy(f => f.SortOrder)
            .Select(f => new FaqDto(f.Question, f.Answer)).ToListAsync());

    [HttpPost("contact")]
    public async Task<IActionResult> Contact(ContactRequest request)
    {
        db.ContactMessages.Add(new ContactMessage
        {
            FullName = request.FullName,
            Email = request.Email,
            Topic = request.Topic,
            Message = request.Message
        });
        await db.SaveChangesAsync();
        await notifications.NotifyAdminContactAsync(request.FullName, request.Email, request.Topic, request.Message);
        return Ok(new { message = "Thanks — your message has been received." });
    }

    [HttpGet("home")]
    public async Task<ActionResult<object>> Home()
    {
        var featured = await db.Courses.AsNoTracking()
            .Where(c => c.Published && c.Featured)
            .OrderBy(c => c.Title)
            .ToListAsync();
        var testimonials = await db.Testimonials.AsNoTracking().ToListAsync();
        return Ok(new
        {
            brand = "Prime Innovation Network",
            academy = "Prime Digital Academy",
            featuredCourses = featured.Select(CourseMapper.ToListItem),
            categories = new[]
            {
                new { name = "AI", label = "Artificial Intelligence", category = "AI & Machine Learning" },
                new { name = "Data", label = "Data & Analytics", category = "Data Analysis" },
                new { name = "Programming", label = "Web Development", category = "Web Development" },
                new { name = "Design", label = "UI/UX Design", category = "UI/UX" }
            },
            testimonials = testimonials.Select(t => new TestimonialDto(t.Name, t.Role, t.Quote))
        });
    }
}

