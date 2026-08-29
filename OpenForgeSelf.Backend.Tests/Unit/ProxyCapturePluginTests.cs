using System.Reflection;
using System.Text;
using FluentAssertions;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Core;

namespace OpenForgeSelf.Backend.Tests.Unit;

/// <summary>
/// 抓包代理插件（ProxyCapture）核心逻辑单测：
/// HTTP 报文解析（Content-Length / chunked）、首字节还原流、自签 CA 签发、头/体序列化工具。
/// （会读写 CaptureEngine 静态单例的数据目录，故归入 SharedGlobalState 集合串行执行。）
/// </summary>
[Collection("SharedGlobalState")]
public class ProxyCapturePluginTests : IDisposable
{
    private readonly string _tempDir;

    public ProxyCapturePluginTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ProxyCaptureTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    #region HttpReader - HTTP 报文解析

    [Fact]
    public async Task ReadMessageAsync_WithContentLength_ParsesHeadersAndBody()
    {
        // Arrange：GET 请求 + Content-Length 报文
        var raw = "GET /api/items HTTP/1.1\r\n" +
                  "Host: myapp.local\r\n" +
                  "Content-Length: 5\r\n" +
                  "\r\n" +
                  "hello";
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes(raw));

        // Act
        var msg = await HttpReader.ReadMessageAsync(stream, CancellationToken.None);

        // Assert
        msg.StartLine.Should().Be("GET /api/items HTTP/1.1");
        msg.Headers["Host"].Should().Be("myapp.local");
        msg.Headers["Content-Length"].Should().Be("5");
        Encoding.ASCII.GetString(msg.Body).Should().Be("hello");
    }

    [Fact]
    public async Task ReadMessageAsync_WithChunked_DechunksBody()
    {
        // Arrange：chunked 报文（"abc" + "defgh" + 终止块）
        var raw = "POST /upload HTTP/1.1\r\n" +
                  "Transfer-Encoding: chunked\r\n" +
                  "\r\n" +
                  "3\r\nabc\r\n" +
                  "5\r\ndefgh\r\n" +
                  "0\r\n\r\n";
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes(raw));

        // Act
        var msg = await HttpReader.ReadMessageAsync(stream, CancellationToken.None);

        // Assert：chunk 体按序拼接
        Encoding.ASCII.GetString(msg.Body).Should().Be("abcdefgh");
    }

    [Fact]
    public async Task ReadMessageAsync_WithoutBody_ReturnsEmptyBody()
    {
        // Arrange：无 body 的 GET 请求
        var raw = "GET /ping HTTP/1.1\r\nHost: localhost\r\n\r\n";
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes(raw));

        // Act
        var msg = await HttpReader.ReadMessageAsync(stream, CancellationToken.None);

        // Assert
        msg.Body.Should().BeEmpty();
        msg.StartLine.Should().Be("GET /ping HTTP/1.1");
    }

    #endregion

    #region PrefixStream - 首字节还原

    [Fact]
    public async Task Read_WithPrefix_ReplaysPrefixThenInner()
    {
        // Arrange：首字节 0x16（TLS）被嗅探读出，需还原到流前端
        var inner = new MemoryStream(Encoding.ASCII.GetBytes("G rest"));
        var prefix = new PrefixStream(inner, new byte[] { 0x16 });

        // Act：一次读出 6 字节（1 字节前缀 + 5 字节内层）
        var buf = new byte[6];
        var read = prefix.Read(buf, 0, 6);

        // Assert：完整还原为 0x16 开头
        read.Should().Be(6);
        buf[0].Should().Be(0x16);
        Encoding.ASCII.GetString(buf, 1, 5).Should().Be("G res");
    }

    [Fact]
    public async Task ReadAsync_WithPrefix_ReplaysPrefixThenInner()
    {
        // Arrange
        var inner = new MemoryStream(Encoding.ASCII.GetBytes("TLS-data"));
        var prefix = new PrefixStream(inner, new byte[] { 0x16 });

        // Act
        var buf = new byte[9];
        var read = await prefix.ReadAsync(buf, 0, 9, CancellationToken.None);

        // Assert
        read.Should().Be(9);
        buf[0].Should().Be(0x16);
        Encoding.ASCII.GetString(buf, 1, 8).Should().Be("TLS-data");
    }

    #endregion

    #region CertificateAuthority - 自签 CA 与叶子证书

    [Fact]
    public void GetCaCertificate_GeneratesPersistentCaFiles()
    {
        // Arrange
        var ca = new CertificateAuthority(_tempDir);

        // Act
        var cert = ca.GetCaCertificate();

        // Assert：CA 证书与 pfx 落盘，subject 为本地抓包专用 CA
        File.Exists(Path.Combine(_tempDir, "ca.cer")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "ca.pfx")).Should().BeTrue();
        cert.Subject.Should().Contain("OpenForgeSelf ProxyCapture CA");
    }

    [Fact]
    public void GetLeafCertificate_IssuesCertWithRequestedHost()
    {
        // Arrange
        var ca = new CertificateAuthority(_tempDir);

        // Act
        var leaf = ca.GetLeafCertificate("myapp.local");

        // Assert：叶子证书 CN 与请求主机一致（供 SNI 动态签发）
        leaf.Subject.Should().Contain("CN=myapp.local");
    }

    #endregion

    #region HttpCaptureHandler - 头/体序列化工具（internal 反射）

    [Fact]
    public void HeadersToJson_SerializesHeaderDictionary()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            ["Host"] = "myapp.local",
            ["Content-Type"] = "application/json"
        };
        var method = typeof(HttpCaptureHandler).GetMethod(
            "HeadersToJson", BindingFlags.Static | BindingFlags.NonPublic)!;

        // Act
        var json = (string)method.Invoke(null, new object[] { headers })!;

        // Assert
        json.Should().Contain("\"Host\":\"myapp.local\"");
        json.Should().Contain("Content-Type");
    }

    [Fact]
    public void TryDecode_Utf8Text_ReturnsOriginalText()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("{\"ok\":true}");
        var method = typeof(HttpCaptureHandler).GetMethod(
            "TryDecode", BindingFlags.Static | BindingFlags.NonPublic)!;

        // Act
        var result = (string?)method.Invoke(null, new object[] { data });

        // Assert
        result.Should().Be("{\"ok\":true}");
    }

    [Fact]
    public void TryDecode_Empty_ReturnsNull()
    {
        // Arrange
        var method = typeof(HttpCaptureHandler).GetMethod(
            "TryDecode", BindingFlags.Static | BindingFlags.NonPublic)!;

        // Act
        var result = (string?)method.Invoke(null, new object[] { Array.Empty<byte>() });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryDecode_BinaryBody_ReturnsBinaryNotice()
    {
        // Arrange：含空字节的二进制载荷
        var data = new byte[] { 0x1F, 0x8B, 0x00, 0x00, 0x00, 0x00 };
        var method = typeof(HttpCaptureHandler).GetMethod(
            "TryDecode", BindingFlags.Static | BindingFlags.NonPublic)!;

        // Act
        var result = (string?)method.Invoke(null, new object[] { data });

        // Assert：判定为二进制并提示字节数
        result.Should().Contain("[binary body: 6 bytes]");
    }

    #endregion
}
