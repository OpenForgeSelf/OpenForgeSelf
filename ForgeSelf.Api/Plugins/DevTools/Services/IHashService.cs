using ForgeSelf.Api.Plugins.DevTools.Models;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public interface IHashService
{
    Task<string> ComputeMd5Async(string text);
    Task<string> ComputeSha1Async(string text);
    Task<string> ComputeSha256Async(string text);
    Task<string> ComputeSha512Async(string text);
    Task<HashAllResult> ComputeAllHashesAsync(string text);
    Task<string> ComputeHmacAsync(string text, string key, string algorithm);
    Task<string> AesEncryptAsync(string text, string key, string? iv = null);
    Task<string> AesDecryptAsync(string text, string key, string? iv = null);
}
