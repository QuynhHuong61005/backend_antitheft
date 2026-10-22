using Microsoft.AspNetCore.Identity;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}