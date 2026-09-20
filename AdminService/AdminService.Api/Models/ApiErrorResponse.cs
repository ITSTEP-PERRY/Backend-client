namespace AdminService.Api.Models;

public sealed class ApiErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }
}

internal static class ApiErrorWriter
{
    public static Task WriteAsync(
        HttpResponse response,
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Code = code,
            Message = message,
            Errors = errors
        });
    }
}
