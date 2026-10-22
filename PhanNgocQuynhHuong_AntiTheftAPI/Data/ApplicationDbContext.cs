using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Data;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<FaceProfile> FaceProfiles => Set<FaceProfile>();

    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();

    public DbSet<SecuritySetting> SecuritySettings
        => Set<SecuritySetting>();

    public DbSet<AlertLog> AlertLogs => Set<AlertLog>();

    public DbSet<EmailOtp> EmailOtps => Set<EmailOtp>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Mỗi người dùng chỉ có một cấu hình bảo mật.
        builder.Entity<SecuritySetting>()
            .HasIndex(s => s.UserId)
            .IsUnique();

        // Mỗi hồ sơ khuôn mặt thuộc về một người dùng.
        builder.Entity<FaceProfile>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lịch sử truy cập có thể không xác định được người dùng.
        builder.Entity<AccessLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<SecuritySetting>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AlertLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tìm OTP theo email, mục đích và thời gian tạo.
        builder.Entity<EmailOtp>()
            .HasIndex(e => new
            {
                e.Email,
                e.Purpose,
                e.CreatedAt
            });
    }
}