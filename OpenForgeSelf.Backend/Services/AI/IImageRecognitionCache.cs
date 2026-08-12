using System.Security.Cryptography;
using System.Text;

namespace OpenForgeSelf.Backend.Services.AI;

/// <summary>
/// 图片识别结果缓存：同一会话内相同图片首次识别后缓存结果，后续请求直接复用。
/// 缓存按会话 id 分区（分文件夹本地存储）。
/// </summary>
public interface IImageRecognitionCache
{
    /// <summary>尝试读取缓存的识别结果；未命中或缓存损坏返回 null。</summary>
    Task<string?> TryGetAsync(string sessionId, string imageKey, CancellationToken cancellationToken = default);

    /// <summary>写入识别结果缓存。</summary>
    Task SetAsync(string sessionId, string imageKey, string result, CancellationToken cancellationToken = default);
}

/// <summary>
/// 图片识别缓存键计算：同一图片（同来源内容）+ 同一视觉模型 → 同一键。
/// </summary>
public static class ImageRecognitionCacheKey
{
    /// <summary>
    /// 根据图片内容与视觉模型 id 计算缓存键（SHA256 十六进制小写）。
    /// url 图片以 URL 文本为内容标识；base64 图片以编码数据为内容标识。
    /// </summary>
    public static string Compute(ImageInfo image, string? visionModel = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        var canonical = image.Source == "base64"
            ? $"{visionModel ?? string.Empty}|base64|{image.MediaType ?? string.Empty}|{image.Base64Data ?? string.Empty}"
            : $"{visionModel ?? string.Empty}|url|{image.Detail ?? string.Empty}|{image.Url ?? string.Empty}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
