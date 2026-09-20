using AdminService.Application.DTOs.Users;
using FluentValidation;

namespace AdminService.Application.Validators.Users;

public sealed class GetUsersRequestValidator : AbstractValidator<GetUsersRequest>
{
    private static readonly string[] SortFields =
        ["createdAt", "updatedAt", "email", "firstName", "lastName", "role", "status"];

    public GetUsersRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Номер сторінки має бути не менше 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Розмір сторінки має бути від 1 до 100.");
        RuleFor(x => x.Search).MaximumLength(200).WithMessage("Пошуковий запит не може перевищувати 200 символів.");
        RuleFor(x => x.Role).Must(x => !x.HasValue || Enum.IsDefined(x.Value)).WithMessage("Вказано недійсну роль.");
        RuleFor(x => x.Status).Must(x => !x.HasValue || Enum.IsDefined(x.Value)).WithMessage("Вказано недійсний статус.");
        RuleFor(x => x.SortBy).Must(value => SortFields.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Вказано недійсне поле сортування.");
        RuleFor(x => x.SortDirection).Must(value => value.Equals("asc", StringComparison.OrdinalIgnoreCase) || value.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Напрямок сортування має бути 'asc' або 'desc'.");
    }
}
