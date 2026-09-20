using AuthService.Application.DTOs.Account;
using FluentValidation;

namespace AuthService.Application.Validators.Auth;

public sealed class ChangeNameRequestValidator : AbstractValidator<ChangeNameRequest>
{
    public ChangeNameRequestValidator()
    {
        RuleFor(x => x.FirstName).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("Ім'я є обов'язковим.")
            .Must(x => x.Trim().Length >= 2).WithMessage("Ім'я має містити щонайменше 2 символи.")
            .Must(x => x.Trim().Length <= 50).WithMessage("Ім'я не може перевищувати 50 символів.");
        RuleFor(x => x.LastName).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("Прізвище є обов'язковим.")
            .Must(x => x.Trim().Length >= 2).WithMessage("Прізвище має містити щонайменше 2 символи.")
            .Must(x => x.Trim().Length <= 50).WithMessage("Прізвище не може перевищувати 50 символів.");
    }
}

public sealed class EmailChangeStartRequestValidator : AbstractValidator<EmailChangeStartRequest>
{
    public EmailChangeStartRequestValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().WithMessage("Електронна пошта є обов'язковою.")
            .EmailAddress().WithMessage("Вкажіть коректну адресу електронної пошти.")
            .MaximumLength(254).WithMessage("Електронна пошта не може перевищувати 254 символи.");
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Поточний пароль є обов'язковим.");
    }
}

public sealed class EmailChangeVerifyRequestValidator : AbstractValidator<EmailChangeVerifyRequest>
{
    public EmailChangeVerifyRequestValidator() => RuleFor(x => x.Code).NotEmpty().WithMessage("Код підтвердження є обов'язковим.")
        .Length(6).WithMessage("Код підтвердження має містити рівно 6 цифр.")
        .Matches(@"^\d{6}$").WithMessage("Код підтвердження має містити лише цифри.");
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Поточний пароль є обов'язковим.");
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage("Новий пароль є обов'язковим.")
            .MinimumLength(8).WithMessage("Пароль має містити щонайменше 8 символів.")
            .MaximumLength(128).WithMessage("Пароль не може перевищувати 128 символів.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Новий пароль має відрізнятися від поточного.");
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage("Підтвердження пароля є обов'язковим.")
            .Equal(x => x.NewPassword).WithMessage("Паролі не збігаються.");
    }
}

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Поточний пароль є обов'язковим.");
        RuleFor(x => x.Confirmation).Equal("DELETE").WithMessage("Для видалення облікового запису введіть DELETE.");
    }
}
