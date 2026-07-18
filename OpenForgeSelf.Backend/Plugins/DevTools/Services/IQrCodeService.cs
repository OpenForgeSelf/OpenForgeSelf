using OpenForgeSelf.Backend.Plugins.DevTools.Models;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public interface IQrCodeService
{
    Task<QrCodeGenerateResult> GenerateQrCodeAsync(string text, int size, string level, int margin);
    Task<QrCodeGenerateResult> GenerateCustomQrCodeAsync(string text, int size, string level, int margin, string foregroundColor, string backgroundColor);
    Task<QrCodeDecodeResult> DecodeQrCodeAsync(string imageBase64);
}
