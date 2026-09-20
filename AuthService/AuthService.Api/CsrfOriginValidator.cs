using AuthService.Application.Exceptions;
using AuthService.Api.Infrastructure;

namespace AuthService.Api.Security;

public sealed class CsrfOriginValidator(IConfiguration configuration, IWebHostEnvironment environment)
{
    private readonly HashSet<string> _allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>()?
        .Where(value => !string.IsNullOrWhiteSpace(value)).Select(Normalize).Where(value => value is not null)
        .Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

    public void Validate(HttpRequest request)
    {
        var candidate = request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(candidate) &&
            Uri.TryCreate(request.Headers.Referer.FirstOrDefault(), UriKind.Absolute, out var referer))
            candidate = referer.GetLeftPart(UriPartial.Authority);
        if (!IsAllowed(candidate, request))
            throw new AuthException("CSRF_VALIDATION_FAILED", "Не вдалося підтвердити походження запиту.", 403);
    }

    private bool IsAllowed(string? candidate, HttpRequest request)
    {
        var normalized = Normalize(candidate);
        if (normalized is null || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri)) return false;
        if (uri.Authority.Equals(request.Host.Value, StringComparison.OrdinalIgnoreCase)) return true;
        return environment.IsDevelopment()
            ? ApiPipelineConfiguration.IsDevelopmentOrigin(normalized)
            : _allowedOrigins.Contains(normalized);
    }

    private static string? Normalize(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') : null;
}
