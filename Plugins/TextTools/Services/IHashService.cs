namespace ForgeSelf.Api.Plugins.TextTools.Services;

public interface IHashService
{
    Task<string> ComputeMD5Async(string text);
    Task<string> ComputeSHA1Async(string text);
    Task<string> ComputeSHA256Async(string text);
    Task<string> ComputeSHA512Async(string text);
    Task<string> ComputeFileMD5Async(Stream stream);
}
