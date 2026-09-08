namespace ForgeSelf.Api.Plugins.MemorySystem.Data;

/// <summary>短期记忆条目（进程内，非持久化，按 TTL 过期）。</summary>
public class ShortTermMemoryItem
{
    public string SessionId { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
