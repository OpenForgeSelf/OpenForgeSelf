using FluentAssertions;
using ForgeSelf.Api.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 「选定模型 · 真实对话」测试连接守卫（PC-034，2026-11-14 输入1）：
/// 成功判据 = 回复非空白即可用，不对回复内容做关键词/语义校验——
/// 模型可拒绝「请回复且仅回复ok」的指令但仍完全正常工作，任何正常回复都应判连接成功；
/// 空回复仍判失败（PC-013 不变量：上游空响应不得当成功）。
/// </summary>
public class AIProviderTestConnectionReplyGuardTests
{
    [Theory]
    [InlineData("ok", true)]
    [InlineData("OK", true)]
    [InlineData("我无法按要求只回复指定词语。", true)]  // 拒绝指令但正常回复 ⇒ 可用（本批缺陷复现例）
    [InlineData("no", true)]
    [InlineData("失败", true)]
    [InlineData("连接正常，一切就绪。", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]                          // 纯空白 = 未取到有效回复
    [InlineData(null, false)]
    public void HasUsableReply_AnyNonBlankTextIsUsable(string? content, bool expected)
    {
        AIProviderService.HasUsableReply(content).Should().Be(expected);
    }
}
