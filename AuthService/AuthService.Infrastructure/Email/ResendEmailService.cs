using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using AuthService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Email;

public sealed class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.resend.com/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public Task SendVerificationCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default) =>
        SendCodeAsync(
            email,
            "Подтверждение почты — Perry",
            "Подтвердите вашу почту",
            code,
            codeLifetime,
            cancellationToken);

    public Task SendPasswordResetCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default) =>
        SendCodeAsync(
            email,
            "Восстановление пароля — Perry",
            "Восстановление пароля",
            code,
            codeLifetime,
            cancellationToken);

    private async Task SendCodeAsync(
        string email,
        string subject,
        string heading,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken)
    {
        var safeCode = HtmlEncoder.Default.Encode(code);
        var safeHeading = HtmlEncoder.Default.Encode(heading);
        var request = new ResendEmailRequest(
            $"{_options.FromName} <{_options.FromEmail}>",
            [email],
            subject,
            BuildHtml(safeHeading, safeCode, FormatLifetime(codeLifetime)),
            BuildText(heading, code, FormatLifetime(codeLifetime)));

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "emails",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Resend rejected an email delivery request with status code {StatusCode}.",
                    (int)response.StatusCode);

                throw new EmailDeliveryException(
                    $"The email provider rejected the delivery request (HTTP {(int)response.StatusCode}).");
            }

            var result = await response.Content.ReadFromJsonAsync<ResendEmailResponse>(
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Resend accepted the email delivery request. MessageId: {MessageId}",
                result?.Id ?? "unavailable");
        }
        catch (EmailDeliveryException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new EmailDeliveryException(
                "The email provider could not be reached.",
                exception);
        }
    }

    private static string FormatLifetime(TimeSpan lifetime)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));
        var lastTwo = minutes % 100;
        var suffix = lastTwo is >= 11 and <= 14 ? "минут" : (minutes % 10) switch
        {
            1 => "минута",
            >= 2 and <= 4 => "минуты",
            _ => "минут"
        };
        return $"{minutes} {suffix}.";
    }

    private static string BuildText(string heading, string code, string lifetime) =>
        $"Perry\n\n{heading}\n\nКод: {code}\n\nКод действует {lifetime}\nЕсли вы не запрашивали этот код, просто проигнорируйте письмо.";

    private static string BuildHtml(string heading, string code, string lifetime) => $$"""
        <!doctype html>
        <html lang="ru">
        <body style="margin:0;padding:24px;background-color:#F3F7FF;font-family:Arial,sans-serif;color:#0F172A;">
          <div style="max-width:520px;margin:0 auto;background-color:#FFFFFF;border:1px solid #BFDBFE;border-radius:14px;padding:32px;text-align:center;">
            <div style="font-size:25px;font-weight:700;color:#2563EB;margin-bottom:24px;">Perry</div>
            <h1 style="font-size:20px;color:#0F172A;margin:0 0 20px;">{{heading}}</h1>
            <div style="font-size:36px;font-weight:700;color:#2563EB;letter-spacing:8px;padding:18px;background-color:#EFF6FF;border:1px solid #BFDBFE;border-radius:9px;">{{code}}</div>
            <p style="font-size:15px;line-height:1.6;margin:24px 0 8px;">Код действует {{lifetime}}</p>
            <p style="font-size:13px;line-height:1.6;color:#64748B;margin:0;">Если вы не запрашивали этот код, просто проигнорируйте письмо.</p>
          </div>
        </body>
        </html>
        """;

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ResendEmailResponse(
        [property: JsonPropertyName("id")] string Id);
}
