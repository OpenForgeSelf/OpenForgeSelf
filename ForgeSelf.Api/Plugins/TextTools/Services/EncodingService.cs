using System.Net;
using System.Text;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TextTools.Services;

public class EncodingService : IEncodingService
{
    public Task<string> Base64EncodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("Base64编码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var bytes = Encoding.UTF8.GetBytes(text);
            var result = Convert.ToBase64String(bytes);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("Base64编码失败: {0}", ex.Message);
            throw new ArgumentException("Base64编码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> Base64DecodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("Base64解码");

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
            XTrace.Log.Error("Base64解码失败: {0}", ex.Message);
            throw new ArgumentException("无效的Base64字符串: " + ex.Message, nameof(text), ex);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("Base64解码失败: {0}", ex.Message);
            throw new ArgumentException("Base64解码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> UrlEncodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("URL编码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var result = WebUtility.UrlEncode(text);
            return Task.FromResult(result ?? string.Empty);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("URL编码失败: {0}", ex.Message);
            throw new ArgumentException("URL编码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> UrlDecodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("URL解码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var result = WebUtility.UrlDecode(text);
            return Task.FromResult(result ?? string.Empty);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("URL解码失败: {0}", ex.Message);
            throw new ArgumentException("URL解码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> UnicodeEncodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("Unicode编码");

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
            XTrace.Log.Error("Unicode编码失败: {0}", ex.Message);
            throw new ArgumentException("Unicode编码失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> UnicodeDecodeAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("Unicode解码");

            if (string.IsNullOrEmpty(text))
                return Task.FromResult(string.Empty);

            var result = new StringBuilder();
            var i = 0;

            while (i < text.Length)
            {
                if (text[i] == '\\' && i + 5 < text.Length && text[i + 1] == 'u')
                {
                    var hex = text.Substring(i + 2, 4);
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code))
                    {
                        result.Append((char)code);
                        i += 6;
                        continue;
                    }
                }

                result.Append(text[i]);
                i++;
            }

            return Task.FromResult(result.ToString());
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("Unicode解码失败: {0}", ex.Message);
            throw new ArgumentException("Unicode解码失败: " + ex.Message, nameof(text), ex);
        }
    }
}
