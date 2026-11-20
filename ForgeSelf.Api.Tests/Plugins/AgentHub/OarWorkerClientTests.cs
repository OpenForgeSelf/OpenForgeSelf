using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// P1：OarWorkerClient ↔ RuntimeWorker 真实 stdio 协议面。
/// 需要 Node ≥24 与已构建的 Worker <c>dist/index.js</c>；否则整类 Skip。
/// </summary>
public class OarWorkerClientTests
{
    private static readonly Lazy<(bool Ok, string Reason, string? Node, string? Entry)> Env = new(ResolveEnv);

    public static bool WorkerReady => Env.Value.Ok;

    [Fact]
    public async Task Hello_与_ListRuntimes_含_scripted()
    {
        if (!WorkerReady)
            throw Xunit.Sdk.SkipException.ForSkip(Env.Value.Reason);

        await using var client = CreateClient();
        var started = await client.EnsureStartedAsync();
        started.Should().BeTrue(client.UnavailableReason);
        client.IsAvailable.Should().BeTrue();

        var hello = client.LastHello;
        hello.Should().NotBeNull();
        hello!.ProtocolVersion.Should().Be(1);
        hello.Oar.Should().Be("0.109.0");

        var runtimes = await client.ListRuntimesAsync();
        runtimes.Should().Contain(r => r.Id == "scripted");
        runtimes.Select(r => r.Id).Should().Contain(new[] { "claude", "codex", "opencode" });
    }

    [Fact]
    public async Task Scripted_Open_Prompt_Dispose_然后结束进程()
    {
        if (!WorkerReady)
            throw Xunit.Sdk.SkipException.ForSkip(Env.Value.Reason);

        await using var client = CreateClient();
        (await client.EnsureStartedAsync()).Should().BeTrue(client.UnavailableReason);

        var events = new List<string>();
        client.SessionEventReceived += _ => events.Add("event");

        var cwd = Path.GetTempPath();
        var open = await client.OpenSessionAsync("scripted", cwd);
        open.SessionKey.Should().NotBeNullOrWhiteSpace();
        open.Generation.Should().BeGreaterThanOrEqualTo(1);

        var prompt = await client.PromptAsync(open.SessionKey, "ping-from-dotnet");
        prompt.Accepted.Should().BeTrue($"code={prompt.Code} reason={prompt.Reason}");

        // 事件通知为尽力而为；给一小段时间
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (events.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        await client.DisposeSessionAsync(open.SessionKey);
        await client.DisposeAsync();

        client.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task 缺少入口时_EnsureStarted_返回false且可解释()
    {
        var client = new OarWorkerClient(new OarWorkerClientOptions
        {
            NodeExecutable = Env.Value.Node ?? "node",
            WorkerEntryPath = Path.Combine(Path.GetTempPath(), "no-such-oar-worker-entry.js"),
        });

        try
        {
            var ok = await client.EnsureStartedAsync();
            ok.Should().BeFalse();
            client.IsAvailable.Should().BeFalse();
            client.UnavailableReason.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    private static OarWorkerClient CreateClient()
    {
        var (_, _, node, entry) = Env.Value;
        return new OarWorkerClient(new OarWorkerClientOptions
        {
            NodeExecutable = node!,
            WorkerEntryPath = entry!,
            WorkingDirectory = Path.GetDirectoryName(entry),
            RequestTimeout = TimeSpan.FromSeconds(45),
            StartupTimeout = TimeSpan.FromSeconds(30),
        });
    }

    private static (bool Ok, string Reason, string? Node, string? Entry) ResolveEnv()
    {
        var repo = FindRepoRoot();
        if (repo == null)
            return (false, "找不到仓库根（ForgeSelf.slnx）", null, null);

        var entry = Path.Combine(repo, "Plugins", "AgentHub", "RuntimeWorker", "dist", "index.js");
        if (!File.Exists(entry))
            return (false, $"Worker 未构建：缺少 {entry}", null, null);

        var node = FindNode24();
        if (node == null)
            return (false, "未找到 Node ≥24（PATH 或 /tmp/node24）", null, entry);

        return (true, "ok", node, entry);
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    private static string? FindNode24()
    {
        var candidates = new List<string>();
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var n = Path.Combine(segment, OperatingSystem.IsWindows() ? "node.exe" : "node");
            if (File.Exists(n)) candidates.Add(n);
        }

        var tmp = "/tmp/node24/node-v24.11.0-linux-x64/bin/node";
        if (File.Exists(tmp)) candidates.Insert(0, tmp);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var nvmNode = Path.Combine(home, ".nvm", "versions", "node", "v24.22.3", "bin", "node");
        if (File.Exists(nvmNode)) candidates.Insert(0, nvmNode);

        foreach (var c in candidates.Distinct(StringComparer.Ordinal))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = c,
                    ArgumentList = { "-e", "const m=+process.versions.node.split('.')[0]; process.exit(m>=24?0:2)" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null) continue;
                if (!p.WaitForExit(5000))
                {
                    try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                    continue;
                }

                if (p.ExitCode == 0) return c;
            }
            catch
            {
                // try next
            }
        }

        return null;
    }
}
