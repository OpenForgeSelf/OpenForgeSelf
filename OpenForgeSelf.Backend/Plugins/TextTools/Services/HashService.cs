using System.Security.Cryptography;
using System.Text;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.TextTools.Services;

public class HashService : IHashService
{
    public Task<string> ComputeMD5Async(string text)
    {
        try
        {
            XTrace.Log.Debug("计算MD5哈希");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = MD5.HashData(bytes);
            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("MD5计算失败: {0}", ex.Message);
            throw new ArgumentException("MD5计算失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> ComputeSHA1Async(string text)
    {
        try
        {
            XTrace.Log.Debug("计算SHA1哈希");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = SHA1.HashData(bytes);
            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("SHA1计算失败: {0}", ex.Message);
            throw new ArgumentException("SHA1计算失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> ComputeSHA256Async(string text)
    {
        try
        {
            XTrace.Log.Debug("计算SHA256哈希");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = SHA256.HashData(bytes);
            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("SHA256计算失败: {0}", ex.Message);
            throw new ArgumentException("SHA256计算失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> ComputeSHA512Async(string text)
    {
        try
        {
            XTrace.Log.Debug("计算SHA512哈希");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = SHA512.HashData(bytes);
            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("SHA512计算失败: {0}", ex.Message);
            throw new ArgumentException("SHA512计算失败: " + ex.Message, nameof(text), ex);
        }
    }

    public async Task<string> ComputeFileMD5Async(Stream stream)
    {
        try
        {
            XTrace.Log.Debug("计算文件MD5哈希");

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            stream.Position = 0;
            var hashBytes = await MD5.HashDataAsync(stream);
            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return result;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("文件MD5计算失败: {0}", ex.Message);
            throw new ArgumentException("文件MD5计算失败: " + ex.Message, nameof(stream), ex);
        }
    }
}
