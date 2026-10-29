using FluentAssertions;
using ForgeSelf.Api.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 「选定模型 · 真实对话」测试连接守卫（2026-10-18 输入3）：
/// TestConnection 带 modelId 时，用该已登记模型发真实 chat（content「请回复且仅回复ok」），
/// 成功判据 = 响应文本含 ok（忽略大小写）。输入6 后探针统一切 SDK，直接读 UnifiedChatResponse，
/// 本组钉死 SaysOk（含 ok 判定，大小写不敏感）。
/// </summary>
public class AIProviderTestConnectionOkGuardTests
{
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
