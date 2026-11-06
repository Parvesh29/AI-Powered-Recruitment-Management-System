namespace HireFlow.Application.Authentication;

public interface IPasswordResetLinkSender
{
    Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken);
}
