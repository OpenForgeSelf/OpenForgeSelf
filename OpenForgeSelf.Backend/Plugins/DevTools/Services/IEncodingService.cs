namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public interface IEncodingService
{
    Task<string> Base64EncodeAsync(string text);
    Task<string> Base64DecodeAsync(string text);
    Task<string> UrlEncodeAsync(string text);
    Task<string> UrlDecodeAsync(string text);
    Task<string> UnicodeEncodeAsync(string text);
    Task<string> UnicodeDecodeAsync(string text);
    Task<string> HtmlEncodeAsync(string text);
    Task<string> HtmlDecodeAsync(string text);
    Task<string> HexEncodeAsync(string text);
    Task<string> HexDecodeAsync(string text);
}
