using ForgeSelf.Api.Plugins.DevTools.Models;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public interface IQrCodeService
{
    Task<QrCodeGenerateResult> GenerateQrCodeAsync(string text, int size, string level, int margin);
    Task<QrCodeGenerateResult> GenerateCustomQrCodeAsync(string text, int size, string level, int margin, string foregroundColor, string backgroundColor);
    Task<QrCodeDecodeResult> DecodeQrCodeAsync(string imageBase64);
}
