using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems;
using ForgeSelf.Api.Plugins.Sems.Controllers;
using ForgeSelf.Api.Plugins.Sems.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// 工程插件只读查询（GET api/projects/{id}/engineering-plugins）与扫描行为。
/// 用 Mock <see cref="IProjectRegistry"/> 解析项目根目录，扫描落在临时目录的 package.json；
/// 测试目录只创建不删除。不触碰 SQLite / HostProjectRegistry。
/// </summary>
public class EngineeringPluginsControllerTests
{
    private readonly string _tempRoot;
    private readonly Mock<IProjectRegistry> _registry = new();
    private readonly EngineeringPluginsController _controller;

    public EngineeringPluginsControllerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ForgeSelfSemsEng_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IProjectRegistry>()).Returns(_registry.Object);
        _controller = new EngineeringPluginsController(new EngineeringPluginService(ctx.Object));
    }

    private string CreateProjectDir(string name)
    {
        var path = Path.Combine(_tempRoot, "projects", name);
        Directory.CreateDirectory(path);
        return path;
    }

    private int RegisterProject(string name)
    {
        var dir = CreateProjectDir(name);
        const int id = 17;
        _registry.Setup(r => r.Get(id)).Returns(new ProjectInfo { Id = id, Root = dir, Name = name });
        return id;
    }

    private static (int Status, JsonElement Body) Decode(IActionResult result)
    {
        var (status, value) = result switch
        {
            OkObjectResult ok => (ok.StatusCode ?? 200, ok.Value),
            ObjectResult obj => (obj.StatusCode ?? 500, obj.Value),
            _ => throw new InvalidOperationException($"未预期的结果类型：{result.GetType().Name}")
        };
        var body = JsonSerializer.SerializeToElement(value ?? new { }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return (status, body);
    }

    private static void WritePackageJson(string projectRoot, string json)
    {
        File.WriteAllText(Path.Combine(projectRoot, "package.json"), json);
    }

    [Fact]
    public void List_Returns_Plugins_From_All_Dependency_Buckets_With_Version_And_Group()
    {
        var id = RegisterProject("mixed-deps");
        var root = _registry.Object.Get(id)!.Root;
        WritePackageJson(root, """
            {
              "name": "demo",
              "dependencies": {
                "@vitejs/plugin-vue": "^5.2.1",
                "vue": "^3.5.0"
              },
              "devDependencies": {
                "eslint-plugin-vue": "^9.0.0",
                "eslint-config-prettier": "^9.1.0",
                "@typescript-eslint/eslint-plugin": "^8.0.0",
                "babel-plugin-import": "^1.13.8",
                "vite-plugin-dts": "^4.0.0"
              },
              "optionalDependencies": {
                "prettier-plugin-tailwindcss": "^0.6.0",
                "html-webpack-plugin": "^5.6.0",
                "@vue/cli-plugin-babel": "~5.0.0"
              }
            }
            """);

        var (status, body) = Decode(_controller.List(id).Result!);

        status.Should().Be(200);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.TryGetProperty("error", out var errorEl).Should().BeTrue();
        (errorEl.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
         || (errorEl.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(errorEl.GetString())))
            .Should().BeTrue("成功或空态时 error 应为 null 或空");
        body.GetProperty("total").GetInt32().Should().Be(8);

        var plugins = body.GetProperty("plugins").EnumerateArray().ToList();
        plugins.Select(p => p.GetProperty("name").GetString())
            .Should().Equal(
                "@typescript-eslint/eslint-plugin",
                "@vitejs/plugin-vue",
                "@vue/cli-plugin-babel",
                "babel-plugin-import",
                "eslint-plugin-vue",
                "html-webpack-plugin",
                "prettier-plugin-tailwindcss",
                "vite-plugin-dts");

        static (string Version, string Group, string Source) Row(JsonElement el) =>
            (el.GetProperty("version").GetString()!, el.GetProperty("group").GetString()!, el.GetProperty("source").GetString()!);

        var map = plugins.ToDictionary(p => p.GetProperty("name").GetString()!, Row);
        map["@vitejs/plugin-vue"].Should().Be(("^5.2.1", "构建", "package.json"));
        map["vite-plugin-dts"].Should().Be(("^4.0.0", "构建", "package.json"));
        map["html-webpack-plugin"].Should().Be(("^5.6.0", "构建", "package.json"));
        map["babel-plugin-import"].Should().Be(("^1.13.8", "转译", "package.json"));
        map["eslint-plugin-vue"].Should().Be(("^9.0.0", "代码质量", "package.json"));
        map["@typescript-eslint/eslint-plugin"].Should().Be(("^8.0.0", "代码质量", "package.json"));
        map["prettier-plugin-tailwindcss"].Should().Be(("^0.6.0", "代码质量", "package.json"));
        map["@vue/cli-plugin-babel"].Should().Be(("~5.0.0", "框架", "package.json"));

        plugins.Select(p => p.GetProperty("name").GetString())
            .Should().NotContain(n => n!.Contains("eslint-config") || n.StartsWith("sems_") || n == "vue");
    }

    [Fact]
    public void List_Prefers_Dependencies_When_Same_Package_Is_Duplicated()
    {
        var id = RegisterProject("dedup");
        var root = _registry.Object.Get(id)!.Root;
        WritePackageJson(root, """
            {
              "dependencies": { "vite-plugin-dts": "^4.0.0" },
              "devDependencies": { "vite-plugin-dts": "^3.0.0" },
              "optionalDependencies": { "vite-plugin-dts": "^2.0.0" }
            }
            """);

        var (status, body) = Decode(_controller.List(id).Result!);
        status.Should().Be(200);
        body.GetProperty("total").GetInt32().Should().Be(1);
        var plugin = body.GetProperty("plugins")[0];
        plugin.GetProperty("name").GetString().Should().Be("vite-plugin-dts");
        plugin.GetProperty("version").GetString().Should().Be("^4.0.0");
    }

    [Fact]
    public void List_Missing_PackageJson_Returns_Empty_List_Without_Error()
    {
        var id = RegisterProject("no-pkg");

        var (status, body) = Decode(_controller.List(id).Result!);

        status.Should().Be(200);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("total").GetInt32().Should().Be(0);
        body.GetProperty("plugins").GetArrayLength().Should().Be(0);
        var error = body.GetProperty("error");
        (error.ValueKind == JsonValueKind.Null || string.IsNullOrEmpty(error.GetString()))
            .Should().BeTrue();
    }

    [Fact]
    public void List_PackageJson_Without_Matching_Plugins_Is_Empty_And_Has_No_Sems_Names()
    {
        var id = RegisterProject("plain-deps");
        var root = _registry.Object.Get(id)!.Root;
        Directory.CreateDirectory(Path.Combine(root, "node_modules", "vite-plugin-hidden"));
        File.WriteAllText(Path.Combine(root, "node_modules", "vite-plugin-hidden", "package.json"),
            """{ "name": "vite-plugin-hidden", "version": "1.0.0" }""");
        WritePackageJson(root, """
            {
              "dependencies": {
                "vue": "^3.5.0",
                "eslint-config-prettier": "^9.1.0",
                "sems_list_projects": "1.3.0",
                "typescript": "^5.6.0"
              },
              "devDependencies": {
                "@vue/eslint-config-typescript": "^14.0.0"
              }
            }
            """);

        var (status, body) = Decode(_controller.List(id).Result!);

        status.Should().Be(200);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("total").GetInt32().Should().Be(0);
        body.GetProperty("plugins").GetArrayLength().Should().Be(0);
        var error = body.GetProperty("error");
        (error.ValueKind == JsonValueKind.Null || string.IsNullOrEmpty(error.GetString()))
            .Should().BeTrue();
        body.GetRawText().Should().NotContain("sems_");
        body.GetRawText().Should().NotContain("vite-plugin-hidden");
    }

    [Fact]
    public void List_Invalid_Json_Returns_200_With_Empty_List_And_Error()
    {
        var id = RegisterProject("bad-json");
        var root = _registry.Object.Get(id)!.Root;
        WritePackageJson(root, "{ name: not-json, ");

        var (status, body) = Decode(_controller.List(id).Result!);

        status.Should().Be(200);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("total").GetInt32().Should().Be(0);
        body.GetProperty("plugins").GetArrayLength().Should().Be(0);
        body.GetProperty("error").GetString().Should().Contain("不是合法 JSON");
    }

    [Fact]
    public void List_Missing_Project_Returns_404()
    {
        _registry.Setup(r => r.Get(It.IsAny<int>())).Returns((ProjectInfo?)null);

        var (status, body) = Decode(_controller.List(9_999_999).Result!);

        status.Should().Be(404);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("message").GetString().Should().Contain("项目不存在");
    }

    [Fact]
    public void List_Returns_503_When_Registry_Unavailable()
    {
        var controller = new EngineeringPluginsController(new EngineeringPluginService(new Mock<IContext>().Object));

        var (status, body) = Decode(controller.List(1).Result!);

        status.Should().Be(503);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("message").GetString().Should().Contain("项目注册表不可用");
    }

    [Fact]
    public void List_Unreadable_PackageJson_Returns_200_With_Error()
    {
        if (!OperatingSystem.IsLinux())
            return;

        ListUnreadablePackageJsonOnLinux();
    }

    [SupportedOSPlatform("linux")]
    private void ListUnreadablePackageJsonOnLinux()
    {
        var id = RegisterProject("unreadable-file");
        var root = _registry.Object.Get(id)!.Root;
        var packagePath = Path.Combine(root, "package.json");
        WritePackageJson(root, """{ "devDependencies": { "vite-plugin-dts": "^4.0.0" } }""");
        File.SetUnixFileMode(packagePath, (UnixFileMode)0);

        try
        {
            var (status, body) = Decode(_controller.List(id).Result!);

            status.Should().Be(200);
            body.GetProperty("success").GetBoolean().Should().BeTrue();
            body.GetProperty("total").GetInt32().Should().Be(0);
            body.GetProperty("plugins").GetArrayLength().Should().Be(0);
            body.GetProperty("error").GetString().Should().Contain("无法读取");
        }
        finally
        {
            File.SetUnixFileMode(packagePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void List_Unreadable_Directory_Returns_200_With_Error()
    {
        if (!OperatingSystem.IsLinux())
            return;

        ListUnreadableDirectoryOnLinux();
    }

    [SupportedOSPlatform("linux")]
    private void ListUnreadableDirectoryOnLinux()
    {
        var id = RegisterProject("unreadable-dir");
        var root = _registry.Object.Get(id)!.Root;
        WritePackageJson(root, """{ "devDependencies": { "vite-plugin-dts": "^4.0.0" } }""");
        var previous = File.GetUnixFileMode(root);
        File.SetUnixFileMode(root, (UnixFileMode)0);

        try
        {
            var (status, body) = Decode(_controller.List(id).Result!);

            status.Should().Be(200);
            body.GetProperty("success").GetBoolean().Should().BeTrue();
            body.GetProperty("total").GetInt32().Should().Be(0);
            body.GetProperty("plugins").GetArrayLength().Should().Be(0);
            body.GetProperty("error").GetString().Should().Contain("目录不可读");
        }
        finally
        {
            File.SetUnixFileMode(root, previous);
        }
    }

    [Fact]
    public void Controller_Has_Class_Level_ApiKeyPolicy_And_No_Anonymous_Endpoint()
    {
        var type = typeof(EngineeringPluginsController);
        var auth = type.GetCustomAttribute<AuthorizeAttribute>();
        auth.Should().NotBeNull();
        auth!.Policy.Should().Be("ApiKeyPolicy");

        type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Should().NotContain(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);
    }

    [Fact]
    public void Apply_Registers_EngineeringPluginService_Without_Changing_Tool_Count()
    {
        var services = new ServiceCollection();
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<PluginMetadata>()).Returns(new PluginMetadata { Id = "sems" });
        ctx.Setup(c => c.Get<IServiceCollection>()).Returns(services);

        var plugin = new SemsPlugin();
        plugin.Apply(ctx.Object);

        plugin.ToolExtensions.Should().HaveCount(22);
        plugin.ToolExtensions.Select(t => t.Name).Should().OnlyContain(n => n.StartsWith("sems_"));
        services.Should().Contain(d =>
            d.ServiceType == typeof(IEngineeringPluginService)
            && d.ImplementationType == typeof(EngineeringPluginService)
            && d.Lifetime == ServiceLifetime.Scoped);
    }
}
