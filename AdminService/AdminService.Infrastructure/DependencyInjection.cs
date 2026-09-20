using AdminService.Application.Interfaces;
using AdminService.Application.Services;
using AdminService.Infrastructure.AuthService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdminService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthServiceOptions>()
            .Bind(configuration.GetSection(AuthServiceOptions.SectionName))
            .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _), "AuthService:BaseUrl must be an absolute URL.")
            .Validate(x => x.TimeoutSeconds is > 0 and <= 120, "AuthService:TimeoutSeconds must be between 1 and 120.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.ServiceName), "AuthService:ServiceName is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.ServiceCredential), "AuthService:ServiceCredential is required.")
            .ValidateOnStart();

        var settings = configuration.GetSection(AuthServiceOptions.SectionName).Get<AuthServiceOptions>()
            ?? new AuthServiceOptions();
        var baseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        var timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

        services.AddHttpClient<IAuthServiceClient, AuthServiceClient>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = timeout;
        });
        services.AddHttpClient(AuthServiceClient.ServiceTokenClientName, client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = timeout;
        });
        services.AddSingleton<IServiceTokenProvider, ServiceTokenProvider>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        return services;
    }
}
