using System.Net.Http.Json;
using AdminService.Application.Exceptions;

namespace AdminService.Infrastructure.AuthService;

internal static class AuthServiceErrorMapper
{
    public static async Task<AdminServiceException> FromResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        AuthErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(cancellationToken);
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
        {
            // The downstream body is not part of the expected error contract.
        }

        var status = (int)response.StatusCode;
        if (string.IsNullOrWhiteSpace(error?.Code))
            return InvalidResponse();
        return new AdminServiceException(
            error.Code,
            PublicMessage(error?.Code, status),
            status,
            error?.Errors);
    }

    public static AdminServiceException Unavailable(Exception exception) =>
        new("AUTH_SERVICE_UNAVAILABLE", "Сервіс авторизації тимчасово недоступний. Спробуйте пізніше.", 503, innerException: exception);

    public static async Task<AdminServiceException> ServiceAuthenticationFailedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        _ = await FromResponseAsync(response, cancellationToken);
        return new AdminServiceException(
            "AUTH_SERVICE_AUTHENTICATION_FAILED",
            "Не вдалося безпечно автентифікуватися в сервісі авторизації.",
            502);
    }

    public static AdminServiceException InvalidResponse() =>
        new("AUTH_SERVICE_INVALID_RESPONSE", "Сервіс авторизації повернув некоректну відповідь.", 502);

    private static string PublicMessage(string? code, int status) => code switch
    {
        "USER_NOT_FOUND" => "Користувача не знайдено.",
        "INVALID_USER_ROLE" => "Вказано недійсну роль користувача.",
        "INVALID_USER_STATUS" => "Вказано недійсний статус користувача.",
        "SELF_MANAGEMENT_NOT_ALLOWED" => "Ви не можете змінити роль або статус власного облікового запису.",
        "LAST_ADMIN_PROTECTION" => "Неможливо змінити цього адміністратора, оскільки він є останнім активним адміністратором.",
        "INVALID_STATUS_TRANSITION" => "Логічно видалений обліковий запис не можна повторно активувати або заблокувати.",
        "VALIDATION_ERROR" or "INVALID_PAGINATION" => "Перевірте правильність введених даних.",
        _ when status == 404 => "Запитаний ресурс не знайдено.",
        _ when status is 400 or 422 => "Перевірте правильність введених даних.",
        _ => "Сервіс авторизації не зміг виконати запит."
    };
}
