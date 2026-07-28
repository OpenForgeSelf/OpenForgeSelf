using System.IO.Pipes;
using System.Text;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 服务端命名管道服务器，用于服务进程与托盘辅助进程通信。
/// 服务模式启动托盘辅助进程后，服务端通过此管道发送控制信号（如关闭）。
/// </summary>
public sealed class ServicePipeServer : IDisposable
{
    private readonly string _pipeName;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _disposed;

    /// <summary>
    /// 初始化管道服务器。
    /// </summary>
    /// <param name="pipeName">管道名称，例如 "OpenForgeSelf-Tray-{ProcessId}"</param>
    public ServicePipeServer(string pipeName)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
    }

    /// <summary>
    /// 开始监听托盘辅助进程的连接。
    /// 异步接受连接后，等待客户端发送消息。
    /// </summary>
    public void Start()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _listenTask = ListenAsync(_cts.Token);
        XTrace.Log.Info("ServicePipeServer: 开始监听管道 '{0}'", _pipeName);
    }

    /// <summary>
    /// 发送关闭信号给托盘辅助进程（向管道写入 "shutdown"）。
    /// </summary>
    public void SendShutdown()
    {
        if (_disposed) return;

        XTrace.Log.Info("ServicePipeServer: 发送关闭信号给托盘辅助进程");
        // 创建一个独立的客户端连接，写入 shutdown 命令
        Task.Run(async () =>
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                await pipe.ConnectAsync(3000);
                using var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };
                await writer.WriteLineAsync("shutdown");
                XTrace.Log.Info("ServicePipeServer: 关闭信号已发送");
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("ServicePipeServer: 发送关闭信号失败: {0}", ex.Message);
            }
        });
    }

    /// <summary>
    /// 停止监听并释放资源。
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // 任务取消时的 TaskCanceledException 是预期行为
        }
        XTrace.Log.Info("ServicePipeServer: 已停止监听");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts?.Dispose();
    }

    /// <summary>
    /// 异步监听循环：接受一个客户端连接，读取消息后关闭管道，继续等待下一个客户端。
    /// 当前设计中，托盘辅助进程连接后发送 "hello" 表示就绪，服务端据此确认托盘已启动。
    /// </summary>
    private async Task ListenAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await using var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,                // 同一时间最多一个客户端
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                XTrace.Log.Info("ServicePipeServer: 等待托盘辅助进程连接...");

                // 等待客户端连接（支持取消）
                await pipe.WaitForConnectionAsync(token);

                XTrace.Log.Info("ServicePipeServer: 托盘辅助进程已连接");

                // 读取客户端消息
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                var message = await reader.ReadLineAsync(token);

                if (!string.IsNullOrEmpty(message))
                {
                    XTrace.Log.Info("ServicePipeServer: 收到消息: {0}", message);
                }

                // 客户端断开连接后，管道自动关闭，循环继续等待下一个连接
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("ServicePipeServer: 监听异常: {0}", ex.Message);
        }
    }
}