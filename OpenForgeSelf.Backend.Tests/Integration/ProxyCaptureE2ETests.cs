using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Core;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Data.Entities;

namespace OpenForgeSelf.Backend.Tests.Integration;

/// <summary>
/// 抓包代理插件（ProxyCapture）真实端到端测试：
/// 真实 TCP 监听 + 协议嗅探 + HTTP 解析 + 转发/200 占位 + SQLite 抓包入库，全链路真跑。
/// （CaptureEngine 静态单例 + 共享 ProxyCapture.db + 真实 TCP 端口绑定；
/// 归入 SharedGlobalState 集合与其它改写进程级单例的测试类串行，用例间以 URL 精确匹配隔离断言。）
/// </summary>
[Collection("SharedGlobalState")]
public class ProxyCaptureE2ETests : IDisposable
{
    private readonly List<TcpListener> _targets = new();
    private readonly List<int> _startedListeners = new();

    public void Dispose()
    {
        // 清理监听器与目标服务，避免端口/线程残留
        foreach (var id in _startedListeners)
        {
            CaptureEngine.Instance.StopListener(id);
        }

        foreach (var t in _targets)
        {
            try { t.Stop(); } catch { }
        }
    }

    /// <summary>占用一个空闲回环端口（绑定 0 取端口后释放）。</summary>
    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>极简目标 HTTP 服务：读请求头部后回 200 OK，记录收到的原始请求行。</summary>
    private (TcpListener Listener, List<string> Received) StartTargetService()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _targets.Add(listener);
        var receivedLines = new List<string>();

        _ = Task.Run(async () =>
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                var buf = new byte[4096];
                var total = new MemoryStream();
                // 读到 \r\n\r\n 为止（请求头部）
                while (!ContainsHeaderEnd(total))
                {
                    var n = await stream.ReadAsync(buf);
                    if (n == 0) break;
                    total.Write(buf, 0, n);
                }

                var reqText = Encoding.ASCII.GetString(total.ToArray());
                lock (receivedLines)
                {
                    receivedLines.AddRange(reqText.Split("\r\n").Where(l => l.Length > 0));
                }

                var resp = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nOK");
                await stream.WriteAsync(resp);
            }
            catch
            {
                // 测试结束关闭端口时的正常异常
            }
        });

        return (listener, receivedLines);
    }

    private static bool ContainsHeaderEnd(MemoryStream ms)
    {
        var data = ms.ToArray();
        for (var i = 0; i < data.Length - 3; i++)
        {
            if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10) return true;
        }

        return false;
    }

    /// <summary>启动监听器并返回实际监听端口（失败自动换端口重试，规避端口竞态）。</summary>
    private int StartListener(ListenerConfig cfg)
    {
        // FreePort 取端口后存在竞态窗口（全量并行跑时端口可能被占用），
        // 失败则换端口重试，保证测试在负载环境下稳定。
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                CaptureEngine.Instance.StartListener(cfg);
                _startedListeners.Add(cfg.Id);
                return cfg.ListenPort;
            }
            catch (SocketException)
            {
                // 端口被占：换端口重试
                cfg.ListenPort = FreePort();
            }
        }

        throw new InvalidOperationException($"无法在 5 次尝试内启动监听器（端口均被占用），最后端口 {cfg.ListenPort}");
    }

    [Fact]
    public async Task HttpRequest_WithTarget_ForwardsAndCaptures()
    {
        // Arrange：目标服务 + 带目标的监听器（真实 TCP 全链路）
        var (target, received) = StartTargetService();
        var targetPort = ((IPEndPoint)target.LocalEndpoint).Port;

        var listenPort = StartListener(new ListenerConfig
        {
            Name = "e2e-fwd",
            ListenAddress = "127.0.0.1",
            ListenPort = FreePort(),
            TargetHost = "127.0.0.1",
            TargetPort = targetPort,
            Enabled = true
        });

        // Act：真实 HTTP 请求打到监听端口
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{listenPort}/e2e/hello?x=1");
        var body = await resp.Content.ReadAsStringAsync();

        // Assert：响应来自目标（原样转发 + 回写）
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("OK");

        // Assert：目标收到原样请求（含路径与查询串）
        lock (received)
        {
            received.Should().Contain(l => l.Contains("GET /e2e/hello?x=1"));
        }

        // Assert：抓包入库（URL/方法/状态/转发标记/目标）
        var rec = CaptureSession.FindAll().OrderByDescending(s => s.Id)
            .FirstOrDefault(s => s.Url == "/e2e/hello?x=1");
        rec.Should().NotBeNull();
        rec!.Forwarded.Should().BeTrue();
        rec.StatusCode.Should().Be(200);
        rec.Method.Should().Be("GET");
        rec.Target.Should().Be($"127.0.0.1:{targetPort}");
    }

    [Fact]
    public async Task HttpRequest_WithoutTarget_Returns200AndCaptures()
    {
        // Arrange：无目标监听器（仅抓包）
        var listenPort = StartListener(new ListenerConfig
        {
            Name = "e2e-nofwd",
            ListenAddress = "127.0.0.1",
            ListenPort = FreePort(),
            Enabled = true
        });

        // Act
        using var http = new HttpClient();
        var resp = await http.GetAsync($"http://127.0.0.1:{listenPort}/e2e/capture-only");
        var body = await resp.Content.ReadAsStringAsync();

        // Assert：200 占位响应
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("request captured");

        // Assert：抓包入库且未转发
        var rec = CaptureSession.FindAll().OrderByDescending(s => s.Id)
            .FirstOrDefault(s => s.Url == "/e2e/capture-only");
        rec.Should().NotBeNull();
        rec!.Forwarded.Should().BeFalse();
        rec.StatusCode.Should().Be(200);
    }
}
