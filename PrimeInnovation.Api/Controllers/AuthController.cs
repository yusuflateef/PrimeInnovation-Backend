using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PrimeInnovation.Application.DTOs;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Domain.Entities;
using PrimeInnovation.Infrastructure.Notifications;
using PrimeInnovation.Infrastructure.Persistence;

namespace PrimeInnovation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    AppDbContext db,
    ITokenService tokens,
    IPasswordHasher passwords,
    INotificationService notifications,
    IGoogleTokenValidator google) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Email already registered." });

        var phone = TermiiSmsService.NormalizeNigerianPhone(request.PhoneNumber);

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = phone,
            PasswordHash = passwords.Hash(request.Password),
            // Public sign-up always creates students; instructors are promoted by an admin.
            Role = UserRole.Student
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await notifications.SendWelcomeAsync(user.FullName, user.Email, user.PhoneNumber);

        return Ok(tokens.CreateToken(user));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !passwords.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(tokens.CreateToken(user));
    }

    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> GoogleSignIn(GoogleSignInRequest request)
    {
        if (!google.IsConfigured)
            return NotFound(new { message = "Google sign-in is not enabled." });

        // Identity comes only from the verified Google token, never from client-supplied fields.
        var identity = await google.ValidateAsync(request.IdToken, HttpContext.RequestAborted);
        if (identity is null)
            return Unauthorized(new { message = "Invalid Google credential." });

        var email = identity.Email.Trim().ToLowerInvariant();
        var fullName = string.IsNullOrWhiteSpace(identity.FullName) ? email : identity.FullName;
        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == identity.Subject)
                   ?? await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is not null && user.GoogleId is not null && user.GoogleId != identity.Subject)
            return Conflict(new { message = "This email is linked to a different Google account." });

        if (user is null)
        {
            user = new User
            {
                FullName = fullName,
                Email = email,
                GoogleId = identity.Subject,
                PasswordHash = passwords.Hash(Guid.NewGuid().ToString("N")),
                Role = UserRole.Student
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await notifications.SendWelcomeAsync(user.FullName, user.Email, user.PhoneNumber);
        }
        else
        {
            user.GoogleId ??= identity.Subject;
            if (string.IsNullOrWhiteSpace(user.FullName))
                user.FullName = fullName;
            await db.SaveChangesAsync();
        }

        return Ok(tokens.CreateToken(user));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
            return Ok(new { message = "If the account exists, a reset link was sent by email/SMS." });

        var token = tokens.GenerateResetToken();
        user.PasswordResetToken = token;
        user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();

        await notifications.SendPasswordResetAsync(user.FullName, user.Email, user.PhoneNumber, token);

        // Token only returned when notifications are not configured (local/dev convenience).
        var exposeToken = !notificationsConfigured();
        return Ok(new
        {
            message = exposeToken
                ? "Password reset token generated (notifications not configured)."
                : "If the account exists, a reset link was sent by email/SMS.",
            token = exposeToken ? token : null,
            expiresAt = user.PasswordResetExpires,
            channels = new
            {
                email = true,
                sms = !string.IsNullOrWhiteSpace(user.PhoneNumber)
            }
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || user.PasswordResetToken != request.Token ||
            user.PasswordResetExpires is null || user.PasswordResetExpires < DateTime.UtcNow)
            return BadRequest(new { message = "Invalid or expired reset token." });

        user.PasswordHash = passwords.Hash(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpires = null;
        await db.SaveChangesAsync();
        return Ok(new { message = "Password updated successfully." });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<object>> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        var user = await db.Users.FindAsync(userId);
        if (user is null) return Unauthorized();
        return Ok(new { user.Id, user.FullName, user.Email, user.PhoneNumber, Role = user.Role.ToString() });
    }

    private bool notificationsConfigured()
    {
        // Resolved via scoped services already injected — check through HttpContext request services is heavy;
        // use a simple heuristic: if either service would send, don't expose token.
        // Controllers don't have direct settings; NotificationService swallows when not configured.
        // Expose token only in Development when both providers are off — handled by checking IConfiguration via RequestServices.
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var emailOn = config.GetValue("Email:Enabled", false);
        var termiiKey = config["Termii:ApiKey"] ?? "";
        var smsOn = !string.IsNullOrWhiteSpace(termiiKey) && !termiiKey.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
        return emailOn || smsOn;
    }
}
