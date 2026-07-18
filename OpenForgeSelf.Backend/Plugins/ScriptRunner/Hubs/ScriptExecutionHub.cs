using Microsoft.AspNetCore.SignalR;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Hubs;

public class ScriptExecutionHub : Hub
{
    private static readonly HashSet<string> ConnectedConnections = [];
    private static readonly Dictionary<long, List<string>> ExecutionSubscriptions = [];
    private static readonly object _lock = new();
    private static IServiceProvider? _serviceProvider;

    public override async Task OnConnectedAsync()
    {
        lock (_lock)
        {
            ConnectedConnections.Add(Context.ConnectionId);
        }

        XTrace.Log.Info("[ScriptExecutionHub] 客户端连接: {0}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        lock (_lock)
        {
            ConnectedConnections.Remove(Context.ConnectionId);

            var executionsToRemove = new List<long>();
            foreach (var kvp in ExecutionSubscriptions)
            {
                kvp.Value.Remove(Context.ConnectionId);
                if (kvp.Value.Count == 0)
                {
                    executionsToRemove.Add(kvp.Key);
                }
            }
            foreach (var execId in executionsToRemove)
            {
                ExecutionSubscriptions.Remove(execId);
            }
        }

        XTrace.Log.Info("[ScriptExecutionHub] 客户端断开连接: {0}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToExecution(long executionId)
    {
        lock (_lock)
        {
            if (!ExecutionSubscriptions.TryGetValue(executionId, out var connections))
            {
                connections = [];
                ExecutionSubscriptions[executionId] = connections;
            }
            if (!connections.Contains(Context.ConnectionId))
            {
                connections.Add(Context.ConnectionId);
            }
        }

        XTrace.Log.Debug("[ScriptExecutionHub] 客户端 {0} 订阅执行 {1}", Context.ConnectionId, executionId);
        await Task.CompletedTask;
    }

    public async Task UnsubscribeFromExecution(long executionId)
    {
        lock (_lock)
        {
            if (ExecutionSubscriptions.TryGetValue(executionId, out var connections))
            {
                connections.Remove(Context.ConnectionId);
                if (connections.Count == 0)
                {
                    ExecutionSubscriptions.Remove(executionId);
                }
            }
        }

        XTrace.Log.Debug("[ScriptExecutionHub] 客户端 {0} 取消订阅执行 {1}", Context.ConnectionId, executionId);
        await Task.CompletedTask;
    }

    public static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private static IHubContext<ScriptExecutionHub>? GetHubContext()
    {
        return _serviceProvider?.GetService<IHubContext<ScriptExecutionHub>>();
    }

    public static async Task BroadcastStatusUpdateAsync(long executionId, ScriptExecutionStatus status)
    {
        var context = GetHubContext();
        if (context == null) return;

        List<string> connections;
        lock (_lock)
        {
            if (ExecutionSubscriptions.TryGetValue(executionId, out var connList))
            {
                connections = new List<string>(connList);
            }
            else
            {
                connections = new List<string>(ConnectedConnections);
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
                await context.Clients.Client(connId).SendAsync("ReceiveStatusUpdate", update);
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[ScriptExecutionHub] 发送状态更新到 {0} 失败: {1}", connId, ex.Message);
            }
        }
    }

    public static async Task BroadcastOutputLogAsync(long executionId, ScriptExecutionLog logEntry)
    {
        var context = GetHubContext();
        if (context == null) return;

        List<string> connections;
        lock (_lock)
        {
            if (ExecutionSubscriptions.TryGetValue(executionId, out var connList))
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
                await context.Clients.Client(connId).SendAsync("ReceiveOutputLog", logEntry);
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[ScriptExecutionHub] 发送输出日志到 {0} 失败: {1}", connId, ex.Message);
            }
        }
    }
}

public class ScriptExecutionStatusUpdate
{
    public long ExecutionId { get; set; }
    public ScriptExecutionStatus Status { get; set; }
    public DateTime Timestamp { get; set; }
}
