using System.ComponentModel.DataAnnotations;

namespace PrimeInnovation.Application.DTOs;

public record RegisterRequest(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    string? PhoneNumber = null,
    string? Role = null);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record GoogleSignInRequest(
    [Required] string Email,
    [Required] string FullName,
    [Required] string GoogleId);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);
public record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword);

public record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    Guid UserId,
    string FullName,
    string Email,
    string Role);

public record CourseListItemDto(
    Guid Id,
    string Slug,
    string Title,
    string Instructor,
    string InstructorRole,
    string Duration,
    object Price,
    double Rating,
    int Reviews,
    string Level,
    string Category,
    string Overview,
    List<string> Skills,
    int Students,
    bool Featured,
    string Mode);

public record LessonDto(Guid Id, string Title, string Duration, string? VideoUrl);
public record ModuleDto(Guid Id, string Title, List<LessonDto> Lessons);

public record CourseDetailDto(
    Guid Id,
    string Slug,
    string Title,
    string Instructor,
    string InstructorRole,
    string InstructorBio,
    string Duration,
    object Price,
    double Rating,
    int Reviews,
    string Level,
    string Category,
    string Overview,
    List<string> Skills,
    int Students,
    bool Featured,
    string Mode,
    string? PreviewVideoUrl,
    List<ModuleDto> Curriculum,
    List<MaterialDto> Materials,
    List<AssignmentDto> Assignments,
    List<QuizQuestionDto> Quiz);

public record MaterialDto(Guid Id, string Title, string FileUrl, string FileType);
public record AssignmentDto(Guid Id, string Title, string Description);
public record QuizQuestionDto(Guid Id, string Question, List<string> Options);

public record CreateCourseRequest(
    [Required] string Title,
    [Required] string Overview,
    [Required] string Duration,
    decimal? Price,
    [Required] string Level,
    [Required] string Category,
    string Mode = "Online",
    List<string>? Skills = null,
    bool Featured = false,
    string? InstructorName = null,
    string? InstructorRole = null,
    string? InstructorBio = null,
    List<CreateModuleRequest>? Curriculum = null);

public record CreateModuleRequest(string Title, List<CreateLessonRequest>? Lessons);
public record CreateLessonRequest(string Title, string Duration, string? VideoUrl);

public record EnrollRequest(string PaymentProvider = "Free", string? PaymentReference = null);
public record PaymentInitRequest(string Provider = "Paystack", string? CallbackUrl = null);
public record PaymentInitResponse(
    string Provider,
    string Reference,
    string AuthorizationUrl,
    decimal Amount,
    string Currency,
    string? PublicKey = null,
    bool LiveMode = false);

public record ProgressUpdateRequest(Guid LessonId, bool Completed = true);
public record NoteRequest(string Content, Guid? LessonId = null);
public record NoteDto(Guid Id, string Content, Guid? LessonId, DateTime UpdatedAt);

public record AssignmentSubmitRequest([Required] string Content);
public record QuizSubmitRequest(Guid QuestionId, int SelectedOptionIndex);
public record QuizResultDto(bool Correct, string Message);

public record EnrollmentDto(
    Guid Id,
    Guid CourseId,
    string CourseSlug,
    string CourseTitle,
    string Instructor,
    decimal ProgressPercent,
    bool Completed,
    DateTime EnrolledAt);

public record DashboardDto(
    string FullName,
    int ActiveCourses,
    decimal AverageProgress,
    int CertificatesCount,
    List<EnrollmentDto> Courses);

public record CertificateDto(
    string CertificateId,
    string StudentName,
    string CourseName,
    string BrandName,
    string Level,
    string FieldOfStudy,
    string Duration,
    string Mode,
    string Skills,
    string Grade,
    string? ProjectTitle,
    DateTime IssueDate,
    string VerificationUrl,
    DateTime Date,
    string QrCodeUrl = "");

public record ContactRequest(
    [Required] string FullName,
    [Required, EmailAddress] string Email,
    [Required] string Topic,
    [Required] string Message);

public record BlogPostDto(string Slug, string Title, string Excerpt, string Category, DateTime Date, string? Content = null);
public record FaqDto(string Question, string Answer);
public record TestimonialDto(string Name, string Role, string Quote);
public record ProfileUpdateRequest([Required] string FullName, [Required, EmailAddress] string Email);
public record InstructorStatsDto(int PublishedCourses, int ActiveStudents, decimal AverageCompletion);

public record UpdateCourseRequest(
    string? Title,
    string? Overview,
    string? Duration,
    decimal? Price,
    bool? IsFree,
    string? Level,
    string? Category,
    string? Mode,
    bool? Featured,
    bool? Published,
    List<string>? Skills,
    string? InstructorName,
    string? InstructorRole,
    string? InstructorBio,
    string? PreviewVideoUrl);

public record AddModuleRequest([Required] string Title, List<CreateLessonRequest>? Lessons = null);
public record AddLessonRequest([Required] string Title, string Duration = "10 min", string? VideoUrl = null);
public record AddAssignmentRequest([Required] string Title, [Required] string Description);
public record AddQuizRequest([Required] string Question, [Required] List<string> Options, int CorrectOptionIndex = 0);
public record AddMaterialRequest([Required] string Title, [Required] string FileUrl, string FileType = "pdf");

public record InstructorStudentDto(
    string FullName,
    string Email,
    string Course,
    string CourseSlug,
    decimal ProgressPercent,
    bool Completed,
    DateTime EnrolledAt);

