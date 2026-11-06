using System.Net;
using System.Net.Mail;
using HireFlow.Application.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HireFlow.Infrastructure.Authentication;

public sealed class PasswordResetLinkSender(IConfiguration configuration, ILogger<PasswordResetLinkSender> logger) : IPasswordResetLinkSender
{
    public async Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken)
    {
        var smtp = configuration.GetSection("Smtp");
        var host = smtp["Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Development password reset link for {Email}: {ResetUrl}", email, resetUrl);
                return;
            }

            throw new InvalidOperationException("SMTP must be configured to send password reset messages.");
        }

        using var message = new MailMessage(smtp["From"] ?? "no-reply@hireflow.local", email)
        {
            Subject = "Reset your HireFlow password",
            Body = $"Use this link within one hour to reset your HireFlow password: {WebUtility.HtmlEncode(resetUrl)}",
            IsBodyHtml = false
        };
#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(host, int.TryParse(smtp["Port"], out var port) ? port : 587)
        {
            EnableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl,
            Credentials = string.IsNullOrWhiteSpace(smtp["Username"])
                ? null
                : new NetworkCredential(smtp["Username"], smtp["Password"])
        };
        await client.SendMailAsync(message, cancellationToken);
#pragma warning restore SYSLIB0014
    }
}
