using AdminService.Api.Models;
using AdminService.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace AdminService.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is AdminServiceException known)
        {
            if (known.StatusCode >= 500)
                logger.LogError(exception, "Admin request failed because a dependency was unavailable or invalid.");
            await ApiErrorWriter.WriteAsync(httpContext.Response, known.StatusCode, known.ErrorCode,
                PublicMessage(known.ErrorCode, known.Message), known.Errors);
            return true;
        }

        logger.LogError(exception, "Unhandled exception while processing an admin request.");
        await ApiErrorWriter.WriteAsync(httpContext.Response, StatusCodes.Status500InternalServerError,
            "INTERNAL_ERROR", "Сталася непередбачена внутрішня помилка.");
        return true;
    }

    private static string PublicMessage(string code, string fallback) => code switch
    {
        "USER_NOT_FOUND" => "Користувача не знайдено.",
        "INVALID_USER_ROLE" => "Вказано недійсну роль користувача.",
        "INVALID_USER_STATUS" => "Вказано недійсний статус користувача.",
        "SELF_MANAGEMENT_NOT_ALLOWED" => "Ви не можете змінити роль або статус власного облікового запису.",
        "LAST_ADMIN_PROTECTION" => "Неможливо змінити цього адміністратора, оскільки він є останнім активним адміністратором.",
        "INVALID_STATUS_TRANSITION" => "Логічно видалений обліковий запис не можна повторно активувати або заблокувати.",
        "VALIDATION_ERROR" or "INVALID_PAGINATION" => "Перевірте правильність введених даних.",
        "AUTH_SERVICE_UNAVAILABLE" => "Сервіс авторизації тимчасово недоступний. Спробуйте пізніше.",
        "AUTH_SERVICE_AUTHENTICATION_FAILED" => "Не вдалося безпечно автентифікуватися в сервісі авторизації.",
        "AUTH_SERVICE_INVALID_RESPONSE" => "Сервіс авторизації повернув некоректну відповідь.",
        _ => "Не вдалося виконати запит."
    };
}
