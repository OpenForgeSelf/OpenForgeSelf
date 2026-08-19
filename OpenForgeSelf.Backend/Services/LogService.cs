using NewLife.Log;
using OpenForgeSelf.Abstractions;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 日志服务实现 - 基于XTrace
/// </summary>
public class LogService : ILogService
{
    /// <summary>
    /// 记录信息日志
    /// </summary>
    public void Info(string message, params object[] args)
    {
        XTrace.Log.Info(message, args);
    }

    /// <summary>
    /// 记录警告日志
    /// </summary>
    public void Warn(string message, params object[] args)
    {
        XTrace.Log.Warn(message, args);
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    public void Error(string message, params object[] args)
    {
        XTrace.Log.Error(message, args);
    }

    /// <summary>
    /// 记录调试日志
    /// </summary>
    public void Debug(string message, params object[] args)
    {
        XTrace.Log.Debug(message, args);
    }
}