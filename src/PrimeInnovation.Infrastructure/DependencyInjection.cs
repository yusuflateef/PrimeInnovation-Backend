using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PrimeInnovation.Application.Interfaces;
using PrimeInnovation.Application.Options;
using PrimeInnovation.Infrastructure.Auth;
using PrimeInnovation.Infrastructure.Notifications;
using PrimeInnovation.Infrastructure.Payments;
using PrimeInnovation.Infrastructure.Persistence;

namespace PrimeInnovation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        services.AddSingleton(jwtSettings);
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        var googleSettings = configuration.GetSection("Google").Get<GoogleSettings>() ?? new GoogleSettings();
        services.AddSingleton(googleSettings);
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

        var paystackSettings = configuration.GetSection("Paystack").Get<PaystackSettings>() ?? new PaystackSettings();
        services.AddSingleton(paystackSettings);
        services.AddHttpClient<IPaystackService, PaystackService>(client =>
        {
            client.BaseAddress = new Uri((paystackSettings.BaseUrl ?? "https://api.paystack.co").TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        var emailSettings = configuration.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings();
        services.AddSingleton(emailSettings);
        services.AddScoped<IEmailService, SmtpEmailService>();

        var termiiSettings = configuration.GetSection("Termii").Get<TermiiSettings>() ?? new TermiiSettings();
        services.AddSingleton(termiiSettings);
        services.AddHttpClient<ISmsService, TermiiSmsService>(client =>
        {
            client.BaseAddress = new Uri((termiiSettings.BaseUrl ?? "https://api.ng.termii.com").TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<INotificationService, NotificationService>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Default")
                ?? "Server=localhost\\SQLEXPRESS;Database=PrimeInnovationDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"));

        return services;
    }
}
