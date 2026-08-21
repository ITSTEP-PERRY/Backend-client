using AuthService.Application.DTOs.Auth;
using FluentValidation;

namespace AuthService.Application.Validators.Auth;

public class ResendVerificationCodeRequestValidator
    : AbstractValidator<ResendVerificationCodeRequest>
{
    public ResendVerificationCodeRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid email address.");
    }
}