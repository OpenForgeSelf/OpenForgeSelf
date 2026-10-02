using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// Agent（工具/MCP）写开关（FR3）。文件 <c>{数据目录}/agent-access.json</c> 落插件数据目录，
/// 随数据走、重启后保持。只写不删：不提供删除该文件的能力（plugin-development 数据安全铁律 10）。
///
/// fail-closed：文件损坏（JSON 无法解析 / 缺 allowWrite / 类型不是布尔）一律按只读处理 ——
/// "没人写"和"不让写"必须在状态上能区分，否则一次手改坏文件就会静默放开写权限。
/// 每次 Get() 现读文件不缓存：REST PUT 立即生效、其他进程改后重启也保持。
/// </summary>
public sealed class AgentAccess
{
    readonly DesignSystemPaths _paths;

    public AgentAccess(DesignSystemPaths paths) => _paths = paths;

    /// <summary>读当前写开关。Source = default（文件不存在）/ file（来自文件）。</summary>
    public (Boolean AllowWrite, String Source, Boolean Corrupt, DateTime? UpdatedAt) Get()
    {
        var file = _paths.AgentAccessFile;
        if (!File.Exists(file)) return (true, "default", false, null);

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("allowWrite", out var aw)
                || (aw.ValueKind != JsonValueKind.True && aw.ValueKind != JsonValueKind.False))
                return (false, "file", true, null);

            DateTime? updated = null;
            if (root.TryGetProperty("updatedAt", out var ua) && ua.ValueKind == JsonValueKind.String
                && DateTime.TryParse(ua.GetString(), out var dt))
                updated = dt;
            return (aw.GetBoolean(), "file", false, updated);
        }
        catch (Exception)
        {
            return (false, "file", true, null);
        }
    }

    /// <summary>设置写开关：先写临时文件再原子替换，避免半截文件被当成"损坏→只读"。</summary>
    public void Set(Boolean allowWrite)
    {
        var file = _paths.AgentAccessFile;
        var dir = Path.GetDirectoryName(file);
        if (!String.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var payload = JsonSerializer.Serialize(new { allowWrite, updatedAt = DateTime.Now.ToString("o") });
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, payload);
        File.Move(tmp, file, true);
    }
}
