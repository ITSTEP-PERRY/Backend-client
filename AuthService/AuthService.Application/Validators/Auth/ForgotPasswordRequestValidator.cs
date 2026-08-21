using AuthService.Application.DTOs.Auth;
using FluentValidation;

namespace AuthService.Application.Validators.Auth;

public class ForgotPasswordRequestValidator
    : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid email address.");
    }
}