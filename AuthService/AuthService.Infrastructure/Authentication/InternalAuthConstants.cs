namespace AuthService.Infrastructure.Authentication;

public static class InternalAuthConstants
{
    public const string Scheme = "InternalService";
    public const string TokenUseClaim = "token_use";
    public const string TokenUseService = "service";
    public const string TokenUseAccess = "access";
    public const string PermissionClaim = "permission";
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string UsersReadPolicy = "InternalUsersRead";
    public const string UsersManagePolicy = "InternalUsersManage";
}
