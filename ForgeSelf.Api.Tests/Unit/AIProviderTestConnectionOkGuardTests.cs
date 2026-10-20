using FluentAssertions;
using ForgeSelf.Api.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 「选定模型 · 真实对话」测试连接守卫（2026-10-18 输入3）：
/// TestConnection 带 modelId 时，用该已登记模型发真实 chat（content「请回复且仅回复ok」），
/// 成功判据 = 响应文本含 ok（忽略大小写）。本组用例钉死两个纯助手：
/// ExtractContent（OpenAI/Anthropic 两种响应体取回复文本）与 SaysOk（含 ok 判定，大小写不敏感）。
/// </summary>
public class AIProviderTestConnectionOkGuardTests
{
    // ── ExtractContent：OpenAI choices[0].message.content ──
    [Theory]
    [InlineData("""{"choices":[{"message":{"role":"assistant","content":"ok"}}]}""", "ok")]
    [InlineData("""{"choices":[{"message":{"content":"OK"}}]}""", "OK")]
    [InlineData("""{"choices":[{"message":{"content":"好的，收到"}}]}""", "好的，收到")]
    [InlineData("""{"choices":[{"text":"raw-completion"}]}""", "raw-completion")]
    // Anthropic content[0].text
    [InlineData("""{"content":[{"type":"text","text":"ok"}]}""", "ok")]
    // qwen3 等思考型模型：content 为 null/空，回复在 reasoning_content → 回退取（对齐 AIService 聊天链标语义）
    [InlineData("""{"choices":[{"message":{"content":null,"reasoning_content":"ok"}}]}""", "ok")]
    [InlineData("""{"choices":[{"message":{"content":"","reasoning_content":"OK done"}}]}""", "OK done")]
    // vLLM 0.31（gpustack）/deepseek 系：思考字段名为 reasoning（2026-10-18 实测 content=null+reasoning 被截断）
    [InlineData("""{"choices":[{"message":{"role":"assistant","content":null,"reasoning":"ok"}}]}""", "ok")]
    [InlineData("""{"choices":[{"message":{"content":null,"reasoning_content":"","reasoning":"OK!"}}]}""", "OK!")]
    // reasoning_content 优先于 reasoning（两者都有时取前者）
    [InlineData("""{"choices":[{"message":{"content":null,"reasoning_content":"ok","reasoning":"no"}}]}""", "ok")]
    // content 有值时优先 content，不得被 reasoning_content 覆盖
    [InlineData("""{"choices":[{"message":{"content":"ok","reasoning_content":"thinking..."}}]}""", "ok")]
    // 多模态数组型 content：拼接 text 项
    [InlineData("""{"choices":[{"message":{"content":[{"type":"text","text":"o"},{"type":"text","text":"k"}]}}]}""", "ok")]
    // 空 choices / 无 content → null
    [InlineData("""{"choices":[]}""", null)]
    [InlineData("""{"choices":[{"message":{"content":null}}]}""", null)]
    [InlineData("""{"error":{"message":"bad key"}}""", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractContent_ParsesReply(string? json, string? expected)
    {
        AIProviderService.ExtractContent(json).Should().Be(expected);
    }

    [Fact]
    public void ExtractContent_MalformedJson_ReturnsNull()
    {
        AIProviderService.ExtractContent("{ not json").Should().BeNull();
    }

    // ── SaysOk：含 ok 忽略大小写判成功 ──
    [Theory]
    [InlineData("ok", true)]
    [InlineData("OK", true)]
    [InlineData("Ok", true)]
    [InlineData("oK", true)]
    [InlineData("OK!", true)]
    [InlineData("  ok  ", true)]
    [InlineData("回复：ok。", true)]
    [InlineData("token", true)]          // 子串命中（含 ok）
    [InlineData("no", false)]
    [InlineData("失败", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SaysOk_CaseInsensitiveContainsOk(string? content, bool expected)
    {
        AIProviderService.SaysOk(content).Should().Be(expected);
    }
}
