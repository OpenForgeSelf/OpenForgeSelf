namespace ForgeSelf.Abstractions;

/// <summary>
/// 宿主工具注册表：注册 + 查询 + 执行。
/// </summary>
/// <remarks>
/// B8（042 六闸门管线）：注册/查询 6 方法 + 执行面 <see cref="ExecuteAsync"/> / <see cref="ExecuteBatchAsync"/>
/// （走 tools/pre-execute → 单调守卫 → tools/execute → tools/post-execute → finalize → tools/result 六闸门）。
/// B9（退役收尾）：旧 <c>ExecuteToolWithResultAsync</c> / <c>ExecuteToolWithTimeoutAsync</c> 已随调用方
/// 迁移删除；<see cref="ExecuteToolAsync"/> 保留为旧 string 门面（委托 <see cref="ExecuteAsync"/>，返回 Result 原文）。
/// </remarks>
public interface IToolRegistry
{
    void RegisterTool(IToolFunctionExtension tool);
    void UnregisterTool(string toolId);
    IToolFunctionExtension? GetTool(string toolName);
    IEnumerable<IToolFunctionExtension> GetAllTools();
    List<AIToolDefinition> GetToolDefinitions();
    Task<string> ExecuteToolAsync(string toolName, string parameters);
    ToolValidationResult ValidateParameters(string toolName, string parameters);

    /// <summary>
    /// 六闸门管线单工具执行（B8）：pre-execute 三态 → 单调守卫 → tools/execute(waterfall) →
    /// tools/post-execute(waterfall) → finalize（恰好一次）→ tools/result（EmitAsync，观测隔离）。
    /// </summary>
    /// <param name="execution">执行上下文（<see cref="ToolExecution.Signal"/> 默认取 <paramref name="ct"/>，可被 execute 中间件替换）。</param>
    /// <param name="ct">调用方取消令牌。</param>
    Task<ToolExecutionResult> ExecuteAsync(ToolExecution execution, CancellationToken ct = default);

    /// <summary>
    /// 六闸门管线批量执行（B8/A4 model-ordered commit）：按模型返回顺序<b>串行</b>逐个执行，
    /// 取消后剩余 call 合成 <see cref="ToolOutcome.Skipped"/> 结果——N 个 call 必有 N 个 result。
    /// </summary>
    Task<IReadOnlyList<ToolExecutionResult>> ExecuteBatchAsync(
        IReadOnlyList<ToolCallRef> calls, string sessionId, CancellationToken ct = default);
}
