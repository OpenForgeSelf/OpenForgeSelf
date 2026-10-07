namespace ForgeSelf.Api.Plugins.ToolBridge.Models;

/// <summary>一条被识别出来的工具调用（PILOT-053 02-spec FR-2）。</summary>
public sealed class ParsedCall
{
    /// <summary>归一后的规范工具名（read_file / write_file / list_dir / run_command）。</summary>
    public string Tool { get; init; } = string.Empty;

    /// <summary>AI 原文里写的名字（回粘时保留，便于观察模型的措辞）。</summary>
    public string RawName { get; init; } = string.Empty;

    /// <summary>参数（键为 schema 里的参数名；camelCase 与 PascalCase 均已归一到 schema 名）。</summary>
    public Dictionary<string, JsonNode> Args { get; init; } = new();

    /// <summary>命中的格式档位：json / openai / tag / kv / arrow（02-spec FR-2.2）。</summary>
    public string Via { get; init; } = string.Empty;

    /// <summary>该调用在原文里的起始位置（界面定位用）。</summary>
    public int Start { get; init; }

    /// <summary>原文片段（折叠展示与回看用）。</summary>
    public string Fragment { get; init; } = string.Empty;

    public string? GetArgString(string key) =>
        Args.TryGetValue(key, out var v) && v != null ? v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : v.ToJsonString() : null;

    public int? GetArgInt(string key)
    {
        if (!Args.TryGetValue(key, out var v) || v == null) return null;
        try { return v.GetValue<int>(); }
        catch { return null; }
    }
}

/// <summary>认得出"这是一次调用"但工具名不在清单里（绝不就近执行，02-spec BR-1）。</summary>
public sealed class UnknownCall
{
    public string RawName { get; init; } = string.Empty;
    public string Fragment { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;

    /// <summary>清单里编辑距离最小的候选（仅提示，不代跑）。</summary>
    public string? Suggestion { get; init; }
}

/// <summary>既未识别、也无法判定为调用的原文片段，带具体原因（AC4）。</summary>
public sealed class UnparsedFragment
{
    public string Fragment { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class ParseStats
{
    public int Recognized { get; init; }
    public int Unknown { get; init; }
    public int Unparsed { get; init; }
}

/// <summary>解析结果三段 + 统计（FR-2.4）。</summary>
public sealed class ParseResult
{
    public List<ParsedCall> Calls { get; } = new();
    public List<UnknownCall> Unknown { get; } = new();
    public List<UnparsedFragment> Unparsed { get; } = new();
    public ParseStats Stats { get; set; } = new();
}
