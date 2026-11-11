using System.Text.Encodings.Web;
using System.Text.Json;

namespace ForgeSelf.Abstractions;

/// <summary>
/// 全仓 System.Text.Json 写出统一选项（PC-027，2026-11-07 用户指令）。
/// </summary>
/// <remarks>
/// <para>
/// STJ 默认 Encoder（<see cref="JavaScriptEncoder.Default"/>）把一切非 ASCII 字符转义成
/// <c>\uXXXX</c>，中文在工具结果回显 / MCP 客户端 / 执行记录 / 落盘配置里不可读。
/// STJ 没有进程级默认覆盖钩子（<c>JsonSerializerOptions.Default</c> 只读），
/// 因此本仓约定：<b>所有 JSON 写出路径</b>显式使用 <see cref="ForgeJson"/>；
/// 守卫 <c>ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs</c>（G2 源码枚举）
/// 拦截漏网的单参 Serialize 调用与缺 Encoder 的 options 初始化器。
/// </para>
/// <para>
/// 只放宽转义（UnsafeRelaxedJsonEscaping：仅保留 JSON 结构必需的转义），
/// <b>不设命名策略、不改缩进</b>，与「无 options」默认行为除转义外逐字节等价，避免契约漂移。
/// 反序列化对 <c>\uXXXX</c> 与原文两种形态等价 ⇒ 存量数据/上游消费者双向兼容。
/// </para>
/// </remarks>
public static class ForgeJson
{
    /// <summary>等价于「无 options」默认行为，仅非 ASCII（中文）与 HTML 敏感字符不再转义。</summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>落盘给人读的配置文件用：缩进 + 中文原文。仅用于原本就 WriteIndented 的落点。</summary>
    public static JsonSerializerOptions Indented { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };
}
