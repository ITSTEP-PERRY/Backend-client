using AuthService.Application.DTOs.Auth;
using FluentValidation;

namespace AuthService.Application.Validators.Auth;

public class ResetPasswordRequestValidator
    : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid email address.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Reset code is required.")
            .Length(6)
            .WithMessage("Reset code must contain exactly 6 digits.")
            .Matches(@"^\d{6}$")
            .WithMessage("Reset code must contain only digits.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Password is required.")
            .MinimumLength(8)
            .WithMessage("Password must contain at least 8 characters.")
            .MaximumLength(128)
            .WithMessage("Password is too long.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Password confirmation is required.")
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match.");
    }
}