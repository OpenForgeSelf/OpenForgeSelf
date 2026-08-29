namespace ForgeSelf.Api.Plugins.TextTools.Services;

public interface IEncodingService
{
    Task<string> Base64EncodeAsync(string text);
    Task<string> Base64DecodeAsync(string text);
    Task<string> UrlEncodeAsync(string text);
    Task<string> UrlDecodeAsync(string text);
    Task<string> UnicodeEncodeAsync(string text);
    Task<string> UnicodeDecodeAsync(string text);
}
