namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 日志服务接口
/// </summary>
public interface ILogService
{
    /// <summary>
    /// 记录信息日志
    /// </summary>
    void Info(string message, params object[] args);

    /// <summary>
    /// 记录警告日志
    /// </summary>
    void Warn(string message, params object[] args);

    /// <summary>
    /// 记录错误日志
    /// </summary>
    void Error(string message, params object[] args);

    /// <summary>
    /// 记录调试日志
    /// </summary>
    void Debug(string message, params object[] args);
}
