using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenForgeSelf.Backend.Services.AI;

/// <summary>
/// 本地文件图片识别缓存：按会话 id 分文件夹，存储于 <c>&lt;root&gt;/&lt;sessionId&gt;/&lt;imageKey&gt;.json</c>。
/// 会话 id / 键做文件系统清洗（防路径穿越）；写入经临时文件 + 原子改名，避免并发读到半截数据；
/// 缓存损坏或读写异常按未命中处理，绝不影响主流程。
/// </summary>
public class LocalFileImageRecognitionCache : IImageRecognitionCache
{
    private readonly string _rootPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public LocalFileImageRecognitionCache(string rootPath)
    {
        _rootPath = rootPath;
    }

    public Task<string?> TryGetAsync(string sessionId, string imageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = GetCacheFilePath(sessionId, imageKey);
            if (!File.Exists(path))
                return Task.FromResult<string?>(null);

            var json = File.ReadAllText(path);
            var entry = JsonSerializer.Deserialize<CacheEntry>(json, JsonOptions);
            return Task.FromResult(entry?.Result);
        }
        catch (Exception ex)
        {
            // 缓存损坏 / 读取异常按未命中处理，不影响识别主流程
            NewLife.Log.XTrace.Log.Debug("读取图片识别缓存失败（按未命中处理）: {0}", ex.Message);
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetAsync(string sessionId, string imageKey, string result, CancellationToken cancellationToken = default)
    {
        var path = GetCacheFilePath(sessionId, imageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var entry = new CacheEntry
        {
            ImageKey = imageKey,
            Result = result,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 临时文件 + 原子改名：并发读写场景下避免读到写了一半的文件
        var tmpPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(tmpPath, JsonSerializer.Serialize(entry, JsonOptions));
        File.Move(tmpPath, path, overwrite: true);

        return Task.CompletedTask;
    }

    private string GetCacheFilePath(string sessionId, string imageKey)
    {
        var sessionDir = SanitizeFileName(sessionId, "default");
        var keyFile = SanitizeFileName(imageKey, "unknown") + ".json";
        return Path.Combine(_rootPath, sessionDir, keyFile);
    }

    /// <summary>
    /// 清洗为合法文件/文件夹名：非法字符替换为下划线、去首尾空白与结尾点号、截断长度。
    /// 防止会话 id 中的路径分隔符或 ".." 造成路径穿越。
    /// </summary>
    internal static string SanitizeFileName(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray();
        var name = new string(chars).Trim().TrimEnd('.');

        if (name.Length == 0)
            return fallback;
        if (name.Length > 64)
            name = name[..64];

        return name;
    }

    /// <summary>缓存条目（JSON 持久化结构）。</summary>
    private sealed class CacheEntry
    {
        [JsonPropertyName("imageKey")]
        public string ImageKey { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;

        [JsonPropertyName("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; }
    }
}
