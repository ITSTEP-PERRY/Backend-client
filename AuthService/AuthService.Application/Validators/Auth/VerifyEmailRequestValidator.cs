using AuthService.Application.DTOs.Auth;
using FluentValidation;

namespace AuthService.Application.Validators.Auth;

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Електронна пошта є обов'язковою.")
            .EmailAddress()
            .WithMessage("Вкажіть коректну адресу електронної пошти.")
            .MaximumLength(254)
            .WithMessage("Електронна пошта не може перевищувати 254 символи.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Код підтвердження є обов'язковим.")
            .Length(6)
            .WithMessage("Код підтвердження має містити рівно 6 цифр.")
            .Matches(@"^\d{6}$")
            .WithMessage("Код підтвердження має містити лише цифри.");
    }
}
