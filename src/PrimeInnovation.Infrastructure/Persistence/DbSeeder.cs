using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrimeInnovation.Domain.Entities;

namespace PrimeInnovation.Infrastructure.Persistence;

public static class DbSeeder
{
    /// <summary>Inserts demo users/courses/content into an empty database. Schema is managed by EF migrations.</summary>
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!db.Users.Any())
        {
            var admin = new User
            {
                FullName = "Dr. Ngozi Adewale",
                Email = "admin@primeinnovation.network",
                PhoneNumber = "2348011111111",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
                Role = UserRole.Admin
            };
            var instructor = new User
            {
                FullName = "Dr. Ada Okonkwo",
                Email = "ada@primedigitalacademy.com",
                PhoneNumber = "2348022222222",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Instructor@123"),
                Role = UserRole.Instructor
            };
            var student = new User
            {
                FullName = "Alex Johnson",
                Email = "alex@example.com",
                PhoneNumber = "2348033333333",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student@123"),
                Role = UserRole.Student
            };
            db.Users.AddRange(admin, instructor, student);
            await db.SaveChangesAsync();
        }

        if (!db.Courses.Any())
        {
            var instructor = db.Users.First(u => u.Role == UserRole.Instructor);
            var courses = BuildCourses(instructor.Id);
            db.Courses.AddRange(courses);
            await db.SaveChangesAsync();

            foreach (var course in courses)
            {
                db.Materials.Add(new CourseMaterial
                {
                    CourseId = course.Id,
                    Title = $"{course.Title} — Slide Deck",
                    FileUrl = $"/files/{course.Slug}-slides.pdf",
                    FileType = "pdf"
                });
                db.Materials.Add(new CourseMaterial
                {
                    CourseId = course.Id,
                    Title = $"{course.Title} — Dataset",
                    FileUrl = $"/files/{course.Slug}-dataset.zip",
                    FileType = "zip"
                });
                db.Assignments.Add(new Assignment
                {
                    CourseId = course.Id,
                    Title = "Assignment 1",
                    Description = "Complete the practical exercise and submit your notebook or project link."
                });
                db.QuizQuestions.Add(new QuizQuestion
                {
                    CourseId = course.Id,
                    Question = "Which step comes first when framing an AI/data problem?",
                    OptionsJson = JsonSerializer.Serialize(new[]
                    {
                        "Define the business outcome",
                        "Train the largest model available",
                        "Skip data cleaning",
                        "Deploy immediately"
                    }),
                    CorrectOptionIndex = 0
                });
            }
            await db.SaveChangesAsync();
        }

        if (!db.BlogPosts.Any())
        {
            db.BlogPosts.AddRange(
                new BlogPost
                {
                    Slug = "future-of-ai-education-africa",
                    Title = "The Future of AI Education in Africa",
                    Excerpt = "Why practical, accessible AI training is the next infrastructure for opportunity.",
                    Content = "Practical AI education is becoming essential infrastructure across Africa...",
                    Category = "Insights",
                    PublishedAt = new DateTime(2026, 6, 12)
                },
                new BlogPost
                {
                    Slug = "building-job-ready-data-talent",
                    Title = "Building Job-Ready Data Talent",
                    Excerpt = "How Prime Digital Academy designs programs that employers actually trust.",
                    Content = "Employers trust programs that combine theory with measurable project outcomes...",
                    Category = "Academy",
                    PublishedAt = new DateTime(2026, 5, 28)
                },
                new BlogPost
                {
                    Slug = "partners-ngos-investors",
                    Title = "Partnering for Scale: NGOs & Investors",
                    Excerpt = "A look at how collaboration expands access to digital skills across communities.",
                    Content = "Partnerships with NGOs and investors help Prime Innovation Network scale access...",
                    Category = "Network",
                    PublishedAt = new DateTime(2026, 5, 4)
                });
        }

        if (!db.Faqs.Any())
        {
            db.Faqs.AddRange(
                new FaqItem
                {
                    SortOrder = 1,
                    Question = "What is Prime Innovation Network?",
                    Answer = "Prime Innovation Network is our official corporate identity and brand authority — showcasing vision, programs, and partnerships. Prime Digital Academy is our learning portal powered by the Network."
                },
                new FaqItem
                {
                    SortOrder = 2,
                    Question = "Are courses free or paid?",
                    Answer = "We offer both. Some foundational courses are free; advanced and specialized programs are paid. Prices are shown on each course page."
                },
                new FaqItem
                {
                    SortOrder = 3,
                    Question = "Do I get a certificate?",
                    Answer = "Yes. After completing a program and meeting requirements, you receive a downloadable Certificate of Completion from Prime Digital Academy with a unique verification ID."
                },
                new FaqItem
                {
                    SortOrder = 4,
                    Question = "What payment methods do you accept?",
                    Answer = "We support Paystack and Flutterwave for secure local and international payments."
                },
                new FaqItem
                {
                    SortOrder = 5,
                    Question = "Can I learn at my own pace?",
                    Answer = "Most online courses are self-paced within the program window, with progress tracking, notes, quizzes, and downloadable materials in your learning dashboard."
                },
                new FaqItem
                {
                    SortOrder = 6,
                    Question = "How do I become an instructor?",
                    Answer = "Apply through the Instructor Panel. Approved instructors can upload courses, manage content, and track students."
                });
        }

        if (!db.Testimonials.Any())
        {
            db.Testimonials.AddRange(
                new Testimonial
                {
                    Name = "Amaka Eze",
                    Role = "Data Analyst, Lagos",
                    Quote = "Prime Digital Academy gave me the skills and confidence to land my first analytics role within three months."
                },
                new Testimonial
                {
                    Name = "Ibrahim Yusuf",
                    Role = "NGO Program Manager",
                    Quote = "The AI automation course helped our team cut reporting time in half. Practical, relevant, and well taught."
                },
                new Testimonial
                {
                    Name = "Sarah Mensah",
                    Role = "Product Designer",
                    Quote = "Clear curriculum, strong mentorship, and a certificate that actually reflects real competence."
                });
        }

        await db.SaveChangesAsync();
    }

    private static List<Course> BuildCourses(Guid instructorId)
    {
        Course Make(
            string slug,
            string title,
            string instructor,
            string role,
            string bio,
            string duration,
            decimal? price,
            double rating,
            int reviews,
            string level,
            string category,
            string overview,
            string[] skills,
            int students,
            bool featured,
            string mode,
            (string title, (string title, string duration)[] lessons)[] modules)
        {
            var course = new Course
            {
                Slug = slug,
                Title = title,
                InstructorName = instructor,
                InstructorRole = role,
                InstructorBio = bio,
                InstructorUserId = instructorId,
                Duration = duration,
                Price = price,
                Rating = rating,
                Reviews = reviews,
                Level = level,
                Category = category,
                Overview = overview,
                SkillsJson = JsonSerializer.Serialize(skills),
                StudentsCount = students,
                Featured = featured,
                Mode = mode,
                PreviewVideoUrl = $"https://cdn.primedigitalacademy.com/previews/{slug}.mp4",
                Published = true
            };

            var mOrder = 0;
            foreach (var m in modules)
            {
                var module = new CourseModule
                {
                    Title = m.title,
                    SortOrder = mOrder++
                };
                var lOrder = 0;
                foreach (var l in m.lessons)
                {
                    module.Lessons.Add(new Lesson
                    {
                        Title = l.title,
                        Duration = l.duration,
                        SortOrder = lOrder++,
                        VideoUrl = $"https://cdn.primedigitalacademy.com/lessons/{slug}/{lOrder}.mp4"
                    });
                }
                course.Modules.Add(module);
            }

            return course;
        }

        return
        [
            Make("ai-fundamentals", "AI Fundamentals for Innovators", "Dr. Ada Okonkwo", "AI Research Lead",
                "Former ML engineer with 10+ years building production AI systems across Africa and Europe.",
                "6 Weeks", null, 4.9, 312, "Beginner", "AI & Machine Learning",
                "Build a practical foundation in artificial intelligence — from core concepts to real-world applications. Ideal for beginners ready to enter the AI economy.",
                ["Machine Learning Basics", "Python for AI", "Model Evaluation", "Problem Framing"], 1840, true, "Online",
                [
                    ("Introduction to AI", [("What is Artificial Intelligence?", "12 min"), ("History & Modern Applications", "18 min"), ("Ethics and Responsible AI", "15 min")]),
                    ("Machine Learning Essentials", [("Supervised vs Unsupervised Learning", "22 min"), ("Your First Model in Python", "35 min"), ("Evaluating Model Performance", "20 min")]),
                    ("Capstone Project", [("Project Brief & Dataset", "10 min"), ("Build & Present Your Solution", "45 min")])
                ]),
            Make("data-analysis-with-python", "Data Analysis with Python", "Chinedu Bello", "Senior Data Analyst",
                "Data lead who has trained 2,000+ analysts for startups, NGOs, and government programs.",
                "8 Weeks", 45000, 4.8, 256, "Beginner", "Data Analysis",
                "Master data cleaning, visualization, and insight storytelling with Python, Pandas, and modern analytics workflows used in industry.",
                ["Data Cleaning", "Data Visualization", "Python Programming", "Problem Solving"], 2210, true, "Hybrid",
                [
                    ("Python for Analysts", [("Setup & Notebooks", "15 min"), ("Pandas Essentials", "28 min")]),
                    ("Visualization & Storytelling", [("Charts that Communicate", "24 min"), ("Dashboard Thinking", "30 min")])
                ]),
            Make("full-stack-web-development", "Full-Stack Web Development", "Tomiwa Adeyemi", "Lead Software Engineer",
                "Builds scalable products for edtech and fintech. Passionate about teaching clean architecture.",
                "12 Weeks", 85000, 4.7, 189, "Intermediate", "Web Development",
                "Learn to design, build, and deploy modern web applications — from responsive frontends to secure APIs and databases.",
                ["HTML/CSS", "JavaScript", "React", "APIs", "Deployment"], 1560, true, "Online",
                [
                    ("Frontend Foundations", [("Semantic HTML & Modern CSS", "25 min"), ("JavaScript Deep Dive", "40 min")]),
                    ("React & Backend", [("Component Architecture", "35 min"), ("APIs & Authentication", "42 min")])
                ]),
            Make("ui-ux-product-design", "UI/UX Product Design", "Zainab Musa", "Product Design Director",
                "Design systems specialist focused on inclusive, high-conversion digital experiences.",
                "6 Weeks", 55000, 4.9, 142, "Beginner", "UI/UX",
                "From research to high-fidelity prototypes — learn how to craft interfaces people love and businesses trust.",
                ["User Research", "Wireframing", "Figma", "Design Systems"], 980, true, "Online",
                [
                    ("Design Thinking", [("Empathy & Research", "20 min"), ("Flows & Wireframes", "26 min")])
                ]),
            Make("generative-ai-mastery", "Generative AI Mastery", "Dr. Ada Okonkwo", "AI Research Lead",
                "Former ML engineer with 10+ years building production AI systems across Africa and Europe.",
                "5 Weeks", 65000, 4.8, 201, "Intermediate", "Generative AI",
                "Prompt engineering, LLM workflows, and building useful generative AI tools for business and education.",
                ["Prompt Engineering", "LLM Apps", "RAG Basics", "AI Product Thinking"], 1340, false, "Online",
                [
                    ("Working with LLMs", [("Prompt Patterns that Work", "22 min"), ("Building a Simple AI Assistant", "38 min")])
                ]),
            Make("data-science-bootcamp", "Applied Data Science Bootcamp", "Chinedu Bello", "Senior Data Analyst",
                "Data lead who has trained 2,000+ analysts for startups, NGOs, and government programs.",
                "10 Weeks", 95000, 4.7, 118, "Advanced", "Data Science",
                "Go beyond analysis — build predictive models, evaluate rigorously, and ship data products that drive decisions.",
                ["Statistics", "Feature Engineering", "ML Pipelines", "Model Deployment"], 760, false, "Hybrid",
                [
                    ("Modeling in Practice", [("Feature Stores & Pipelines", "30 min"), ("Deploying Predictions", "35 min")])
                ]),
            Make("ai-automation-for-business", "AI Automation for Business", "Funke Adebayo", "Automation Strategist",
                "Helps organizations cut operational waste with intelligent workflows and no-code AI tools.",
                "4 Weeks", 40000, 4.6, 97, "Beginner", "AI Automation",
                "Automate repetitive work with AI agents, integrations, and practical workflows tailored for African businesses and NGOs.",
                ["Workflow Automation", "No-code AI", "Integrations", "ROI Measurement"], 640, false, "Online",
                [
                    ("Automation Foundations", [("Mapping Processes Worth Automating", "18 min"), ("Building Your First Agent", "32 min")])
                ])
        ];
    }
}

