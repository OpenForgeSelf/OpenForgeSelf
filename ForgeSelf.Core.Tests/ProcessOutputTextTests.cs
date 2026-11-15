using System.Runtime.InteropServices;
using System.Text;
using ForgeSelf.Core;
using Xunit;

namespace ForgeSelf.Core.Tests;

/// <summary>
/// ProcessOutputText 纯单测（PC-011 canonical fix，PILOT-073 · 2026-10-22）：
/// 五类入参——UTF-8 / UTF-8+BOM / 系统 ANSI（GBK）字节 / 空与纯 ASCII / 双坏垃圾字节。
/// GBK 期望字节用 kernel32 WideCharToMultiByte(GetACP()) 现场生成，不依赖测试环境码页为 936：
/// 环境 ANSI 即 UTF-8（65001）时 ANSI 回退路径按设计不可达，相应断言改为验证 utf8 采信。
/// </summary>
public class ProcessOutputTextTests
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int WideCharToMultiByte(
        uint codePage, uint dwFlags, string lpWideCharStr, int cchWideChar,
        byte[]? lpMultiByteStr, int cbMultiByte, IntPtr lpDefaultChar, IntPtr lpUsedDefaultChar);

    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    /// <summary>"中文编码测试"——测试侧用 \u 转义固化，与源文件编码无关。</summary>
    private const string Chinese = "\u4e2d\u6587\u7f16\u7801\u6d4b\u8bd5";

    [Fact]
    public void PlainUtf8Bytes_DecodeAsUtf8()
    {
        var raw = Encoding.UTF8.GetBytes("build ok: " + Chinese);

        var result = ProcessOutputText.Decode(raw);

        Assert.Equal("build ok: " + Chinese, result.Text);
        Assert.Equal("utf8", result.EncodingUsed);
    }

    [Fact]
    public void Utf8BomBytes_DecodeAsUtf8Bom_WithoutBomChar()
    {
        var payload = Encoding.UTF8.GetBytes(Chinese);
        var raw = new byte[3 + payload.Length];
        raw[0] = 0xEF; raw[1] = 0xBB; raw[2] = 0xBF;
        payload.CopyTo(raw, 3);

        var result = ProcessOutputText.Decode(raw);

        Assert.Equal(Chinese, result.Text);
        // 上一行 Equal 已同时守住「剥掉 BOM」与「内容无损」：BOM 残留会让 Equal 红。
        Assert.Equal("utf8-bom", result.EncodingUsed);
    }

    [Fact]
    public void PureAsciiBytes_DecodeAsUtf8_NotAnsi()
    {
        // 纯 ASCII 双码页皆合法：必须按 utf8 采信，避免无意义地把来源标成 ansi。
        var result = ProcessOutputText.Decode(Encoding.UTF8.GetBytes("git status --short"));

        Assert.Equal("git status --short", result.Text);
        Assert.Equal("utf8", result.EncodingUsed);
    }

    [Fact]
    public void EmptyAndNull_ReturnEmptyText()
    {
        Assert.Equal(string.Empty, ProcessOutputText.Decode(null).Text);
        Assert.Equal("utf8", ProcessOutputText.Decode(null).EncodingUsed);
        Assert.Equal(string.Empty, ProcessOutputText.Decode(Array.Empty<byte>()).Text);
    }

    [Fact]
    public void GbkChineseBytes_CoupledAsValidUtf8_PreferAnsiWhenAnsiYieldsCjk()
    {
        // PC-011 逃逸（2026-11-15 实测：PowerShell 5.1 `ls` 表头「目录:」在页面显示为「Ŀ¼:」）：
        // GBK 双字节「目录」(C4 BF C2 BC) 恰好构成合法 UTF-8 的拉丁扩展序列（U+013F U+00BC），
        // 严格 UTF-8「合法即采信」的判据对此盲区。修复期望：解码文本出现可疑拉丁扩展且不含 CJK，
        // 而 ANSI 往返成功且结果含 CJK 时 → 采信 ANSI。
        var acp = GetACP();
        var raw = ToAnsiBytesAllowingUtf8Collision("    目录: demo/示例", acp);
        if (raw == null)
        {
            // 65001：子进程按 UTF-8 写出，该盲区形态在环境上不存在，退化为验证 UTF-8 采信。
            var utf8 = Encoding.UTF8.GetBytes("    目录: demo/示例");
            var r = ProcessOutputText.Decode(utf8);
            Assert.Equal("    目录: demo/示例", r.Text);
            Assert.Equal("utf8", r.EncodingUsed);
            return;
        }
        var utf8Probe = Encoding.UTF8.GetString(raw);
        Assert.NotEqual("    目录: demo/示例", utf8Probe); // 前提自查：该字节串确实会被 UTF-8 误读

        var result = ProcessOutputText.Decode(raw);

        Assert.Equal("    目录: demo/示例", result.Text);
        Assert.StartsWith("ansi-", result.EncodingUsed);
    }

    [Fact]
    public void Utf8ChineseWithLatinAccent_KeepsUtf8Verdict()
    {
        // 对照护栏：UTF-8 解码结果本身含 CJK（真中文）时，即便夹杂拉丁重音也不翻案——
        // 翻案只在「可疑拉丁扩展且全文无 CJK」的窄门触发。
        var text = "中文 café 列表";
        var result = ProcessOutputText.Decode(Encoding.UTF8.GetBytes(text));

        Assert.Equal(text, result.Text);
        Assert.Equal("utf8", result.EncodingUsed);
    }

    [Fact]
    public void SystemAnsiBytes_FallBackToAnsiDecode()
    {
        var acp = GetACP();
        // 需要一次"对 UTF-8 非法、对 ANSI 合法"的字节；环境 ANSI 即 UTF-8 时该形态不存在。
        var ansiBytes = ToAnsiBytesOrThrow(Chinese, acp);
        if (ansiBytes == null)
        {
            // 65001：GBK 形态不可构造，退化为验证严格 UTF-8 正常采信
            var utf8 = Encoding.UTF8.GetBytes(Chinese);
            var r = ProcessOutputText.Decode(utf8);
            Assert.Equal(Chinese, r.Text);
            Assert.Equal("utf8", r.EncodingUsed);
            return;
        }

        var result = ProcessOutputText.Decode(ansiBytes);

        Assert.Equal(Chinese, result.Text);
        Assert.StartsWith("ansi-", result.EncodingUsed);
    }

    [Fact]
    public void GarbageBytes_FallToLossyUtf8()
    {
        // 两码页皆坏：非法 UTF-8 序列 + GBK 落单前导字节（无后续字节，ANSI 转换必失败）。
        var raw = new byte[] { 0x81 };

        var result = ProcessOutputText.Decode(raw);

        // 兜底不抛异常、文本非空（坏点以 U+FFFD 呈现）、标记 lossy 供调用方记日志
        Assert.NotEmpty(result.Text);
        Assert.Equal("utf8-lossy", result.EncodingUsed);
    }

    /// <summary>
    /// 用系统 ANSI 码页把文本编成字节；若该码页对 UTF-8 解码器也合法（如 65001）返回 null。
    /// 孤立坏点导致 WideCharToMultiByte 抛异常时同样返回 null（等价于环境不支持该形态）。
    /// </summary>
    private static byte[]? ToAnsiBytesOrThrow(string text, uint acp)
    {
        if (acp == 65001) return null;
        try
        {
            var needed = WideCharToMultiByte(acp, 0, text, text.Length, null, 0, IntPtr.Zero, IntPtr.Zero);
            if (needed <= 0) return null;
            var buf = new byte[needed];
            var written = WideCharToMultiByte(acp, 0, text, text.Length, buf, buf.Length, IntPtr.Zero, IntPtr.Zero);
            if (written <= 0) return null;
            var raw = buf[..written];
            // 对严格 UTF-8 也合法 = 该 ANSI 码页与 UTF-8 撞车（理论上不发生）：视作不可构造
            try
            {
                new UTF8Encoding(false, true).GetString(raw);
                return null;
            }
            catch (DecoderFallbackException)
            {
                return raw;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// PC-011 逃逸测试专用：与 <see cref="ToAnsiBytesOrThrow"/> 唯一差别是**允许**「ANSI 字节对严格
    /// UTF-8 也合法」的撞车形态——那正是被测盲区本身（旧 helper 把它当"不可构造"拒收，
    /// 测试基建自己挡住了逃逸形态的入册，见 2026-11-15 逃逸分析）。
    /// </summary>
    private static byte[]? ToAnsiBytesAllowingUtf8Collision(string text, uint acp)
    {
        if (acp == 65001) return null;
        try
        {
            var needed = WideCharToMultiByte(acp, 0, text, text.Length, null, 0, IntPtr.Zero, IntPtr.Zero);
            if (needed <= 0) return null;
            var buf = new byte[needed];
            var written = WideCharToMultiByte(acp, 0, text, text.Length, buf, buf.Length, IntPtr.Zero, IntPtr.Zero);
            if (written <= 0) return null;
            return buf[..written];
        }
        catch (Exception)
        {
            return null;
        }
    }
}
