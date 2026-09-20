using AuthService.Application.Exceptions;

namespace AuthService.Api.Security;

public interface ICurrentUserContext
{
    Guid UserId { get; }
}

public sealed class CurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    public Guid UserId
    {
        get
        {
            var subject = accessor.HttpContext?.User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(subject, out var userId))
                throw new AuthException("UNAUTHORIZED", "Authentication is required.", 401);
            return userId;
        }
    }
}
