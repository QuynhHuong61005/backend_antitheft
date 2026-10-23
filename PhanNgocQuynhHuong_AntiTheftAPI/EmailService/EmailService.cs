using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
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
        var fromEmail = !string.IsNullOrWhiteSpace(_settings.SenderEmail)
            ? _settings.SenderEmail
            : _settings.Username;
        var appPassword = _settings.Password;
        var host = !string.IsNullOrWhiteSpace(_settings.Host)
            ? _settings.Host
            : "smtp.gmail.com";

        using var client = new SmtpClient(host);
        client.Port = 587; 
        client.EnableSsl = true; 
        client.UseDefaultCredentials = false; // Bắt buộc nằm trên dòng gán Credentials
        client.Credentials = new NetworkCredential(fromEmail, appPassword);
        client.Timeout = 10000; // Ngắt kết nối sau 10 giây nếu bị treo

        using var message = new MailMessage();
        message.From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(_settings.SenderName) ? fromEmail : _settings.SenderName);
        message.To.Add(toEmail);
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = body.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || body.Contains("<div", StringComparison.OrdinalIgnoreCase)
            || body.Contains("<p", StringComparison.OrdinalIgnoreCase)
            || body.Contains("<h2", StringComparison.OrdinalIgnoreCase);

        await client.SendMailAsync(message);
    }
}
