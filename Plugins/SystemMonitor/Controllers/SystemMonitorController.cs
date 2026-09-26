using Microsoft.AspNetCore.Mvc;
using ForgeSelf.Api.Plugins.SystemMonitor.Models;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.SystemMonitor.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SystemMonitor.Controllers;

[ApiController]
[Route("api/monitor")]
public class SystemMonitorController : ControllerBase
{
    private readonly ICpuMonitorService _cpuMonitorService;
    private readonly IMemoryMonitorService _memoryMonitorService;
    private readonly IDiskMonitorService _diskMonitorService;
    private readonly INetworkMonitorService _networkMonitorService;
    private readonly IProcessMonitorService _processMonitorService;

    public SystemMonitorController(
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

    [HttpGet("cpu")]
    public async Task<ActionResult<ApiResponse<CpuUsage>>> GetCpuUsage()
    {
        try
        {
            XTrace.Log.Debug("调用CPU使用率接口");
            var result = await _cpuMonitorService.GetCpuUsageAsync();
            return Ok(ApiResponse<CpuUsage>.Ok(result, "获取CPU使用率成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取CPU使用率失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<CpuUsage>.Error("获取CPU使用率失败: " + ex.Message));
        }
    }

    [HttpGet("cpu/history")]
    public async Task<ActionResult<ApiResponse<CpuHistoryData>>> GetCpuHistory([FromQuery] string duration = "1m")
    {
        try
        {
            XTrace.Log.Debug("调用CPU历史数据接口，持续时间: {0}", duration);
            var result = await _cpuMonitorService.GetCpuHistoryAsync(duration);
            return Ok(ApiResponse<CpuHistoryData>.Ok(result, "获取CPU历史数据成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取CPU历史数据失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<CpuHistoryData>.Error("获取CPU历史数据失败: " + ex.Message));
        }
    }

    [HttpGet("memory")]
    public async Task<ActionResult<ApiResponse<MemoryInfo>>> GetMemoryUsage()
    {
        try
        {
            XTrace.Log.Debug("调用内存使用率接口");
            var result = await _memoryMonitorService.GetMemoryUsageAsync();
            return Ok(ApiResponse<MemoryInfo>.Ok(result, "获取内存使用率成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取内存使用率失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<MemoryInfo>.Error("获取内存使用率失败: " + ex.Message));
        }
    }

    [HttpGet("memory/history")]
    public async Task<ActionResult<ApiResponse<MemoryHistoryData>>> GetMemoryHistory([FromQuery] string duration = "1m")
    {
        try
        {
            XTrace.Log.Debug("调用内存历史数据接口，持续时间: {0}", duration);
            var result = await _memoryMonitorService.GetMemoryHistoryAsync(duration);
            return Ok(ApiResponse<MemoryHistoryData>.Ok(result, "获取内存历史数据成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取内存历史数据失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<MemoryHistoryData>.Error("获取内存历史数据失败: " + ex.Message));
        }
    }

    [HttpGet("disks")]
    public async Task<ActionResult<ApiResponse<List<DiskDriveInfo>>>> GetDiskDrives()
    {
        try
        {
            XTrace.Log.Debug("调用磁盘分区列表接口");
            var result = await _diskMonitorService.GetDiskDrivesAsync();
            return Ok(ApiResponse<List<DiskDriveInfo>>.Ok(result, "获取磁盘分区列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取磁盘分区列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<DiskDriveInfo>>.Error("获取磁盘分区列表失败: " + ex.Message));
        }
    }

    [HttpGet("disks/{drive}/io")]
    public async Task<ActionResult<ApiResponse<DiskIOInfo>>> GetDiskIO(string drive)
    {
        try
        {
            XTrace.Log.Debug("调用磁盘IO接口，磁盘: {0}", drive);
            var result = await _diskMonitorService.GetDiskIOAsync(drive);
            return Ok(ApiResponse<DiskIOInfo>.Ok(result, "获取磁盘IO成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取磁盘IO失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<DiskIOInfo>.Error("获取磁盘IO失败: " + ex.Message));
        }
    }

    [HttpGet("disks/{drive}/history")]
    public async Task<ActionResult<ApiResponse<DiskHistoryData>>> GetDiskHistory(string drive, [FromQuery] string duration = "1m")
    {
        try
        {
            XTrace.Log.Debug("调用磁盘历史数据接口，磁盘: {0}, 持续时间: {1}", drive, duration);
            var result = await _diskMonitorService.GetDiskHistoryAsync(drive, duration);
            return Ok(ApiResponse<DiskHistoryData>.Ok(result, "获取磁盘历史数据成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取磁盘历史数据失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<DiskHistoryData>.Error("获取磁盘历史数据失败: " + ex.Message));
        }
    }

    [HttpGet("network")]
    public async Task<ActionResult<ApiResponse<NetworkSpeedInfo>>> GetNetworkSpeed()
    {
        try
        {
            XTrace.Log.Debug("调用网络速度接口");
            var result = await _networkMonitorService.GetNetworkSpeedAsync();
            return Ok(ApiResponse<NetworkSpeedInfo>.Ok(result, "获取网络速度成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取网络速度失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<NetworkSpeedInfo>.Error("获取网络速度失败: " + ex.Message));
        }
    }

    [HttpGet("network/connections")]
    public async Task<ActionResult<ApiResponse<List<NetworkConnectionInfo>>>> GetNetworkConnections()
    {
        try
        {
            XTrace.Log.Debug("调用网络连接列表接口");
            var result = await _networkMonitorService.GetNetworkConnectionsAsync();
            return Ok(ApiResponse<List<NetworkConnectionInfo>>.Ok(result, "获取网络连接列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取网络连接列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<NetworkConnectionInfo>>.Error("获取网络连接列表失败: " + ex.Message));
        }
    }

    [HttpGet("network/history")]
    public async Task<ActionResult<ApiResponse<NetworkHistoryData>>> GetNetworkHistory([FromQuery] string duration = "1m")
    {
        try
        {
            XTrace.Log.Debug("调用网络历史数据接口，持续时间: {0}", duration);
            var result = await _networkMonitorService.GetNetworkHistoryAsync(duration);
            return Ok(ApiResponse<NetworkHistoryData>.Ok(result, "获取网络历史数据成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取网络历史数据失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<NetworkHistoryData>.Error("获取网络历史数据失败: " + ex.Message));
        }
    }

    [HttpGet("processes")]
    public async Task<ActionResult<ApiResponse<ProcessListResult>>> GetProcesses(
        [FromQuery] string? sortBy = null,
        [FromQuery] bool ascending = false,
        [FromQuery] string? searchFilter = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            XTrace.Log.Debug("调用进程列表接口");
            var request = new ProcessListRequest
            {
                SortBy = sortBy,
                Ascending = ascending,
                SearchFilter = searchFilter,
                Page = page,
                PageSize = pageSize
            };
            var result = await _processMonitorService.GetProcessesAsync(request);
            return Ok(ApiResponse<ProcessListResult>.Ok(result, "获取进程列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取进程列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ProcessListResult>.Error("获取进程列表失败: " + ex.Message));
        }
    }

    [HttpGet("processes/{pid}")]
    public async Task<ActionResult<ApiResponse<ProcessInfo>>> GetProcessById(int pid)
    {
        try
        {
            XTrace.Log.Debug("调用进程详情接口，PID: {0}", pid);
            var result = await _processMonitorService.GetProcessByIdAsync(pid);
            if (result == null)
            {
                return NotFound(ApiResponse<ProcessInfo>.Error("进程不存在", 404));
            }
            return Ok(ApiResponse<ProcessInfo>.Ok(result, "获取进程详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取进程详情失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ProcessInfo>.Error("获取进程详情失败: " + ex.Message));
        }
    }

    [HttpDelete("processes/{pid}")]
    public async Task<ActionResult<ApiResponse<bool>>> KillProcess(int pid, [FromQuery] bool force = false)
    {
        try
        {
            XTrace.Log.Info("调用结束进程接口，PID: {0}, Force: {1}", pid, force);
            var result = await _processMonitorService.KillProcessAsync(pid, force);
            if (result)
            {
                return Ok(ApiResponse<bool>.Ok(true, "进程已结束"));
            }
            else
            {
                return BadRequest(ApiResponse<bool>.Error("结束进程失败", 400));
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("结束进程失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<bool>.Error("结束进程失败: " + ex.Message));
        }
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<SystemOverview>>> GetOverview()
    {
        try
        {
            XTrace.Log.Debug("调用系统概览接口");

            var cpuTask = _cpuMonitorService.GetCpuUsageAsync();
            var memoryTask = _memoryMonitorService.GetMemoryUsageAsync();
            var disksTask = _diskMonitorService.GetDiskDrivesAsync();
            var networkTask = _networkMonitorService.GetNetworkSpeedAsync();

            await Task.WhenAll(cpuTask, memoryTask, disksTask, networkTask);

            var processes = await _processMonitorService.GetProcessesAsync(new ProcessListRequest { Page = 1, PageSize = 1 });

            var overview = new SystemOverview
            {
                Cpu = cpuTask.Result,
                Memory = memoryTask.Result,
                Disks = disksTask.Result,
                Network = networkTask.Result,
                ProcessCount = processes.TotalCount,
                Timestamp = DateTime.Now
            };

            return Ok(ApiResponse<SystemOverview>.Ok(overview, "获取系统概览成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取系统概览失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<SystemOverview>.Error("获取系统概览失败: " + ex.Message));
        }
    }
}
