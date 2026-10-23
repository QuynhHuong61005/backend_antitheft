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

    public Task SendEmailAsync(
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

        using var mailMessage = new MailMessage();
        mailMessage.From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(_settings.SenderName) ? fromEmail : _settings.SenderName);
        mailMessage.To.Add(toEmail);
        mailMessage.Subject = subject;
        mailMessage.Body = body;
        mailMessage.IsBodyHtml = body.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || body.Contains("<div", StringComparison.OrdinalIgnoreCase)
            || body.Contains("<p", StringComparison.OrdinalIgnoreCase)
            || body.Contains("<h2", StringComparison.OrdinalIgnoreCase);

        try
        {
            client.Send(mailMessage);
            Console.WriteLine("✅ Gửi mail thành công!"); 
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // BẮT BUỘC phải có dòng này để Render in lỗi ra tab Logs
            Console.WriteLine("❌ LỖI GỬI MAIL GẶP PHẢI: " + ex.ToString());
            
            // Quăng lỗi ra ngoài để API trả về lỗi 500 cho Flutter, tránh Flutter bị treo 
            throw; 
        }
    }
}
