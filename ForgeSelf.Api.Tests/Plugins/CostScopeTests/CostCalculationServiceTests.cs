using ForgeSelf.Api.Plugins.CostScope.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.CostScopeTests;

/// <summary>
/// A3 成本纯函数锁死（04-task AC3 / 02-spec BR-1~BR-2、BC-3）。
/// 被测类零 IO，故无需 XCode 夹具；单价一律由用例注入，断言值来自手算黄金值。
/// </summary>
public sealed class CostCalculationServiceTests
{
    /// <summary>黄金值手算：1_000_000 tokens ÷ 1e6 × 2.4 元 = 2.4 元（FR-3.2 的单位基准）。</summary>
    [Theory]
    [InlineData(1_000_000, 0, 2.4, 0, 2.4)]
    [InlineData(0, 1_000_000, 0, 4.8, 4.8)]
    [InlineData(1_000_000, 1_000_000, 2.4, 4.8, 7.2)]
    [InlineData(1_000, 2_000, 2.4, 4.8, 0.012)]     // 1000/1e6×2.4=0.0024 + 2000/1e6×4.8=0.0096
    [InlineData(3, 7, 2.4, 4.8, 0.0000408)]        // 精度到第 8 位：0.0000072 + 0.0000336
    [InlineData(0, 0, 2.4, 4.8, 0)]                // 0 tokens
    [InlineData(1_000_000, 0, 0, 4.8, 0)]          // 单价 0 元（合法配置：真实免费模型）
    public void 单位换算_每100万token_与手算黄金值一致(
        int prompt, int completion, decimal inPrice, decimal outPrice, decimal expected)
    {
        var cost = CostCalculationService.Calculate(prompt, completion, inPrice, outPrice);

        Assert.False(cost.HasUnknownPart, "两段单价齐备时不得判为未知");
        Assert.Equal(expected, cost.Total);
    }

    [Fact]
    public void 输入输出分段_各自可单独核对()
    {
        var cost = CostCalculationService.Calculate(1_000_000, 500_000, 2.4m, 4.8m);

        Assert.Equal(2.4m, cost.InputCost);
        Assert.Equal(2.4m, cost.OutputCost); // 500_000/1e6 × 4.8 = 2.4
        Assert.Equal(4.8m, cost.Total);
    }

    /// <summary>
    /// FR-3.3 / BR-2：未配单价的模型成本是「未知」，<b>绝不默默计 0</b>——
    /// 计 0 会让用户误以为「没花钱」（032 D5）。本任务最容易写错的一条，单独钉死。
    /// </summary>
    [Fact]
    public void 未配单价_判未知_不静默计零()
    {
        var unknown = CostCalculationService.Calculate(new CostInput(1_000, 2_000, null, null));

        Assert.True(unknown.IsFullyUnknown);
        Assert.False(unknown.HasKnownPart);
        Assert.Equal(0m, unknown.Total);
    }

    /// <summary>只配了输入价：输入段照算、输出段未知，总额只是<b>下界</b>而非全量。</summary>
    [Fact]
    public void 单边有价_总额为下界且标出未知段()
    {
        var partial = CostCalculationService.Calculate(
            new CostInput(1_000_000, 1_000_000, 2.4m, null));

        Assert.Equal(2.4m, partial.InputCost);
        Assert.Null(partial.OutputCost);
        Assert.True(partial.HasUnknownPart);
        Assert.True(partial.HasKnownPart);
        Assert.False(partial.IsFullyUnknown);
        Assert.Equal(2.4m, partial.Total);
    }

    /// <summary>负 token 必须抛（02-spec「非法值 → 后端拒绝并返回 400」的前置条件）。</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void 负token_抛出ArgumentOutOfRangeException(int prompt, int completion)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CostCalculationService.Calculate(prompt, completion, 2.4m, 4.8m));
    }

    /// <summary>单价越界（负价 / 超上界）同样拒绝，不静默按 0 计。</summary>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1_000_000.01)]
    public void 非法单价_拒绝(double badPrice)
    {
        var price = (decimal)badPrice;
        Assert.False(CostCalculationService.IsPriceValid(price));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CostCalculationService.Calculate(1_000, 0, price, 4.8m));
    }

    /// <summary>0 元与「未配单价」是两个不同事实，必须可区分（与 FR-1.4 同口径）。</summary>
    [Fact]
    public void 零元单价与未配单价_可区分()
    {
        var free = CostCalculationService.Calculate(new CostInput(1_000, 1_000, 0m, 0m));
        var unpriced = CostCalculationService.Calculate(new CostInput(1_000, 1_000, null, null));

        Assert.False(free.HasUnknownPart);
        Assert.Equal(0m, free.Total);
        Assert.True(unpriced.IsFullyUnknown);
    }

    /// <summary>
    /// FR-3.4：成本<b>不入明细层</b> ⇒ 改单价后对同一批 token 重算即得新结果。
    /// 这里锁的是纯函数性质：同输入同输出、改价即改结果、无内部缓存。
    /// </summary>
    [Fact]
    public void 改价后重算_历史成本随单价变化()
    {
        var turn = new CostInput(1_000_000, 1_000_000, 2.4m, 4.8m);
        var before = CostCalculationService.Calculate(turn);

        var repriced = turn with { InputPricePer1M = 1.2m, OutputPricePer1M = 2.4m };
        var after = CostCalculationService.Calculate(repriced);

        Assert.Equal(7.2m, before.Total);
        Assert.Equal(3.6m, after.Total);
        Assert.Equal(before, CostCalculationService.Calculate(turn)); // 幂等：无隐藏状态
    }

    /// <summary>聚合口径（AC2）：未知成本的轮次不得计入总额，但要单独计数上报。</summary>
    [Fact]
    public void 聚合_未知单价不计入总额()
    {
        var rows = new[]
        {
            new CostInput(1_000_000, 0, 2.4m, 4.8m),           // 2.4
            new CostInput(500_000, 0, 2.4m, 4.8m),             // 1.2
            new CostInput(9_000_000, 9_000_000, null, null),   // 未知，不计
        };

        var total = 0m;
        var unknownCount = 0;
        foreach (var row in rows)
        {
            var cost = CostCalculationService.Calculate(row);
            if (cost.HasUnknownPart)
            {
                unknownCount++;
                continue; // BR-2：未知不进总额，也不拿 0 冒充「已计」
            }

            total += cost.Total;
        }

        Assert.Equal(3.6m, total);
        Assert.Equal(1, unknownCount);
    }
}
