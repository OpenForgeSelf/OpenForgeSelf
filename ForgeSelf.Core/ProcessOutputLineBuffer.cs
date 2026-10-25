namespace ForgeSelf.Core;

/// <summary>
/// 子进程输出的**增量行解码器**（PC-011 流式形态，PILOT-073）。
/// 用于必须"边读边出事件"的站点（逐行解析 CLI 输出）：这类站点不能退化成整段读完再解码，
/// 所以按行切分后逐行走 <see cref="ProcessOutputText"/> 裁决。
/// 两点安全性（实测依据见 PILOT-073 00-repository-understanding）：
/// - 按 0x0A 切分对 GBK 也安全：GBK trail 字节范围 0x40–0xFE（不含 0x7F），不会出现裸 0x0A；
/// - 多字节字符跨两次读取边界不受影响：字节先入缓冲，凑齐整行才解码。
/// 语义与 <c>StreamReader.ReadLineAsync</c> 对齐：遇到 0x0A 即出一行（空行也出），行尾 CR 剥除。
/// </summary>
public sealed class ProcessOutputLineBuffer
{
    private readonly List<byte> _pending = new();

    /// <summary>是否出现过非 UTF-8 生效的行（站点据此履行日志义务，见 PILOT-073 FR-3）。</summary>
    public bool UsedNonUtf8 { get; private set; }

    /// <summary>首个生效的非 UTF-8 解码标签（供日志；全 UTF-8 时为 null）。</summary>
    public string? NonUtf8EncodingUsed { get; private set; }

    /// <summary>
    /// 追加一段原始字节，返回其中**已完整**的行（以 0x0A 结束，行尾 CR 已剥）；不足一行的余量留在缓冲。
    /// </summary>
    public List<string> Append(ReadOnlySpan<byte> data)
    {
        var lines = new List<string>();
        while (!data.IsEmpty)
        {
            var nl = data.IndexOf((byte)'\n');
            if (nl < 0)
            {
                _pending.AddRange(data.ToArray());
                break;
            }

            _pending.AddRange(data[..nl].ToArray());
            data = data[(nl + 1)..];
            lines.Add(DecodeLine());
        }
        return lines;
    }

    /// <summary>取走缓冲里的收尾片段（子进程结束且最后一行无换行时）；无内容则返回空表。</summary>
    public List<string> Flush()
    {
        return _pending.Count == 0 ? new List<string>() : new List<string> { DecodeLine() };
    }

    /// <summary>把缓冲里的字节当一行解码并清空缓冲（行尾 CR 剥除后再交给裁决器）。</summary>
    private string DecodeLine()
    {
        var raw = _pending.ToArray();
        _pending.Clear();
        if (raw.Length > 0 && raw[^1] == (byte)'\r')
        {
            raw = raw[..^1];
        }

        var decoded = ProcessOutputText.Decode(raw);
        if (!decoded.EncodingUsed.StartsWith("utf8", StringComparison.Ordinal))
        {
            UsedNonUtf8 = true;
            NonUtf8EncodingUsed ??= decoded.EncodingUsed;
        }
        return decoded.Text;
    }
}
