namespace OpenForgeSelf.Backend.Models.Plugins;

public class PluginMenuItemDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public int Order { get; set; }

    public string? ParentId { get; set; }

    public string PluginId { get; set; } = string.Empty;

    public List<PluginMenuItemDto>? Children { get; set; }
}
