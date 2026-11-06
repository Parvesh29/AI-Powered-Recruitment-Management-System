namespace HireFlow.Application.Authentication;

public sealed record RegisterRequest(string OrganizationName, string OrganizationSlug, string FirstName, string LastName, string Email, string Password);
public sealed record LoginRequest(string OrganizationSlug, string Email, string Password);
public sealed record PasswordResetRequest(string OrganizationSlug, string Email);
public sealed record PasswordResetCompletion(string Token, string NewPassword);
public sealed record AuthUserDetails(Guid Id, Guid OrganizationId, string Email, string DisplayName, IReadOnlyCollection<string> Roles);
public sealed record AuthSession(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, AuthUserDetails User);

public interface IAuthenticationService
{
    Task<AuthSession> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthSession?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthUserDetails?> GetCurrentUserAsync(CancellationToken cancellationToken);
    Task<AuthSession?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);
    Task RevokeAsync(string? refreshToken, CancellationToken cancellationToken);
    Task RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken);
    Task<bool> CompletePasswordResetAsync(PasswordResetCompletion request, CancellationToken cancellationToken);
}

public sealed class AuthenticationConflictException(string message) : Exception(message);

