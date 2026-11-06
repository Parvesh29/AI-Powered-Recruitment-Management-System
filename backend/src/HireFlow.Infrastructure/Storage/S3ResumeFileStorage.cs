using Amazon.S3;
using Amazon.S3.Model;
using HireFlow.Application.Abstractions;

namespace HireFlow.Infrastructure.Storage;

public sealed class S3ResumeFileStorage(IAmazonS3 client, string bucket) : IResumeFileStorage
{
    public async Task<string> SaveAsync(Guid organizationId, Guid resumeId, string extension, string contentType, Stream content, CancellationToken cancellationToken)
    {
        var key = $"{organizationId:N}/{resumeId:N}{extension}";
        await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key, InputStream = content, ContentType = contentType, ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256, AutoCloseStream = false }, cancellationToken);
        return key;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        using var response = await client.GetObjectAsync(bucket, storageKey, cancellationToken);
        var content = new MemoryStream();
        await response.ResponseStream.CopyToAsync(content, cancellationToken);
        content.Position = 0;
        return content;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
        await client.DeleteObjectAsync(bucket, storageKey, cancellationToken);
}

