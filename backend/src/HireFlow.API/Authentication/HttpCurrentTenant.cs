using System.Security.Claims;
using HireFlow.Application.Abstractions;

namespace HireFlow.API.Authentication;

public sealed class HttpCurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid OrganizationId => Guid.TryParse(Principal?.FindFirstValue("org"), out var id) ? id : Guid.Empty;
    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

