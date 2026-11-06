namespace HireFlow.Application.Abstractions;

public interface ICurrentTenant
{
    Guid OrganizationId { get; }
    Guid UserId { get; }
    bool IsAuthenticated { get; }
}
