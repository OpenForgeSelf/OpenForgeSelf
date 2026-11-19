using System.IO.Pipes;
using System.Text;
using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// API 与托盘进程之间的命名管道。一条双工会话：
/// 托盘写入 <c>hello</c> 表示就绪，写入 <c>shutdown</c> 表示用户在交互模式下点了退出；
/// API 写入 <c>shutdown</c> 表示宿主正在停止，托盘应退出。
/// </summary>
public sealed class ServicePipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private NamedPipeServerStream? _connected;
    private bool _disposed;

    public ServicePipeServer(string pipeName)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
    }

    /// <summary>托盘发来 <c>shutdown</c> 时调用。交互模式下用来停掉 API。</summary>
    public Action? OnClientShutdown { get; set; }

    public void Start()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _listenTask = ListenAsync(_cts.Token);
        XTrace.Log.Info("ServicePipeServer: 开始监听管道 '{0}'", _pipeName);
    }

    /// <summary>让已连接的托盘退出。</summary>
    public void SendShutdown()
    {
        if (_disposed) return;

        NamedPipeServerStream? pipe;
        lock (_gate) pipe = _connected;
        if (pipe == null || !pipe.IsConnected)
        {
            XTrace.Log.Info("ServicePipeServer: 托盘尚未连接，跳过 shutdown");
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes("shutdown\n");
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
            XTrace.Log.Info("ServicePipeServer: 关闭信号已发送");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("ServicePipeServer: 发送关闭信号失败: {0}", ex.Message);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
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

    private async Task ListenAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var options = PipeOptions.Asynchronous;
                if (OperatingSystem.IsWindows())
                    options |= PipeOptions.CurrentUserOnly;

                await using var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    options);

                XTrace.Log.Info("ServicePipeServer: 等待托盘连接...");
                await pipe.WaitForConnectionAsync(token);
                lock (_gate) _connected = pipe;
                XTrace.Log.Info("ServicePipeServer: 托盘已连接");

                try
                {
                    while (!token.IsCancellationRequested && pipe.IsConnected)
                    {
                        var message = await ReadLineAsync(pipe, token);
                        if (message == null) break;

                        XTrace.Log.Info("ServicePipeServer: 收到消息: {0}", message);
                        if (message.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                            OnClientShutdown?.Invoke();
                    }
                }
                finally
                {
                    lock (_gate)
                    {
                        if (ReferenceEquals(_connected, pipe))
                            _connected = null;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("ServicePipeServer: 监听异常: {0}", ex.Message);
        }
    }

    internal static async Task<string?> ReadLineAsync(Stream pipe, CancellationToken token)
    {
        var buffer = new byte[1];
        var text = new StringBuilder();
        while (true)
        {
            var read = await pipe.ReadAsync(buffer.AsMemory(0, 1), token);
            if (read == 0)
                return text.Length == 0 ? null : text.ToString();

            if (buffer[0] == '\n')
                return text.ToString().TrimEnd('\r');

            text.Append((char)buffer[0]);
        }
    }
}
