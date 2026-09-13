using System.Text.Json.Serialization;

namespace AuthService.Api.Models;

public sealed class ApiErrorResponse
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetryAfterSeconds { get; init; }
}

internal static class ApiErrorWriter
{
    public static Task WriteAsync(HttpResponse response, int statusCode, string code, string message)
    {
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(new ApiErrorResponse { Code = code, Message = message });
    }
}
