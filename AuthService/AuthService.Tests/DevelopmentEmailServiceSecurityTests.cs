using AuthService.Infrastructure.Email;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuthService.Tests;

public sealed class DevelopmentEmailServiceSecurityTests
{
    [Fact]
    public void CannotBeCreatedOutsideDevelopment()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DevelopmentEmailService(new RecordingLogger(), new TestEnvironment("Production")));
    }

    [Fact]
    public async Task DoesNotLogRecipientOrPlaintextCode()
    {
        var logger = new RecordingLogger();
        var service = new DevelopmentEmailService(logger, new TestEnvironment(Environments.Development));
        await service.SendVerificationCodeAsync("private@example.com", "123456", TimeSpan.FromMinutes(10));
        Assert.DoesNotContain("private@example.com", logger.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123456", logger.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingLogger : ILogger<DevelopmentEmailService>
    {
        public string Message { get; private set; } = string.Empty;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Message = formatter(state, exception);
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
