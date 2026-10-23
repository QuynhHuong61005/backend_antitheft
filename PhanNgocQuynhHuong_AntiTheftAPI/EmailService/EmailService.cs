using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Configuration;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Services;

public class EmailService
{
    private readonly IConfiguration? _configuration;

    public EmailService(IConfiguration? configuration = null)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        // Đường link Google Apps Script Web App
        string googleScriptUrl = Environment.GetEnvironmentVariable("GOOGLE_SCRIPT_URL")
            ?? _configuration?["GoogleScriptUrl"]
            ?? "https://script.google.com/macros/s/AKfycbwTZ1mL2FaTxMw5-UUKoHxcoFjq8MoWC3V15_b0LFJYAkoRQIlZtTlSxrI99bQzmeJt/exec"; 

        using var httpClient = new HttpClient();
        
        var payload = new {
            to = toEmail,
            subject = subject,
            body = body
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try 
        {
            // Gửi request qua HTTPS (Cổng 443 - không bao giờ bị Render chặn)
            var response = await httpClient.PostAsync(googleScriptUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✅ ĐÃ GỬI MAIL THÀNH CÔNG QUA GOOGLE SCRIPT");
            }
            else
            {
                Console.WriteLine($"❌ LỖI TỪ GOOGLE SCRIPT: {response.StatusCode}");
                throw new Exception($"Lỗi từ Google Script: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ LỖI KẾT NỐI SCRIPT: " + ex.Message);
            throw;
        }
    }
}
