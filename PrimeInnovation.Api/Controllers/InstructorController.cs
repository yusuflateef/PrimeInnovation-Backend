using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
[Authorize(Roles = "Instructor,Admin")]
public class InstructorController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<InstructorStatsDto>> Stats()
    {
        var userId = CurrentUserId();
        var courseQuery = db.Courses.AsQueryable();
        if (!IsAdmin())
            courseQuery = courseQuery.Where(c => c.InstructorUserId == userId);

        var courseIds = await courseQuery.Select(c => c.Id).ToListAsync();
        var enrollments = await db.Enrollments.Where(e => courseIds.Contains(e.CourseId)).ToListAsync();
        var avg = enrollments.Count == 0 ? 0 : Math.Round(enrollments.Average(e => e.ProgressPercent), 1);
        return Ok(new InstructorStatsDto(courseIds.Count, enrollments.Select(e => e.UserId).Distinct().Count(), avg));
    }

    [HttpGet("students")]
    public async Task<ActionResult<List<InstructorStudentDto>>> Students([FromQuery] string? courseSlug = null)
    {
        var userId = CurrentUserId();
        var query = db.Enrollments.AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.Course)
            .AsQueryable();

        if (!IsAdmin())
            query = query.Where(e => e.Course.InstructorUserId == userId);
        if (!string.IsNullOrWhiteSpace(courseSlug))
            query = query.Where(e => e.Course.Slug == courseSlug);

        var data = await query
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new InstructorStudentDto(
                e.User.FullName,
                e.User.Email,
                e.Course.Title,
                e.Course.Slug,
                e.ProgressPercent,
                e.Completed,
                e.EnrolledAt))
            .ToListAsync();
        return Ok(data);
    }

    [HttpGet("courses")]
    public async Task<ActionResult<List<CourseDetailDto>>> MyCourses()
    {
        var userId = CurrentUserId();
        var query = db.Courses
            .Include(c => c.Modules).ThenInclude(m => m.Lessons)
            .Include(c => c.Materials)
            .Include(c => c.Assignments)
            .Include(c => c.QuizQuestions)
            .AsQueryable();
        if (!IsAdmin())
            query = query.Where(c => c.InstructorUserId == userId);

        var courses = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return Ok(courses.Select(CourseMapper.ToDetail).ToList());
    }

    [HttpPost("courses")]
    public async Task<ActionResult<CourseDetailDto>> CreateCourse(CreateCourseRequest request)
    {
        var userId = CurrentUserId();
        var user = await db.Users.FindAsync(userId);
        var slug = await UniqueSlug(request.Title);

        var course = new Course
        {
            Slug = slug,
            Title = request.Title,
            Overview = request.Overview,
            Duration = request.Duration,
            Price = request.Price is 0 ? null : request.Price,
            Level = request.Level,
            Category = request.Category,
            Mode = request.Mode,
            Featured = request.Featured,
            SkillsJson = System.Text.Json.JsonSerializer.Serialize(request.Skills ?? []),
            InstructorUserId = userId,
            InstructorName = request.InstructorName ?? user?.FullName ?? "Instructor",
            InstructorRole = request.InstructorRole ?? "Instructor",
            InstructorBio = request.InstructorBio ?? "Prime Digital Academy instructor.",
            Published = true,
            Rating = 5,
            Reviews = 0,
            StudentsCount = 0
        };

        ApplyCurriculum(course, request.Curriculum);
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(CoursesController.GetBySlug), "Courses", new { slug = course.Slug }, await Detail(course.Id));
    }

    [HttpPut("courses/{slug}")]
    public async Task<ActionResult<CourseDetailDto>> UpdateCourse(string slug, UpdateCourseRequest request)
    {
        var course = await OwnedCourse(slug);
        if (course is null) return NotFound(new { message = "Course not found." });

        if (request.Title is not null) course.Title = request.Title;
        if (request.Overview is not null) course.Overview = request.Overview;
        if (request.Duration is not null) course.Duration = request.Duration;
        if (request.IsFree == true) course.Price = null;
        else if (request.Price is not null) course.Price = request.Price == 0 ? null : request.Price;
        if (request.Level is not null) course.Level = request.Level;
        if (request.Category is not null) course.Category = request.Category;
        if (request.Mode is not null) course.Mode = request.Mode;
        if (request.Featured is not null) course.Featured = request.Featured.Value;
        if (request.Published is not null) course.Published = request.Published.Value;
        if (request.Skills is not null) course.SkillsJson = System.Text.Json.JsonSerializer.Serialize(request.Skills);
        if (request.InstructorName is not null) course.InstructorName = request.InstructorName;
        if (request.InstructorRole is not null) course.InstructorRole = request.InstructorRole;
        if (request.InstructorBio is not null) course.InstructorBio = request.InstructorBio;
        if (request.PreviewVideoUrl is not null) course.PreviewVideoUrl = request.PreviewVideoUrl;

        await db.SaveChangesAsync();
        return Ok(await Detail(course.Id));
    }

    [HttpDelete("courses/{slug}")]
    public async Task<IActionResult> DeleteCourse(string slug)
    {
        var course = await OwnedCourse(slug, tracking: true, includeChildren: true);
        if (course is null) return NotFound(new { message = "Course not found." });

        var enrollmentIds = await db.Enrollments.Where(e => e.CourseId == course.Id).Select(e => e.Id).ToListAsync();
        db.LessonProgresses.RemoveRange(db.LessonProgresses.Where(p => enrollmentIds.Contains(p.EnrollmentId)));
        db.LessonNotes.RemoveRange(db.LessonNotes.Where(n => enrollmentIds.Contains(n.EnrollmentId)));
        var assignmentIds = course.Assignments.Select(a => a.Id).ToList();
        db.AssignmentSubmissions.RemoveRange(db.AssignmentSubmissions.Where(s => assignmentIds.Contains(s.AssignmentId)));
        db.Certificates.RemoveRange(db.Certificates.Where(c => c.CourseId == course.Id));
        db.Enrollments.RemoveRange(db.Enrollments.Where(e => e.CourseId == course.Id));
        db.Courses.Remove(course);
        await db.SaveChangesAsync();
        return Ok(new { message = "Course deleted." });
    }

    [HttpPost("courses/{slug}/modules")]
    public async Task<ActionResult<CourseDetailDto>> AddModule(string slug, AddModuleRequest request)
    {
        var course = await OwnedCourse(slug, tracking: true, includeChildren: true);
        if (course is null) return NotFound(new { message = "Course not found." });

        var module = new CourseModule
        {
            Title = request.Title,
            SortOrder = course.Modules.Count == 0 ? 0 : course.Modules.Max(m => m.SortOrder) + 1
        };
        var lOrder = 0;
        foreach (var l in request.Lessons ?? [])
        {
            module.Lessons.Add(new Lesson
            {
                Title = l.Title,
                Duration = l.Duration,
                VideoUrl = l.VideoUrl,
                SortOrder = lOrder++
            });
        }
        course.Modules.Add(module);
        await db.SaveChangesAsync();
        return Ok(await Detail(course.Id));
    }

    [HttpPost("courses/{slug}/modules/{moduleId:guid}/lessons")]
    public async Task<ActionResult<CourseDetailDto>> AddLesson(string slug, Guid moduleId, AddLessonRequest request)
    {
        var course = await OwnedCourse(slug, tracking: true, includeChildren: true);
        if (course is null) return NotFound(new { message = "Course not found." });
        var module = course.Modules.FirstOrDefault(m => m.Id == moduleId);
        if (module is null) return NotFound(new { message = "Module not found." });

        module.Lessons.Add(new Lesson
        {
            Title = request.Title,
            Duration = request.Duration,
            VideoUrl = request.VideoUrl,
            SortOrder = module.Lessons.Count == 0 ? 0 : module.Lessons.Max(l => l.SortOrder) + 1
        });
        await db.SaveChangesAsync();
        return Ok(await Detail(course.Id));
    }

    [HttpDelete("courses/{slug}/modules/{moduleId:guid}")]
    public async Task<ActionResult<CourseDetailDto>> DeleteModule(string slug, Guid moduleId)
    {
        var course = await OwnedCourse(slug, tracking: true, includeChildren: true);
        if (course is null) return NotFound(new { message = "Course not found." });
        var module = course.Modules.FirstOrDefault(m => m.Id == moduleId);
        if (module is null) return NotFound(new { message = "Module not found." });
        db.Modules.Remove(module);
        await db.SaveChangesAsync();
        return Ok(await Detail(course.Id));
    }

    [HttpPost("courses/{slug}/assignments")]
    public async Task<ActionResult<CourseDetailDto>> AddAssignment(string slug, AddAssignmentRequest request)
    {
        var course = await OwnedCourse(slug);
        if (course is null) return NotFound(new { message = "Course not found." });
        db.Assignments.Add(new Assignment { CourseId = course.Id, Title = request.Title, Description = request.Description });
        await db.SaveChangesAsync();
        return Ok(await Detail(course.Id));
    }

    [HttpPost("courses/{slug}/quiz")]
    public async Task<ActionResult<CourseDetailDto>> AddQuiz(string slug, AddQuizRequest request)
    {
        var course = await OwnedCourse(slug);
        if (course is null) return NotFound(new { message = "Course not found." });
        if (request.Options.Count == 0) return BadRequest(new { message = "At least one option is required." });
        db.QuizQuestions.Add(new QuizQuestion
        {
            CourseId = course.Id,
            Question = request.Question,
            OptionsJson = System.Text.Json.JsonSerializer.Serialize(request.Options),
            CorrectOptionIndex = Math.Clamp(request.CorrectOptionIndex, 0, request.Options.Count - 1)
        });
        await db.SaveChangesAsync();
        return Ok(await Detail(course.Id));
    }

    [HttpPost("courses/{slug}/materials")]
    public async Task<ActionResult<CourseDetailDto>> AddMaterial(string slug, AddMaterialRequest request)
    {
        var course = await OwnedCourse(slug);
        if (course is null) return NotFound(new { message = "Course not found." });
        db.Materials.Add(new CourseMaterial
        {
            CourseId = course.Id,
            Title = request.Title,
            FileUrl = request.FileUrl,
            FileType = request.FileType
        });
        await db.SaveChangesAsync();
        return Ok(await Detail(course.Id));
    }

    [HttpPost("courses/{slug}/upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<object>> UploadFile(string slug, IFormFile file, [FromForm] string? title = null, [FromForm] string kind = "material")
    {
        var course = await OwnedCourse(slug);
        if (course is null) return NotFound(new { message = "Course not found." });
        if (file is null || file.Length == 0) return BadRequest(new { message = "No file uploaded." });

        var uploadsRoot = Path.Combine(env.ContentRootPath, "wwwroot", "uploads", course.Slug);
        Directory.CreateDirectory(uploadsRoot);
        var safeName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var path = Path.Combine(uploadsRoot, safeName);
        await using (var stream = System.IO.File.Create(path))
            await file.CopyToAsync(stream);

        var url = $"/uploads/{course.Slug}/{safeName}";
        var ext = Path.GetExtension(file.FileName).Trim('.').ToLowerInvariant();

        if (string.Equals(kind, "preview", StringComparison.OrdinalIgnoreCase))
        {
            course.PreviewVideoUrl = url;
            await db.SaveChangesAsync();
            return Ok(new { url, kind = "preview", course = await Detail(course.Id) });
        }

        db.Materials.Add(new CourseMaterial
        {
            CourseId = course.Id,
            Title = string.IsNullOrWhiteSpace(title) ? file.FileName : title,
            FileUrl = url,
            FileType = string.IsNullOrWhiteSpace(ext) ? "file" : ext
        });
        await db.SaveChangesAsync();
        return Ok(new { url, kind = "material", course = await Detail(course.Id) });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin() => User.IsInRole("Admin");

    private async Task<string> UniqueSlug(string title)
    {
        var slugBase = CourseMapper.Slugify(title);
        var slug = slugBase;
        var i = 1;
        while (await db.Courses.AnyAsync(c => c.Slug == slug))
            slug = $"{slugBase}-{i++}";
        return slug;
    }

    private static void ApplyCurriculum(Course course, List<CreateModuleRequest>? curriculum)
    {
        var mOrder = 0;
        foreach (var m in curriculum ?? [])
        {
            var module = new CourseModule { Title = m.Title, SortOrder = mOrder++ };
            var lOrder = 0;
            foreach (var l in m.Lessons ?? [])
            {
                module.Lessons.Add(new Lesson
                {
                    Title = l.Title,
                    Duration = l.Duration,
                    VideoUrl = l.VideoUrl,
                    SortOrder = lOrder++
                });
            }
            course.Modules.Add(module);
        }
    }

    private async Task<Course?> OwnedCourse(string slug, bool tracking = true, bool includeChildren = false)
    {
        var userId = CurrentUserId();
        IQueryable<Course> query = tracking ? db.Courses : db.Courses.AsNoTracking();
        if (includeChildren)
        {
            query = query
                .Include(c => c.Modules).ThenInclude(m => m.Lessons)
                .Include(c => c.Materials)
                .Include(c => c.Assignments)
                .Include(c => c.QuizQuestions);
        }

        var course = await query.FirstOrDefaultAsync(c => c.Slug == slug);
        if (course is null) return null;
        if (!IsAdmin() && course.InstructorUserId != userId) return null;
        return course;
    }

    private async Task<CourseDetailDto> Detail(Guid id)
    {
        var course = await db.Courses
            .Include(c => c.Modules).ThenInclude(m => m.Lessons)
            .Include(c => c.Materials)
            .Include(c => c.Assignments)
            .Include(c => c.QuizQuestions)
            .FirstAsync(c => c.Id == id);
        return CourseMapper.ToDetail(course);
    }
}


