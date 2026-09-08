using AuthService.Application.DTOs.Users;
using FluentValidation;

namespace AuthService.Application.Validators.Users;

public sealed class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    public UpdateUserStatusRequestValidator() => RuleFor(x => x.Status).IsInEnum();
}
