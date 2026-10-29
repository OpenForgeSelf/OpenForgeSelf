using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// SemsPlugin.Apply 的注册守卫（03-plan 增补方案二）：工具类型集合、Id、plugin.json 里的工具数都动态断言。
/// </summary>
public class SemsPluginRegistrationTests
{
    private static readonly string[] OriginalThirteen =
    {
        "sems_list_projects", "sems_register_project", "sems_update_project", "sems_remove_project",
        "sems_list_commands", "sems_add_command", "sems_update_command", "sems_delete_command",
        "sems_list_runs", "sems_check_runs", "sems_run_command", "sems_stop_command", "sems_stop_run"
    };

    private sealed class RecordingContext : IContext
    {
        private readonly Dictionary<Type, object> _local = new();

        public void Register<TService>(TService instance) where TService : class { }

        public void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new() { }

        public void RegisterLocal<TService>(TService instance) where TService : class
            => _local[typeof(TService)] = instance;

        public TService? Get<TService>() where TService : class
            => GetService(typeof(TService)) as TService;

        public object? GetService(Type serviceType)
            => _local.TryGetValue(serviceType, out var local) ? local : null;

        public IDisposable Effect(Func<IDisposable> sideEffect) => sideEffect();
        public IEventBus Events => throw new NotSupportedException();
        public IContext Derive() => throw new NotSupportedException();
    }

    [Fact]
    public void Apply_Registers_Every_Concrete_Tool_With_Unique_Ids_Matching_Manifest_Count()
    {
        var services = new ServiceCollection();
        var ctx = new RecordingContext();
        ctx.RegisterLocal(new PluginMetadata { Id = "sems" });
        ctx.RegisterLocal<IServiceCollection>(services);

        var plugin = new SemsPlugin();
        plugin.Apply(ctx);

        var asm = typeof(SemsPlugin).Assembly;
        var concrete = asm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IToolFunctionExtension).IsAssignableFrom(t))
            .ToHashSet();
        var registered = plugin.ToolExtensions.Select(t => t.GetType()).ToHashSet();

        Assert.NotEmpty(plugin.ToolExtensions);
        Assert.Equal(concrete, registered);
        Assert.Equal(plugin.ToolExtensions.Count, plugin.ToolExtensions.Select(t => t.Id).Distinct().Count());
        Assert.All(plugin.ToolExtensions, t =>
        {
            Assert.StartsWith("sems.tool.", t.Id);
            Assert.StartsWith("sems_", t.Name);
            Assert.Equal("sems", t.PluginId);
        });
        Assert.All(OriginalThirteen, name => Assert.Contains(plugin.ToolExtensions, t => t.Name == name));
        Assert.Contains(services, d => d.ServiceType == typeof(ForgeSelf.Api.Plugins.Sems.Services.ISpecService));

        var root = RepoRoot();
        var json = File.ReadAllText(Path.Combine(root, "Plugins", "Sems", "plugin.json"));
        var match = Regex.Match(json, @"(\d+)\s*个\s*sems_\*\s*工具");
        Assert.True(match.Success, "plugin.json 描述里应有「N 个 sems_* 工具」");
        Assert.Equal(plugin.ToolExtensions.Count, int.Parse(match.Groups[1].Value));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }
}
