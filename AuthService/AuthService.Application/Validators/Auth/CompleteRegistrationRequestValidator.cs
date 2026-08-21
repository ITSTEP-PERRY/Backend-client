using AuthService.Application.DTOs.Auth;
using FluentValidation;

namespace AuthService.Application.Validators.Auth;

public class CompleteRegistrationRequestValidator
    : AbstractValidator<CompleteRegistrationRequest>
{
    public CompleteRegistrationRequestValidator()
    {
        RuleFor(x => x.RegistrationToken)
            .NotEmpty()
            .WithMessage("Registration token is required.");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required.")
            .MinimumLength(2)
            .WithMessage("First name must contain at least 2 characters.")
            .MaximumLength(50)
            .WithMessage("First name is too long.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required.")
            .MinimumLength(2)
            .WithMessage("Last name must contain at least 2 characters.")
            .MaximumLength(50)
            .WithMessage("Last name is too long.");
    }
}
