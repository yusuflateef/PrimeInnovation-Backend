using Microsoft.EntityFrameworkCore;
using PrimeInnovation.Domain.Entities;

namespace PrimeInnovation.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseModule> Modules => Set<CourseModule>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<CourseMaterial> Materials => Set<CourseMaterial>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LessonProgress> LessonProgresses => Set<LessonProgress>();
    public DbSet<LessonNote> LessonNotes => Set<LessonNote>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<FaqItem> Faqs => Set<FaqItem>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.FullName).HasMaxLength(120);
        });

        modelBuilder.Entity<Course>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.InstructorUser)
                .WithMany(x => x.InstructedCourses)
                .HasForeignKey(x => x.InstructorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Enrollment>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();
            e.HasOne(x => x.User)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Course)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Certificate>(e =>
        {
            e.HasIndex(x => x.CertificateId).IsUnique();
            e.HasOne(x => x.User)
                .WithMany(x => x.Certificates)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Course)
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LessonProgress>(e =>
        {
            e.HasIndex(x => new { x.EnrollmentId, x.LessonId }).IsUnique();
            e.HasOne(x => x.Enrollment)
                .WithMany(x => x.LessonProgresses)
                .HasForeignKey(x => x.EnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Lesson)
                .WithMany()
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LessonNote>(e =>
        {
            e.HasOne(x => x.Enrollment)
                .WithMany(x => x.Notes)
                .HasForeignKey(x => x.EnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssignmentSubmission>(e =>
        {
            e.HasOne(x => x.Assignment)
                .WithMany(x => x.Submissions)
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

