using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace OpenForgeSelf.Backend.Controllers.UnifiedAI;

/// <summary>
/// Agent Framework 测试接口使用的「服务端自有工具」示例。
/// 这些工具由 agent 在服务端自动执行（区别于代理模式下由客户端执行的工具），
/// 用于验证 Microsoft Agent Framework 的 agent 能正确完成「请求 → 模型决策 → 执行工具 → 汇总」的完整流程，
/// 且工具调用不会在流式响应中被遗漏。
/// </summary>
public static class AgentDemoTools
{
    [Description("获取当前的 UTC 日期与时间，返回 ISO8601 格式字符串。")]
    public static string GetCurrentDateTime()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    [Description("计算两个整数的和并返回结果。")]
    public static int AddNumbers(
        [Description("第一个整数")] int a,
        [Description("第二个整数")] int b)
    {
        return a + b;
    }
}
