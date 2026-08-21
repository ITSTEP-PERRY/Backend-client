using System;
using System.Collections.Generic;
using System.Text;

namespace AuthService.Infrastructure.Verification
{
    public sealed class VerificationCodeOptions
    {
        public const string SectionName = "VerificationCodes";

        public string HashSecret { get; set; } = string.Empty;

        public int ExpirationMinutes { get; set; } = 10;

        public int MaxAttempts { get; set; } = 5;

        public int ResendCooldownSeconds { get; set; } = 60;
    }
}
