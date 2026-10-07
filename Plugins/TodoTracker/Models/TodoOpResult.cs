namespace ForgeSelf.Api.Plugins.TodoTracker.Models;

/// <summary>
/// 统一操作结果（HTTP 码与工具错误同源，一份语义两个消费面 —— 形状参照 <c>Plugins/Sems</c> 的
/// <c>SemsResult&lt;T&gt;</c> 先例）。控制器据此选状态码，AI 工具函数据此出 <c>{success,error}</c> 封套，
/// <b>禁止</b>靠字符串嗅探判错。
/// </summary>
public class TodoOpResult
{
    /// <summary>是否成功。</summary>
    public bool Ok { get; init; }

    /// <summary>失败原因原文（中文，可直接展示）。</summary>
    public string? Error { get; init; }

    /// <summary>HTTP 状态码（成功 200/201，失败 400/404/409/503）。</summary>
    public int StatusCode { get; init; } = 200;

    /// <summary>结果载荷（任务投影，可空）。</summary>
    public TodoDto? Data { get; init; }

    /// <summary>附带提示（不影响成败判定，例如「已入队但台账未回填」）。</summary>
    public string? Warning { get; init; }

    public static TodoOpResult Success(TodoDto? data = null, int code = 200, string? warning = null) =>
        new() { Ok = true, Data = data, StatusCode = code, Warning = warning };

    public static TodoOpResult Failed(int code, string error) =>
        new() { Ok = false, Error = error, StatusCode = code };

    /// <summary>入参非法（400）。</summary>
    public static TodoOpResult Invalid(string error) => Failed(400, error);

    /// <summary>目标不存在（404）。</summary>
    public static TodoOpResult NotFound(string error) => Failed(404, error);

    /// <summary>状态冲突 / 非法流转 / 未确认覆盖（409）。</summary>
    public static TodoOpResult Conflict(string error) => Failed(409, error);

    /// <summary>依赖能力缺席（503）。</summary>
    public static TodoOpResult Unavailable(string error) => Failed(503, error);
}
