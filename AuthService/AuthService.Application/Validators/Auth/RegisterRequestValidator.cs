using AuthService.Application.DTOs.Auth;
using FluentValidation;

namespace AuthService.Application.Validators.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Електронна пошта є обов'язковою.")
            .EmailAddress()
            .WithMessage("Вкажіть коректну адресу електронної пошти.")
            .MaximumLength(254)
            .WithMessage("Електронна пошта не може перевищувати 254 символи.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Пароль є обов'язковим.")
            .MinimumLength(8)
            .WithMessage("Пароль має містити щонайменше 8 символів.")
            .MaximumLength(128)
            .WithMessage("Пароль не може перевищувати 128 символів.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Підтвердження пароля є обов'язковим.")
            .Equal(x => x.Password)
            .WithMessage("Паролі не збігаються.");
    }
}
