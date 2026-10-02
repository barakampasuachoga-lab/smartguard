using System.Net;
using System.Net.Mail;
using System.Text;

namespace SmartGuard.API.Services;

public sealed class SmtpEmailSender(IConfiguration configuration)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"]) &&
        !string.IsNullOrWhiteSpace(configuration["Email:Smtp:FromAddress"]);

    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var host = configuration["Email:Smtp:Host"];
        var fromAddress = configuration["Email:Smtp:FromAddress"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException("Email delivery is not configured.");
        }

        var fromName = configuration["Email:Smtp:FromName"] ?? "SmartGuard";
        var port = int.TryParse(configuration["Email:Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        var enableSsl = !bool.TryParse(configuration["Email:Smtp:EnableSsl"], out var configuredSsl) || configuredSsl;

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipient));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20000
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(username, password ?? string.Empty);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
