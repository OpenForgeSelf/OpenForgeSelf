using System.Diagnostics;
using System.IO;
using System.Linq;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Plugins.Sems.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Moq;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// <see cref="RunnerService"/> 行为测试：启动真实短命进程→有 PID；停止后进程退出且会话移除；
/// 重复启动拒绝。使用临时 SQLite 库（XCode）并通过 Mock IContext 注入 HostProjectRegistry。
/// </summary>
[Collection("XCode")]
public class RunnerServiceTests : IDisposable
{
    private readonly string _dbDir;
    private readonly string _hostDataDir;
    private readonly string _projectRoot;
    private readonly HostProjectRegistry _registry;
    private readonly Mock<IContext> _ctx = new();
    private readonly RunnerService _runner;

    public RunnerServiceTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfRun_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);
        _hostDataDir = Path.Combine(_dbDir, "hostdata");
        Directory.CreateDirectory(_hostDataDir);

        // 真实存在的项目根（RunnerService 在 WorkingDirectory 启动进程需要目录存在）
        _projectRoot = Path.Combine(_dbDir, "proj");
        Directory.CreateDirectory(_projectRoot);

        DAL.AddConnStr("ForgeSelf", $"Data Source={Path.Combine(_dbDir, "ForgeSelf.db")}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");
        Project.Meta.Cache.Clear("test reset");
        RunCommand.Meta.Cache.Clear("test reset");
        Project.Meta.Cache.Expire = 0;
        RunCommand.Meta.Cache.Expire = 0;

        _registry = new HostProjectRegistry(_hostDataDir);
        _registry.Register(_projectRoot, out _);

        _ctx.Setup(c => c.Get<IProjectRegistry>()).Returns(_registry);
        _runner = new RunnerService(_ctx.Object);
    }

    private int AddCommand(string name, string script)
    {
        var projectId = _registry.GetAll().Single().Id;
        return _registry.AddCommand(projectId, new RunCommandInfo
        {
            Name = name,
            Script = script,
            Sort = 0
        });
    }

    /// <summary>保持进程活过断言窗口。Windows 用 ping，其他系统用 sleep。</summary>
    private static string HoldScript(int seconds) =>
        OperatingSystem.IsWindows()
            ? $"ping -n {Math.Max(2, seconds)} 127.0.0.1"
            : $"sleep {Math.Max(2, seconds)}";

    private static ProcessStartInfo HoldProcess(int seconds)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo("cmd.exe", $"/c {HoldScript(seconds)}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
        }

        var psi = new ProcessStartInfo("/bin/sh")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(HoldScript(seconds));
        return psi;
    }

    public void Dispose()
    {
        try { _runner.StopAll(); } catch { }
    }

    [Fact]
    public void Launch_Starts_ShortLivedProcess_And_Returns_Session_With_Pid()
    {
        var cmdId = AddCommand("hold", HoldScript(20));

        var session = _runner.Launch(cmdId);

        Assert.NotNull(session);
        Assert.True(session!.Pid > 0);
        Assert.Equal(cmdId, session.CommandId);
        Assert.Equal("Launched", session.Origin);
        Assert.True(session.StartedAt != default);

        // 进程应已在运行
        var proc = Process.GetProcessById(session.Pid);
        Assert.False(proc.HasExited);
        proc.Dispose();
    }

    [Fact]
    public void Launch_Then_Stop_Removes_Session_And_Process_Exits()
    {
        var cmdId = AddCommand("hold", HoldScript(20));

        var session = _runner.Launch(cmdId);
        Assert.NotNull(session);

        var result = _runner.Stop(cmdId);
        Assert.Equal(StopOutcome.Stopped, result.Outcome);

        // 会话应被移除
        Assert.DoesNotContain(_runner.Current(), s => s.Pid == session!.Pid);

        // 进程应已退出（cmd 已被杀 → GetProcessById 可能立即抛，视为已退出）
        try
        {
            using var proc = Process.GetProcessById(session!.Pid);
            Assert.True(proc.WaitForExit(3000) || proc.HasExited);
        }
        catch (ArgumentException)
        {
            // 进程已不存在，符合「已退出」预期
        }
    }

    [Fact]
    public void Launch_Duplicate_ForSameCommand_Rejected_ReturnsNull()
    {
        var cmdId = AddCommand("hold", HoldScript(20));

        var first = _runner.Launch(cmdId);
        Assert.NotNull(first);

        var second = _runner.Launch(cmdId);
        Assert.Null(second);
        Assert.Contains("存活会话", _runner.LastLaunchError);

        _runner.Stop(cmdId);
    }

    /// <summary>PILOT-087：会话不存在（进程早已退出 / 宿主重启过）→ AlreadyStopped，不算失败。</summary>
    [Fact]
    public void Stop_UnknownCommand_ReturnsAlreadyStopped()
    {
        var result = _runner.Stop(99999);
        Assert.Equal(StopOutcome.AlreadyStopped, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    /// <summary>PILOT-087：真实进程「启动→停止→再停止」第二次必须返回 AlreadyStopped（进程已不在）。</summary>
    [Fact]
    public void Stop_SecondCall_AfterStopped_ReturnsAlreadyStopped()
    {
        var cmdId = AddCommand("hold", HoldScript(20));
        var session = _runner.Launch(cmdId);
        Assert.NotNull(session);

        Assert.Equal(StopOutcome.Stopped, _runner.Stop(cmdId).Outcome);
        var again = _runner.Stop(cmdId);
        Assert.Equal(StopOutcome.AlreadyStopped, again.Outcome);
        Assert.DoesNotContain(_runner.Current(), s => s.Pid == session!.Pid);
    }

    [Fact]
    public void Current_Empty_When_NothingLaunched()
    {
        Assert.Empty(_runner.Current());
    }

    [Fact]
    public void StopExternal_Kills_ExternalProcess()
    {
        // 启动一个外部进程（不经 RunnerService.Launch），然后用 StopExternal 杀掉
        var psi = HoldProcess(20);
        using var ext = Process.Start(psi)!;
        var pid = ext.Id;
        Assert.False(ext.HasExited);

        var ok = _runner.StopExternal(pid);
        Assert.Equal(StopOutcome.Stopped, ok.Outcome);

        try { Assert.True(ext.WaitForExit(3000) || ext.HasExited); }
        finally { ext.Dispose(); }

        // PILOT-087：进程已死后再停一次 → AlreadyStopped（不算失败，可区分反馈）
        var again = _runner.StopExternal(pid);
        Assert.Equal(StopOutcome.AlreadyStopped, again.Outcome);
    }

    /// <summary>PILOT-087：不存在的 PID → AlreadyStopped（本就不在运行），而非失败。</summary>
    [Fact]
    public void StopExternal_UnknownPid_ReturnsAlreadyStopped()
    {
        // 用一个极大 PID（几乎肯定不存在）验证「已不在运行」归类而非抛异常
        var ok = _runner.StopExternal(int.MaxValue);
        Assert.Equal(StopOutcome.AlreadyStopped, ok.Outcome);
    }

    /// <summary>PILOT-087：非法 PID → Failed 带原因（不抛异常）。</summary>
    [Fact]
    public void StopExternal_InvalidPid_ReturnsFailed()
    {
        var ok = _runner.StopExternal(-1);
        Assert.Equal(StopOutcome.Failed, ok.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(ok.Reason));
    }

    [Fact]
    public async Task GetLogs_Captures_Stdout_From_Short_Command()
    {
        var cmdId = AddCommand("echo", "echo sems-log-marker");

        var session = _runner.Launch(cmdId);
        Assert.NotNull(session);

        await Task.Delay(2000);

        var snap = _runner.GetLogs(cmdId, 0);
        Assert.NotNull(snap);
        Assert.True(snap!.Lines.Count > 0);
        Assert.Contains(snap.Lines, l => l.Text.Contains("sems-log-marker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CheckAndMerge_DetectsExternalProcess_ByWorkingDirectory()
    {
        if (!Directory.Exists("/proc")) return;

        var project = _registry.GetAll().Single();
        Assert.True(_registry.Update(project.Id, new ProjectUpdate { Type = "tool" }));

        var psi = HoldProcess(30);
        psi.WorkingDirectory = _projectRoot;
        using var ext = Process.Start(psi)!;
        try
        {
            Assert.False(ext.HasExited);
            var runs = _runner.CheckAndMerge();
            Assert.Contains(runs, s => s.Pid == ext.Id && s.Origin == "Detected" && s.ProjectId == project.Id);
            Assert.Contains(_runner.Current(), s => s.Pid == ext.Id && s.Origin == "Detected");
        }
        finally
        {
            try { ext.Kill(entireProcessTree: true); } catch { /* 已退出 */ }
        }
    }
}
