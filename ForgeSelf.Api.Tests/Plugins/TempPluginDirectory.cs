using System.Text.Json;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Tests.Plugins;

public class TempPluginDirectory
{
    public string RootPath { get; }

    public TempPluginDirectory()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"test_plugins_{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string CreatePluginDirectory(string pluginId)
    {
        var pluginDir = Path.Combine(RootPath, pluginId);
        Directory.CreateDirectory(pluginDir);
        return pluginDir;
    }

    public void CreatePluginManifest(string pluginDir, PluginMetadata metadata)
    {
        var manifestPath = Path.Combine(pluginDir, "plugin.json");
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(manifestPath, json);
    }

    public void CreatePluginManifest(string pluginId, Action<PluginMetadata>? configure = null)
    {
        var metadata = PluginManifestGenerator.CreateBasic(pluginId);
        configure?.Invoke(metadata);
        var pluginDir = CreatePluginDirectory(pluginId);
        CreatePluginManifest(pluginDir, metadata);
    }

    public void CreateFakeAssembly(string pluginDir, string assemblyName)
    {
        var assemblyPath = Path.Combine(pluginDir, assemblyName);
        File.WriteAllBytes(assemblyPath, Array.Empty<byte>());
    }
}

public static class PluginManifestGenerator
{
    public static PluginMetadata CreateBasic(string pluginId)
    {
        return new PluginMetadata
        {
            Id = pluginId,
            Name = $"Test Plugin {pluginId}",
            Version = "1.0.0",
            Author = "Test Author",
            Description = $"A test plugin with ID {pluginId}",
            IconUrl = "https://example.com/icon.png",
            EntryAssembly = $"{pluginId}.dll",
            EntryType = $"{pluginId}.PluginEntry",
            Dependencies = new List<string>(),
            Permissions = new List<string>()
        };
    }

    public static PluginMetadata WithDependencies(this PluginMetadata metadata, params string[] dependencies)
    {
        metadata.Dependencies = new List<string>(dependencies);
        return metadata;
    }

    public static PluginMetadata WithPermissions(this PluginMetadata metadata, params string[] permissions)
    {
        metadata.Permissions = new List<string>(permissions);
        return metadata;
    }

    public static PluginMetadata WithEntryAssembly(this PluginMetadata metadata, string entryAssembly)
    {
        metadata.EntryAssembly = entryAssembly;
        return metadata;
    }

    public static PluginMetadata WithEntryType(this PluginMetadata metadata, string entryType)
    {
        metadata.EntryType = entryType;
        return metadata;
    }
}
