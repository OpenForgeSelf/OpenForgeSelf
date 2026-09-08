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

    public void Dispose()
    {
        try { _runner.StopAll(); } catch { }
        try { if (Directory.Exists(_dbDir)) Directory.Delete(_dbDir, true); } catch { }
    }

    [Fact]
    public void Launch_Starts_ShortLivedProcess_And_Returns_Session_With_Pid()
    {
        var cmdId = AddCommand("ping", "ping -n 2 localhost");

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
        var cmdId = AddCommand("ping", "ping -n 20 localhost"); // 长一点，便于先停

        var session = _runner.Launch(cmdId);
        Assert.NotNull(session);

        var ok = _runner.Stop(cmdId);
        Assert.True(ok);

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
        var cmdId = AddCommand("ping", "ping -n 20 localhost");

        var first = _runner.Launch(cmdId);
        Assert.NotNull(first);

        var second = _runner.Launch(cmdId);
        Assert.Null(second);

        _runner.Stop(cmdId);
    }

    [Fact]
    public void Stop_UnknownCommand_ReturnsFalse()
    {
        Assert.False(_runner.Stop(99999));
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
        var psi = new ProcessStartInfo("cmd.exe", "/c ping -n 20 localhost")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        using var ext = Process.Start(psi)!;
        var pid = ext.Id;
        Assert.False(ext.HasExited);

        var ok = _runner.StopExternal(pid);
        Assert.True(ok);

        try { Assert.True(ext.WaitForExit(3000) || ext.HasExited); }
        finally { ext.Dispose(); }
    }

    [Fact]
    public void StopExternal_UnknownPid_ReturnsFalse()
    {
        // 用一个极大 PID（几乎肯定不存在）验证失败返回 false 而非抛异常
        var ok = _runner.StopExternal(int.MaxValue);
        Assert.False(ok);
    }
}
