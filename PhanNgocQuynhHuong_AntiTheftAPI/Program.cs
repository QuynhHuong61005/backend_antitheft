using PhanNgocQuynhHuong_AntiTheftAPI.Services;
using PhanNgocQuynhHuong_AntiTheftAPI.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PhanNgocQuynhHuong_AntiTheftAPI.Configurations;
using PhanNgocQuynhHuong_AntiTheftAPI.Data;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "Không tìm th?y c?u hình Jwt trong appsettings.json.");

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) ||
    string.IsNullOrWhiteSpace(jwtSettings.Audience) ||
    string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
{
    throw new InvalidOperationException(
        "Issuer, Audience và SecretKey không ???c ?? tr?ng.");
}

if (Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:SecretKey ph?i có ít nh?t 32 byte.");
}

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// ??c connection string t? appsettings.json.
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Không tìm th?y connection string DefaultConnection.");

// ??ng ký EF Core và SQL Server.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ??ng ký ASP.NET Core Identity.
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán accessToken nh?n ???c khi ??ng nh?p."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services
    .AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection("EmailSettings"))
    .Validate(settings =>
        !string.IsNullOrWhiteSpace(settings.Host) &&
        settings.Port > 0 &&
        !string.IsNullOrWhiteSpace(settings.SenderEmail) &&
        !string.IsNullOrWhiteSpace(settings.Username) &&
        !string.IsNullOrWhiteSpace(settings.Password),
        "EmailSettings ch?a ???c c?u hình ??y ??.")
    .ValidateOnStart();

builder.Services.AddTransient<EmailService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<EmailService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(
                new IdentityRole(role));

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(e => e.Description));

                throw new InvalidOperationException(
                    $"Không th? t?o role {role}: {errors}");
            }
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();