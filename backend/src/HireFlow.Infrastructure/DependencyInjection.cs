using HireFlow.Application.Authentication;
using HireFlow.Application.Abstractions;
using Amazon;
using Amazon.S3;
using HireFlow.Domain.Entities.Organizations;
using HireFlow.Infrastructure.Authentication;
using HireFlow.Infrastructure.Persistence;
using HireFlow.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");

        services.AddDbContext<HireFlowDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(HireFlowDbContext).Assembly.FullName)
                .MigrationsHistoryTable("__EFMigrationsHistory", "public")));
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordResetLinkSender, PasswordResetLinkSender>();
        var bucket = configuration["Storage:S3:Bucket"];
        if (!string.IsNullOrWhiteSpace(bucket))
        {
            var s3Config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(configuration["Storage:S3:Region"] ?? "us-east-1"),
                ForcePathStyle = bool.TryParse(configuration["Storage:S3:ForcePathStyle"], out var pathStyle) && pathStyle
            };
            var serviceUrl = configuration["Storage:S3:ServiceUrl"];
            if (!string.IsNullOrWhiteSpace(serviceUrl)) s3Config.ServiceURL = serviceUrl;
            var accessKey = configuration["Storage:S3:AccessKey"];
            var secretKey = configuration["Storage:S3:SecretKey"];
            IAmazonS3 s3 = string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey)
                ? new AmazonS3Client(s3Config)
                : new AmazonS3Client(accessKey, secretKey, s3Config);
            services.AddSingleton(s3);
            services.AddSingleton<IResumeFileStorage>(_ => new S3ResumeFileStorage(s3, bucket));
        }
        else
        {
            var resumePath = configuration["Storage:LocalPath"] ?? Path.Combine(AppContext.BaseDirectory, "private-resumes");
            services.AddSingleton<IResumeFileStorage>(_ => new LocalResumeFileStorage(resumePath));
        }
        return services;
    }
}



