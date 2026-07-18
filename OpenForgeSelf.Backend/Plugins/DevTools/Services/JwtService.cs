using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.DevTools.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public class JwtService : IJwtService
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public Task<JwtDecodeResult> DecodeJwtAsync(string token)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 解析JWT Token");

            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("JWT Token 不能为空");

            var parts = token.Split('.');
            if (parts.Length != 3)
                throw new ArgumentException("无效的 JWT 格式，应为 header.payload.signature");

            var headerJson = Base64UrlDecode(parts[0]);
            var payloadJson = Base64UrlDecode(parts[1]);
            var signature = parts[2];

            var header = JsonSerializer.Deserialize<Dictionary<string, object>>(headerJson)
                ?? new Dictionary<string, object>();
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson)
                ?? new Dictionary<string, object>();

            DateTime? issuedAt = null;
            DateTime? expiration = null;
            bool isExpired = false;
            string? timeRemaining = null;

            if (payload.TryGetValue("iat", out var iatObj) && iatObj is JsonElement iatElem && iatElem.TryGetInt64(out var iatVal))
            {
                issuedAt = UnixEpoch.AddSeconds(iatVal);
            }

            if (payload.TryGetValue("exp", out var expObj) && expObj is JsonElement expElem && expElem.TryGetInt64(out var expVal))
            {
                expiration = UnixEpoch.AddSeconds(expVal);
                var now = DateTime.UtcNow;
                isExpired = now > expiration;

                var diff = expiration.Value - now;
                if (isExpired)
                {
                    timeRemaining = $"已过期 {FormatDuration(-diff)}";
                }
                else
                {
                    timeRemaining = $"还有 {FormatDuration(diff)} 过期";
                }
            }

            return Task.FromResult(new JwtDecodeResult
            {
                Success = true,
                Header = header,
                Payload = payload,
                Signature = signature,
                IsExpired = isExpired,
                IssuedAt = issuedAt,
                Expiration = expiration,
                TimeRemaining = timeRemaining
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JWT解析失败: {0}", ex.Message);
            return Task.FromResult(new JwtDecodeResult
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    public Task<JwtValidateResult> ValidateSignatureAsync(string token, string secret, string algorithm = "HS256")
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 验证JWT签名，算法: {0}", algorithm);

            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("JWT Token 不能为空");
            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("密钥不能为空");

            var parts = token.Split('.');
            if (parts.Length != 3)
                throw new ArgumentException("无效的 JWT 格式");

            var headerPayload = parts[0] + "." + parts[1];
            var signature = parts[2];

            var computedSignature = ComputeSignature(headerPayload, secret, algorithm);
            var isValid = signature == computedSignature;

            return Task.FromResult(new JwtValidateResult
            {
                IsValid = isValid,
                ErrorMessage = isValid ? null : "签名验证失败"
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JWT签名验证失败: {0}", ex.Message);
            return Task.FromResult(new JwtValidateResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            });
        }
    }

    public async Task<bool> IsExpiredAsync(string token)
    {
        var result = await DecodeJwtAsync(token);
        return result.IsExpired;
    }

    public async Task<Dictionary<string, object>> GetClaimsAsync(string token)
    {
        var result = await DecodeJwtAsync(token);
        return result.Payload ?? new Dictionary<string, object>();
    }

    public Task<JwtGenerateResult> GenerateJwtAsync(Dictionary<string, object> payload, string secret, string algorithm = "HS256", int? expiresInMinutes = null)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 生成JWT，算法: {0}", algorithm);

            if (payload == null)
                throw new ArgumentException("Payload 不能为空");
            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("密钥不能为空");

            var now = DateTime.UtcNow;
            var iat = (long)(now - UnixEpoch).TotalSeconds;

            var payloadCopy = new Dictionary<string, object>(payload);
            payloadCopy["iat"] = iat;

            DateTime? expiration = null;
            if (expiresInMinutes.HasValue)
            {
                var exp = iat + expiresInMinutes.Value * 60;
                payloadCopy["exp"] = exp;
                expiration = UnixEpoch.AddSeconds(exp);
            }

            var header = new Dictionary<string, object>
            {
                ["alg"] = algorithm,
                ["typ"] = "JWT"
            };

            var headerJson = JsonSerializer.Serialize(header);
            var payloadJson = JsonSerializer.Serialize(payloadCopy);

            var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

            var headerPayload = headerBase64 + "." + payloadBase64;
            var signature = ComputeSignature(headerPayload, secret, algorithm);

            var token = headerPayload + "." + signature;

            return Task.FromResult(new JwtGenerateResult
            {
                Token = token,
                IssuedAt = now,
                Expiration = expiration
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] JWT生成失败: {0}", ex.Message);
            throw new ArgumentException("JWT生成失败: " + ex.Message, nameof(payload), ex);
        }
    }

    public async Task<JwtHeaderInfo> GetHeaderInfoAsync(string token)
    {
        var result = await DecodeJwtAsync(token);
        var header = result.Header ?? new Dictionary<string, object>();

        return new JwtHeaderInfo
        {
            Alg = header.TryGetValue("alg", out var alg) ? alg.ToString() ?? "HS256" : "HS256",
            Typ = header.TryGetValue("typ", out var typ) ? typ.ToString() ?? "JWT" : "JWT"
        };
    }

    private static string ComputeSignature(string input, string secret, string algorithm)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var inputBytes = Encoding.UTF8.GetBytes(input);

        byte[] hashBytes = algorithm.ToUpperInvariant() switch
        {
            "HS256" => HMACSHA256.HashData(keyBytes, inputBytes),
            "HS384" => HMACSHA384.HashData(keyBytes, inputBytes),
            "HS512" => HMACSHA512.HashData(keyBytes, inputBytes),
            _ => throw new ArgumentException($"不支持的算法: {algorithm}，支持 HS256, HS384, HS512")
        };

        return Base64UrlEncode(hashBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string Base64UrlDecode(string input)
    {
        var base64 = input
            .Replace('-', '+')
            .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}天{span.Hours}小时";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}小时{span.Minutes}分钟";
        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}分钟{span.Seconds}秒";
        return $"{span.Seconds}秒";
    }
}
