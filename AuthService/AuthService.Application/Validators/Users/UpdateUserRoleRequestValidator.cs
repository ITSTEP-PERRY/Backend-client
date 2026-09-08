using AuthService.Application.DTOs.Users;
using FluentValidation;

namespace AuthService.Application.Validators.Users;

public sealed class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator() => RuleFor(x => x.Role).IsInEnum();
}
