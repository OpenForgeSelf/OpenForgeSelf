namespace ForgeSelf.Abstractions;

/// <summary>
/// 高风险工具判定（P0 审批）：后端硬名单，不信前端工具列表。
/// 写/执行/破坏/外部副作用类工具挂 MAF 时须包成 <c>ApprovalRequiredAIFunction</c>。
/// </summary>
public static class HighRiskToolPolicy
{
    private static readonly HashSet<string> Required = new(StringComparer.OrdinalIgnoreCase)
    {
        "run_terminal_command",
        "write_file",
        "move_file",
        "rename_file",
        "delete_file",
        // 本机浏览器：点击可提交/支付/删除；下载只落盘不执行，名字仍进高风险名单。
        "browser_click",
        "browser_download",
    };

    /// <summary>风险等级字面量（API / 持久化）。</summary>
    public const string RiskHigh = "high";

    /// <summary>默认审批超时（fail-closed）。</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    /// <summary>是否必须人工审批后才能执行。</summary>
    public static bool RequiresApproval(string? toolName)
        => !string.IsNullOrWhiteSpace(toolName) && Required.Contains(toolName.Trim());

    /// <summary>只读枚举（测试/诊断）。</summary>
    public static IReadOnlyCollection<string> RequiredTools => Required;
}
