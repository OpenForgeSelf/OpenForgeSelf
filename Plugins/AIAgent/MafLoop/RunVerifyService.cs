using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.MafLoop;

/// <summary>Run 级 Verify 校验器接缝（B2②）：两台引擎（计划任务编排器 / 开放任务回合）共用。</summary>
public interface IRunVerifyService
{
    /// <summary>对出口判据逐条做确定性校验，返回整体结论（零 LLM、零命令执行）。</summary>
    Task<RunVerifyOutcome> VerifyAsync(IReadOnlyList<RunExitCriterion> criteria, RunVerifyInputs inputs, CancellationToken ct = default);
}

/// <summary>
/// Run 级 Verify 闸门实现（B2②，战役批 B2 FR-2）。
/// </summary>
/// <remarks>
/// <para>
/// <b>确定性断言 V1</b>（零 LLM；命令执行类断言必须过 ToolRegistry 六道闸，本批不碰——见 02-spec 边界）：
/// <list type="bullet">
/// <item><c>file_exists</c>：相对路径经 <see cref="IProjectWorkspaceService.ResolveSafePath"/> 越界防护后
/// <see cref="File.Exists"/>；绝对路径必须在工作区根之下（越界 = 判据不通过，带原因）。</item>
/// <item><c>output_contains</c>：断言字面量（<see cref="RunExitCriterion.Value"/>，缺省回退 Text）是产出摘要的子串（OrdinalIgnoreCase）。</item>
/// <item><c>files_changed_not_empty</c>：本次改动清单非空。</item>
/// </list>
/// </para>
/// <para>
/// <b>fail-safe（BR-2）</b>：未知断言类型 = 该判据不通过并写明原因；工作区缺席时 file_exists 不通过；
/// 单条校验异常 = 不通过。绝不静默放行——配置了出口验收的 Run 不允许在未验证时置 Completed。
/// </para>
/// </remarks>
public sealed class RunVerifyService : IRunVerifyService
{
    public const string TypeFileExists = "file_exists";
    public const string TypeOutputContains = "output_contains";
    public const string TypeFilesChangedNotEmpty = "files_changed_not_empty";

    private readonly IProjectWorkspaceService? _workspace;

    public RunVerifyService(IProjectWorkspaceService? workspace) => _workspace = workspace;

    /// <summary>校验器缺席时的 fail-safe 结论：配置了判据却无法校验 = 未通过，绝不假绿（BR-2）。</summary>
    public static RunVerifyOutcome VerifierAbsent(int criterionCount) => new(
        RunVerifyStatus.Failed,
        [new RunVerifyItem($"校验器缺席（{criterionCount} 条判据未校验）", "verifier_absent", false,
            "Verify 校验器未接线：配置了出口验收标准的 Run 不得在无校验时放行")]);

    /// <inheritdoc />
    public async Task<RunVerifyOutcome> VerifyAsync(IReadOnlyList<RunExitCriterion> criteria, RunVerifyInputs inputs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(inputs);

        var items = new List<RunVerifyItem>(criteria.Count);
        foreach (var criterion in criteria)
        {
            if (ct.IsCancellationRequested) break;
            items.Add(await VerifyOneAsync(criterion, inputs, ct).ConfigureAwait(false));
        }

        var status = items.Count > 0 && items.All(i => i.Passed)
            ? RunVerifyStatus.Passed
            : RunVerifyStatus.Failed;
        return new RunVerifyOutcome(status, items);
    }

    private Task<RunVerifyItem> VerifyOneAsync(RunExitCriterion criterion, RunVerifyInputs inputs, CancellationToken ct)
    {
        var type = criterion.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        try
        {
            return type switch
            {
                TypeFileExists => Task.FromResult(CheckFileExists(criterion, inputs)),
                TypeOutputContains => Task.FromResult(CheckOutputContains(criterion, inputs)),
                TypeFilesChangedNotEmpty => Task.FromResult(CheckFilesChangedNotEmpty(criterion, inputs)),
                _ => Task.FromResult(new RunVerifyItem(criterion.Text, criterion.Type ?? type, false,
                    $"未知断言类型「{criterion.Type}」（支持：{TypeFileExists}/{TypeOutputContains}/{TypeFilesChangedNotEmpty}）")),
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[RunVerify] 判据「{0}」校验异常: {1}", criterion.Text, ex.Message);
            return Task.FromResult(new RunVerifyItem(criterion.Text, criterion.Type ?? type, false, $"校验异常：{ex.Message}"));
        }
    }

    /// <summary>断言字面量：Value 优先（句式描述判据的匹配串/路径与给用户看的 Text 分离），缺省回退 Text。</summary>
    private static string LiteralOf(RunExitCriterion criterion)
        => string.IsNullOrWhiteSpace(criterion.Value) ? (criterion.Text?.Trim() ?? string.Empty) : criterion.Value.Trim();

    private RunVerifyItem CheckFileExists(RunExitCriterion criterion, RunVerifyInputs inputs)
    {
        var target = LiteralOf(criterion);
        if (target.Length == 0)
        {
            return new RunVerifyItem(criterion.Text, criterion.Type, false, "判据文本未给出文件路径");
        }

        if (_workspace == null || string.IsNullOrWhiteSpace(_workspace.ProjectRoot))
        {
            return new RunVerifyItem(criterion.Text, criterion.Type, false, "工作区未设置（无法解析文件路径）");
        }

        var root = _workspace.ProjectRoot!;
        var path = target;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(root, path);
        }
        else if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return new RunVerifyItem(criterion.Text, criterion.Type, false, $"路径越界：{target} 不在工作区 {root} 之下");
        }

        var exists = File.Exists(path);
        return new RunVerifyItem(criterion.Text, criterion.Type, exists,
            exists ? $"文件存在：{path}" : $"文件不存在：{path}");
    }

    private static RunVerifyItem CheckOutputContains(RunExitCriterion criterion, RunVerifyInputs inputs)
    {
        var needle = LiteralOf(criterion);
        if (needle.Length == 0)
        {
            return new RunVerifyItem(criterion.Text, criterion.Type, false, "判据文本为空");
        }

        var haystack = inputs.SummaryText ?? string.Empty;
        var contains = haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
        return new RunVerifyItem(criterion.Text, criterion.Type, contains,
            contains ? $"产出包含「{needle}」" : $"产出不包含「{needle}」（产出长度 {haystack.Length}）");
    }

    private static RunVerifyItem CheckFilesChangedNotEmpty(RunExitCriterion criterion, RunVerifyInputs inputs)
    {
        var count = inputs.FilesChanged?.Count ?? 0;
        return new RunVerifyItem(criterion.Text, criterion.Type, count > 0,
            count > 0 ? $"改动文件 {count} 个" : "本次无文件改动记录");
    }
}
