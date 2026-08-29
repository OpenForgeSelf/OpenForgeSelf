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
}
