using AuthService.Application.DTOs.Internal;

namespace AuthService.Application.Interfaces;

public interface IServiceTokenService
{
    ServiceTokenResponse Issue(string serviceName, string credential);
}
