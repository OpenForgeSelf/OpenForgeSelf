using Microsoft.AspNetCore.SignalR;
using ForgeSelf.Api.Plugins.SystemMonitor.Models;
using ForgeSelf.Api.Plugins.SystemMonitor.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SystemMonitor.Hubs;

/// <summary>
/// 系统监控数据广播器（宿主 DI 单例）。
/// 持有 SignalR <see cref="IHubContext{MonitorHub}"/> 与连接/订阅路由状态，并负责后台实时广播循环。
/// 取代原先「静态 SetServiceProvider + 静态访问」模式：不再以静态字段长期持有插件 Fiber 上下文，
/// 避免插件卸载后因静态根引用导致 ALC 无法被 GC 回收。监控服务与 Hub 上下文均通过构造注入。
/// </summary>
public class MonitorBroadcaster
{
    private readonly IHubContext<MonitorHub> _hubContext;
    private readonly ICpuMonitorService _cpuMonitorService;
    private readonly IMemoryMonitorService _memoryMonitorService;
    private readonly IDiskMonitorService _diskMonitorService;
    private readonly INetworkMonitorService _networkMonitorService;

    private readonly HashSet<string> _connectedConnections = new();
    private readonly Dictionary<string, List<string>> _subscriptions = new();
    private int _updateIntervalMs = 1000;
    private bool _isRunning;
    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private readonly object _lock = new();

    public MonitorBroadcaster(
        IHubContext<MonitorHub> hubContext,
        ICpuMonitorService cpuMonitorService,
        IMemoryMonitorService memoryMonitorService,
        IDiskMonitorService diskMonitorService,
        INetworkMonitorService networkMonitorService)
    {
        _hubContext = hubContext;
        _cpuMonitorService = cpuMonitorService;
        _memoryMonitorService = memoryMonitorService;
        _diskMonitorService = diskMonitorService;
        _networkMonitorService = networkMonitorService;
    }

    public void OnConnected(string connectionId)
    {
        lock (_lock)
        {
            _connectedConnections.Add(connectionId);
            _subscriptions[connectionId] = new List<string>
            {
                "cpu", "memory", "disks", "network", "processes"
            };
        }

        StartBroadcasting();
    }

    public void OnDisconnected(string connectionId)
    {
        lock (_lock)
        {
            _connectedConnections.Remove(connectionId);
            _subscriptions.Remove(connectionId);

            if (_connectedConnections.Count == 0)
            {
                StopBroadcasting();
            }
        }
    }

    public void Subscribe(string connectionId, string dataType)
    {
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(connectionId, out var subs))
            {
                if (!subs.Contains(dataType))
                {
                    subs.Add(dataType);
                }
            }
        }
    }

    public void Unsubscribe(string connectionId, string dataType)
    {
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(connectionId, out var subs))
            {
                subs.Remove(dataType);
            }
        }
    }

    public void SetUpdateInterval(int intervalMs)
    {
        if (intervalMs < 100)
            intervalMs = 100;
        if (intervalMs > 60000)
            intervalMs = 60000;

        _updateIntervalMs = intervalMs;
    }

    public void SetPageVisible(bool isVisible)
    {
        _updateIntervalMs = isVisible ? 1000 : 5000;
    }

    private void StartBroadcasting()
    {
        lock (_lock)
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _broadcastTask = Task.Run(() => BroadcastLoop(_cts.Token));
            XTrace.Log.Info("[MonitorHub] 实时数据广播已启动");
        }
    }

    private void StopBroadcasting()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _cts?.Cancel();
            try
            {
                _broadcastTask?.Wait(2000);
            }
            catch { }
            _cts?.Dispose();
            _cts = null;
            _broadcastTask = null;
            XTrace.Log.Info("[MonitorHub] 实时数据广播已停止");
        }
    }

    private async Task BroadcastLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastMonitorDataAsync();
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[MonitorHub] 广播监控数据失败: {0}", ex.Message);
            }

            try
            {
                await Task.Delay(_updateIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task BroadcastMonitorDataAsync()
    {
        List<string> connections;
        Dictionary<string, List<string>> subs;

        lock (_lock)
        {
            connections = _connectedConnections.ToList();
            subs = new Dictionary<string, List<string>>(_subscriptions);
        }

        if (connections.Count == 0) return;

        try
        {
            var cpuData = await _cpuMonitorService.GetCpuUsageAsync();
            var memoryData = await _memoryMonitorService.GetMemoryUsageAsync();
            var disksData = await _diskMonitorService.GetDiskDrivesAsync();
            var networkData = await _networkMonitorService.GetNetworkSpeedAsync();

            var overview = new SystemOverview
            {
                Cpu = cpuData,
                Memory = memoryData,
                Disks = disksData,
                Network = networkData,
                Timestamp = DateTime.Now
            };

            foreach (var connId in connections)
            {
                if (!subs.TryGetValue(connId, out var subList)) continue;

                try
                {
                    if (subList.Contains("cpu"))
                    {
                        await _hubContext.Clients.Client(connId).SendAsync("ReceiveCpuData", cpuData);
                    }

                    if (subList.Contains("memory"))
                    {
                        await _hubContext.Clients.Client(connId).SendAsync("ReceiveMemoryData", memoryData);
                    }

                    if (subList.Contains("disks"))
                    {
                        await _hubContext.Clients.Client(connId).SendAsync("ReceiveDisksData", disksData);
                    }

                    if (subList.Contains("network"))
                    {
                        await _hubContext.Clients.Client(connId).SendAsync("ReceiveNetworkData", networkData);
                    }

                    await _hubContext.Clients.Client(connId).SendAsync("ReceiveOverview", overview);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Debug("[MonitorHub] 发送数据到客户端 {0} 失败: {1}", connId, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[MonitorHub] 获取监控数据失败: {0}", ex.Message);
        }
    }
}

public class MonitorHub : Hub
{
    private readonly MonitorBroadcaster _broadcaster;

    public MonitorHub(MonitorBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public override async Task OnConnectedAsync()
    {
        _broadcaster.OnConnected(Context.ConnectionId);

        XTrace.Log.Info("[MonitorHub] 客户端连接: {0}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _broadcaster.OnDisconnected(Context.ConnectionId);

        XTrace.Log.Info("[MonitorHub] 客户端断开连接: {0}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task Subscribe(string dataType)
    {
        _broadcaster.Subscribe(Context.ConnectionId, dataType);

        XTrace.Log.Debug("[MonitorHub] 客户端 {0} 订阅: {1}", Context.ConnectionId, dataType);
        await Task.CompletedTask;
    }

    public async Task Unsubscribe(string dataType)
    {
        _broadcaster.Unsubscribe(Context.ConnectionId, dataType);

        XTrace.Log.Debug("[MonitorHub] 客户端 {0} 取消订阅: {1}", Context.ConnectionId, dataType);
        await Task.CompletedTask;
    }

    public async Task SetUpdateInterval(int intervalMs)
    {
        _broadcaster.SetUpdateInterval(intervalMs);

        XTrace.Log.Debug("[MonitorHub] 更新间隔设置为: {0}ms", intervalMs);
        await Task.CompletedTask;
    }

    public async Task SetPageVisible(bool isVisible)
    {
        _broadcaster.SetPageVisible(isVisible);

        XTrace.Log.Debug("[MonitorHub] 页面可见性: {0}", isVisible);
        await Task.CompletedTask;
    }
}
