namespace ForgeSelf.Api.Plugins.MemorySystem.Data;

/// <summary>短期记忆条目（进程内，非持久化，按 TTL 过期）。</summary>
public class ShortTermMemoryItem
{
    public string SessionId { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

/// <summary>记忆类型。</summary>
public enum MemoryType
{
    Fact = 0,
    Preference = 1,
    Project = 2,
    Personal = 3,
    Workflow = 4,
    Skill = 5,
    Other = 99
}

/// <summary>记忆重要程度。</summary>
public enum MemoryImportance
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
