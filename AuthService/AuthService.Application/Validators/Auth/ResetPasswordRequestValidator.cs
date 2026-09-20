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
            .WithMessage("Електронна пошта є обов'язковою.")
            .EmailAddress()
            .WithMessage("Вкажіть коректну адресу електронної пошти.")
            .MaximumLength(254)
            .WithMessage("Електронна пошта не може перевищувати 254 символи.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Код скидання пароля є обов'язковим.")
            .Length(6)
            .WithMessage("Код скидання пароля має містити рівно 6 цифр.")
            .Matches(@"^\d{6}$")
            .WithMessage("Код скидання пароля має містити лише цифри.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Пароль є обов'язковим.")
            .MinimumLength(8)
            .WithMessage("Пароль має містити щонайменше 8 символів.")
            .MaximumLength(128)
            .WithMessage("Пароль не може перевищувати 128 символів.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Підтвердження пароля є обов'язковим.")
            .Equal(x => x.NewPassword)
            .WithMessage("Паролі не збігаються.");
    }
}
