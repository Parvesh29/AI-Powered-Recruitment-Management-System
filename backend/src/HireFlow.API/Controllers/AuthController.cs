using FluentValidation;
using HireFlow.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthenticationService authentication,
    IHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string RefreshCookie = "hireflow_refresh";

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await authentication.GetCurrentUserAsync(cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, IValidator<RegisterRequest> validator, CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(request, validator, cancellationToken);
        if (errors is not null) return BadRequest(new ValidationProblemDetails(errors));

        try
        {
            var session = await authentication.RegisterAsync(request, cancellationToken);
            SetRefreshCookie(session);
            return Ok(new { session.AccessToken, session.AccessTokenExpiresAt, session.User });
        }
        catch (AuthenticationConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Registration conflict", Detail = exception.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, IValidator<LoginRequest> validator, CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(request, validator, cancellationToken);
        if (errors is not null) return BadRequest(new ValidationProblemDetails(errors));

        var session = await authentication.LoginAsync(request, cancellationToken);
        if (session is null) return Unauthorized(new ProblemDetails { Title = "Invalid credentials." });
        SetRefreshCookie(session);
        return Ok(new { session.AccessToken, session.AccessTokenExpiresAt, session.User });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
            return NoContent();

        var session = await authentication.RefreshAsync(refreshToken, cancellationToken);
        if (session is null)
        {
            DeleteRefreshCookie();
            return Unauthorized(new ProblemDetails { Title = "Refresh session is invalid or expired." });
        }

        SetRefreshCookie(session);
        return Ok(new { session.AccessToken, session.AccessTokenExpiresAt, session.User });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authentication.RevokeAsync(Request.Cookies[RefreshCookie], cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(PasswordResetRequest request, IValidator<PasswordResetRequest> validator, CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(request, validator, cancellationToken);
        if (errors is not null) return BadRequest(new ValidationProblemDetails(errors));
        try
        {
            await authentication.RequestPasswordResetAsync(request, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Password reset delivery is not configured.");
            return Problem("Password reset delivery is temporarily unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        return Accepted(new { message = "If the account exists, password reset instructions will be sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(PasswordResetCompletion request, IValidator<PasswordResetCompletion> validator, CancellationToken cancellationToken)
    {
        var errors = await ValidateAsync(request, validator, cancellationToken);
        if (errors is not null) return BadRequest(new ValidationProblemDetails(errors));
        var completed = await authentication.CompletePasswordResetAsync(request, cancellationToken);
        return completed ? NoContent() : BadRequest(new ProblemDetails { Title = "Reset token is invalid or expired." });
    }

    private void SetRefreshCookie(AuthSession session)
    {
        Response.Cookies.Append(RefreshCookie, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() || Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = session.RefreshTokenExpiresAt,
            IsEssential = true
        });
    }

    private void DeleteRefreshCookie() => Response.Cookies.Delete(RefreshCookie, new CookieOptions
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth"
    });

    private static async Task<IDictionary<string, string[]>?> ValidateAsync<T>(T model, IValidator<T> validator, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(model, cancellationToken);
        return result.IsValid ? null : result.ToDictionary();
    }
}




