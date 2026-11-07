using UserAuthService.DTOs;
using UserAuthService.Interfaces;
using UserAuthService.Models;

namespace UserAuthService.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(MapToResponseDto);
    }

    public async Task<UserResponseDto?> GetUserByIdAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user != null ? MapToResponseDto(user) : null;
    }

    public async Task<UserResponseDto> CreateUserAsync(UserCreateDto createDto)
    {
        if (await _userRepository.EmailExistsAsync(createDto.Email))
            throw new ArgumentException($"Email {createDto.Email} is already registered.");

        var user = new User
        {
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            Email = createDto.Email,
            PasswordHash = HashPassword(createDto.Password),
            Phone = createDto.Phone,
            Address = createDto.Address,
            Role = createDto.Role
        };

        var created = await _userRepository.CreateAsync(user);
        _logger.LogInformation("User created with ID: {UserId}, Role: {Role}", created.Id, created.Role);
        return MapToResponseDto(created);
    }

    public async Task<UserResponseDto?> UpdateUserAsync(int id, UserUpdateDto updateDto)
    {
        var existing = await _userRepository.GetByIdAsync(id);
        if (existing == null) return null;

        if (updateDto.Email != null && updateDto.Email != existing.Email)
        {
            if (await _userRepository.EmailExistsAsync(updateDto.Email))
                throw new ArgumentException($"Email {updateDto.Email} is already registered.");
            existing.Email = updateDto.Email;
        }

        if (updateDto.FirstName != null) existing.FirstName = updateDto.FirstName;
        if (updateDto.LastName != null) existing.LastName = updateDto.LastName;
        if (updateDto.Phone != null) existing.Phone = updateDto.Phone;
        if (updateDto.Address != null) existing.Address = updateDto.Address;
        if (updateDto.Role.HasValue) existing.Role = updateDto.Role.Value;

        var updated = await _userRepository.UpdateAsync(existing);
        return updated != null ? MapToResponseDto(updated) : null;
    }

    public async Task<bool> DeleteUserAsync(int id) =>
        await _userRepository.DeleteAsync(id);

    public async Task<LoginResponseDto?> AuthenticateAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            _logger.LogWarning("Login failed: user not found for email {Email}", email);
            return null;
        }

        if (user.PasswordHash != HashPassword(password))
        {
            _logger.LogWarning("Login failed: invalid password for email {Email}", email);
            return null;
        }

        var token = _tokenService.GenerateToken(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString());
        var expiryMinutes = _configuration.GetValue<int>("JwtSettings:ExpiryMinutes", 60);

        _logger.LogInformation("User {UserId} authenticated successfully", user.Id);

        return new LoginResponseDto
        {
            Token = token,
            TokenType = "Bearer",
            ExpiresIn = expiryMinutes * 60,
            User = MapToResponseDto(user)
        };
    }

    private static UserResponseDto MapToResponseDto(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Phone = user.Phone,
        Address = user.Address,
        Role = user.Role.ToString(),
        CreatedAt = user.CreatedAt
    };

    private static string HashPassword(string password) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
}
