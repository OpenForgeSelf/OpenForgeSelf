using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

public class OarHandlersHonestyGuardTests
{
    [Fact]
    public void handlers_ts_无installation不得返回bundled_available()
    {
        var path = Find("Plugins/AgentHub/RuntimeWorker/src/handlers.ts");
        var text = File.ReadAllText(path);
        text.Should().NotContain("treat missing probe as bundled");
        // 精确拦假路径：return { … kind:'available' … via:'bundled' }（注释里的 unavailable/bundled 不算）
        text.Should().NotMatchRegex(@"kind:\s*'available'\s*,\s*via:\s*'bundled'");
        text.Should().Contain("kind: 'not_found'");
    }

    [Fact]
    public void OarTransport_不得用短Delay冒充Exit成功()
    {
        var path = Find("Plugins/AgentHub/Services/OarTransport.cs");
        var text = File.ReadAllText(path);
        text.Should().NotContain("await Task.Delay(50, ct)");
        text.Should().Contain("WaitUntilSettledAsync");
        text.Should().Contain("SessionStatusAsync");
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var p = Path.Combine(dir.FullName, relative);
            if (File.Exists(p)) return p;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relative);
    }
}
