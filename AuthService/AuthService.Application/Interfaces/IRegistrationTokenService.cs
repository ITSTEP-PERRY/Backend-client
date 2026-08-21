namespace AuthService.Application.Interfaces;

public interface IRegistrationTokenService
{
    string Generate(Guid userId);
    Guid Validate(string token);
}
