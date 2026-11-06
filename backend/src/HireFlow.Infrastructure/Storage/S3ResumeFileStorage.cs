using Amazon.S3;
using Amazon.S3.Model;
using HireFlow.Application.Abstractions;

namespace HireFlow.Infrastructure.Storage;

public sealed class S3ResumeFileStorage(IAmazonS3 client, string bucket, bool serverSideEncryption) : IResumeFileStorage
{
    public async Task<string> SaveAsync(Guid organizationId, Guid resumeId, string extension, string contentType, Stream content, CancellationToken cancellationToken)
    {
        var key = $"{organizationId:N}/{resumeId:N}{extension}";
        var request = new PutObjectRequest { BucketName = bucket, Key = key, InputStream = content, ContentType = contentType, AutoCloseStream = false };
        if (serverSideEncryption) request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
        await client.PutObjectAsync(request, cancellationToken);
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

