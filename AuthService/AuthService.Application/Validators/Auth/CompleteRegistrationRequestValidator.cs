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
            .WithMessage("Токен реєстрації є обов'язковим.");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Ім'я є обов'язковим.")
            .MinimumLength(2)
            .WithMessage("Ім'я має містити щонайменше 2 символи.")
            .MaximumLength(50)
            .WithMessage("Ім'я не може перевищувати 50 символів.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Прізвище є обов'язковим.")
            .MinimumLength(2)
            .WithMessage("Прізвище має містити щонайменше 2 символи.")
            .MaximumLength(50)
            .WithMessage("Прізвище не може перевищувати 50 символів.");
    }
}
