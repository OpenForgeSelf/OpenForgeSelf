using System.Collections.Concurrent;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>
/// 权限模式：全局默认（持久化到插件数据目录 <c>permission-mode.txt</c>，缺省 AskWrite）+ Agent 会话级覆盖（仅内存）。
/// </summary>
public sealed class PermissionModeStore
{
    private readonly String? _file;
    private String _global = PermissionModes.AskWrite;
    private readonly ConcurrentDictionary<String, String> _sessions = new(StringComparer.Ordinal);

    public PermissionModeStore(String? dataDirectory)
    {
        if (String.IsNullOrWhiteSpace(dataDirectory)) return;
        _file = Path.Combine(dataDirectory, "permission-mode.txt");
        try
        {
            if (File.Exists(_file)) _global = PermissionModes.Normalize(File.ReadAllText(_file).Trim()) ?? PermissionModes.AskWrite;
        }
        catch (Exception ex) { XTrace.Log.Warn("[DbAdmin] 读取权限模式失败，使用 AskWrite: {0}", ex.Message); }
    }

    public String Global => _global;

    public void SetGlobal(String mode)
    {
        _global = PermissionModes.Normalize(mode) ?? throw new DbAdminException(DbErrorCodes.InvalidRequest, "mode 只能是 AskWrite 或 Auto");
        if (_file == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, _global);
        }
        catch (Exception ex) { XTrace.Log.Warn("[DbAdmin] 保存权限模式失败: {0}", ex.Message); }
    }

    /// <summary>会话级（Agent）模式；不改全局默认</summary>
    public void SetSession(String sessionId, String mode)
    {
        if (String.IsNullOrWhiteSpace(sessionId)) throw new DbAdminException(DbErrorCodes.InvalidRequest, "sessionId 不能为空");
        _sessions[sessionId] = PermissionModes.Normalize(mode) ?? throw new DbAdminException(DbErrorCodes.InvalidRequest, "mode 只能是 AskWrite 或 Auto");
    }

    public String Effective(QueryContext ctx) =>
        ctx.SessionId != null && _sessions.TryGetValue(ctx.SessionId, out var m) ? m : _global;
}
