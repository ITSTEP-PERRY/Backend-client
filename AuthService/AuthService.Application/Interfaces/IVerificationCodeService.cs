namespace AuthService.Application.Interfaces;

public interface IVerificationCodeService
{
    string GenerateCode();

    string HashCode(string code);

    bool VerifyCode(string code, string codeHash);

    TimeSpan Lifetime { get; }

    int MaxAttempts { get; }

    TimeSpan ResendCooldown { get; }
}
