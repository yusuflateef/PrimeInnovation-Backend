namespace PrimeInnovation.Domain.Entities;

public enum UserRole
{
    Student = 0,
    Instructor = 1,
    Admin = 2
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public string? GoogleId { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpires { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Course> InstructedCourses { get; set; } = new List<Course>();
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
}

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public bool IsFree => Price is null or 0;
    public double Rating { get; set; }
    public int Reviews { get; set; }
    public string Level { get; set; } = "Beginner";
    public string Category { get; set; } = string.Empty;
    public string Mode { get; set; } = "Online";
    public string SkillsJson { get; set; } = "[]";
    public int StudentsCount { get; set; }
    public bool Featured { get; set; }
    public bool Published { get; set; } = true;
    public string InstructorName { get; set; } = string.Empty;
    public string InstructorRole { get; set; } = string.Empty;
    public string InstructorBio { get; set; } = string.Empty;
    public Guid? InstructorUserId { get; set; }
    public User? InstructorUser { get; set; }
    public string? PreviewVideoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CourseModule> Modules { get; set; } = new List<CourseModule>();
    public ICollection<CourseMaterial> Materials { get; set; } = new List<CourseMaterial>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

public class CourseModule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}

public class Lesson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModuleId { get; set; }
    public CourseModule Module { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public int SortOrder { get; set; }
}

public class CourseMaterial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = "pdf";
}

public class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
}

public class AssignmentSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public class QuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Question { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]";
    public int CorrectOptionIndex { get; set; }
}

public class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public decimal ProgressPercent { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string PaymentProvider { get; set; } = "Free";
    public string? PaymentReference { get; set; }
    public string PaymentStatus { get; set; } = "Paid";

    public ICollection<LessonProgress> LessonProgresses { get; set; } = new List<LessonProgress>();
    public ICollection<LessonNote> Notes { get; set; } = new List<LessonNote>();
}

public class LessonProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class LessonNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public Guid? LessonId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Certificate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CertificateId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string StudentName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string Grade { get; set; } = "Distinction / 85%";
    public string? ProjectTitle { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public string VerificationUrl { get; set; } = "https://verify.primedigitalacademy.com";
}

public class BlogPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "Insights";
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}

public class FaqItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class ContactMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Resolved { get; set; }
}

public class Testimonial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Quote { get; set; } = string.Empty;
}

