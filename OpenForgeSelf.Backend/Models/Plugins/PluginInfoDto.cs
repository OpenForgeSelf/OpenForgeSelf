using OpenForgeSelf.Backend.Plugins.Abstractions;

namespace OpenForgeSelf.Backend.Models.Plugins;

public class PluginInfoDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IconUrl { get; set; } = string.Empty;

    public PluginState State { get; set; }

    public bool IsEnabled { get; set; }

    public string Category { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    public long InstallCount { get; set; }

    public double Rating { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
