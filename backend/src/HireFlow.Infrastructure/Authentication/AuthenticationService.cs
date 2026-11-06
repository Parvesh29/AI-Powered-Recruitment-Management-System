using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HireFlow.Application.Authentication;
using HireFlow.Application.Abstractions;
using HireFlow.Domain.Entities.Organizations;
using HireFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HireFlow.Infrastructure.Authentication;

public sealed class AuthenticationService(
    HireFlowDbContext db,
    ICurrentTenant currentTenant,
    IPasswordHasher<User> passwordHasher,
    IPasswordResetLinkSender resetLinkSender,
    IConfiguration configuration,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    public async Task<AuthSession> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var slug = request.OrganizationSlug.Trim().ToLowerInvariant();
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await db.Organizations.AnyAsync(x => x.Slug == slug, cancellationToken))
            throw new AuthenticationConflictException("The organization slug is already registered.");

        var now = DateTimeOffset.UtcNow;
        var organization = new Organization { Name = request.OrganizationName.Trim(), Slug = slug, CreatedAt = now, UpdatedAt = now };
        var user = new User
        {
            OrganizationId = organization.Id,
            Email = email,
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var roleNames = new[] { "OrganizationAdmin", "Recruiter", "Interviewer", "Candidate" };
        var roles = roleNames.Select(name => new Role { OrganizationId = organization.Id, Name = name, IsSystemRole = true }).ToArray();
        var adminRole = roles[0];

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Organizations.Add(organization);
        db.Users.Add(user);
        db.Roles.AddRange(roles);
        db.UserRoles.Add(new UserRole { OrganizationId = organization.Id, UserId = user.Id, RoleId = adminRole.Id });
        var allPermissions = await db.Permissions.ToListAsync(cancellationToken);
        db.RolePermissions.AddRange(allPermissions.Select(permission => new RolePermission
        {
            OrganizationId = organization.Id,
            RoleId = adminRole.Id,
            PermissionId = permission.Id
        }));
        var grantsByRole = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Recruiter"] = ["jobs.read", "jobs.create", "jobs.update", "jobs.delete", "candidates.read", "candidates.create", "candidates.update", "applications.read", "applications.update", "interviews.read", "interviews.create", "analytics.read"],
            ["Interviewer"] = ["interviews.read", "interviews.feedback"]
        };
        foreach (var role in roles.Where(x => grantsByRole.ContainsKey(x.Name)))
        {
            var grantCodes = grantsByRole[role.Name];
            db.RolePermissions.AddRange(allPermissions.Where(x => grantCodes.Contains(x.Code, StringComparer.Ordinal)).Select(permission => new RolePermission
            {
                OrganizationId = organization.Id,
                RoleId = role.Id,
                PermissionId = permission.Id
            }));
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogInformation(exception, "Organization registration conflicted with a unique database constraint.");
            throw new AuthenticationConflictException("The organization slug or email address is already registered.");
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<AuthUserDetails?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAuthenticated || currentTenant.OrganizationId == Guid.Empty || currentTenant.UserId == Guid.Empty)
            return null;

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            x => x.OrganizationId == currentTenant.OrganizationId && x.Id == currentTenant.UserId,
            cancellationToken);
        if (user is null) return null;

        var roles = await (from userRole in db.UserRoles.IgnoreQueryFilters()
                           join role in db.Roles.IgnoreQueryFilters()
                               on new { userRole.OrganizationId, userRole.RoleId } equals new { role.OrganizationId, RoleId = role.Id }
                           where userRole.OrganizationId == user.OrganizationId && userRole.UserId == user.Id
                           select role.Name).Distinct().ToListAsync(cancellationToken);
        return new AuthUserDetails(user.Id, user.OrganizationId, user.Email, $"{user.FirstName} {user.LastName}".Trim(), roles);
    }
    public async Task<AuthSession?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var slug = request.OrganizationSlug.Trim().ToLowerInvariant();
        var organization = await db.Organizations.SingleOrDefaultAsync(x => x.Slug == slug && x.IsActive, cancellationToken);
        if (organization is null) return null;

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(
            x => x.OrganizationId == organization.Id && x.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (user is null || !user.IsActive) return null;

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed) return null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<AuthSession?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        var tokenHash = HashToken(refreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stored = await db.RefreshTokens.IgnoreQueryFilters().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (stored is null) return null;

        var now = DateTimeOffset.UtcNow;
        if (stored.RevokedAt is not null || stored.ExpiresAt <= now || stored.User is null || !stored.User.IsActive)
        {
            if (stored.RevokedAt is not null && stored.ReplacedByTokenHash is not null)
            {
                await RevokeAllRefreshTokensAsync(stored.OrganizationId, stored.UserId, now, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var rawReplacement = CreateRawToken();
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = HashToken(rawReplacement);
        var replacement = new RefreshToken
        {
            OrganizationId = stored.OrganizationId,
            UserId = stored.UserId,
            TokenHash = stored.ReplacedByTokenHash,
            ExpiresAt = now.AddDays(GetInt("Jwt:RefreshTokenDays", 30))
        };
        db.RefreshTokens.Add(replacement);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            await RevokeAllRefreshTokensAsync(stored.OrganizationId, stored.UserId, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        return await CreateSessionAsync(stored.User, cancellationToken, rawReplacement, replacement.ExpiresAt);
    }

    public async Task RevokeAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (stored is null || stored.RevokedAt is not null) return;
        stored.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.SingleOrDefaultAsync(
            x => x.Slug == request.OrganizationSlug.Trim().ToLowerInvariant() && x.IsActive,
            cancellationToken);
        if (organization is null) return;

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(
            x => x.OrganizationId == organization.Id && x.NormalizedEmail == normalizedEmail && x.IsActive,
            cancellationToken);
        if (user is null) return;

        var rawToken = CreateRawToken();
        var now = DateTimeOffset.UtcNow;
        var existing = await db.PasswordResetTokens.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organization.Id && x.UserId == user.Id && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var oldToken in existing) oldToken.UsedAt = now;

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = now.AddHours(1)
        });
        await db.SaveChangesAsync(cancellationToken);

        var baseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var resetUrl = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}&organization={Uri.EscapeDataString(organization.Slug)}";
        try
        {
            await resetLinkSender.SendAsync(user.Email, resetUrl, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not deliver a password reset link for user {UserId}.", user.Id);
        }
    }

    public async Task<bool> CompletePasswordResetAsync(PasswordResetCompletion request, CancellationToken cancellationToken)
    {
        var hash = HashToken(request.Token);
        var resetToken = await db.PasswordResetTokens.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (resetToken is null || resetToken.UsedAt is not null || resetToken.ExpiresAt <= now) return false;

        var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(
            x => x.OrganizationId == resetToken.OrganizationId && x.Id == resetToken.UserId && x.IsActive,
            cancellationToken);
        if (user is null) return false;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        resetToken.UsedAt = now;
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        await RevokeAllRefreshTokensAsync(resetToken.OrganizationId, resetToken.UserId, now, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    private async Task<AuthSession> CreateSessionAsync(User user, CancellationToken cancellationToken, string? replacementToken = null, DateTimeOffset? replacementExpiry = null)
    {
        var roleNames = await (from userRole in db.UserRoles.IgnoreQueryFilters()
                               join role in db.Roles.IgnoreQueryFilters()
                                   on new { userRole.OrganizationId, userRole.RoleId } equals new { role.OrganizationId, RoleId = role.Id }
                               where userRole.OrganizationId == user.OrganizationId && userRole.UserId == user.Id
                               select role.Name).Distinct().ToListAsync(cancellationToken);

        var permissions = await (from userRole in db.UserRoles.IgnoreQueryFilters()
                                 join rolePermission in db.RolePermissions.IgnoreQueryFilters()
                                     on new { userRole.OrganizationId, userRole.RoleId } equals new { rolePermission.OrganizationId, rolePermission.RoleId }
                                 join permission in db.Permissions
                                     on rolePermission.PermissionId equals permission.Id
                                 where userRole.OrganizationId == user.OrganizationId && userRole.UserId == user.Id
                                 select permission.Code).Distinct().ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(GetInt("Jwt:AccessTokenMinutes", 15));
        var rawRefresh = replacementToken ?? CreateRawToken();
        var refreshExpiry = replacementExpiry ?? now.AddDays(GetInt("Jwt:RefreshTokenDays", 30));
        if (replacementToken is null)
        {
            db.RefreshTokens.Add(new RefreshToken
            {
                OrganizationId = user.OrganizationId,
                UserId = user.Id,
                TokenHash = HashToken(rawRefresh),
                ExpiresAt = refreshExpiry
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return new AuthSession(
            CreateAccessToken(user, roleNames, permissions, now, accessExpiry),
            accessExpiry,
            rawRefresh,
            refreshExpiry,
            new AuthUserDetails(user.Id, user.OrganizationId, user.Email, $"{user.FirstName} {user.LastName}".Trim(), roleNames));
    }

    private string CreateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, DateTimeOffset now, DateTimeOffset expiry)
    {
        var key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
            
            new("org", user.OrganizationId.ToString("D")),
            new("email", user.Email),
            new("name", $"{user.FirstName} {user.LastName}".Trim())
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        claims.AddRange(permissions.Select(code => new Claim("permission", code)));

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "HireFlow",
            audience: configuration["Jwt:Audience"] ?? "HireFlow.Client",
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiry.UtcDateTime,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task RevokeAllRefreshTokensAsync(Guid organizationId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens) token.RevokedAt = now;
    }

    private int GetInt(string key, int fallback) => int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
    private static string CreateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}







