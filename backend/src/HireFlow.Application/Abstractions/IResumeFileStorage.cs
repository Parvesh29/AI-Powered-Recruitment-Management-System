namespace HireFlow.Application.Abstractions;

public interface IResumeFileStorage
{
    Task<string> SaveAsync(Guid organizationId, Guid resumeId, string extension, string contentType, Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

