using System.Security.Cryptography;
using System.Text;
using AuthService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Verification;

public class VerificationCodeService : IVerificationCodeService
{
    private readonly byte[] _hashSecret;
    private readonly VerificationCodeOptions _options;


    public VerificationCodeService(
        IOptions<VerificationCodeOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.HashSecret))
        {
            throw new InvalidOperationException(
                "Verification code hash secret is not configured.");
        }

        _hashSecret = Encoding.UTF8.GetBytes(_options.HashSecret);
    }

    public TimeSpan Lifetime =>
        TimeSpan.FromMinutes(_options.ExpirationMinutes);

    public int MaxAttempts => _options.MaxAttempts;

    public TimeSpan ResendCooldown =>
        TimeSpan.FromSeconds(_options.ResendCooldownSeconds);

    public string GenerateCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);

        return value.ToString("D6");
    }

    public string HashCode(string code)
    {
        var bytes = Encoding.UTF8.GetBytes(code);
        var hash = HMACSHA256.HashData(_hashSecret, bytes);

        return Convert.ToHexString(hash);
    }

    public bool VerifyCode(string code, string codeHash)
    {
        byte[] storedHash;

        try
        {
            storedHash = Convert.FromHexString(codeHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var providedHash = Convert.FromHexString(HashCode(code));

        return CryptographicOperations.FixedTimeEquals(
            providedHash,
            storedHash);
    }
}
