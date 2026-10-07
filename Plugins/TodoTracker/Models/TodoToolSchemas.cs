namespace ForgeSelf.Api.Plugins.TodoTracker.Models;

/// <summary>
/// AI 工具函数的参数 JSON Schema（单点存放，避免 description 与 schema 在两个文件里各写一份而漂移）。
/// 取值一律与 <see cref="Services.TodoStage"/> / <see cref="TodoStatus"/> 的真实词表一致。
/// </summary>
public static class TodoToolSchemas
{
    public const string ById = """
{
  "type": "object",
  "properties": {
    "id": { "type": "integer", "description": "待办 ID", "minimum": 1 }
  },
  "required": ["id"]
}
""";

    public const string ByTaskKey = """
{
  "type": "object",
  "properties": {
    "taskKey": { "type": "string", "description": "任务外部键（create_todo/claim_agent_task 返回的 taskKey）" }
  },
  "required": ["taskKey"]
}
""";

    public const string CreateTodo = """
{
  "type": "object",
  "properties": {
    "title": { "type": "string", "description": "标题（最长 200 字符）" },
    "remark": { "type": "string", "description": "备注（最长 1000 字符）" },
    "dueDate": { "type": "string", "description": "截止日期 ISO 8601，如 2026-12-31T23:59:59" },
    "objective": { "type": "string", "description": "可验证目标（一句话）" },
    "content": { "type": "string", "description": "任务正文（markdown）" },
    "acceptance": { "type": "string", "description": "验收判据，每行一条" },
    "verification": { "type": "string", "description": "验证命令，每行一条" },
    "allowedScope": { "type": "string", "description": "允许改动范围" },
    "forbiddenScope": { "type": "string", "description": "禁止改动范围" },
    "priority": { "type": "integer", "description": "1=P1 2=P2 3=P3", "minimum": 1, "maximum": 3 },
    "assignee": { "type": "string", "description": "下发对象（agent 名/manual）" },
    "projectPath": { "type": "string", "description": "项目路径；正斜杠/反斜杠/Git-Bash /d/proj/WSL /mnt/d/proj 四种写法视为同一项目" }
  },
  "required": ["title"]
}
""";

    public const string ListTodos = """
{
  "type": "object",
  "properties": {
    "status": { "type": "string", "enum": ["Pending", "Completed"], "description": "按旧二元状态过滤；不传返回全部" },
    "stage": { "type": "string", "enum": ["Draft", "Ready", "Dispatched", "Running", "Blocked", "Review", "Done", "Cancelled"], "description": "按下发阶段过滤" },
    "projectId": { "type": "integer", "description": "按项目过滤，0=全部" },
    "q": { "type": "string", "description": "关键字（标题/备注/目标）" },
    "page": { "type": "integer", "description": "页码（从 1 开始），默认 1", "minimum": 1 },
    "pageSize": { "type": "integer", "description": "每页条数（最大 100），默认 20", "minimum": 1, "maximum": 100 }
  }
}
""";

    public const string ClaimTask = """
{
  "type": "object",
  "properties": {
    "assignee": { "type": "string", "description": "只领下发给该对象的任务；不传=不限" },
    "projectId": { "type": "integer", "description": "只领该项目下的任务，0=不限" }
  }
}
""";

    public const string AppendExecution = """
{
  "type": "object",
  "properties": {
    "taskKey": { "type": "string", "description": "任务外部键" },
    "action": { "type": "string", "description": "做了什么操作（一句话，必填）" },
    "result": { "type": "string", "description": "什么结果（含输出要点）" },
    "detail": { "type": "string", "description": "操作明细（步骤/命令）" },
    "filesChanged": { "description": "改了哪些文件：数组 [{path,change}]，或一行一个路径的文本", "oneOf": [
      { "type": "array", "items": { "type": "object", "properties": { "path": { "type": "string" }, "change": { "type": "string" } } } },
      { "type": "string" }
    ] },
    "verification": { "type": "string", "description": "跑了什么验证 + 结果（命令与真实输出摘要）" },
    "risks": { "type": "string", "description": "风险" },
    "residuals": { "type": "string", "description": "遗留/未做" },
    "evidence": { "type": "string", "description": "证据（日志/截图/工件路径）" },
    "nextStep": { "type": "string", "description": "下一步（回流入口）" },
    "elapsedMs": { "type": "integer", "description": "耗时（毫秒）" },
    "stageTo": { "type": "string", "enum": ["Running", "Blocked", "Review", "Done"], "description": "同批流转到的阶段；Blocked 必须同时给 blockReason" },
    "blockReason": { "type": "string", "description": "阻塞原因（进 Blocked 时必填）" }
  },
  "required": ["taskKey", "action"]
}
""";

    public const string UpdateStage = """
{
  "type": "object",
  "properties": {
    "taskKey": { "type": "string", "description": "任务外部键" },
    "stage": { "type": "string", "enum": ["Draft", "Ready", "Dispatched", "Running", "Blocked", "Review", "Done", "Cancelled"], "description": "目标阶段" },
    "reason": { "type": "string", "description": "变更说明（写入执行记录留痕）" },
    "blockReason": { "type": "string", "description": "阻塞原因（进 Blocked 时必填）" }
  },
  "required": ["taskKey", "stage"]
}
""";

    public const string DispatchTask = """
{
  "type": "object",
  "properties": {
    "taskKey": { "type": "string", "description": "任务外部键" },
    "assignee": { "type": "string", "description": "下发对象（不传则沿用任务上的值）" }
  },
  "required": ["taskKey"]
}
""";
}
