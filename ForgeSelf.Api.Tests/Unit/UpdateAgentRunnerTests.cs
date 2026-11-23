using ForgeSelf.UpdateAgent;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 原生更新代理（ForgeSelf.UpdateAgent）单测（2026-11-21 输入15）。
/// 进程内直调 RunAsync 走真实全流程（不走 exe 子进程——沙箱按进程身份拒写本机新构建
/// 未签名 exe；exe 侧另有零写冒烟）。临时探针不入库，结论沉淀为本可重复测试。
/// </summary>
public class UpdateAgentRunnerTests : IDisposable
{
    private readonly string _root;

    public UpdateAgentRunnerTests()
    {
        _root = Directory.CreateTempSubdirectory("updateagent-test-").FullName;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* 清理失败不影响结论 */ }
        GC.SuppressFinalize(this);
    }

    // ────────────── UpdateAgentCore：PC-041 补零定宽排序 ──────────────

    [Fact]
    public void ToSortKey_十位时间码第四段_补零定宽不溢出()
    {
        // 2611211942 > int.MaxValue —— [version] 解析必抛，补零字符串比较必须安然
        var key = UpdateAgentCore.ToSortKey("2.4.22.2611211942");
        key.Should().Be("000000000002|000000000004|000000000022|002611211942");
    }

    [Fact]
    public void CompareVersionNames_时间码段大小即序()
    {
        UpdateAgentCore.CompareVersionNames("2.4.22.2611211942", "2.4.22.2611211941").Should().BePositive();
        UpdateAgentCore.CompareVersionNames("2.4.22.2611211942", "2.4.22.2611211942").Should().Be(0);
        UpdateAgentCore.CompareVersionNames("2.4.9.9999999999", "2.4.10.1").Should().BeNegative();
    }

    [Fact]
    public void CompareVersionNames_段数不一致_短串在前()
    {
        UpdateAgentCore.CompareVersionNames("2.4", "2.4.1").Should().BeNegative();
        UpdateAgentCore.CompareVersionNames("2.4.0.0", "2.4").Should().BePositive();
    }

    [Fact]
    public void CompareVersionNames_null与空串_视为最旧()
    {
        UpdateAgentCore.CompareVersionNames(null, "1.0.0").Should().BeNegative();
        UpdateAgentCore.CompareVersionNames("", "1.0.0").Should().BeNegative();
    }

    [Fact]
    public void SortNewestFirst_乱序列表_补零序从新到旧()
    {
        var list = new[] { "2.4.9.999", "2.4.10.1", "2.4.10.0", "2.3.1.1" };
        UpdateAgentCore.SortNewestFirst(list)
            .Should().ContainInOrder("2.4.10.1", "2.4.10.0", "2.4.9.999", "2.3.1.1");
    }

    [Fact]
    public void UpdateAgentOptions_Parse_缺参数_抛ArgumentException()
    {
        var act = () => UpdateAgentOptions.Parse(Array.Empty<string>());
        act.Should().Throw<ArgumentException>().WithMessage("*host-pid*");
    }

    // ────────────── 进程内全流程 E2E：等退出→落位→切 current→保留→清理→重启 ──────────────

    [Fact]
    public async Task RunAsync_全流程_落位切指针清理并重启()
    {
        // staged（QQNT 布局：解压根公共层 + versions/<newVer>/ 业务层）
        var newVer = "2.4.23.2611221200";
        var updatesRoot = Path.Combine(_root, "Updates");
        var stagedDir = Path.Combine(updatesRoot, "v2.4.23.2611221200", "extracted");
        var stagedVersion = Path.Combine(stagedDir, "versions", newVer);
        Directory.CreateDirectory(stagedVersion);
        Directory.CreateDirectory(Path.Combine(stagedDir, "host"));
        Directory.CreateDirectory(Path.Combine(stagedDir, "plugins"));
        // 假启动器必须是真可执行（重启步骤 Process.Start；cmd 副本 + /c exit 瞬时退出）
        File.Copy(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            Path.Combine(stagedDir, "ForgeSelf.exe"), overwrite: true);
        File.WriteAllText(Path.Combine(stagedDir, "update-agent.exe"), "new-agent");
        File.WriteAllText(Path.Combine(stagedDir, "host", "hostfxr.dll"), "rt");
        File.WriteAllText(Path.Combine(stagedVersion, "ForgeSelf.Api.dll"), "new-api");
        File.WriteAllText(Path.Combine(stagedVersion, "ForgeSelf.Api.deps.json"), "{}");
        File.WriteAllText(Path.Combine(updatesRoot, "v2.4.23.2611221200", "update.zip"), "zip");

        // 安装根（公共层 + 旧版本两代 + 扁平残留）
        var installDir = Path.Combine(_root, "install");
        var versionsRoot = Path.Combine(installDir, "versions");
        var oldVer = "2.4.22.2611211942";
        var olderVer = "2.4.21.2611201000";
        Directory.CreateDirectory(Path.Combine(versionsRoot, oldVer));
        Directory.CreateDirectory(Path.Combine(versionsRoot, olderVer));
        Directory.CreateDirectory(Path.Combine(installDir, "plugins"));
        File.WriteAllText(Path.Combine(installDir, "ForgeSelf.exe"), "old-launcher");
        File.WriteAllText(Path.Combine(installDir, "update-agent.ps1"), "old-ps1");
        File.WriteAllText(Path.Combine(installDir, "ForgeSelf.Api.dll"), "stale-flat");
        File.WriteAllText(Path.Combine(versionsRoot, oldVer, "ForgeSelf.Api.dll"), "old-api");
        // current 指向旧版本（应用后应为 newVer）
        File.WriteAllText(Path.Combine(versionsRoot, "current"), oldVer);

        var options = new UpdateAgentOptions(
            HostPid: int.MaxValue, // 不存在的 PID → 视为已退出（不阻塞测试）
            InstallDir: installDir,
            StagedDir: stagedDir,
            ExeName: "ForgeSelf.exe",
            ArgList: "/c;exit"); // 假启动器（cmd 副本）瞬时退出

        var exitCode = await UpdateAgentRunner.RunAsync(options);

        // 退出码
        exitCode.Should().Be(0, "等退出→落位→切 current→保留→清理→重启全链应成功；失败读 %LOCALAPPDATA%/ForgeSelf/Updates/ 下 agent-*.log");

        // 业务层落位 + 版本层接管
        File.Exists(Path.Combine(versionsRoot, newVer, "ForgeSelf.Api.dll")).Should().BeTrue();
        File.ReadAllText(Path.Combine(versionsRoot, newVer, "ForgeSelf.Api.dll")).Should().Be("new-api");

        // 公共层合并覆盖（版本目录之外）：根启动器被新 zip 的可执行副本替换；ps1 保留随包
        new FileInfo(Path.Combine(installDir, "ForgeSelf.exe")).Length.Should().BeGreaterThan(10000,
            "根启动器应被 staged 公共层合并覆盖为真可执行副本");
        File.ReadAllText(Path.Combine(installDir, "update-agent.exe")).Should().Be("new-agent");
        File.ReadAllText(Path.Combine(installDir, "update-agent.ps1")).Should().Be("old-ps1");
        File.ReadAllText(Path.Combine(installDir, "host", "hostfxr.dll")).Should().Be("rt");

        // /XD versions：旧版本目录不被公共层合并覆盖
        File.ReadAllText(Path.Combine(versionsRoot, oldVer, "ForgeSelf.Api.dll")).Should().Be("old-api");

        // current 原子切换
        File.ReadAllText(Path.Combine(versionsRoot, "current")).Should().Be(newVer);

        // 版本保留 = current + 补零定宽序上一版；更旧删除
        Directory.GetDirectories(versionsRoot).Select(Path.GetFileName)
            .Should().BeEquivalentTo(new[] { newVer, oldVer });

        // 扁平残留：被版本层接管的旧 dll 清掉
        File.Exists(Path.Combine(installDir, "ForgeSelf.Api.dll")).Should().BeFalse();

        // staged tag 清理（zip + extracted）
        Directory.Exists(stagedDir).Should().BeFalse();
        File.Exists(Path.Combine(updatesRoot, "v2.4.23.2611221200", "update.zip")).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_安装根在versions内_拒绝并报嵌套()
    {
        var installDir = Path.Combine(_root, "install", "versions", "2.4.22.2611211942");
        Directory.CreateDirectory(installDir);
        var stagedDir = Path.Combine(_root, "staged");
        Directory.CreateDirectory(Path.Combine(stagedDir, "versions", "2.4.23.2611221200"));
        File.WriteAllText(Path.Combine(stagedDir, "versions", "2.4.23.2611221200", "a.dll"), "x");

        var options = new UpdateAgentOptions(1, installDir, stagedDir, "ForgeSelf.exe", string.Empty);

        var act = async () => await UpdateAgentRunner.RunAsync(options);
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*逐代嵌套*");
    }

    [Fact]
    public async Task RunAsync_staged缺versions_报错不落位()
    {
        var installDir = Path.Combine(_root, "install");
        Directory.CreateDirectory(installDir);
        var stagedDir = Path.Combine(_root, "staged-empty");
        Directory.CreateDirectory(stagedDir);

        var options = new UpdateAgentOptions(1, installDir, stagedDir, "ForgeSelf.exe", string.Empty);
        var act = async () => await UpdateAgentRunner.RunAsync(options);
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*versions*");
        Directory.Exists(Path.Combine(installDir, "versions", "2.4.23.2611221200")).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_同版本重装_robocopy覆盖不炸()
    {
        var ver = "2.4.22.2611211942";
        var updatesRoot = Path.Combine(_root, "Updates");
        var stagedDir = Path.Combine(updatesRoot, "tag", "extracted");
        Directory.CreateDirectory(Path.Combine(stagedDir, "versions", ver));
        File.WriteAllText(Path.Combine(stagedDir, "versions", ver, "ForgeSelf.Api.dll"), "reinstalled");
        File.Copy(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            Path.Combine(stagedDir, "ForgeSelf.exe"), overwrite: true);

        var installDir = Path.Combine(_root, "install");
        Directory.CreateDirectory(Path.Combine(installDir, "versions", ver));
        File.WriteAllText(Path.Combine(installDir, "versions", ver, "ForgeSelf.Api.dll"), "old");
        File.Copy(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            Path.Combine(installDir, "ForgeSelf.exe"), overwrite: true);

        var options = new UpdateAgentOptions(int.MaxValue, installDir, stagedDir, "ForgeSelf.exe", ArgList: "/c;exit");
        var exitCode = await UpdateAgentRunner.RunAsync(options);

        exitCode.Should().Be(0);
        File.ReadAllText(Path.Combine(installDir, "versions", ver, "ForgeSelf.Api.dll")).Should().Be("reinstalled");
    }

    [Fact]
    public async Task RunAsync_参数含分号ArgList_重启原样回传()
    {
        // 重启时 ArgList 以分号拆回原参数——用假启动器（cmd 副本）验证参数回传不抛
        var ver = "2.4.23.2611221200";
        var stagedDir = Path.Combine(_root, "staged");
        Directory.CreateDirectory(Path.Combine(stagedDir, "versions", ver));
        File.WriteAllText(Path.Combine(stagedDir, "versions", ver, "a.dll"), "x");
        var fakeLauncher = Path.Combine(_root, "ForgeSelf.exe");
        File.Copy(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", fakeLauncher, overwrite: true);

        var installDir = _root; // 安装根=测试根（不在 versions 内；公共层合并后启动器已在位）

        var options = new UpdateAgentOptions(
            int.MaxValue, installDir, stagedDir, "ForgeSelf.exe", ArgList: "/c;exit");
        var exitCode = await UpdateAgentRunner.RunAsync(options);

        exitCode.Should().Be(0, "假启动器 cmd /c exit 应瞬时退出且全链成功");
    }
}
