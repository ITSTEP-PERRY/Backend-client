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
            .WithMessage("Електронна пошта є обов'язковою.")
            .EmailAddress()
            .WithMessage("Вкажіть коректну адресу електронної пошти.")
            .MaximumLength(254)
            .WithMessage("Електронна пошта не може перевищувати 254 символи.");
    }
}
