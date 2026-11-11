using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>
/// JSONL 审计存储（CC Safety Net / aishield 风格：一行一事、只追加、可过滤回放）。
/// </summary>
public interface IAuditLogStore
{
    void Append(AuditEventDto evt);
    IReadOnlyList<AuditEventDto> Query(string? sessionId = null, string? kind = null, string? agentId = null, int take = 100, int skip = 0);
}

public sealed class AuditLogStore : IAuditLogStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _filePath;
    private readonly object _writeLock = new();
    private readonly ConcurrentQueue<AuditEventDto> _recent = new();
    private const int RecentCap = 2000;

    public AuditLogStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        var auditDir = Path.Combine(dataDirectory, "audit");
        Directory.CreateDirectory(auditDir);
        _filePath = Path.Combine(auditDir, $"smart-terminal-{DateTime.UtcNow:yyyyMM}.jsonl");
        WarmRecent();
    }

    public void Append(AuditEventDto evt)
    {
        var line = JsonSerializer.Serialize(evt, JsonOpts);
        lock (_writeLock)
        {
            File.AppendAllText(_filePath, line + "\n");
        }
        _recent.Enqueue(evt);
        while (_recent.Count > RecentCap && _recent.TryDequeue(out _)) { }
    }

    public IReadOnlyList<AuditEventDto> Query(string? sessionId = null, string? kind = null, string? agentId = null, int take = 100, int skip = 0)
    {
        take = Math.Clamp(take, 1, 500);
        skip = Math.Max(0, skip);
        var all = _recent.ToArray().Reverse();
        IEnumerable<AuditEventDto> q = all;
        if (!string.IsNullOrWhiteSpace(sessionId))
            q = q.Where(e => string.Equals(e.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(kind))
            q = q.Where(e => string.Equals(e.Kind, kind, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(agentId))
            q = q.Where(e => string.Equals(e.AgentId, agentId, StringComparison.OrdinalIgnoreCase));
        return q.Skip(skip).Take(take).ToList();
    }

    private void WarmRecent()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            foreach (var line in File.ReadLines(_filePath).Reverse().Take(RecentCap).Reverse())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var evt = JsonSerializer.Deserialize<AuditEventDto>(line, JsonOpts);
                    if (evt != null) _recent.Enqueue(evt);
                }
                catch
                {
                    // skip bad line
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SmartTerminal] 预热审计缓存失败: {0}", ex.Message);
        }
    }
}
