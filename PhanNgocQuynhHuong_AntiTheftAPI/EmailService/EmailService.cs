
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PhanNgocQuynhHuong_AntiTheftAPI.Settings;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Services;

public class EmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body)
    {
        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                _settings.SenderName,
                _settings.SenderEmail));

        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = body
        };

        using var client = new MailKit.Net.Smtp.SmtpClient();

        await client.ConnectAsync(
            _settings.Host,
            _settings.Port,
            SecureSocketOptions.StartTls);

        await client.AuthenticateAsync(
            _settings.Username,
            _settings.Password);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
