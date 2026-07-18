using Microsoft.AspNetCore.SignalR;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Hubs;

public class WorkflowHub : Hub
{
    private static readonly HashSet<string> ConnectedConnections = new();
    private static readonly Dictionary<long, List<string>> ExecutionSubscriptions = new();
    private static readonly object _lock = new();
    private static IServiceProvider? _serviceProvider;

    public override async Task OnConnectedAsync()
    {
        lock (_lock)
        {
            ConnectedConnections.Add(Context.ConnectionId);
        }

        XTrace.Log.Info("[WorkflowHub] 客户端连接: {0}", Context.ConnectionId);
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

        XTrace.Log.Info("[WorkflowHub] 客户端断开连接: {0}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToExecution(long executionId)
    {
        lock (_lock)
        {
            if (!ExecutionSubscriptions.TryGetValue(executionId, out var connections))
            {
                connections = new List<string>();
                ExecutionSubscriptions[executionId] = connections;
            }
            if (!connections.Contains(Context.ConnectionId))
            {
                connections.Add(Context.ConnectionId);
            }
        }

        XTrace.Log.Debug("[WorkflowHub] 客户端 {0} 订阅执行 {1}", Context.ConnectionId, executionId);
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

        XTrace.Log.Debug("[WorkflowHub] 客户端 {0} 取消订阅执行 {1}", Context.ConnectionId, executionId);
        await Task.CompletedTask;
    }

    public static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private static IHubContext<WorkflowHub>? GetHubContext()
    {
        return _serviceProvider?.GetService<IHubContext<WorkflowHub>>();
    }

    public static async Task BroadcastExecutionUpdateAsync(WorkflowExecution execution)
    {
        var context = GetHubContext();
        if (context == null) return;

        List<string>? connections;
        lock (_lock)
        {
            if (ExecutionSubscriptions.TryGetValue(execution.Id, out var connList))
            {
                connections = new List<string>(connList);
            }
            else
            {
                connections = new List<string>(ConnectedConnections);
            }
        }

        if (connections.Count == 0) return;

        var update = new WorkflowExecutionUpdate
        {
            ExecutionId = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowName = execution.WorkflowName,
            Status = execution.Status,
            Progress = execution.Progress,
            CurrentStepId = execution.CurrentStepId,
            ErrorMessage = execution.ErrorMessage,
            StartTime = execution.StartTime,
            EndTime = execution.EndTime,
            Timestamp = DateTime.Now
        };

        foreach (var connId in connections)
        {
            try
            {
                await context.Clients.Client(connId).SendAsync("ReceiveExecutionUpdate", update);
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[WorkflowHub] 发送执行更新到 {0} 失败: {1}", connId, ex.Message);
            }
        }
    }

    public static async Task BroadcastStepUpdateAsync(long executionId, string stepId, string stepName, WorkflowStepStatus stepStatus, string? message = null)
    {
        var context = GetHubContext();
        if (context == null) return;

        List<string>? connections;
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

        var stepUpdate = new WorkflowStepUpdate
        {
            ExecutionId = executionId,
            StepId = stepId,
            StepName = stepName,
            StepStatus = stepStatus,
            Message = message,
            Timestamp = DateTime.Now
        };

        foreach (var connId in connections)
        {
            try
            {
                await context.Clients.Client(connId).SendAsync("ReceiveStepUpdate", stepUpdate);
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[WorkflowHub] 发送步骤更新到 {0} 失败: {1}", connId, ex.Message);
            }
        }
    }

    public static async Task BroadcastExecutionLogAsync(long executionId, ExecutionLogEntry logEntry)
    {
        var context = GetHubContext();
        if (context == null) return;

        List<string>? connections;
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
                await context.Clients.Client(connId).SendAsync("ReceiveExecutionLog", logEntry);
            }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[WorkflowHub] 发送执行日志到 {0} 失败: {1}", connId, ex.Message);
            }
        }
    }
}

public enum WorkflowStepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}

public class WorkflowExecutionUpdate
{
    public long ExecutionId { get; set; }
    public long WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public WorkflowStatus Status { get; set; }
    public double Progress { get; set; }
    public string? CurrentStepId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime Timestamp { get; set; }
}

public class WorkflowStepUpdate
{
    public long ExecutionId { get; set; }
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public WorkflowStepStatus StepStatus { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; }
}
