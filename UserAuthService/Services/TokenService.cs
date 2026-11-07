using SharedKernel.Auth;
using Microsoft.Extensions.Configuration;
using UserAuthService.Interfaces;

namespace UserAuthService.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IConfiguration configuration, ILogger<TokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateToken(int userId, string email, string firstName, string lastName, string role)
    {
        var token = JwtHelper.GenerateToken(_configuration, userId, email, firstName, lastName, role);
        _logger.LogInformation("Generated JWT token for user {UserId}", userId);
        return token;
    }
}
