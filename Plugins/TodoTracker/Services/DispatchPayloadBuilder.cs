using System.Text;
using System.Text.Json;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 下发载荷组装器（PILOT-054 · FR-1.1 / BR-6）——把一条任务变成「agent 拿到就能开工」的两份产物：
/// ① 可直接粘贴给任何 agent 的 markdown 提示词；② 交给 AgentHub 委派接口的 JSON 载荷。
///
/// 设计约束：
/// <list type="bullet">
/// <item><b>纯函数</b>：输入是扁平 <see cref="TaskPayload"/>（不碰实体、不碰网络），单测直接锁形状。</item>
/// <item><b>唯一组装点</b>：界面与工具函数都调它，禁止各处自己拼字符串（否则"下发的内容"会有第二份真相）。</item>
/// <item>回报契约写进提示词，但 <b>token 一律用占位符</b> <c>&lt;token&gt;</c>，绝不把密钥烤进正文（BR-7）。</item>
/// </list>
/// </summary>
public static class DispatchPayloadBuilder
{
    /// <summary>下发必填：目标 / 正文 / 验收判据 / 验证命令（标题在创建时已强制）。</summary>
    public static readonly string[] RequiredFields = ["objective", "content", "acceptance", "verification"];

    /// <summary>任务字段快照（由服务层从实体投影，字段名与 REST 出参一致）。</summary>
    public readonly record struct TaskPayload(
        int Id,
        string TaskKey,
        string Title,
        string Objective,
        string Content,
        string Acceptance,
        string Verification,
        string AllowedScope,
        string ForbiddenScope,
        int Priority,
        int Stage,
        string ProjectRoot,
        string ProjectPathRaw,
        string ArtifactRef,
        string Assignee,
        string PermissionMode);

    /// <summary>缺哪些必填项（返回英文字段名，界面据此点名提示）。</summary>
    public static IReadOnlyList<string> Missing(TaskPayload task)
    {
        var list = new List<string>();
        if (IsBlank(task.Objective)) list.Add("objective");
        if (IsBlank(task.Content)) list.Add("content");
        if (IsBlank(task.Acceptance)) list.Add("acceptance");
        if (IsBlank(task.Verification)) list.Add("verification");
        return list;
    }

    /// <summary>软提醒（不拦下发，但会如实告诉用户后果）。</summary>
    public static IReadOnlyList<string> Warnings(TaskPayload task)
    {
        var list = new List<string>();
        if (IsBlank(task.ProjectRoot))
            list.Add("未关联项目：交给 AgentHub 时将使用 agent 的默认工作目录");
        if (SplitLines(task.Acceptance).Count == 0)
            list.Add("验收判据里没有 - [ ] 条目，验收时缺少可勾判据");
        return list;
    }

    /// <summary>可否下发（必填齐备）。</summary>
    public static bool CanDispatch(TaskPayload task) => Missing(task).Count == 0;

    /// <summary>
    /// 提示词正文。结构对齐本项目 04-task 工件（Objective / Scope / Acceptance / Verification），
    /// 让接收方 agent 不必猜"做到什么程度算完"。
    /// </summary>
    public static string BuildMarkdown(TaskPayload task, string? baseUrl)
    {
        var sb = new StringBuilder();
        sb.Append("# 任务 ").Append(OrNone(task.TaskKey)).Append("：").Append(OrNone(task.Title)).Append('\n').Append('\n');

        sb.Append("- 优先级：").Append(PriorityName(task.Priority)).Append('\n');
        sb.Append("- 下发对象：").Append(OrNone(task.Assignee, "未指定")).Append('\n');
        sb.Append("- 当前阶段：").Append(TodoStage.ToName(task.Stage)).Append("（").Append(TodoStage.ToLabel(task.Stage)).Append("）\n");
        sb.Append("- 项目路径：").Append(OrNone(task.ProjectRoot, "未关联"));
        if (!IsBlank(task.ProjectPathRaw) && !string.Equals(task.ProjectPathRaw, task.ProjectRoot, StringComparison.Ordinal))
            sb.Append("（你的写法：").Append(task.ProjectPathRaw.Trim()).Append("）");
        sb.Append('\n');
        if (!IsBlank(task.ArtifactRef)) sb.Append("- 来源工件：").Append(task.ArtifactRef.Trim()).Append('\n');
        sb.Append('\n');

        sb.Append("## Objective（做完后仓库达到的可验证状态）\n\n");
        sb.Append(OrNone(task.Objective)).Append("\n\n");

        sb.Append("## 任务内容\n\n");
        sb.Append(OrNone(task.Content)).Append("\n\n");

        sb.Append("## 允许改动范围（Allowed）\n\n").Append(Bullets(task.AllowedScope)).Append('\n');
        sb.Append("## 禁止改动范围（Forbidden）\n\n").Append(Bullets(task.ForbiddenScope)).Append('\n');

        sb.Append("## 验收判据（逐条自查，完成时把勾上的结果写进回报）\n\n");
        sb.Append(Checkboxes(task.Acceptance)).Append('\n');

        sb.Append("## 验证命令（必须真跑，回报里附真实输出摘要）\n\n");
        sb.Append(Commands(task.Verification)).Append('\n');

        var key = OrPlaceholder(task.TaskKey);
        var recordsUrl = Endpoint(baseUrl, $"/api/todos/by-key/{key}/records");
        var taskUrl = Endpoint(baseUrl, $"/api/todos/by-key/{key}");
        var stageUrl = Endpoint(baseUrl, $"/api/todos/by-key/{key}/stage");

        // 回报契约整段用 raw string 写：里面既有 ASCII 双引号又有 JSON 花括号，
        // 拆成字符串拼接很容易把引号配错（本项目踩过「中文串里的 ASCII 引号会断句」这一类坑）。
        // 用 $$ 前缀：这样 JSON 的 { } 是字面量，插值写成 {{expr}}（$ 单个时 {{ 会被当插值起点，编不过）。
        sb.Append($$"""
            ## 完成后必须回报

            把结果写回任务台账（一条任务可多次回报，记录按序追加）：

            ```bash
            curl -X POST "{{recordsUrl}}" \
              -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
              -d '{"actor":"<你的标识>","action":"做了什么操作","result":"什么结果","filesChanged":[{"path":"文件路径","change":"A|M|D"}],"verification":"跑了什么命令 + 结果","risks":"风险","residuals":"遗留","stageTo":"Review"}'
            ```

            - 读任务：`GET {{taskUrl}}`（正文、判据、验证命令与已有记录都在里面）
            - 改状态：`POST {{stageUrl}}`，body `{"stage":"Blocked","reason":"阻塞在哪、缺什么"}`
            - 宿主装了 AIAgent 时，等价的工具函数是 `get_agent_task` / `append_task_execution` / `update_task_stage`

            """);

        return sb.ToString();
    }

