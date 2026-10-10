using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PrimeInnovation.Application.Options;
using PrimeInnovation.Infrastructure;
using PrimeInnovation.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

// Fail fast rather than sign tokens with a missing or publicly known key.
string[] knownInsecureJwtKeys =
[
    "PrimeInnovationNetwork_SuperSecret_Key_ChangeInProduction_2026!",
    "Dev-Only_PrimeInnovation_Local_Signing_Key_Not_For_Production_0001"
];
if (Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes. Set it with `dotnet user-secrets set Jwt:Key <value>` or the Jwt__Key environment variable.");
if (!builder.Environment.IsDevelopment() && knownInsecureJwtKeys.Contains(jwtSettings.Key))
    throw new InvalidOperationException("Jwt:Key is a known development key. Provide a unique secret outside Development.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Prime Innovation Network API",
        Version = "v1",
        Description = "Backend API for Prime Innovation Network / Prime Digital Academy — courses, auth, learning, payments, certificates."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ??
                ["http://localhost:5173", "http://127.0.0.1:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Brute-force protection for the anonymous auth endpoints (login, register, password reset, Google).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, ct) =>
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many attempts. Please wait a minute and try again." }, ct);
});

// Only honor X-Forwarded-* when actually deployed behind a reverse proxy; otherwise clients
// could spoof their IP and sidestep rate limiting.
var useForwardedHeaders = builder.Configuration.GetValue("ForwardedHeaders:Enabled", false);
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration.GetValue("Seed:Enabled", false))
        await DbSeeder.SeedAsync(db);
}

if (useForwardedHeaders)
    app.UseForwardedHeaders();

// Unhandled exceptions return { message } like every other API error (the client reads `message`).
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred. Please try again." });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Prime Innovation Network API v1");
        c.DocumentTitle = "PIN API Docs";
    });
    app.MapGet("/", () => Results.Redirect("/swagger"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
    app.MapGet("/", () => Results.Ok(new { name = "Prime Innovation Network API", status = "ok" }));
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
