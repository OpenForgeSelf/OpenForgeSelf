using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public class EncodingService : IEncodingService
{
    public Task<string> Base64EncodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] Base64编码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var result = Convert.ToBase64String(bytes);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] Base64编码失败: {0}", ex.Message);
            throw new ArgumentException("Base64编码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> Base64DecodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] Base64解码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var cleanText = text.Trim()
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace(" ", "");

            var bytes = Convert.FromBase64String(cleanText);
            var result = Encoding.UTF8.GetString(bytes);
            return Task.FromResult(result);
        }
        catch (FormatException ex)
        {
            XTrace.Log.Error("[DevTools] Base64解码失败: {0}", ex.Message);
            throw new ArgumentException("无效的Base64字符串: " + ex.Message, nameof(text), ex);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] Base64解码失败: {0}", ex.Message);
            throw new ArgumentException("Base64解码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> UrlEncodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] URL编码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var result = WebUtility.UrlEncode(text);
            return Task.FromResult(result ?? string.Empty);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] URL编码失败: {0}", ex.Message);
            throw new ArgumentException("URL编码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> UrlDecodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] URL解码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var result = WebUtility.UrlDecode(text);
            return Task.FromResult(result ?? string.Empty);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] URL解码失败: {0}", ex.Message);
            throw new ArgumentException("URL解码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> UnicodeEncodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] Unicode编码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var result = new StringBuilder();
            foreach (var c in text)
            {
                if (c < 128)
                {
                    result.Append(c);
                }
                else
                {
                    result.Append("\\u");
                    result.Append(((int)c).ToString("x4"));
                }
            }

            return Task.FromResult(result.ToString());
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] Unicode编码失败: {0}", ex.Message);
            throw new ArgumentException("Unicode编码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> UnicodeDecodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] Unicode解码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var result = Regex.Unescape(text);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] Unicode解码失败: {0}", ex.Message);
            throw new ArgumentException("Unicode解码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> HtmlEncodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] HTML实体编码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var result = WebUtility.HtmlEncode(text);
            return Task.FromResult(result ?? string.Empty);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] HTML实体编码失败: {0}", ex.Message);
            throw new ArgumentException("HTML实体编码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> HtmlDecodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] HTML实体解码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var result = WebUtility.HtmlDecode(text);
            return Task.FromResult(result ?? string.Empty);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] HTML实体解码失败: {0}", ex.Message);
            throw new ArgumentException("HTML实体解码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> HexEncodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 十六进制编码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var result = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 十六进制编码失败: {0}", ex.Message);
            throw new ArgumentException("十六进制编码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> HexDecodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 十六进制解码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var cleanText = text.Trim().Replace(" ", "").Replace("-", "");
            if (cleanText.Length % 2 != 0)
                throw new ArgumentException("无效的十六进制字符串，长度必须为偶数");

            var bytes = new byte[cleanText.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(cleanText.Substring(i * 2, 2), 16);
            }

            var result = Encoding.UTF8.GetString(bytes);
            return Task.FromResult(result);
        }
        catch (FormatException ex)
        {
            XTrace.Log.Error("[DevTools] 十六进制解码失败: {0}", ex.Message);
            throw new ArgumentException("无效的十六进制字符串: " + ex.Message, nameof(text), ex);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 十六进制解码失败: {0}", ex.Message);
            throw new ArgumentException("十六进制解码失败: " + ex.Message, nameof(text), ex);
        }
    }
}
