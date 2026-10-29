namespace ForgeSelf.Abstractions;

/// <summary>
/// 工具命名空间规则（2026-10-28 工具命名空间统一，ADR-004）：
/// 1) 工具正式名 = <c>&lt;toolPrefix&gt;_&lt;语义名&gt;</c>，前缀按插件声明（<see cref="PrefixByPluginId"/> 唯一真源，
///    plugin.json 的 toolPrefix 字段与之同源，由 ToolNamespaceGuardTests 校验一致）；
/// 2) 平台保留名（<see cref="ReservedNames"/>，11 个）豁免前缀——运行时协议面：网关/发现/循环控制/计划流协议/命令执行/文件三件套；
/// 3) **mcp-center 整插件豁免**（用户拍板 2026-10-28 17:29「mcp中心的工具不用前缀，是特殊例外」）：
///    mcp-center 是平台公共工具宿主，其工具名即公共 API 面，不加插件前缀（get_current_time/calculate 因此不加 mcp_ 前缀）；
/// 4) 不做存量兼容（用户拍板 2026-10-28）：旧名直接退役，无别名层、无数据迁移，模型调旧名走 unknown 提示。
/// 宿主 ExtensionPointManager 注册时强制校验（不合规拒绝注册 + Error 日志）；ToolNamespaceGuardTests 防漂移。
/// </summary>
public static class ToolNamespace
{
    /// <summary>mcp-center 整插件豁免前缀（用户拍板 2026-10-28 17:29：mcp 中心的工具不用前缀，是特殊例外）。</summary>
    public const string ExemptPluginId = "mcp-center";

    /// <summary>平台保留名（豁免前缀）。新增保留名必须先扩此清单并过闸门评审。</summary>
    public static readonly IReadOnlyCollection<string> ReservedNames = new[]
    {
        // 发现与网关（mcp-center）
        "universal_tool", "search_tools", "list_tools",
        // 公共执行与文件三件套（mcp-center，用户拍板 2026-10-28 裸名豁免）
        "run_terminal_command", "read_file", "write_file", "list_files",
        // Agent 循环控制与计划流协议（ai-agent）
        "finish", "submit_plan", "complete_step", "request_help",
    };

    /// <summary>插件 id → toolPrefix（唯一真源）。仅收录有工具注册的插件；plugin.json 的 toolPrefix 字段须与此一致。</summary>
    public static readonly IReadOnlyDictionary<string, string> PrefixByPluginId = BuildPrefixMap();

    /// <summary>判断是否平台保留名。</summary>
    public static bool IsReserved(string name) =>
        !string.IsNullOrEmpty(name) && ReservedNames.Contains(name);

    /// <summary>
    /// 校验工具名是否合规（保留名 或 以本插件 toolPrefix + '_' 开头）。
    /// 返回 null=合规；否则返回错误原因（供 ExtensionPointManager 拒绝注册时记 Error 日志）。
    /// </summary>
    public static string? Validate(string? pluginId, string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return "工具名为空";
        if (IsReserved(toolName)) return null;
        if (string.IsNullOrWhiteSpace(pluginId)) return $"工具 {toolName} 无归属插件 id，无法校验前缀";
        if (string.Equals(pluginId, ExemptPluginId, StringComparison.OrdinalIgnoreCase)) return null;
        if (!PrefixByPluginId.TryGetValue(pluginId, out var prefix) || string.IsNullOrEmpty(prefix))
            return $"插件 {pluginId} 未在 ToolNamespace.PrefixByPluginId 声明 toolPrefix，拒绝注册工具 {toolName}";
        return toolName.StartsWith(prefix + "_", StringComparison.Ordinal)
            ? null
            : $"工具名 {toolName} 未带插件前缀 {prefix}_（保留名白名单除外），拒绝注册";
    }

    // ────────────────────────── 内部构建（紧凑元组，便于维护） ──────────────────────────

