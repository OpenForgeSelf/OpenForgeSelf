namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 下发阶段状态机（PILOT-054 · BR-4）。整张流转图的唯一真源，控制器/服务/工具/前端都据它判定，
/// 不在别处再写一份「谁能到谁」。
///
/// <code>
/// 0 Draft ──▶ 1 Ready ──▶ 2 Dispatched ──▶ 3 Running ──▶ 5 Review ──▶ 6 Done
///                  │              │              │            │
///                  │              ▼              ▼            ▼(打回)
///                  └──────────▶ 7 Cancelled ◀── 4 Blocked ────┘
/// </code>
///
/// 与旧 <see cref="TodoStatus"/>（0=Pending/1=Completed，Home 面板与既有 e2e 在用）的关系：
/// <c>Status</c> 由 <c>Stage</c> 派生（Stage&gt;=Done ⇒ Completed），<b>不改旧语义</b>；
/// <c>complete</c>=置 Done、<c>reopen</c>=回 Draft，两个旧端点内部同步两者。
/// </summary>
public static class TodoStage
{
    /// <summary>草稿：信息还不齐，不能下发。</summary>
    public const int Draft = 0;

    /// <summary>就绪：必填齐备，等待下发。</summary>
    public const int Ready = 1;

    /// <summary>已下发：任务已交付给执行方（人或 agent），等待领取。</summary>
    public const int Dispatched = 2;

    /// <summary>执行中。</summary>
    public const int Running = 3;

    /// <summary>阻塞：必须带 <c>BlockReason</c>。</summary>
    public const int Blocked = 4;

    /// <summary>待验收：执行方认为做完了，等人/审查确认。</summary>
    public const int Review = 5;

    /// <summary>完成（终态，可重开）。</summary>
    public const int Done = 6;

    /// <summary>取消（终态，可重开）。</summary>
    public const int Cancelled = 7;

    /// <summary>全部取值。</summary>
    public static readonly int[] All = [Draft, Ready, Dispatched, Running, Blocked, Review, Done, Cancelled];

    private static readonly Dictionary<int, string> Names = new()
    {
        [Draft] = "Draft",
        [Ready] = "Ready",
        [Dispatched] = "Dispatched",
        [Running] = "Running",
        [Blocked] = "Blocked",
        [Review] = "Review",
        [Done] = "Done",
        [Cancelled] = "Cancelled"
    };

    private static readonly Dictionary<int, string> Labels = new()
    {
        [Draft] = "草稿",
        [Ready] = "就绪",
        [Dispatched] = "已下发",
        [Running] = "执行中",
        [Blocked] = "阻塞",
        [Review] = "待验收",
        [Done] = "完成",
        [Cancelled] = "已取消"
    };

    /// <summary>
    /// 合法流转边。集合外一律非法（含「跳过 Dispatched 直接 Running」——下发这一步必须留痕）。
    /// 同态（from==to）视为幂等放行，由调用方自己决定要不要落记录。
    /// </summary>
    private static readonly Dictionary<int, int[]> Edges = new()
    {
        [Draft] = [Ready],
        [Ready] = [Dispatched, Cancelled],
        [Dispatched] = [Running, Blocked, Ready, Cancelled],
        [Running] = [Review, Blocked, Done, Cancelled],
        [Blocked] = [Ready, Running, Cancelled],
        [Review] = [Done, Running],
        [Done] = [Running, Review],
        [Cancelled] = [Running, Review]
    };

    /// <summary>数值 → 英文键（未知值回落 Draft，与旧 <see cref="TodoStatus.ToName"/> 的宽容口径一致）。</summary>
    public static string ToName(int value) => Names.TryGetValue(value, out var n) ? n : Names[Draft];

    /// <summary>数值 → 中文标签（界面展示用）。</summary>
    public static string ToLabel(int value) => Labels.TryGetValue(value, out var l) ? l : Labels[Draft];

    /// <summary>
    /// 解析入参：接受英文键（大小写不敏感）或数字字符串（"0".."7"）。
    /// <b>显式空串是客户端错误</b>（返回 false），"未传"由调用方自己判 null 后给默认值。
    /// </summary>
    public static bool TryParse(string? text, out int value)
    {
        value = -1;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var key = text.Trim();
        foreach (var pair in Names)
        {
            if (string.Equals(pair.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Key;
                return true;
            }
        }

        if (int.TryParse(key, out var num) && All.Contains(num))
        {
            value = num;
            return true;
        }

        return false;
    }

    /// <summary>某状态可达的下一状态集合（未知值返回空集）。</summary>
    public static IReadOnlyList<int> NextOf(int from) =>
        Edges.TryGetValue(from, out var to) ? to : Array.Empty<int>();

    /// <summary>是否允许 from → to（同态视为幂等放行）。</summary>
    public static bool CanTransit(int from, int to) =>
        from == to || (Edges.TryGetValue(from, out var targets) && targets.Contains(to));

    /// <summary>非法流转时给用户的说明：列出该状态可达目标（英文名 + 中文标签）。</summary>
    public static string DescribeAllowed(int from)
    {
        var targets = NextOf(from);
        if (targets.Count == 0) return $"「{ToName(from)}（{ToLabel(from)}）」没有可达状态";
        var list = targets.Select(t => $"{ToName(t)}（{ToLabel(t)}）");
        return $"「{ToName(from)}（{ToLabel(from)}）」只能流转到：{string.Join("、", list)}";
    }

    /// <summary>是否终态（完成/取消）。终态仍可被重开，只是默认不再出现在待办视角。</summary>
    public static bool IsTerminal(int value) => value == Done || value == Cancelled;

    /// <summary>派生旧的二元状态（0=Pending/1=Completed），保持 Home 面板与旧 e2e 语义不变。</summary>
    public static int ToLegacyStatus(int stage) => stage == Done ? TodoStatus.CompletedValue : TodoStatus.PendingValue;
}
