using AuthService.Application.Interfaces;
using AuthService.Infrastructure.Authentication;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AuthService.Infrastructure.Persistence.Repositories;
using AuthService.Infrastructure.Email;
using ApplicationAuthService = AuthService.Application.Services.AuthService;

namespace AuthService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
    {
        services.Configure<VerificationCodeOptions>(
            configuration.GetSection(
                VerificationCodeOptions.SectionName));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "Jwt:Issuer is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "Jwt:Audience is required.")
            .Validate(x => x.SigningSecret.Length >= 32, "Jwt:SigningSecret must contain at least 32 characters.")
            .Validate(x => x.AccessTokenMinutes > 0 && x.RefreshTokenDays > 0 && x.RememberMeRefreshTokenDays > 0,
                "JWT token lifetimes must be positive.")
            .ValidateOnStart();

        services.AddOptions<ResendOptions>()
            .Bind(configuration.GetSection(ResendOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "Resend:ApiKey is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.FromEmail),
                "Resend:FromEmail is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.FromName),
                "Resend:FromName is required.")
            .ValidateOnStart();

        var connectionString =
            configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DefaultConnection is not configured.");
        }

        services.AddDbContext<AuthDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<AuthDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetCodeRepository, PasswordResetCodeRepository>();
        services.AddHttpClient<IEmailService, ResendEmailService>();
        services.AddScoped<IAuthService, ApplicationAuthService>();

        services.AddScoped<
            IEmailVerificationCodeRepository,
            EmailVerificationCodeRepository>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IRegistrationTokenService, RegistrationTokenService>();

        services.AddSingleton<
            IVerificationCodeService,
            VerificationCodeService>();

        return services;
    }
}
