using OpenForgeSelf.Backend.Plugins.Abstractions;

namespace OpenForgeSelf.Backend.Tests.Plugins;

public class FakeToolPlugin : FakePlugin
{
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public FakeToolPlugin()
    {
        Id = "test.tool.plugin";
        Name = "Fake Tool Plugin";
    }

    public void AddToolExtension(IToolFunctionExtension extension)
    {
        ToolExtensions.Add(extension);
    }
}

public class FakeToolFunctionExtension : IToolFunctionExtension
{
    public string Id { get; set; } = "test.tool.func1";
    public string Name { get; set; } = "Test Tool Function";
    public string PluginId { get; set; } = "test.tool.plugin";
    public string Description { get; set; } = "A test tool function";
    public string ParametersJsonSchema { get; set; } = "{}";

    public Func<string, Task<string>>? ExecuteHandler { get; set; }

    public Task<string> ExecuteAsync(string parameters)
    {
        if (ExecuteHandler != null)
            return ExecuteHandler(parameters);

        return Task.FromResult("{}");
    }
}
