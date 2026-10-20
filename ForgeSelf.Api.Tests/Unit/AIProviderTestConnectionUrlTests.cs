using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI.Providers;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 「测试连接」URL 归一化守卫（2026-10-18 现场缺陷）：
/// AIProviderService.TestConnectionAsync 曾直接 POST 裸 Endpoint——Endpoint 配成基础地址（…/v1）时
/// 探测 /v1 必 404，把可用提供方误报为测试失败。修复 = 与聊天链路共用 GetChatEndpointByProviderType；
/// 本组用例钉死推导结果，防再分叉。
/// </summary>
public class AIProviderTestConnectionUrlTests
{
    [Theory]
    // 基础地址带 /v1 前缀（gpustack 现场形态）→ 补 chat/completions
    [InlineData(AIProviderType.OpenAI, "http://gpustack.luxvisions-inno.com/v1", "http://gpustack.luxvisions-inno.com/v1/chat/completions")]
    // 已是完整 chat 地址 → 原样（去尾斜杠）
    [InlineData(AIProviderType.OpenAI, "http://localhost:1234/v1/chat/completions", "http://localhost:1234/v1/chat/completions")]
    // 无路径裸主机 → 补标准 /v1/chat/completions
    [InlineData(AIProviderType.OpenAI, "https://api.openai.com", "https://api.openai.com/v1/chat/completions")]
    // 自定义路径前缀 → 只追加接口后缀
    [InlineData(AIProviderType.OpenAI, "https://gw.example.com/gpus/v5", "https://gw.example.com/gpus/v5/chat/completions")]
    // Anthropic 基础地址 → /v1/messages
    [InlineData(AIProviderType.Anthropic, "https://api.anthropic.com/v1", "https://api.anthropic.com/v1/messages")]
    public void GetChatEndpoint_DerivesFullChatUrl(AIProviderType type, string endpoint, string expected)
    {
        OpenAICompatibleProvider.GetChatEndpointByProviderType(type, endpoint)
            .Should().Be(expected);
    }
}
