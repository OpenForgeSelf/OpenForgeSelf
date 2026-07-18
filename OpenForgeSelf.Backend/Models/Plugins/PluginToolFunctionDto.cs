namespace OpenForgeSelf.Backend.Models.Plugins;

public class PluginToolFunctionDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    public string ParametersJsonSchema { get; set; } = string.Empty;
}
