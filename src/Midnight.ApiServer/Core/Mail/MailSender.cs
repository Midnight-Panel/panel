namespace Midnight.ApiServer.Core.Mail;

public interface IMailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

// TODO: SMTP implementation with settings in the admin ui. Until then mails are only written to the log.
public class LogMailSender : IMailSender
{
    private readonly ILogger<LogMailSender> _logger;

    public LogMailSender(ILogger<LogMailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("No mail transport configured. Mail to {To} with subject '{Subject}':\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
