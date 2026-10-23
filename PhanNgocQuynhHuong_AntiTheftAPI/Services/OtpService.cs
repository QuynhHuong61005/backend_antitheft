
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PhanNgocQuynhHuong_AntiTheftAPI.Data;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Services;

public class OtpService
{
    private readonly ApplicationDbContext _db;
    private readonly EmailService _emailService;

    public OtpService(
        ApplicationDbContext db,
        EmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public async Task SendLoginOtpAsync(string email)
    {
        email = email.Trim();

        var now = DateTime.UtcNow;

        // Vô hiệu hóa các OTP đăng nhập cũ chưa sử dụng.
        var oldOtps = await _db.EmailOtps
            .Where(x =>
                x.Email == email &&
                x.Purpose == "Login" &&
                !x.IsUsed)
            .ToListAsync();

        foreach (var oldOtp in oldOtps)
        {
            oldOtp.IsUsed = true;
        }

        // Tạo OTP ngẫu nhiên gồm 6 chữ số.
        var otp = RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6");

        var otpHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(otp)));

        var record = new EmailOtp
        {
            Email = email,
            OtpHash = otpHash,
            Purpose = "Login",
            ExpiresAt = now.AddMinutes(5),
            IsUsed = false,
            CreatedAt = now
        };

        _db.EmailOtps.Add(record);
        await _db.SaveChangesAsync();

        try
        {
            var html = $"""
                <h2>AntiTheft - Mã OTP đăng nhập</h2>
                <p>Mã xác minh của bạn là:</p>
                <h1 style="letter-spacing: 6px">{otp}</h1>
                <p>Mã có hiệu lực trong 5 phút.</p>
                <p>Nếu bạn không yêu cầu mã này, hãy bỏ qua email.</p>
                """;

            await _emailService.SendEmailAsync(
                email,
                "AntiTheft - Mã OTP đăng nhập",
                html);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine($"[OtpService] Lỗi khi gửi OTP tới {email}: {ex}");
            // Không để lại OTP còn hiệu lực nếu gửi email thất bại.
            record.IsUsed = true;
            await _db.SaveChangesAsync();
            throw;
        }
    }

    public async Task<bool> VerifyLoginOtpAsync(
        string email,
        string otp)
    {
        email = email.Trim();

        if (otp.Length != 6 ||
            !otp.All(char.IsAsciiDigit))
        {
            return false;
        }

        var now = DateTime.UtcNow;

        // Lấy OTP đăng nhập mới nhất của email.
        var record = await _db.EmailOtps
            .Where(x =>
                x.Email == email &&
                x.Purpose == "Login")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (record == null ||
            record.IsUsed ||
            record.ExpiresAt <= now)
        {
            return false;
        }

        var submittedHash = System.Security.Cryptography
            .SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(otp));

        byte[] storedHash;

        try
        {
            storedHash = Convert.FromHexString(record.OtpHash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (storedHash.Length != submittedHash.Length ||
            !System.Security.Cryptography
                .CryptographicOperations.FixedTimeEquals(
                    storedHash, submittedHash))
        {
            return false;
        }

        // Đánh dấu đã sử dụng khi OTP hợp lệ.
        record.IsUsed = true;
        await _db.SaveChangesAsync();

        return true;
    }

}
