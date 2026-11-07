namespace UserAuthService.Interfaces;

public interface ITokenService
{
    string GenerateToken(int userId, string email, string firstName, string lastName, string role);
}
