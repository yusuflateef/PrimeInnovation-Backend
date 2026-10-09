using PrimeInnovation.Application.DTOs;
using PrimeInnovation.Domain.Entities;

namespace PrimeInnovation.Application.Common;

public static class CourseMapper
{
    public static object MapPrice(decimal? price) =>
        price is null or 0 ? "Free" : price.Value;

    public static List<string> ParseSkills(string json)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    public static CourseListItemDto ToListItem(Course c) => new(
        c.Id, c.Slug, c.Title, c.InstructorName, c.InstructorRole, c.Duration,
        MapPrice(c.Price), c.Rating, c.Reviews, c.Level, c.Category, c.Overview,
        ParseSkills(c.SkillsJson), c.StudentsCount, c.Featured, c.Mode);

    public static CourseDetailDto ToDetail(Course c) => new(
        c.Id, c.Slug, c.Title, c.InstructorName, c.InstructorRole, c.InstructorBio,
        c.Duration, MapPrice(c.Price), c.Rating, c.Reviews, c.Level, c.Category, c.Overview,
        ParseSkills(c.SkillsJson), c.StudentsCount, c.Featured, c.Mode, c.PreviewVideoUrl,
        c.Modules.OrderBy(m => m.SortOrder).Select(m => new ModuleDto(
            m.Id, m.Title,
            m.Lessons.OrderBy(l => l.SortOrder).Select(l => new LessonDto(l.Id, l.Title, l.Duration, l.VideoUrl)).ToList()
        )).ToList(),
        c.Materials.Select(m => new MaterialDto(m.Id, m.Title, m.FileUrl, m.FileType)).ToList(),
        c.Assignments.Select(a => new AssignmentDto(a.Id, a.Title, a.Description)).ToList(),
        c.QuizQuestions.Select(q => new QuizQuestionDto(
            q.Id, q.Question,
            System.Text.Json.JsonSerializer.Deserialize<List<string>>(q.OptionsJson) ?? []
        )).ToList());

    public static string Slugify(string title)
    {
        var slug = new string(title.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
