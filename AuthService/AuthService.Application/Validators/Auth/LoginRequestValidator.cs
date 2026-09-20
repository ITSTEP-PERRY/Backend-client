using AuthService.Application.DTOs.Auth;
using FluentValidation;

namespace AuthService.Application.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
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
            .MaximumLength(128)
            .WithMessage("Пароль не може перевищувати 128 символів.");
    }
}
