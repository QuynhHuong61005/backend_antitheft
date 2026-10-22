
using System.ComponentModel.DataAnnotations;

namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs;

public class VerifyLoginOtpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$",
        ErrorMessage = "OTP phải gồm đúng 6 chữ số.")]
    public string Otp { get; set; } = string.Empty;
}
