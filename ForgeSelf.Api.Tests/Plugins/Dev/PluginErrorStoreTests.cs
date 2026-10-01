using ForgeSelf.Api.Plugins.Dev;

namespace ForgeSelf.Api.Tests.Plugins.Dev;

/// <summary>插件错误仓库生命周期测试（进程内静态存储，用例用唯一 pluginId 隔离）。</summary>
public class PluginErrorStoreTests
{
    private static string UniqueId() => "err-store-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void Set_Then_TryGet_ReturnsFullRecord()
    {
        var id = UniqueId();

        try
        {
            var ex = new InvalidOperationException("入口类型不存在", new Exception("inner"));

            PluginErrorStore.Set(id, ex);
            var record = PluginErrorStore.TryGet(id);

            record.Should().NotBeNull();
            record!.Message.Should().Be("入口类型不存在");
            record.ExceptionType.Should().Be("System.InvalidOperationException");
            record.OccurredAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        }
        finally
        {
            PluginErrorStore.Clear(id);
        }
    }

    [Fact]
    public void Set_Twice_KeepsLatest()
    {
        var id = UniqueId();

        try
        {
            PluginErrorStore.Set(id, new Exception("first"));
            PluginErrorStore.Set(id, new Exception("second"));

            PluginErrorStore.TryGet(id)!.Message.Should().Be("second");
        }
        finally
        {
            PluginErrorStore.Clear(id);
        }
    }

    [Fact]
    public void Clear_RemovesRecord()
    {
        var id = UniqueId();
        PluginErrorStore.Set(id, new Exception("boom"));

        PluginErrorStore.Clear(id);

        PluginErrorStore.TryGet(id).Should().BeNull();
    }

    [Fact]
    public void TryGet_UnknownId_ReturnsNull()
    {
        PluginErrorStore.TryGet("never-exists-" + Guid.NewGuid().ToString("N")).Should().BeNull();
    }

    [Fact]
    public void Set_NullOrEmptyId_Ignored()
    {
        var before = PluginErrorStore.GetAll().Count;

        PluginErrorStore.Set("", new Exception("x"));
        PluginErrorStore.Set(null!, new Exception("x"));

        PluginErrorStore.GetAll().Count.Should().Be(before);
    }
}
