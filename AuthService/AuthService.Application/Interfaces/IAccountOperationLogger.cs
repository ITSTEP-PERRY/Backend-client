namespace AuthService.Application.Interfaces;

public interface IAccountOperationLogger
{
    void AvatarCleanupFailed(Guid userId, string operation);
}
