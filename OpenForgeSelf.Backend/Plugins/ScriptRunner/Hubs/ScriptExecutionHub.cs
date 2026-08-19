using Microsoft.AspNetCore.SignalR;
using OpenForgeSelf.Abstractions;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Hubs;

/// <summary>
/// 脚本执行状态广播器（宿主 DI 单例）。
/// 持有 SignalR <see cref="IHubContext{ScriptExecutionHub}"/> 与连接/订阅路由状态，
/// 供 <see cref="ScriptExecutor"/> 等消费方通过实例方法推送执行状态与输出日志。
/// 取代原先「静态 SetServiceProvider + 静态访问」模式：不再以静态字段长期持有插件 Fiber 上下文，
/// 避免插件卸载后因静态根引用导致 ALC 无法被 GC 回收。
/// </summary>
public class ScriptExecutionBroadcaster
{
    private readonly IHubContext<ScriptExecutionHub> _hubContext;
    private readonly HashSet<string> _connectedConnections = [];
    private readonly Dictionary<long, List<string>> _executionSubscriptions = [];
    private readonly object _lock = new();

    public ScriptExecutionBroadcaster(IHubContext<ScriptExecutionHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public void OnConnected(string connectionId)
    {
        lock (_lock)
        {
            _connectedConnections.Add(connectionId);
        }
    }

    public void OnDisconnected(string connectionId)
    {
        lock (_lock)
        {
            _connectedConnections.Remove(connectionId);

            var executionsToRemove = new List<long>();
            foreach (var kvp in _executionSubscriptions)
            {
                kvp.Value.Remove(connectionId);
                if (kvp.Value.Count == 0)
                {
                    executionsToRemove.Add(kvp.Key);
                }
            }
            foreach (var execId in executionsToRemove)
            {
                _executionSubscriptions.Remove(execId);
            }
        }
    }

    public void Subscribe(long executionId, string connectionId)
    {
        lock (_lock)
        {
            if (!_executionSubscriptions.TryGetValue(executionId, out var connections))
            {
                connections = [];
                _executionSubscriptions[executionId] = connections;
            }
            if (!connections.Contains(connectionId))
            {
                connections.Add(connectionId);
            }
        }
    }

    public void Unsubscribe(long executionId, string connectionId)
    {
        lock (_lock)
        {
            if (_executionSubscriptions.TryGetValue(executionId, out var connections))
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _executionSubscriptions.Remove(executionId);
                }
            }
        }
    }

    public async Task BroadcastStatusUpdateAsync(long executionId, ScriptExecutionStatus status)
    {
        List<string> connections;
        lock (_lock)
        {
            if (_executionSubscriptions.TryGetValue(executionId, out var connList))
            {
                connections = new List<string>(connList);
            }
            else
            {
                connections = new List<string>(_connectedConnections);
            }
        }

        if (connections.Count == 0) return;

        var update = new ScriptExecutionStatusUpdate
        {
            ExecutionId = executionId,
            Status = status,
            Timestamp = DateTime.Now
        };

        foreach (var connId in connections)
        {
            try
            {
                await _hubContext.Clients.Client(connId).SendAsync("ReceiveStatusUpdate", update);
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[ScriptExecutionHub] 发送状态更新到 {0} 失败: {1}", connId, ex.Message);
            }
        }
    }

    public async Task BroadcastOutputLogAsync(long executionId, ScriptExecutionLog logEntry)
    {
        List<string> connections;
        lock (_lock)
        {
            if (_executionSubscriptions.TryGetValue(executionId, out var connList))
            {
                connections = new List<string>(connList);
            }
            else
            {
                return;
            }
        }

        if (connections.Count == 0) return;

        foreach (var connId in connections)
        {
            try
            {
                await _hubContext.Clients.Client(connId).SendAsync("ReceiveOutputLog", logEntry);
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[ScriptExecutionHub] 发送输出日志到 {0} 失败: {1}", connId, ex.Message);
            }
        }
    }
}

public class ScriptExecutionHub : Hub
{
    private readonly ScriptExecutionBroadcaster _broadcaster;

    public ScriptExecutionHub(ScriptExecutionBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public override async Task OnConnectedAsync()
    {
        _broadcaster.OnConnected(Context.ConnectionId);

        XTrace.Log.Info("[ScriptExecutionHub] 客户端连接: {0}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _broadcaster.OnDisconnected(Context.ConnectionId);

        XTrace.Log.Info("[ScriptExecutionHub] 客户端断开连接: {0}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToExecution(long executionId)
    {
        _broadcaster.Subscribe(executionId, Context.ConnectionId);

        XTrace.Log.Debug("[ScriptExecutionHub] 客户端 {0} 订阅执行 {1}", Context.ConnectionId, executionId);
        await Task.CompletedTask;
    }

    public async Task UnsubscribeFromExecution(long executionId)
    {
        _broadcaster.Unsubscribe(executionId, Context.ConnectionId);

        XTrace.Log.Debug("[ScriptExecutionHub] 客户端 {0} 取消订阅执行 {1}", Context.ConnectionId, executionId);
        await Task.CompletedTask;
    }
}

public class ScriptExecutionStatusUpdate
{
    public long ExecutionId { get; set; }
    public ScriptExecutionStatus Status { get; set; }
    public DateTime Timestamp { get; set; }
}
