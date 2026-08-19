using OpenForgeSelf.Abstractions;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;

public class WorkflowScheduler : IWorkflowScheduler
{
    private readonly IWorkflowExecutor _executor;
    private readonly Queue<QueuedWorkflow> _executionQueue = new();
    private readonly Dictionary<long, WorkflowExecution> _runningExecutions = new();
    private readonly object _lock = new();

    public int MaxConcurrentExecutions { get; set; } = 3;
    public int CurrentRunningCount => _runningExecutions.Count;
    public int QueuedCount => _executionQueue.Count;

    public event EventHandler<WorkflowExecution>? ExecutionStarted;
    public event EventHandler<WorkflowExecution>? ExecutionCompleted;
    public event EventHandler<WorkflowExecution>? ExecutionFailed;
    public event EventHandler<WorkflowExecution>? ExecutionProgressChanged;

    public WorkflowScheduler(IWorkflowExecutor executor)
    {
        _executor = executor;
    }

    public async Task<WorkflowExecution> QueueExecutionAsync(long workflowId, Dictionary<string, object?>? inputVariables = null, string? triggeredBy = null)
    {
        XTrace.Log.Info("[WorkflowScheduler] 工作流加入队列，workflowId: {0}", workflowId);

        var queuedItem = new QueuedWorkflow
        {
            WorkflowId = workflowId,
            InputVariables = inputVariables,
            TriggeredBy = triggeredBy,
            QueuedAt = DateTime.Now
        };

        WorkflowExecution execution;

        lock (_lock)
        {
            if (_runningExecutions.Count < MaxConcurrentExecutions)
            {
                execution = StartExecution(queuedItem);
            }
            else
            {
                _executionQueue.Enqueue(queuedItem);
                execution = new WorkflowExecution
                {
                    WorkflowId = workflowId,
                    Status = WorkflowStatus.Draft,
                    TriggeredBy = triggeredBy
                };
                XTrace.Log.Info("[WorkflowScheduler] 工作流已排队，当前队列长度: {0}", _executionQueue.Count);
            }
        }

        return await Task.FromResult(execution);
    }

    private WorkflowExecution StartExecution(QueuedWorkflow queuedItem)
    {
        var execution = _executor.ExecuteAsync(
            queuedItem.WorkflowId,
            queuedItem.InputVariables,
            queuedItem.TriggeredBy).GetAwaiter().GetResult();

        _runningExecutions[execution.Id] = execution;

        ExecutionStarted?.Invoke(this, execution);

        XTrace.Log.Info("[WorkflowScheduler] 开始执行工作流，executionId: {0}, 当前运行数: {1}",
            execution.Id, _runningExecutions.Count);

        _ = MonitorExecutionAsync(execution.Id);

        return execution;
    }

    private async Task MonitorExecutionAsync(long executionId)
    {
        try
        {
            var lastProgress = -1.0;

            while (true)
            {
                var execution = await _executor.GetExecutionAsync(executionId);
                if (execution == null) break;

                if (Math.Abs(execution.Progress - lastProgress) > 0.1)
                {
                    lastProgress = execution.Progress;
                    ExecutionProgressChanged?.Invoke(this, execution);
                }

                if (execution.Status == WorkflowStatus.Completed)
                {
                    ExecutionCompleted?.Invoke(this, execution);
                    XTrace.Log.Info("[WorkflowScheduler] 工作流执行完成，executionId: {0}", executionId);
                    break;
                }

                if (execution.Status == WorkflowStatus.Failed)
                {
                    ExecutionFailed?.Invoke(this, execution);
                    XTrace.Log.Error("[WorkflowScheduler] 工作流执行失败，executionId: {0}, 错误: {1}",
                        executionId, execution.ErrorMessage);
                    break;
                }

                if (execution.Status == WorkflowStatus.Cancelled)
                {
                    XTrace.Log.Info("[WorkflowScheduler] 工作流已取消，executionId: {0}", executionId);
                    break;
                }

                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowScheduler] 监控工作流执行失败: {0}", ex.Message);
        }
        finally
        {
            lock (_lock)
            {
                _runningExecutions.Remove(executionId);

                while (_executionQueue.Count > 0 && _runningExecutions.Count < MaxConcurrentExecutions)
                {
                    if (_executionQueue.TryDequeue(out var nextItem))
                    {
                        StartExecution(nextItem);
                    }
                }
            }
        }
    }

    public async Task PauseExecutionAsync(long executionId)
    {
        XTrace.Log.Info("[WorkflowScheduler] 暂停工作流执行，executionId: {0}", executionId);
        await _executor.PauseAsync(executionId);
    }

    public async Task ResumeExecutionAsync(long executionId)
    {
        XTrace.Log.Info("[WorkflowScheduler] 继续工作流执行，executionId: {0}", executionId);
        await _executor.ResumeAsync(executionId);
    }

    public async Task CancelExecutionAsync(long executionId)
    {
        XTrace.Log.Info("[WorkflowScheduler] 取消工作流执行，executionId: {0}", executionId);
        await _executor.CancelAsync(executionId);
    }

    public async Task<WorkflowExecution?> GetExecutionAsync(long executionId)
    {
        return await _executor.GetExecutionAsync(executionId);
    }

    public List<WorkflowExecution> GetRunningExecutions()
    {
        lock (_lock)
        {
            return _runningExecutions.Values.ToList();
        }
    }

    public List<WorkflowExecution> GetQueuedExecutions()
    {
        lock (_lock)
        {
            return _executionQueue.Select(q => new WorkflowExecution
            {
                WorkflowId = q.WorkflowId,
                Status = WorkflowStatus.Draft,
                TriggeredBy = q.TriggeredBy,
                StartTime = q.QueuedAt
            }).ToList();
        }
    }

    private class QueuedWorkflow
    {
        public long WorkflowId { get; set; }
        public Dictionary<string, object?>? InputVariables { get; set; }
        public string? TriggeredBy { get; set; }
        public DateTime QueuedAt { get; set; }
    }
}
