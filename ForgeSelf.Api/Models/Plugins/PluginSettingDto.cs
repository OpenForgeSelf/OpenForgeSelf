namespace ForgeSelf.Api.Models.Plugins;

public class PluginSettingDto
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string ValueType { get; set; } = "string";

    public string? DefaultValue { get; set; }

    public bool IsRequired { get; set; }

    public List<string>? Options { get; set; }

    public string Category { get; set; } = string.Empty;

    public int Order { get; set; }
}
