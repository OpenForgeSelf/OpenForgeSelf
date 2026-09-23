using System.Text;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// stdio Content-Length 帧编解码测试（v2.1.0）：单帧 / 多帧拆包 / 粘包 / 头部容忍 / UTF-8 中文字节长度。
/// 帧体按字节读取（中文 1 字符 ≠ 1 字节）。
/// </summary>
public class McpFrameTests
{
    [Fact]
    public async Task WriteFrame_ThenReadFrame_RoundTrips()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"method":"ping"}""";
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, new UTF8Encoding(false)) { AutoFlush = false };

        await StdioMcpTransport.WriteFrameAsync(writer, json, CancellationToken.None);
        await writer.FlushAsync();

        ms.Position = 0;
        var frame = await StdioMcpTransport.ReadFrameAsync(ms, CancellationToken.None);

        Assert.Equal(json, frame);
    }

    [Fact]
    public async Task ReadFrame_ChineseUtf8_ByteLengthNotCharLength()
    {
        // Content-Length 按 UTF-8 字节计：含中文的 payload 长度必须准确（拆包按字节读）
        const string json = """{"result":{"echo":"你好，MCP 世界"}}""";
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {bytes.Length}\r\n\r\n";
        using var ms = new MemoryStream();
        var payload = Encoding.UTF8.GetBytes(header + json);
        ms.Write(payload);
        ms.Position = 0;

        var frame = await StdioMcpTransport.ReadFrameAsync(ms, CancellationToken.None);

        Assert.Equal(json, frame);
    }

    [Fact]
    public async Task ReadFrame_TwoFramesInOneStream_SplitCorrectly()
    {
        const string j1 = """{"id":1}""";
        const string j2 = """{"id":2}""";
        using var ms = new MemoryStream();
        var b1 = Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(j1)}\r\n\r\n{j1}");
        var b2 = Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(j2)}\r\n\r\n{j2}");
        ms.Write(b1);
        ms.Write(b2);
        ms.Position = 0;

        var f1 = await StdioMcpTransport.ReadFrameAsync(ms, CancellationToken.None);
        var f2 = await StdioMcpTransport.ReadFrameAsync(ms, CancellationToken.None);
        var eof = await StdioMcpTransport.ReadFrameAsync(ms, CancellationToken.None);

        Assert.Equal(j1, f1);
        Assert.Equal(j2, f2);
        Assert.Null(eof);
    }

    [Fact]
    public async Task ReadFrame_HeaderCaseInsensitive_ToleratesExtraHeaders()
    {
        const string json = """{"result":{}}""";
        using var ms = new MemoryStream();
        var header = $"content-length: {Encoding.UTF8.GetByteCount(json)}\r\nx-extra: 1\r\n\r\n";
        ms.Write(Encoding.UTF8.GetBytes(header + json));
        ms.Position = 0;

        var frame = await StdioMcpTransport.ReadFrameAsync(ms, CancellationToken.None);

        Assert.Equal(json, frame);
    }

    [Fact]
    public async Task ReadFrame_EmptyStream_ReturnsNull()
    {
        Assert.Null(await StdioMcpTransport.ReadFrameAsync(new MemoryStream(), CancellationToken.None));
    }
}
