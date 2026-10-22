using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhanNgocQuynhHuong_AntiTheftAPI.Data;
using PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Face;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Services;

public class FaceRecognitionService : IFaceRecognitionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly EmailService _emailService;
    private readonly ILogger<FaceRecognitionService> _logger;

    // Ngưỡng tương đồng Cosine chuẩn cho mô hình nhận diện khuôn mặt (MobileFaceNet/ArcFace/FaceNet)
    public const double DefaultMatchThreshold = 0.75;

    public FaceRecognitionService(
        ApplicationDbContext dbContext,
        EmailService emailService,
        ILogger<FaceRecognitionService> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
    }

    public (bool IsValid, string? ErrorMessage, double[]? Vector) ParseAndValidateEmbedding(string? embeddingJson)
    {
        if (string.IsNullOrWhiteSpace(embeddingJson))
        {
            return (false, "Dữ liệu vector đặc trưng (Embedding) trống.", null);
        }

        try
        {
            var vector = JsonSerializer.Deserialize<double[]>(embeddingJson);
            if (vector == null || vector.Length == 0)
            {
                return (false, "Vector đặc trưng không chứa phần tử hợp lệ.", null);
            }

            // Kiểm tra số chiều thông dụng của các mô hình nhận diện khuôn mặt (32 đến 2048 chiều)
            if (vector.Length < 32 || vector.Length > 2048)
            {
                return (false, $"Số chiều của vector đặc trưng ({vector.Length}) không phù hợp với các chuẩn mô hình nhận diện (32 - 2048 chiều).", null);
            }

            // Kiểm tra giá trị không được là NaN hoặc vô cực
            if (vector.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
            {
                return (false, "Vector đặc trưng chứa giá trị không hợp lệ (NaN hoặc Infinity).", null);
            }

            return (true, null, vector);
        }
        catch (JsonException)
        {
            return (false, "Định dạng JSON của vector đặc trưng không đúng quy cách (phải là JSON array các số thực, ví dụ: [0.12, -0.05, ...]).", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi phân tích vector đặc trưng khuôn mặt.");
            return (false, "Không thể xử lý dữ liệu vector đặc trưng.", null);
        }
    }

    public double CalculateCosineSimilarity(double[] vectorA, double[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
        {
            throw new ArgumentException($"Số chiều vector không khớp: {vectorA.Length} so với {vectorB.Length}.");
        }

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA <= 0.0 || normB <= 0.0)
        {
            return 0.0;
        }

        var similarity = dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        // Giới hạn giá trị trong khoảng [-1.0, 1.0] để tránh sai số số thực
        return Math.Clamp(similarity, -1.0, 1.0);
    }

    /// <summary>
    /// Xác minh 1:1 (giữ tương thích ngược)
    /// </summary>
    public async Task<FaceVerificationResultDto> VerifyFaceAsync(
        ApplicationUser user,
        FaceProfile faceProfile,
        VerifyFaceRequest request,
        string? capturedImagePath)
    {
        return await VerifyFace1ToNAsync(
            user,
            new List<FaceProfile> { faceProfile },
            request,
            capturedImagePath);
    }

    /// <summary>
    /// Xác minh 1:N đối chiếu khuôn mặt camera với tất cả hồ sơ người được phép
    /// </summary>
    public async Task<FaceVerificationResultDto> VerifyFace1ToNAsync(
        ApplicationUser user,
        List<FaceProfile> faceProfiles,
        VerifyFaceRequest request,
        string? capturedImagePath)
    {
        var result = new FaceVerificationResultDto
        {
            VerifiedAt = DateTime.UtcNow,
            CapturedImageUrl = capturedImagePath != null ? $"/api/face/image/{capturedImagePath}" : null,
            IsMatched = false,
            Similarity = 0.0,
            Confidence = 0.0,
            SecurityAlertTriggered = false
        };

        // Lọc các hồ sơ đang kích hoạt
        var activeProfiles = faceProfiles.Where(p => p.IsActive).ToList();
        if (activeProfiles.Count == 0)
        {
            result.Message = "Tài khoản của bạn chưa có hồ sơ người được phép nào đang kích hoạt (Active).";
            await RecordAccessLogAsync(user.Id, "FaceVerificationRejected", result.Message, capturedImagePath);
            return result;
        }

        // Validate vector đặc trưng gửi lên từ camera
        if (string.IsNullOrWhiteSpace(request.EmbeddingJson))
        {
            result.Message = "Dữ liệu xác minh gửi lên từ camera chưa có vector đặc trưng trích xuất từ khuôn mặt.";
            await RecordAccessLogAsync(user.Id, "FaceVerificationPendingEmbedding", result.Message, capturedImagePath);
            return result;
        }

        var (isInputValid, inputErr, inputVector) = ParseAndValidateEmbedding(request.EmbeddingJson);
        if (!isInputValid || inputVector == null)
        {
            result.Message = $"Dữ liệu vector đầu vào không hợp lệ: {inputErr}";
            await RecordAccessLogAsync(user.Id, "FaceVerificationError", result.Message, capturedImagePath);
            return result;
        }

        FaceProfile? bestProfile = null;
        double bestSimilarity = -1.0;
        int validEvaluatedProfiles = 0;

        foreach (var profile in activeProfiles)
        {
            if (string.IsNullOrWhiteSpace(profile.FaceEmbeddingJson))
                continue;

            var (isProfileValid, _, profileVector) = ParseAndValidateEmbedding(profile.FaceEmbeddingJson);
            if (!isProfileValid || profileVector == null || profileVector.Length != inputVector.Length)
                continue;

            validEvaluatedProfiles++;
            var sim = CalculateCosineSimilarity(profileVector, inputVector);
            if (sim > bestSimilarity)
            {
                bestSimilarity = sim;
                bestProfile = profile;
            }
        }

        if (validEvaluatedProfiles == 0)
        {
            result.Message = "Các hồ sơ đã đăng ký chưa có dữ liệu vector đặc trưng hợp lệ hoặc số chiều vector không khớp với mô hình camera.";
            await RecordAccessLogAsync(user.Id, "FaceVerificationError", result.Message, capturedImagePath);
            return result;
        }

        var formattedSim = Math.Round(bestSimilarity, 4);
        var confidence = Math.Round(Math.Max(0.0, bestSimilarity) * 100, 2);

        result.Similarity = formattedSim;
        result.Confidence = confidence;

        if (bestSimilarity >= DefaultMatchThreshold && bestProfile != null)
        {
            // XÁC MINH THÀNH CÔNG (NGƯỜI QUEN TRÙNG KHỚP)
            result.IsMatched = true;
            result.MatchedProfileId = bestProfile.Id;
            result.MatchedProfileName = bestProfile.ProfileName;
            result.Message = $"Nhận diện thành công: {bestProfile.ProfileName}. Độ khớp: {confidence}%.";

            await RecordAccessLogAsync(
                user.Id,
                "FaceVerificationSuccess",
                $"Nhận diện thành công '{bestProfile.ProfileName}' cho tài khoản {user.FullName} ({user.Email}). Độ khớp: {confidence}%.",
                capturedImagePath);
        }
        else
        {
            // XÁC MINH THẤT BẠI (NGƯỜI CHƯA NHẬN DIỆN / NGƯỜI LẠ)
            result.IsMatched = false;
            result.MatchedProfileId = null;
            result.MatchedProfileName = null;
            result.Message = $"Khuôn mặt chưa nhận diện được trong danh sách người được phép. Độ khớp cao nhất: {confidence}% (yêu cầu tối thiểu {DefaultMatchThreshold * 100}%).";

            await RecordAccessLogAsync(
                user.Id,
                "FaceVerificationFailed",
                $"Phát hiện khuôn mặt chưa nhận diện cho tài khoản {user.Email}. Độ tương đồng cao nhất: {confidence}%.",
                capturedImagePath);

            // Kiểm tra cấu hình bảo mật SecuritySetting để kích hoạt cảnh báo nếu Armed
            var setting = await _dbContext.SecuritySettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (setting != null && setting.IsArmed)
            {
                result.SecurityAlertTriggered = true;
                await TriggerSecurityAlertAsync(user, setting, capturedImagePath, confidence);
            }
        }

        return result;
    }

    private async Task RecordAccessLogAsync(string? userId, string eventType, string description, string? capturedImagePath)
    {
        try
        {
            var accessLog = new AccessLog
            {
                UserId = userId,
                EventType = eventType,
                Description = description.Length > 500 ? description[..500] : description,
                CapturedImagePath = capturedImagePath,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AccessLogs.Add(accessLog);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu AccessLog cho sự kiện khuôn mặt.");
        }
    }

    private async Task TriggerSecurityAlertAsync(ApplicationUser user, SecuritySetting setting, string? imagePath, double confidence)
    {
        try
        {
            const string alertType = "FaceAlert";
            var message = $"Cảnh báo an ninh: Phát hiện khuôn mặt không khớp (độ khớp cao nhất {confidence:F1}%) khi chế độ bảo vệ đang bật.";

            var alertLog = new AlertLog
            {
                UserId = user.Id,
                AlertType = alertType,
                Message = message.Length > 500 ? message[..500] : message,
                ImagePath = imagePath,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AlertLogs.Add(alertLog);
            await _dbContext.SaveChangesAsync();

            // Gửi email nếu người dùng bật EnableEmailAlert
            if (setting.EnableEmailAlert && !string.IsNullOrWhiteSpace(user.Email))
            {
                const string subject = "AntiTheft - Cảnh báo nhận diện khuôn mặt lạ!";
                var body = $"""
                    Hệ thống AntiTheft phát hiện một khuôn mặt không có trong danh sách người được phép trước camera.

                    Thời gian ghi nhận (UTC): {alertLog.CreatedAt:yyyy-MM-dd HH:mm:ss}
                    Tài khoản: {user.FullName} ({user.Email})
                    Độ khớp cao nhất: {confidence:F1}%
                    Chế độ an ninh: ĐANG BẬT BẢO VỆ (Armed)

                    Nếu đây không phải bạn hoặc người thân được phép, hãy kiểm tra thiết bị ngay lập tức!
                    """;

                try
                {
                    await _emailService.SendEmailAsync(user.Email, subject, body);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Lỗi gửi email cảnh báo nhận diện khuôn mặt lạ.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kích hoạt cảnh báo an ninh cho sự kiện khuôn mặt.");
        }
    }
}
