using AuthService.Application.Exceptions;
using AuthService.Api.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace AuthService.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, message, retryAfterSeconds) = exception switch
        {
            AuthException e => (e.StatusCode, e.ErrorCode, Localize(e.ErrorCode, e.Message), (int?)null),
            DuplicateEmailException => (409, "EMAIL_ALREADY_REGISTERED", "Користувач із такою електронною поштою вже зареєстрований.", null),
            EmailVerificationException e when e.ErrorCode == EmailVerificationErrorCodes.UserNotFound => (404, e.ErrorCode, Localize(e.ErrorCode, e.Message), e.RetryAfterSeconds),
            EmailVerificationException e when e.ErrorCode == EmailVerificationErrorCodes.ResendCooldownActive => (429, e.ErrorCode, Localize(e.ErrorCode, e.Message), e.RetryAfterSeconds),
            EmailVerificationException e => (400, e.ErrorCode, Localize(e.ErrorCode, e.Message), e.RetryAfterSeconds),
            _ => (500, "INTERNAL_ERROR", "Сталася непередбачена внутрішня помилка.", null)
        };
        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "An unhandled exception occurred while processing the request.");

        context.Response.StatusCode = status;
        if (retryAfterSeconds.HasValue)
            context.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString();
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Code = code, Message = message, RetryAfterSeconds = retryAfterSeconds
        }, cancellationToken);
        return true;
    }

    private static string Localize(string code, string fallback) => code switch
    {
        "USER_NOT_FOUND" => "Користувача не знайдено.",
        "INVALID_USER_ROLE" => "Вказано недійсну роль користувача.",
        "INVALID_USER_STATUS" => "Вказано недійсний статус користувача.",
        "SELF_MANAGEMENT_NOT_ALLOWED" => "Ви не можете змінити роль або статус власного облікового запису.",
        "LAST_ADMIN_PROTECTION" => "Неможливо змінити цього адміністратора, оскільки він є останнім активним адміністратором.",
        "CURRENT_PASSWORD_INVALID" => "Поточний пароль неправильний.",
        "EMAIL_ALREADY_REGISTERED" => "Користувач із такою електронною поштою вже зареєстрований.",
        "EMAIL_CHANGE_CODE_INVALID" or "EMAIL_CHANGE_CODE_EXPIRED" => "Код підтвердження зміни електронної пошти недійсний або прострочений.",
        "EMAIL_CHANGE_ATTEMPTS_EXCEEDED" => "Перевищено кількість спроб підтвердження зміни електронної пошти.",
        "EMAIL_CHANGE_NOT_PENDING" => "Немає активного запиту на зміну електронної пошти.",
        "AVATAR_TOO_LARGE" => "Розмір зображення не може перевищувати 5 МБ.",
        "AVATAR_TYPE_UNSUPPORTED" => "Підтримуються лише зображення JPEG та PNG.",
        "AVATAR_CONTENT_INVALID" => "Вміст зображення недійсний.",
        "AVATAR_NOT_FOUND" => "Зображення профілю не знайдено.",
        "AVATAR_STORAGE_FAILED" => "Не вдалося обробити зображення профілю. Спробуйте пізніше.",
        "ACCOUNT_DELETE_FORBIDDEN" => "Не вдалося підтвердити видалення облікового запису.",
        "INVALID_STATUS_TRANSITION" => "Логічно видалений обліковий запис не можна повторно активувати або заблокувати.",
        "ACCOUNT_BLOCKED" => "Обліковий запис заблоковано.",
        "ACCOUNT_DELETED" => "Обліковий запис видалено.",
        "INVALID_CREDENTIALS" => "Неправильна електронна пошта або пароль.",
        "EMAIL_NOT_VERIFIED" => "Електронну пошту не підтверджено.",
        "REGISTRATION_NOT_COMPLETED" => "Реєстрацію не завершено.",
        "REGISTRATION_ALREADY_COMPLETED" => "Реєстрацію вже завершено.",
        "INVALID_REGISTRATION_TOKEN" => "Токен реєстрації недійсний або прострочений.",
        "INVALID_REFRESH_TOKEN" => "Токен оновлення недійсний.",
        "REFRESH_TOKEN_EXPIRED" => "Строк дії токена оновлення минув.",
        "INVALID_PASSWORD_RESET_CODE" => "Код скидання пароля недійсний або прострочений.",
        "PASSWORD_RESET_CODE_EXPIRED" => "Код скидання пароля недійсний або прострочений.",
        "PASSWORD_RESET_ATTEMPTS_EXCEEDED" => "Перевищено кількість спроб скидання пароля. Спробуйте пізніше.",
        "VERIFICATION_CODE_NOT_FOUND" or "VERIFICATION_CODE_EXPIRED" or "INVALID_VERIFICATION_CODE" => "Код підтвердження недійсний або прострочений.",
        "VERIFICATION_ATTEMPTS_EXCEEDED" => "Перевищено кількість спроб підтвердження.",
        "EMAIL_ALREADY_VERIFIED" => "Електронну пошту вже підтверджено.",
        "RESEND_COOLDOWN_ACTIVE" => "Новий код можна буде надіслати пізніше.",
        "VALIDATION_ERROR" or "INVALID_PAGINATION" => "Перевірте правильність введених даних.",
        "INVALID_SERVICE_CREDENTIALS" => "Не вдалося автентифікувати сервіс.",
        "UNAUTHORIZED" => "Потрібна автентифікація.",
        "CSRF_VALIDATION_FAILED" => "Не вдалося підтвердити походження запиту.",
        "ACTOR_REQUIRED" => "Не вдалося визначити адміністратора, який виконує дію.",
        _ => "Не вдалося виконати запит."
    };
}
