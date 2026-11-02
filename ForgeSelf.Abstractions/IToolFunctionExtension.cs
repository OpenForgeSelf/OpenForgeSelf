namespace ForgeSelf.Abstractions;

/// <summary>
/// AI工具函数扩展点接口，用于向AI注册可调用的工具函数
/// </summary>
public interface IToolFunctionExtension : IExtensionPoint
{
    /// <summary>
    /// 获取工具函数描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 获取工具函数参数JSON Schema
    /// </summary>
    string ParametersJsonSchema { get; }

    /// <summary>
    /// 执行工具函数
    /// </summary>
    /// <param name="parameters">参数JSON字符串</param>
    /// <returns>执行结果JSON字符串</returns>
    Task<string> ExecuteAsync(string parameters);

    /// <summary>
    /// 带取消令牌的执行。默认忽略令牌并委托无令牌重载——既有工具零改动；
    /// 需要响应超时/调用方取消的工具（如 run_terminal_command）覆写此重载并真正吃 token。
    /// </summary>
    Task<string> ExecuteAsync(string parameters, CancellationToken cancellationToken)
        => ExecuteAsync(parameters);
}
