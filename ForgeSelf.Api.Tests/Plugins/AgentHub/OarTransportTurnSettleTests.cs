using System.Diagnostics;
using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Entities;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

[Collection("XCode")]
public class OarTransportTurnSettleTests : IClassFixture<XCodeTestFixture>
{
    public OarTransportTurnSettleTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        AgentHubSession.Meta.Cache.Clear("test reset");
        AgentRuntimeRecord.Meta.Cache.Clear("test reset");
    }

    [Fact]
    public async Task Scripted_hold_Exit不得早于turn结束()
    {
        if (!OarWorkerClientTests.WorkerReady)
            throw Xunit.Sdk.SkipException.ForSkip("OAR Worker/Node24 不可用");

        var repo = FindRepoRoot() ?? throw new InvalidOperationException("no repo");
        var entry = Path.Combine(repo, "Plugins", "AgentHub", "RuntimeWorker", "dist", "index.js");
        var node = FindNode24() ?? throw new InvalidOperationException("no node24");

        await using var client = new OarWorkerClient(new OarWorkerClientOptions
        {
            NodeExecutable = node,
            WorkerEntryPath = entry,
            WorkingDirectory = Path.GetDirectoryName(entry),
            RequestTimeout = TimeSpan.FromSeconds(45),
            StartupTimeout = TimeSpan.FromSeconds(30),
        });
        (await client.EnsureStartedAsync()).Should().BeTrue(client.UnavailableReason);

        var sessions = new AgentSessionManager();
        var store = new AgentRuntimeEventStore(sessions);
        var transport = new OarTransport(client, sessions, store);
        var ap = new AgentAccessPointDto { Id = 1, AgentId = 1, Transport = "Oar", RuntimeId = "scripted", IsDefault = true };
        var holdMs = 400;
        var req = new AgentRunRequest { Prompt = $"hold:{holdMs}", Cwd = Path.GetTempPath(), TimeoutMs = 30_000 };
        var session = new AgentSession { TaskId = 91001, AccessPointId = 1 };

        var sw = Stopwatch.StartNew();
        var events = new List<AgentEvent>();
        await foreach (var ev in transport.StreamAsync(ap, req, session, CancellationToken.None))
            events.Add(ev);
        sw.Stop();

        events.Should().Contain(e => e.Type == AgentEventTypes.Exit && e.ExitCode == 0);
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(holdMs - 50);
        events.Should().Contain(e => e.Type == AgentEventTypes.Text && (e.Text ?? "").Contains("echo:hold:", StringComparison.Ordinal));
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static string? FindNode24()
    {
        var candidates = new List<string>();
        var nvm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nvm", "versions", "node", "v24.22.3", "bin", "node");
        if (File.Exists(nvm)) candidates.Add(nvm);
        foreach (var segment in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var n = Path.Combine(segment, "node");
            if (File.Exists(n)) candidates.Add(n);
        }
        foreach (var c in candidates.Distinct(StringComparer.Ordinal))
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = c, ArgumentList = { "-e", "process.exit(+process.versions.node.split('.')[0]>=24?0:2)" }, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                using var p = Process.Start(psi);
                if (p == null) continue;
                if (!p.WaitForExit(5000)) { try { p.Kill(true); } catch { } continue; }
                if (p.ExitCode == 0) return c;
            }
            catch { }
        }
        return null;
    }
}
