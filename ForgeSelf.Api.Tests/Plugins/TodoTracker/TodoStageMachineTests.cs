using FluentAssertions;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Xunit;
using TodoStatus = ForgeSelf.Api.Plugins.TodoTracker.TodoStatus;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 下发阶段状态机（PILOT-054 · BR-4 / AC-10）。
/// 这里只测纯规则；「非法流转返回 409」那条在 <c>TodoTrackerStageApiTests</c> 里用真实服务验。
/// </summary>
public class TodoStageMachineTests
{
    [Fact]
    public void 八个阶段都应能从名称或数字解析回来()
    {
        foreach (var value in TodoStage.All)
        {
            var name = TodoStage.ToName(value);

            TodoStage.TryParse(name, out var fromName).Should().BeTrue(name);
            fromName.Should().Be(value);

            TodoStage.TryParse(value.ToString(), out var fromNumber).Should().BeTrue(value.ToString());
            fromNumber.Should().Be(value);

            TodoStage.TryParse(name.ToLowerInvariant(), out var lower).Should().BeTrue("入参大小写不敏感");
            lower.Should().Be(value);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Doing")]
    [InlineData("8")]
    [InlineData("-1")]
    public void 无效阶段值应解析失败并由调用方给出可用清单(string? raw)
    {
        TodoStage.TryParse(raw, out _).Should().BeFalse($"{raw} 不是合法阶段");
    }

    [Fact]
    public void 显式空串与未传是两回事_空串是客户端错误()
    {
        // 口径来自 plugin-development §C：未传可给默认值，显式传空串属客户端错误
        TodoStage.TryParse(string.Empty, out _).Should().BeFalse();
    }

    [Theory]
    // 主干：草稿 → 就绪 → 已下发 → 执行中 → 待验收 → 完成
    [InlineData(TodoStage.Draft, TodoStage.Ready, true)]
    [InlineData(TodoStage.Ready, TodoStage.Dispatched, true)]
    [InlineData(TodoStage.Dispatched, TodoStage.Running, true)]
    [InlineData(TodoStage.Running, TodoStage.Review, true)]
    [InlineData(TodoStage.Review, TodoStage.Done, true)]
    // 阻塞与解冻
    [InlineData(TodoStage.Dispatched, TodoStage.Blocked, true)]
    [InlineData(TodoStage.Running, TodoStage.Blocked, true)]
    [InlineData(TodoStage.Blocked, TodoStage.Running, true)]
    [InlineData(TodoStage.Blocked, TodoStage.Ready, true)]
    // 打回与重开
    [InlineData(TodoStage.Review, TodoStage.Running, true)]
    [InlineData(TodoStage.Done, TodoStage.Running, true)]
    [InlineData(TodoStage.Cancelled, TodoStage.Review, true)]
    // 撤回与取消
    [InlineData(TodoStage.Dispatched, TodoStage.Ready, true)]
    [InlineData(TodoStage.Ready, TodoStage.Cancelled, true)]
    // 同态幂等
    [InlineData(TodoStage.Running, TodoStage.Running, true)]
    // 非法：跳步（下发这一步必须留痕）
    [InlineData(TodoStage.Draft, TodoStage.Dispatched, false)]
    [InlineData(TodoStage.Draft, TodoStage.Running, false)]
    [InlineData(TodoStage.Ready, TodoStage.Running, false)]
    [InlineData(TodoStage.Ready, TodoStage.Review, false)]
    [InlineData(TodoStage.Draft, TodoStage.Done, false)]
    [InlineData(TodoStage.Draft, TodoStage.Blocked, false)]
    [InlineData(TodoStage.Blocked, TodoStage.Done, false)]
    [InlineData(TodoStage.Blocked, TodoStage.Review, false)]
    [InlineData(TodoStage.Done, TodoStage.Draft, false)]
    [InlineData(TodoStage.Cancelled, TodoStage.Draft, false)]
    public void 合法与非法边应逐条对得上流转图(int from, int to, bool allowed)
    {
        TodoStage.CanTransit(from, to).Should().Be(allowed,
            $"{TodoStage.ToName(from)} → {TodoStage.ToName(to)} 的判定与 BR-4 流转图不一致");
    }

    [Fact]
    public void 非法流转的说明必须列出可达目标()
    {
        var message = TodoStage.DescribeAllowed(TodoStage.Ready);

        message.Should().Contain(TodoStage.ToName(TodoStage.Dispatched));
        message.Should().Contain(TodoStage.ToLabel(TodoStage.Dispatched));
        message.Should().NotContain(TodoStage.ToName(TodoStage.Review), "就绪态不能直接跳待验收，提示里也不该出现它");
    }

    [Fact]
    public void 每个阶段都应至少有一条出边或说明自己是死路()
    {
        foreach (var value in TodoStage.All)
        {
            var next = TodoStage.NextOf(value);
            next.Should().NotBeEmpty($"{TodoStage.ToName(value)} 必须说得出下一步能去哪");
            next.Should().OnlyContain(t => TodoStage.CanTransit(value, t), "可达集合与 CanTransit 必须同源");
        }
    }

    [Theory]
    [InlineData(TodoStage.Draft, TodoStatus.PendingValue)]
    [InlineData(TodoStage.Running, TodoStatus.PendingValue)]
    [InlineData(TodoStage.Review, TodoStatus.PendingValue)]
    [InlineData(TodoStage.Blocked, TodoStatus.PendingValue)]
    [InlineData(TodoStage.Done, TodoStatus.CompletedValue)]
    public void 旧二元状态应由阶段派生且与Home口径兼容(int stage, int expectedLegacy)
    {
        TodoStage.ToLegacyStatus(stage).Should().Be(expectedLegacy);
    }

    [Fact]
    public void 终态判定只认完成与取消()
    {
        TodoStage.IsTerminal(TodoStage.Done).Should().BeTrue();
        TodoStage.IsTerminal(TodoStage.Cancelled).Should().BeTrue();
        TodoStage.IsTerminal(TodoStage.Review).Should().BeFalse("待验收还没结论，不能算终态");
        TodoStage.IsTerminal(TodoStage.Blocked).Should().BeFalse("阻塞是要人来解的，不是终态");
    }
}
