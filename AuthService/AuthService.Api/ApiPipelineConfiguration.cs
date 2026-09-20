using System.Net;
using AuthService.Api.Models;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AuthService.Api.Infrastructure;

internal static class ApiPipelineConfiguration
{
    internal const string CorsPolicyName = "Frontend";

    public static void ConfigureInvalidModelState(ApiBehaviorOptions options) =>
        options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ApiErrorResponse
        {
            Code = "VALIDATION_ERROR",
            Message = "Перевірте правильність введених даних.",
            Errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                    entry => entry.Value!.Errors.Select(NormalizeValidationMessage).ToArray())
        });

    public static void AddCorsPolicy(CorsOptions options, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var origins = configuration.GetSection("AllowedOrigins").Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

        options.AddPolicy(CorsPolicyName, policy =>
        {
            if (environment.IsDevelopment())
                policy.SetIsOriginAllowed(IsDevelopmentOrigin);
            else if (origins.Length > 0)
                policy.WithOrigins(origins);
            else
                policy.SetIsOriginAllowed(_ => false);

            policy.AllowCredentials().AllowAnyHeader().AllowAnyMethod();
        });
    }

    public static Task WriteStatusCodeErrorAsync(HttpResponse response) => response.StatusCode switch
    {
        StatusCodes.Status400BadRequest => ApiErrorWriter.WriteAsync(response, 400, "BAD_REQUEST", "Не вдалося обробити запит."),
        StatusCodes.Status401Unauthorized => ApiErrorWriter.WriteAsync(response, 401, "UNAUTHORIZED", "Потрібна автентифікація."),
        StatusCodes.Status403Forbidden => ApiErrorWriter.WriteAsync(response, 403, "FORBIDDEN", "У вас недостатньо прав для виконання цієї дії."),
        StatusCodes.Status404NotFound => ApiErrorWriter.WriteAsync(response, 404, "NOT_FOUND", "Запитаний ресурс не знайдено."),
        StatusCodes.Status405MethodNotAllowed => ApiErrorWriter.WriteAsync(response, 405, "METHOD_NOT_ALLOWED", "Метод не підтримується для цього ресурсу."),
        StatusCodes.Status409Conflict => ApiErrorWriter.WriteAsync(response, 409, "CONFLICT", "Запит конфліктує з поточним станом ресурсу."),
        StatusCodes.Status415UnsupportedMediaType => ApiErrorWriter.WriteAsync(response, 415, "UNSUPPORTED_MEDIA_TYPE", "Непідтримуваний формат вмісту запиту."),
        StatusCodes.Status429TooManyRequests => ApiErrorWriter.WriteAsync(response, 429, "TOO_MANY_REQUESTS", "Забагато запитів. Спробуйте пізніше."),
        StatusCodes.Status500InternalServerError => ApiErrorWriter.WriteAsync(response, 500, "INTERNAL_ERROR", "Сталася непередбачена внутрішня помилка."),
        StatusCodes.Status502BadGateway => ApiErrorWriter.WriteAsync(response, 502, "BAD_GATEWAY", "Залежний сервіс повернув некоректну відповідь."),
        StatusCodes.Status503ServiceUnavailable => ApiErrorWriter.WriteAsync(response, 503, "SERVICE_UNAVAILABLE", "Сервіс тимчасово недоступний."),
        _ => Task.CompletedTask
    };

    private static string NormalizeValidationMessage(ModelError error) =>
        !string.IsNullOrWhiteSpace(error.ErrorMessage) && error.ErrorMessage.Any(character => character is >= '\u0400' and <= '\u04ff')
            ? error.ErrorMessage
            : "Вказане значення є недійсним.";

    internal static bool IsDevelopmentOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
            return false;
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!IPAddress.TryParse(uri.Host, out var address)) return false;
        if (IPAddress.IsLoopback(address)) return true;
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 &&
               (bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 ||
                bytes[0] == 172 && bytes[1] is >= 16 and <= 31);
    }
}
