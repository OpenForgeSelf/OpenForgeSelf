using Microsoft.Extensions.Primitives;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Tests.Services;

public class MvcActionDescriptorChangeProviderTests
{
    [Fact]
    public void NotifyChange_TriggersOnChangeConsumer()
    {
        var provider = new MvcActionDescriptorChangeProvider();
        var fired = 0;

        using var registration = ChangeToken.OnChange(provider.GetChangeToken, () => fired++);

        provider.NotifyChange();

        fired.Should().Be(1);
    }

    [Fact]
    public void NotifyChange_MultipleTimes_FiresConsumerEachTime()
    {
        // 关键回归：ChangeToken.OnChange 在回调内会重新订阅；实现必须「先换新 CTS、再取消旧 CTS」，
        // 保证重订阅拿到未取消的新 token——否则第二次通知失效或造成递归/死循环。
        var provider = new MvcActionDescriptorChangeProvider();
        var fired = 0;

        using var registration = ChangeToken.OnChange(provider.GetChangeToken, () => fired++);

        provider.NotifyChange();
        provider.NotifyChange();
        provider.NotifyChange();

        fired.Should().Be(3);
    }

    [Fact]
    public void NotifyChange_WithoutListener_DoesNotThrow()
    {
        var provider = new MvcActionDescriptorChangeProvider();

        var action = () => provider.NotifyChange();

        action.Should().NotThrow();
    }

    [Fact]
    public void GetChangeToken_BeforeAndAfterNotify_ReturnsUsableTokens()
    {
        // 通知后 GetChangeToken 必须返回未触发的 token（供框架重新订阅）。
        var provider = new MvcActionDescriptorChangeProvider();

        provider.NotifyChange();

        var token = provider.GetChangeToken();
        token.HasChanged.Should().BeFalse();
        token.ActiveChangeCallbacks.Should().BeTrue();
    }
}
