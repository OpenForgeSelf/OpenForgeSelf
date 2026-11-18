namespace ForgeSelf.Api.Plugins.AIAgent.Models;

/// <summary>
/// 智能体会话的默认工具范围：未传 toolScope 时挂本插件 + 记忆/设计/智能终端，
/// 并在 pluginId=ai-agent 时额外挂 list_files / write_file（write_file 仍走审批）。
/// 不挂全部宿主工具，避免撑爆本地小模型上下文。
/// </summary>
public static class AgentDefaultToolScope
{
    public const string AgentPluginId = "ai-agent";

    public static readonly string[] ExtraFileTools = ["list_files", "write_file"];

    public static IReadOnlyCollection<string> PluginIds(string ownPluginId) =>
        [ownPluginId, "memory-system", "design-system", "smart-terminal"];

    public static bool IncludesExtraFileTools(string? pluginId) =>
        string.Equals(pluginId, AgentPluginId, StringComparison.OrdinalIgnoreCase);

    public static bool IsExtraFileTool(string? pluginId, string? toolName) =>
        IncludesExtraFileTools(pluginId)
        && !string.IsNullOrWhiteSpace(toolName)
        && ExtraFileTools.Contains(toolName, StringComparer.OrdinalIgnoreCase);

    public static bool IsSpecialLoopTool(string? name) =>
        name is "submit_plan" or "complete_step" or "request_help" or "task_complete";

    /// <summary>未传 toolScope 时的可见工具名。无注册表时，ai-agent 至少返回 list_files / write_file。</summary>
    public static IReadOnlyList<string> ResolveNames(ForgeSelf.Abstractions.IToolRegistry? registry, string pluginId)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (IncludesExtraFileTools(pluginId))
        {
            foreach (var name in ExtraFileTools)
                names.Add(name);
        }

        if (registry == null)
            return names.ToList();

        var allowed = new HashSet<string>(PluginIds(pluginId), StringComparer.OrdinalIgnoreCase);
        foreach (var tool in registry.GetAllTools())
        {
            if (IsSpecialLoopTool(tool.Name)) continue;
            if (allowed.Contains(tool.PluginId) || IsExtraFileTool(pluginId, tool.Name))
                names.Add(tool.Name);
        }

        return names.ToList();
    }
}