    private static IReadOnlyDictionary<string, string> BuildPrefixMap() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ai-agent"] = "aiagent",
            ["agent-hub"] = "hub",
            ["dev-tools"] = "dev",
            ["file-tools"] = "file",
            ["memory-system"] = "memory",
            ["quick-links"] = "link",
            ["sample"] = "sample",
            ["scheduler"] = "schedule",
            ["script-runner"] = "script",
            ["sems"] = "sems",
            ["design-system"] = "design",
            ["system-monitor"] = "sys",
            ["todo-tracker"] = "todo",
            ["workflow-engine"] = "workflow",
        };

    /// <summary>(pluginId, 旧名, 正式名) 行集。</summary>
    private static readonly (string Plugin, string Old, string New)[] RenameRows =
    {
        // ── ai-agent（迁 mcp-center 的 8 件按用户拍板 2026-10-28 17:29 整插件豁免前缀，最终名=原名，不在表内）──
        ("ai-agent", "plan_workflow", "aiagent_plan_workflow"),

        // ── workflow-engine ──
        ("workflow-engine", "create_workflow", "workflow_create"),
        ("workflow-engine", "get_workflow", "workflow_get"),
        ("workflow-engine", "execute_workflow", "workflow_execute"),
        ("workflow-engine", "get_workflow_status", "workflow_get_status"),
        ("workflow-engine", "list_workflows", "workflow_list"),

        // ── memory-system ──
        ("memory-system", "get_relevant_memories", "memory_relevant"),
        ("memory-system", "add_memory", "memory_add"),
        ("memory-system", "search_memories", "memory_search"),
        ("memory-system", "update_memory", "memory_update"),
        ("memory-system", "get_memory_stats", "memory_stats"),

        // ── dev-tools ──
        ("dev-tools", "format_json", "dev_format_json"),
        ("dev-tools", "validate_json", "dev_validate_json"),
        ("dev-tools", "convert_json_yaml", "dev_convert_json_yaml"),
        ("dev-tools", "jsonpath_query", "dev_jsonpath_query"),
        ("dev-tools", "format_xml", "dev_format_xml"),
        ("dev-tools", "base64_encode", "dev_base64_encode"),
        ("dev-tools", "base64_decode", "dev_base64_decode"),
        ("dev-tools", "url_encode", "dev_url_encode"),
        ("dev-tools", "url_decode", "dev_url_decode"),
        ("dev-tools", "compute_hash", "dev_hash"),
        ("dev-tools", "compute_hmac", "dev_hmac"),
        ("dev-tools", "aes_encrypt", "dev_aes_encrypt"),
        ("dev-tools", "aes_decrypt", "dev_aes_decrypt"),
        ("dev-tools", "test_regex", "dev_regex_test"),
        ("dev-tools", "generate_regex", "dev_regex_generate"),
        ("dev-tools", "regex_replace", "dev_regex_replace"),
        ("dev-tools", "convert_timestamp", "dev_timestamp"),
        ("dev-tools", "convert_color", "dev_color_convert"),
        ("dev-tools", "generate_color_palette", "dev_color_palette"),
        ("dev-tools", "check_color_contrast", "dev_color_contrast"),
        ("dev-tools", "decode_jwt", "dev_jwt_decode"),
        ("dev-tools", "validate_jwt", "dev_jwt_validate"),
        ("dev-tools", "generate_jwt", "dev_jwt_generate"),
        ("dev-tools", "generate_uuid", "dev_uuid"),
        ("dev-tools", "generate_snowflake_id", "dev_snowflake_id"),
        ("dev-tools", "generate_qrcode", "dev_qrcode"),
        // 2026-10-28 归属修订轮：text-tools 整插件并入 dev-tools（用户拍板 D-V1）——text_* 五工具退役
        // （text_base64_*/text_hash/text_format_json 由 dev 同功能承接；text_stats 改名 dev_text_stats）。
        // 退役插件不进 RenameRows（S3 对账要求源插件目录存在）；插件级合并对账见 pilot 2026-10-28-plugin-design-boundary。

        // ── file-tools ──
        ("file-tools", "folder_stats", "file_dir_stats"),

        // ── system-monitor ──
        ("system-monitor", "get_cpu_usage", "sys_cpu"),
        ("system-monitor", "get_memory_usage", "sys_memory"),
        ("system-monitor", "get_disk_info", "sys_disk"),
        ("system-monitor", "get_process_list", "sys_processes"),
        ("system-monitor", "kill_process", "sys_kill_process"),

        // ── script-runner ──
        ("script-runner", "run_script", "script_run"),
        ("script-runner", "list_scripts", "script_list"),
        ("script-runner", "execute_code", "script_exec_code"),
        ("script-runner", "generate_script", "script_generate"),
        ("script-runner", "analyze_script_error", "script_analyze_error"),
        ("script-runner", "suggest_script_fix", "script_suggest_fix"),
        ("script-runner", "list_script_templates", "script_templates"),
        ("script-runner", "list_code_snippets", "script_snippet_list"),
        ("script-runner", "create_code_snippet", "script_snippet_create"),
        ("script-runner", "get_personal_stats", "script_personal_stats"),
        ("script-runner", "get_recommendations", "script_recommend"),

        // ── quick-links ──
        ("quick-links", "search_links", "link_search"),
        ("quick-links", "open_link", "link_open"),
        ("quick-links", "get_categories", "link_categories"),
        ("quick-links", "create_link", "link_create"),

        // ── scheduler ──
        ("scheduler", "create_scheduled_task", "schedule_create"),
        ("scheduler", "list_scheduled_tasks", "schedule_list"),
        ("scheduler", "pause_scheduled_task", "schedule_pause"),

        // ── todo-tracker ──
        ("todo-tracker", "create_todo", "todo_create"),
        ("todo-tracker", "list_todos", "todo_list"),
        ("todo-tracker", "complete_todo", "todo_complete"),
        ("todo-tracker", "get_agent_task", "todo_task_get"),
        ("todo-tracker", "claim_agent_task", "todo_task_claim"),
        ("todo-tracker", "append_task_execution", "todo_execution_append"),
        ("todo-tracker", "update_task_stage", "todo_stage_update"),
        ("todo-tracker", "dispatch_task", "todo_dispatch"),

        // ── agent-hub ──
        ("agent-hub", "list_agents", "hub_list_agents"),
        ("agent-hub", "delegate_task", "hub_delegate_task"),
        ("agent-hub", "get_task_result", "hub_task_result"),
        ("agent-hub", "cancel_task", "hub_cancel_task"),
        ("agent-hub", "ask_agent", "hub_ask_agent"),

        // ── sample ──
        ("sample", "示例问候", "sample_greet"),
    };

    /// <summary>
    /// 改名映射（键 = "&lt;pluginId&gt;:&lt;旧名&gt;"，值 = 正式名；唯一真源，供守卫测试与文档对照；不做运行期别名——旧名即退役）。
    /// 键带插件限定：纯旧名做键会撞键。
    /// 2026-10-28 归属修订轮：text-stats 并入 dev-tools 后改名 dev_text_stats（text-tools 整插件退役，不进本表，见上方 RenameRows 注）；
    /// 文件三件套迁 file-tools（保留名不变，MovedTo 在守卫侧）。
    /// 注意：声明必须位于 RenameRows 之后（静态字段按文本顺序初始化，否则 BuildRenameMap 读到 null）。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RenameMap = BuildRenameMap();

    private static IReadOnlyDictionary<string, string> BuildRenameMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in RenameRows)
            map[$"{row.Plugin}:{row.Old}"] = row.New;
        return map;
    }
}
