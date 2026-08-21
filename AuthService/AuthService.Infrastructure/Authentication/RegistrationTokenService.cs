using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace AuthService.Infrastructure.Authentication;

public sealed class RegistrationTokenService(IDataProtectionProvider provider) : IRegistrationTokenService
{
    private readonly ITimeLimitedDataProtector _protector = provider
        .CreateProtector("AuthService.CompleteRegistration.v1").ToTimeLimitedDataProtector();

    public string Generate(Guid userId) => _protector.Protect(userId.ToString(), TimeSpan.FromMinutes(15));
    public Guid Validate(string token) => Guid.Parse(_protector.Unprotect(token));
}
