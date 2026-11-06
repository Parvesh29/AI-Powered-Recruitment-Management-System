using System.Security.Claims;
using System.Text;
using FluentValidation;
using HireFlow.API.Authentication;
using HireFlow.API.Authorization;
using HireFlow.Application.Abstractions;
using HireFlow.Application.Authentication;
using HireFlow.Infrastructure;
using HireFlow.Infrastructure.Persistence;
using HireFlow.API.Notifications;
using Hangfire;
using Hangfire.PostgreSql;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Configure Jwt:SigningKey with at least 32 UTF-8 bytes.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? "HireFlow";
var audience = builder.Configuration["Jwt:Audience"] ?? "HireFlow.Client";

builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 11 * 1024 * 1024);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddRateLimiter(options => options.AddPolicy("public-applications", context =>
    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true
    })));
builder.Services.AddScoped<ICurrentTenant, HttpCurrentTenant>();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHangfire((_, configuration) => configuration.UsePostgreSqlStorage(options =>
    options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.Request.Path.StartsWithSegments("/hubs/notifications"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

var app = builder.Build();

await using (var startupScope = app.Services.CreateAsyncScope())
{
    var database = startupScope.ServiceProvider.GetRequiredService<HireFlowDbContext>();
    if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        await database.Database.MigrateAsync();
    await PermissionCatalogSeeder.SeedAsync(database);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "hireflow-api" })).AllowAnonymous();
app.MapGet("/api/health/database", async (HireFlowDbContext db, CancellationToken cancellationToken) =>
{
    var reachable = await db.Database.CanConnectAsync(cancellationToken);
    return reachable ? Results.Ok(new { status = "ok", database = "postgresql" }) : Results.Problem("Database is unavailable.");
}).AllowAnonymous();
app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");
app.Run();

public partial class Program;



