using System.Runtime.InteropServices;
using System.Text;

namespace ForgeSelf.Core;

/// <summary>
/// 子进程输出文本解码（PC-011 canonical fix，PILOT-073 · 2026-10-22）。
/// 问题：起子进程读 stdout/stderr 时任何"固定码页解码"都必坏一侧——固定 UTF-8 会毁掉
/// 按系统 ANSI 码页（中文 Windows = GBK/936）写出的子进程输出（U+FFFD 不可逆），
/// 固定跟随码页会毁掉协议性 UTF-8 输出（dotnet/pnpm/node 等现代 CLI）。
/// 策略（顺序即优先级）：剥 UTF-8 BOM → 严格 UTF-8（throwOnInvalidBytes，成功即采信）→
/// 失败回退系统 ANSI 码页（kernel32 MultiByteToWideChar(GetACP())，不依赖 CodePages 包）→
/// 两者都坏（纯垃圾字节）时宽容 UTF-8 兜底（尽量保留可恢复信息）。
/// 所有起子进程消费文本的站点统一走本助手；MCP 协议流（StdioMcpTransport）属协议强制
/// UTF-8 的异根因特例，allowlist 见 ProcessOutputEncodingGuardTests。
/// </summary>
public static class ProcessOutputText
{
    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern uint GetACP();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int MultiByteToWideChar(
        uint codePage, uint dwFlags, byte[] lpMultiByteStr, int cbMultiByte,
        StringBuilder lpWideCharStr, int cchWideChar);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int WideCharToMultiByte(
        uint codePage, uint dwFlags, string lpWideCharStr, int cchWideChar,
        byte[]? lpMultiByteStr, int cbMultiByte, IntPtr lpDefaultChar, IntPtr lpUsedDefaultChar);

    /// <summary>UTF-8 BOM 字节（EF BB BF）。</summary>
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>严格 UTF-8：非法字节序列必抛，作为"确系 UTF-8"的判据。</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true); // 不出 BOM + 非法字节必抛

    /// <summary>解码结果：文本 + 实际生效的解码方式（调用方据此履行日志义务，见 PILOT-073 FR-3）。</summary>
    /// <param name="Text">还原后的文本。</param>
    /// <param name="EncodingUsed">"utf8" / "utf8-bom" / "ansi-<codepage>" / "utf8-lossy" 之一。</param>
    public readonly record struct Decoded(string Text, string EncodingUsed);

    /// <summary>
    /// 从子进程管道流一次性读原始字节（异步）。站点用 <c>proc.StandardOutput.BaseStream</c> 传入，
    /// 绕开 StreamReader 的码页解码——本助手才能拿原始字节做裁决。
    /// （不用 Stream.ReadAllBytesAsync：本机 net10.0 引用集未提供该 API，CS1061 实测）
    /// </summary>
    public static async Task<byte[]> CaptureAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    /// <summary>从子进程管道流一次性读原始字节（同步形态，供 ReadToEnd 类站点等价改造）。</summary>
    public static byte[] Capture(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 把子进程输出原始字节还原为文本（三级策略见类型注释）。null/空输入返回空文本（encoding=utf8）。
    /// </summary>
    public static Decoded Decode(byte[]? raw)
    {
        if (raw is null || raw.Length == 0)
        {
            return new Decoded(string.Empty, "utf8");
        }

        // 1. 剥 UTF-8 BOM：带 BOM 即作者明示 UTF-8，直接采信。
        if (raw.Length >= 3 && raw[0] == Utf8Bom[0] && raw[1] == Utf8Bom[1] && raw[2] == Utf8Bom[2])
        {
            var withoutBom = raw.AsSpan(3);
            try
            {
                return new Decoded(StrictUtf8.GetString(withoutBom.ToArray()), "utf8-bom");
            }
            catch (DecoderFallbackException)
            {
                // BOM 后内容仍非合法 UTF-8（罕见：BOM 残留 + ANSI 正文），落入下面统一裁决。
            }
        }

        // 2. 严格 UTF-8：合法即采信（dotnet/pnpm/node/Git 等现代 CLI 的输出面）。
        try
        {
            return new Decoded(StrictUtf8.GetString(raw), "utf8");
        }
        catch (DecoderFallbackException)
        {
            // 非合法 UTF-8 → 试系统 ANSI（本缺陷的主场景：GBK 中文输出）。
        }

        // 3. 系统 ANSI 回退（仅 Windows；GetACP 即控制台子进程默认写入码页）。
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var ansi = DecodeAnsi(raw, out var codepage);
            if (ansi != null)
            {
                return new Decoded(ansi, $"ansi-{codepage}");
            }
        }

        // 4. 双坏兜底：宽容 UTF-8（坏点变 U+FFFD 但保留全部可恢复信息，且履行日志义务）。
        return new Decoded(Encoding.UTF8.GetString(raw), "utf8-lossy");
    }

    /// <summary>
    /// 按系统 ANSI 码页解码，并用**往返校验**判定该字节流是否真出自 ANSI：编回同码页后必与原始字节
    /// 逐字节相等才放行。必需性（本机实测）：cp936 的 MultiByteToWideChar 对任意字节都不报错，
    /// 垃圾字节会被映射成 '?' 或 PUA 字符——若不校验，“ansi-936”标签就是假确信。
    /// 不满足（或码页不可用）时返回 null，由调用方落到兜底解码。
    /// </summary>
    private static string? DecodeAnsi(byte[] raw, out uint codepage)
    {
        codepage = GetACP();
        if (codepage == 0)
        {
            return null;
        }
        if (codepage is 65001) // 系统 ANSI 即 UTF-8：严格 UTF-8 已在第 2 步失败，无新信息
        {
            return null;
        }

        // 先探针调用取所需宽字符数（out-only：零容量 StringBuilder，不写入）。
        var cch = MultiByteToWideChar(codepage, 0, raw, raw.Length, new StringBuilder(0), 0);
        if (cch <= 0)
        {
            return null;
        }

        var sb = new StringBuilder((int)cch);
        var written = MultiByteToWideChar(codepage, 0, raw, raw.Length, sb, (int)cch);
        if (written <= 0)
        {
            return null;
        }

        var text = sb.ToString(0, written);
        return RoundTripsToRaw(text, raw, codepage) ? text : null;
    }

    /// <summary>把解码结果编回同一 ANSI 码页，逐字节比对原始输入（含长度）。</summary>
    private static bool RoundTripsToRaw(string text, byte[] raw, uint codepage)
    {
        var needed = WideCharToMultiByte(codepage, 0, text, text.Length, null, 0, IntPtr.Zero, IntPtr.Zero);
        if (needed != raw.Length)
        {
            return false;
        }

        var back = new byte[needed];
        var written = WideCharToMultiByte(codepage, 0, text, text.Length, back, back.Length, IntPtr.Zero, IntPtr.Zero);
        if (written != raw.Length)
        {
            return false;
        }

        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] != back[i])
            {
                return false;
            }
        }
        return true;
    }
}
