using AuthService.Application.DTOs.Users;
using FluentValidation;

namespace AuthService.Application.Validators.Users;

public sealed class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator() => RuleFor(x => x.Role)
        .NotNull().WithMessage("Роль є обов'язковою.")
        .IsInEnum().WithMessage("Вказано недійсну роль.");
}
