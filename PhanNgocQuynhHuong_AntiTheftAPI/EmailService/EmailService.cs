using MailKit.Net.Smtp;
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

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var fromEmail = !string.IsNullOrWhiteSpace(_settings.SenderEmail)
            ? _settings.SenderEmail.Trim()
            : _settings.Username.Trim();
        var appPassword = _settings.Password?.Trim() ?? string.Empty;
        var host = !string.IsNullOrWhiteSpace(_settings.Host)
            ? _settings.Host.Trim()
            : "smtp.gmail.com";
        var port = _settings.Port > 0 ? _settings.Port : 587;

        var email = new MimeMessage();
        if (!string.IsNullOrWhiteSpace(_settings.SenderName))
        {
            email.From.Add(new MailboxAddress(_settings.SenderName, fromEmail));
        }
        else
        {
            email.From.Add(MailboxAddress.Parse(fromEmail));
        }
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;
        
        var builder = new BodyBuilder { HtmlBody = body };
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        smtp.Timeout = 10000; // Ngắt kết nối sau 10 giây nếu bị treo

        try
        {
            // Bắt buộc dùng StartTls
            await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(fromEmail, appPassword);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
            Console.WriteLine("✅ Gửi mail thành công!");
        }
        catch (Exception ex)
        {
            // BẮT BUỘC phải có dòng này để Render in lỗi ra tab Logs
            Console.WriteLine("❌ LỖI GỬI MAIL GẶP PHẢI: " + ex.ToString());
            throw;
        }
    }
}
