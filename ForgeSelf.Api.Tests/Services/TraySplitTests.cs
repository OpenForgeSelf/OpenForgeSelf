using System.IO.Pipes;
using System.Text;
using FluentAssertions;
using ForgeSelf.Api.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Services;

public class TraySplitTests
{
    [Theory]
    [InlineData(true, null, true)]
    [InlineData(true, "0", true)]
    [InlineData(true, "1", false)]
    [InlineData(false, null, false)]
    [InlineData(false, "1", false)]
    public void 只有_Windows_且未禁用时才拉起托盘(bool isWindows, string? noTray, bool expected)
    {
        TrayHostLauncher.ShouldLaunch(isWindows, noTray).Should().Be(expected);
    }

    [Fact]
    public void 当前进程不是_Windows_时不拉起托盘()
    {
        if (OperatingSystem.IsWindows()) return;

        TrayHostLauncher.ShouldLaunch(OperatingSystem.IsWindows(), Environment.GetEnvironmentVariable(TrayHostLauncher.NoTrayVariable))
            .Should().BeFalse();
    }

    [Fact]
    public void 托盘参数带上角色和_API_入口()
    {
        var args = TrayProcessStarter.BuildTrayArguments(7102, "pipe-a", "interactive", "ForgeSelf.dll");

        args.Should().Contain("--pipe-name \"pipe-a\"");
        args.Should().Contain("--port 7102");
        args.Should().Contain("--role interactive");
        args.Should().Contain("--api-exe \"ForgeSelf.dll\"");
        args.Should().NotContain("--tray");
    }

    [Fact]
    public async Task 管道能把_shutdown_双向送到对端()
    {
        var name = "forgeself-tray-test-" + Guid.NewGuid().ToString("N");
        var clientSawShutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverSawShutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new ServicePipeServer(name);
        server.OnClientShutdown = () => serverSawShutdown.TrySetResult(true);
        server.Start();

        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        var hello = Encoding.UTF8.GetBytes("hello\n");
        await client.WriteAsync(hello);
        await client.FlushAsync();

        var readTask = Task.Run(async () =>
        {
            var line = await ServicePipeServer.ReadLineAsync(client, CancellationToken.None);
            clientSawShutdown.TrySetResult(line != null && line.Equals("shutdown", StringComparison.OrdinalIgnoreCase));
        });

        await Task.Delay(200);
        server.SendShutdown();

        var saw = await clientSawShutdown.Task.WaitAsync(TimeSpan.FromSeconds(5));
        saw.Should().BeTrue();

        var shutdown = Encoding.UTF8.GetBytes("shutdown\n");
        await client.WriteAsync(shutdown);
        await client.FlushAsync();

        var serverSaw = await serverSawShutdown.Task.WaitAsync(TimeSpan.FromSeconds(5));
        serverSaw.Should().BeTrue();
        await readTask;
    }
}
