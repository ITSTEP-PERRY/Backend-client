namespace AdminService.Application.Exceptions;

public sealed class AdminServiceException(
    string errorCode,
    string message,
    int statusCode,
    IReadOnlyDictionary<string, string[]>? errors = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;
}
