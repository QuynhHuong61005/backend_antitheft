using PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Face;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Services;

public interface IFaceRecognitionService
{
    (bool IsValid, string? ErrorMessage, double[]? Vector) ParseAndValidateEmbedding(string? embeddingJson);
    double CalculateCosineSimilarity(double[] vectorA, double[] vectorB);
    Task<FaceVerificationResultDto> VerifyFaceAsync(
        ApplicationUser user,
        FaceProfile faceProfile,
        VerifyFaceRequest request,
        string? capturedImagePath);

    Task<FaceVerificationResultDto> VerifyFace1ToNAsync(
        ApplicationUser user,
        List<FaceProfile> faceProfiles,
        VerifyFaceRequest request,
        string? capturedImagePath);
}
