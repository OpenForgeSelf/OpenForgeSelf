using Microsoft.AspNetCore.SignalR;
using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;
using OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Hubs;

public class MonitorHub : Hub
{
    private readonly ICpuMonitorService _cpuMonitorService;
    private readonly IMemoryMonitorService _memoryMonitorService;
    private readonly IDiskMonitorService _diskMonitorService;
    private readonly INetworkMonitorService _networkMonitorService;
    private readonly IProcessMonitorService _processMonitorService;

    private static readonly HashSet<string> ConnectedConnections = new();
    private static readonly Dictionary<string, List<string>> Subscriptions = new();
    private static int _updateIntervalMs = 1000;
    private static bool _isRunning;
    private static CancellationTokenSource? _cts;
    private static Task? _broadcastTask;
    private static readonly object _lock = new();

    public MonitorHub(
        ICpuMonitorService cpuMonitorService,
        IMemoryMonitorService memoryMonitorService,
        IDiskMonitorService diskMonitorService,
        INetworkMonitorService networkMonitorService,
        IProcessMonitorService processMonitorService)
    {
        _cpuMonitorService = cpuMonitorService;
        _memoryMonitorService = memoryMonitorService;
        _diskMonitorService = diskMonitorService;
        _networkMonitorService = networkMonitorService;
        _processMonitorService = processMonitorService;
    }

    public override async Task OnConnectedAsync()
    {
        lock (_lock)
        {
            ConnectedConnections.Add(Context.ConnectionId);
            Subscriptions[Context.ConnectionId] = new List<string>
            {
                "cpu", "memory", "disks", "network", "processes"
            };
        }

        XTrace.Log.Info("[MonitorHub] 客户端连接: {0}", Context.ConnectionId);

        StartBroadcasting();

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        lock (_lock)
        {
            ConnectedConnections.Remove(Context.ConnectionId);
            Subscriptions.Remove(Context.ConnectionId);

            if (ConnectedConnections.Count == 0)
            {
                StopBroadcasting();
            }
        }

        XTrace.Log.Info("[MonitorHub] 客户端断开连接: {0}", Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task Subscribe(string dataType)
    {
        lock (_lock)
        {
            if (Subscriptions.TryGetValue(Context.ConnectionId, out var subs))
            {
                if (!subs.Contains(dataType))
                {
                    subs.Add(dataType);
                }
            }
        }

        XTrace.Log.Debug("[MonitorHub] 客户端 {0} 订阅: {1}", Context.ConnectionId, dataType);
        await Task.CompletedTask;
    }

    public async Task Unsubscribe(string dataType)
    {
        lock (_lock)
        {
            if (Subscriptions.TryGetValue(Context.ConnectionId, out var subs))
            {
                subs.Remove(dataType);
            }
        }

        XTrace.Log.Debug("[MonitorHub] 客户端 {0} 取消订阅: {1}", Context.ConnectionId, dataType);
        await Task.CompletedTask;
    }

    public async Task SetUpdateInterval(int intervalMs)
    {
        if (intervalMs < 100)
            intervalMs = 100;
        if (intervalMs > 60000)
            intervalMs = 60000;

        _updateIntervalMs = intervalMs;
        XTrace.Log.Debug("[MonitorHub] 更新间隔设置为: {0}ms", intervalMs);
        await Task.CompletedTask;
    }

    public async Task SetPageVisible(bool isVisible)
    {
        if (isVisible)
        {
            _updateIntervalMs = 1000;
        }
        else
        {
            _updateIntervalMs = 5000;
        }

        XTrace.Log.Debug("[MonitorHub] 页面可见性: {0}, 更新间隔: {1}ms", isVisible, _updateIntervalMs);
        await Task.CompletedTask;
    }

    private static void StartBroadcasting()
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

    private static void StopBroadcasting()
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

    private static async Task BroadcastLoop(CancellationToken cancellationToken)
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

    private static async Task BroadcastMonitorDataAsync()
    {
        var context = GetHubContext();
        if (context == null) return;

        List<string> connections;
        Dictionary<string, List<string>> subs;

        lock (_lock)
        {
            connections = ConnectedConnections.ToList();
            subs = new Dictionary<string, List<string>>(Subscriptions);
        }

        if (connections.Count == 0) return;

        var serviceProvider = GetServiceProvider();
        if (serviceProvider == null) return;

        using var scope = serviceProvider.CreateScope();

        try
        {
            var cpuService = scope.ServiceProvider.GetService<ICpuMonitorService>();
            var memoryService = scope.ServiceProvider.GetService<IMemoryMonitorService>();
            var diskService = scope.ServiceProvider.GetService<IDiskMonitorService>();
            var networkService = scope.ServiceProvider.GetService<INetworkMonitorService>();

            var cpuData = cpuService != null ? await cpuService.GetCpuUsageAsync() : null;
            var memoryData = memoryService != null ? await memoryService.GetMemoryUsageAsync() : null;
            var disksData = diskService != null ? await diskService.GetDiskDrivesAsync() : null;
            var networkData = networkService != null ? await networkService.GetNetworkSpeedAsync() : null;

            var overview = new SystemOverview
            {
                Cpu = cpuData ?? new CpuUsage(),
                Memory = memoryData ?? new MemoryInfo(),
                Disks = disksData ?? new List<DiskDriveInfo>(),
                Network = networkData ?? new NetworkSpeedInfo(),
                Timestamp = DateTime.Now
            };

            foreach (var connId in connections)
            {
                if (!subs.TryGetValue(connId, out var subList)) continue;

                try
                {
                    if (subList.Contains("cpu") && cpuData != null)
                    {
                        await context.Clients.Client(connId).SendAsync("ReceiveCpuData", cpuData);
                    }

                    if (subList.Contains("memory") && memoryData != null)
                    {
                        await context.Clients.Client(connId).SendAsync("ReceiveMemoryData", memoryData);
                    }

                    if (subList.Contains("disks") && disksData != null)
                    {
                        await context.Clients.Client(connId).SendAsync("ReceiveDisksData", disksData);
                    }

                    if (subList.Contains("network") && networkData != null)
                    {
                        await context.Clients.Client(connId).SendAsync("ReceiveNetworkData", networkData);
                    }

                    await context.Clients.Client(connId).SendAsync("ReceiveOverview", overview);
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

    private static IHubContext<MonitorHub>? GetHubContext()
    {
        var sp = GetServiceProvider();
        return sp?.GetService<IHubContext<MonitorHub>>();
    }

    private static IServiceProvider? _serviceProvider;

    public static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private static IServiceProvider? GetServiceProvider()
    {
        return _serviceProvider;
    }
}
