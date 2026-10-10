using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PrimeInnovation.Application.DTOs;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Application.Options;
using PrimeInnovation.Domain.Entities;
using PrimeInnovation.Infrastructure.Notifications;
using PrimeInnovation.Infrastructure.Persistence;

namespace PrimeInnovation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController(
    AppDbContext db,
    ITokenService tokens,
    IPasswordHasher passwords,
    INotificationService notifications,
    IGoogleTokenValidator google,
    IWebHostEnvironment env,
    EmailSettings emailSettings,
    TermiiSettings termiiSettings) : ControllerBase
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

        // Local convenience only: with no email/SMS provider in Development, hand the token back directly.
        // Elsewhere the response is identical whether or not the account exists (no user enumeration).
        if (env.IsDevelopment() && !emailSettings.Enabled && !termiiSettings.Enabled)
        {
            return Ok(new
            {
                message = "Password reset token generated (notifications not configured).",
                token,
                expiresAt = user.PasswordResetExpires
            });
        }

        return Ok(new { message = "If the account exists, a reset link was sent by email/SMS." });
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
    [DisableRateLimiting]
    [HttpGet("me")]
    public async Task<ActionResult<object>> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        var user = await db.Users.FindAsync(userId);
        if (user is null) return Unauthorized();
        return Ok(new { user.Id, user.FullName, user.Email, user.PhoneNumber, Role = user.Role.ToString() });
    }
}
