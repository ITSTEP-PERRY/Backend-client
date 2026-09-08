using AuthService.Application.DTOs.Internal;
using FluentValidation;

namespace AuthService.Application.Validators.Internal;

public sealed class ServiceTokenRequestValidator : AbstractValidator<ServiceTokenRequest>
{
    public ServiceTokenRequestValidator()
    {
        RuleFor(x => x.ServiceName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Credential).NotEmpty().MaximumLength(1024);
    }
}
