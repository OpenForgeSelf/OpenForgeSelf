using OpenForgeSelf.Backend.Plugins.Abstractions;

namespace OpenForgeSelf.Backend.Tests.Plugins;

public class FakeMenuPlugin : FakePlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();

    public FakeMenuPlugin()
    {
        Id = "test.menu.plugin";
        Name = "Fake Menu Plugin";
    }

    public void AddMenuExtension(IMenuExtension extension)
    {
        MenuExtensions.Add(extension);
    }
}

public class FakeMenuExtension : IMenuExtension
{
    public string Id { get; set; } = "test.menu.item1";
    public string Name { get; set; } = "Test Menu Item";
    public string PluginId { get; set; } = "test.menu.plugin";
    public string Icon { get; set; } = "fa-test";
    public string Path { get; set; } = "/test";
    public int Order { get; set; } = 100;
    public string? ParentId { get; set; }
    public IReadOnlyList<IMenuExtension>? Children { get; set; }
}
