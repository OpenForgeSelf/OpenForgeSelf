using System.IO;

namespace ForgeSelf.Api.Plugins.ProxyCapture.Core;

/// <summary>
/// 把已读出的首个字节回填到流前端，使上层协议处理可以无感地从头读取整条流。
/// 用于协议嗅探后还原完整流（避免 NetworkStream 无法回退的问题）。
/// </summary>
public class PrefixStream : Stream
{
    private readonly Stream _inner;
    private readonly byte[] _prefix;
    private int _prefixPos;

    public PrefixStream(Stream inner, byte[] prefix)
    {
        _inner = inner;
        _prefix = prefix;
        _prefixPos = 0;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = 0;
        while (read < count && _prefixPos < _prefix.Length)
        {
            buffer[offset + read] = _prefix[_prefixPos++];
            read++;
        }

        if (read < count)
        {
            read += _inner.Read(buffer, offset + read, count - read);
        }

        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < count && _prefixPos < _prefix.Length)
        {
            buffer[offset + read] = _prefix[_prefixPos++];
            read++;
        }

        if (read < count)
        {
            read += await _inner.ReadAsync(buffer.AsMemory(offset + read, count - read), cancellationToken);
        }

        return read;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    // 还原流是连接上的完整双向流：写透传给内层（Handler 用同一流回写响应）
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);

    public override int ReadTimeout
    {
        get => _inner.ReadTimeout;
        set => _inner.ReadTimeout = value;
    }
}
