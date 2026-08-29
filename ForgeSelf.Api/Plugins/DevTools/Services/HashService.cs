using System.Security.Cryptography;
using System.Text;
using ForgeSelf.Api.Plugins.DevTools.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public class HashService : IHashService
{
    public Task<string> ComputeMd5Async(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 计算MD5哈希");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = MD5.HashData(bytes);
            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] MD5计算失败: {0}", ex.Message);
            throw new ArgumentException("MD5计算失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> ComputeSha1Async(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 计算SHA1哈希");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = SHA1.HashData(bytes);
            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] SHA1计算失败: {0}", ex.Message);
            throw new ArgumentException("SHA1计算失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> ComputeSha256Async(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 计算SHA256哈希");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = SHA256.HashData(bytes);
            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] SHA256计算失败: {0}", ex.Message);
            throw new ArgumentException("SHA256计算失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> ComputeSha512Async(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 计算SHA512哈希");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = SHA512.HashData(bytes);
            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] SHA512计算失败: {0}", ex.Message);
            throw new ArgumentException("SHA512计算失败: " + ex.Message, nameof(text), ex);
        }
    }

    public async Task<HashAllResult> ComputeAllHashesAsync(string text)
    {
        XTrace.Log.Debug("[DevTools] 计算所有哈希");

        var md5Task = ComputeMd5Async(text);
        var sha1Task = ComputeSha1Async(text);
        var sha256Task = ComputeSha256Async(text);
        var sha512Task = ComputeSha512Async(text);

        await Task.WhenAll(md5Task, sha1Task, sha256Task, sha512Task);

        return new HashAllResult
        {
            Md5 = md5Task.Result,
            Sha1 = sha1Task.Result,
            Sha256 = sha256Task.Result,
            Sha512 = sha512Task.Result
        };
    }

    public Task<string> ComputeHmacAsync(string text, string key, string algorithm)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 计算HMAC，算法: {0}", algorithm);

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("密钥不能为空", nameof(key));

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var textBytes = Encoding.UTF8.GetBytes(text);
            byte[] hashBytes;

            switch (algorithm.ToLowerInvariant())
            {
                case "md5":
                    hashBytes = HMACMD5.HashData(keyBytes, textBytes);
                    break;
                case "sha1":
                    hashBytes = HMACSHA1.HashData(keyBytes, textBytes);
                    break;
                case "sha256":
                default:
                    hashBytes = HMACSHA256.HashData(keyBytes, textBytes);
                    break;
                case "sha384":
                    hashBytes = HMACSHA384.HashData(keyBytes, textBytes);
                    break;
                case "sha512":
                    hashBytes = HMACSHA512.HashData(keyBytes, textBytes);
                    break;
            }

            var result = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] HMAC计算失败: {0}", ex.Message);
            throw new ArgumentException("HMAC计算失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> AesEncryptAsync(string text, string key, string? iv = null)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] AES加密");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("密钥不能为空", nameof(key));

            using var aes = Aes.Create();
            var keyBytes = DeriveKey(key, aes.KeySize / 8);
            aes.Key = keyBytes;

            byte[] ivBytes;
            if (!string.IsNullOrEmpty(iv))
            {
                ivBytes = DeriveKey(iv, aes.BlockSize / 8);
            }
            else
            {
                aes.GenerateIV();
                ivBytes = aes.IV;
            }
            aes.IV = ivBytes;

            var plainBytes = Encoding.UTF8.GetBytes(text);
            var cipherBytes = aes.EncryptCbc(plainBytes, aes.IV);

            var resultBytes = new byte[ivBytes.Length + cipherBytes.Length];
            Buffer.BlockCopy(ivBytes, 0, resultBytes, 0, ivBytes.Length);
            Buffer.BlockCopy(cipherBytes, 0, resultBytes, ivBytes.Length, cipherBytes.Length);

            var result = Convert.ToBase64String(resultBytes);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] AES加密失败: {0}", ex.Message);
            throw new ArgumentException("AES加密失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> AesDecryptAsync(string text, string key, string? iv = null)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] AES解密");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("密钥不能为空", nameof(key));

            using var aes = Aes.Create();
            var keyBytes = DeriveKey(key, aes.KeySize / 8);
            aes.Key = keyBytes;

            var cipherBytesWithIv = Convert.FromBase64String(text);

            byte[] ivBytes;
            byte[] cipherBytes;

            if (!string.IsNullOrEmpty(iv))
            {
                ivBytes = DeriveKey(iv, aes.BlockSize / 8);
                cipherBytes = cipherBytesWithIv;
            }
            else
            {
                ivBytes = new byte[aes.BlockSize / 8];
                cipherBytes = new byte[cipherBytesWithIv.Length - ivBytes.Length];
                Buffer.BlockCopy(cipherBytesWithIv, 0, ivBytes, 0, ivBytes.Length);
                Buffer.BlockCopy(cipherBytesWithIv, ivBytes.Length, cipherBytes, 0, cipherBytes.Length);
            }
            aes.IV = ivBytes;

            var plainBytes = aes.DecryptCbc(cipherBytes, aes.IV);
            var result = Encoding.UTF8.GetString(plainBytes);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] AES解密失败: {0}", ex.Message);
            throw new ArgumentException("AES解密失败: " + ex.Message, nameof(text), ex);
        }
    }

    private static byte[] DeriveKey(string password, int keyLength)
    {
        using var rfc2898 = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes("ForgeSelf.DevTools"), 1000, HashAlgorithmName.SHA256);
        return rfc2898.GetBytes(keyLength);
    }
}
