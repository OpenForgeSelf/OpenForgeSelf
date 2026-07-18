using OpenForgeSelf.Backend.Plugins.DevTools.Models;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public interface IJwtService
{
    Task<JwtDecodeResult> DecodeJwtAsync(string token);
    Task<JwtValidateResult> ValidateSignatureAsync(string token, string secret, string algorithm = "HS256");
    Task<bool> IsExpiredAsync(string token);
    Task<Dictionary<string, object>> GetClaimsAsync(string token);
    Task<JwtGenerateResult> GenerateJwtAsync(Dictionary<string, object> payload, string secret, string algorithm = "HS256", int? expiresInMinutes = null);
    Task<JwtHeaderInfo> GetHeaderInfoAsync(string token);
}