    /// <summary>
    /// 交给 <c>IAgentDelegation.SubmitAsync</c> 的载荷 JSON（键名与 AgentHub <c>DelegationRequest</c> 对齐：
    /// prompt / agentId / cwd / permissionMode / createdBy）。
    /// </summary>
    public static string BuildAgentHubJson(TaskPayload task, int? agentId = null)
    {
        var payload = new
        {
            prompt = BuildMarkdown(task, null),
            agentId,
            cwd = IsBlank(task.ProjectRoot) ? null : task.ProjectRoot,
            permissionMode = IsBlank(task.PermissionMode) ? "read-only" : task.PermissionMode.Trim(),
            createdBy = "todo-tracker"
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>按行拆条目（去空行、去掉行首的 "- " / "* " / "1. " 前缀）。</summary>
    public static IReadOnlyList<string> SplitLines(string? text)
    {
        if (IsBlank(text)) return [];
        return text!.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Select(StripBullet)
            .ToList();
    }

    private static string Bullets(string? text)
    {
        var lines = SplitLines(text);
        if (lines.Count == 0) return "（未填写）\n\n";
        var sb = new StringBuilder();
        foreach (var line in lines) sb.Append("- ").Append(line).Append('\n');
        sb.Append('\n');
        return sb.ToString();
    }

    private static string Checkboxes(string? text)
    {
        var lines = SplitLines(text);
        if (lines.Count == 0) return "（未填写）\n\n";
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            var body = line.StartsWith("- [ ]", StringComparison.Ordinal) ? line[5..].Trim() : line;
            sb.Append("- [ ] ").Append(body).Append('\n');
        }
        sb.Append('\n');
        return sb.ToString();
    }

    private static string Commands(string? text)
    {
        var lines = SplitLines(text);
        if (lines.Count == 0) return "（未填写）\n\n";
        var sb = new StringBuilder();
        sb.Append("```bash\n");
        foreach (var line in lines) sb.Append(line).Append('\n');
        sb.Append("```\n\n");
        return sb.ToString();
    }

    private static string StripBullet(string line)
    {
        var text = line;
        if (text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("* ", StringComparison.Ordinal))
            text = text[2..].Trim();

        // 判据原文常已带 checkbox 标记（04-task 里就是 `- [ ] …`）；不剥掉的话组装出来是 `- [ ] [ ] …`
        if (text.StartsWith("[ ] ", StringComparison.Ordinal) || text.StartsWith("[x] ", StringComparison.Ordinal) ||
            text.StartsWith("[X] ", StringComparison.Ordinal))
            text = text[4..].Trim();

        var dot = text.IndexOf(". ", StringComparison.Ordinal);
        if (dot > 0 && dot <= 2 && char.IsDigit(text[0])) text = text[(dot + 1)..].Trim();
        return text;
    }

    private static string Endpoint(string? baseUrl, string path)
    {
        var host = IsBlank(baseUrl) ? "<宿主地址>" : baseUrl!.TrimEnd('/');
        return host + path;
    }

    private static string PriorityName(int priority) => priority switch
    {
        1 => "P1",
        2 => "P2",
        3 => "P3",
        _ => "P?"
    };

    private static string OrNone(string? value, string fallback = "（未填写）") =>
        IsBlank(value) ? fallback : value!.Trim();

    private static string OrPlaceholder(string? value) => IsBlank(value) ? "<taskKey>" : value!.Trim();

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
}
