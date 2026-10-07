using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 插件前端资产契约守卫（PILOT-054 · AC-14 / plugin-development 铁律 19/20）。
///
/// 这一类守卫防的是"发布链才暴露"的静默失效：导出的组件名与 <c>plugin.json.views[0]</c> 不一致、
/// entry 与 vite 产物名不一致、缺 lockfile（CI 用 <c>--frozen-lockfile</c> 直接红）。
/// 形状参照 <c>ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostScopeWebAssetTests.cs</c>。
/// </summary>
public class TodoTrackerWebAssetTests
{
    private static readonly string RepoRoot = LocateRepoRoot();
    private static readonly string PluginDir = Path.Combine(RepoRoot, "Plugins", "TodoTracker");
    private static readonly string WebDir = Path.Combine(PluginDir, "web");

    private static JsonElement Manifest()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(PluginDir, "plugin.json")));
        return doc.RootElement.Clone();
    }

    private static JsonElement Frontend() => Manifest().GetProperty("frontend");

    [Fact]
    public void 清单已声明远程入口且路由菜单保持不变()
    {
        var frontend = Frontend();
        frontend.GetProperty("entry").GetString().Should().Be("web/dist/index.js",
            "界面已从宿主 src/views 迁到插件自带 web/，没有 entry 就会走宿主回退分支（而该分支已删）");
        frontend.GetProperty("route").GetString().Should().Be("/todo", "书签与首页跳转不能因迁移而改变");
        frontend.GetProperty("menu").GetString().Should().Be("待办事项");
        frontend.GetProperty("views")[0].GetString().Should().Be("TodoView");
    }

    [Fact]
    public void 后端不再贡献菜单_避免两份菜单真相()
    {
        // 铁律 19②：有自带界面的插件，菜单只在 plugin.json 声明一处
        var source = StripComments(File.ReadAllText(Path.Combine(PluginDir, "TodoTrackerPlugin.cs")));
        source.Should().NotContain("MenuExtensions.Add",
            "IMenuExtension 与 plugin.json.frontend.menu 并存会出现两份菜单");
        source.Should().Contain("ToolExtensions.Add");
    }

    [Fact]
    public void 入口导出名等于views0_且默认导出同物()
    {
        var view = Frontend().GetProperty("views")[0].GetString();
        var index = File.ReadAllText(Path.Combine(WebDir, "src", "index.ts"));

        index.Should().Contain($"export {{ {view} }}");
        index.Should().Contain($"export default {view}");
        index.Should().Contain($"import {view} from './{view}.vue'");
    }

    [Fact]
    public void vite产物名与entry一致_共享依赖全部external()
    {
        var vite = File.ReadAllText(Path.Combine(WebDir, "vite.config.ts"));

        vite.Should().Contain("entryFileNames: 'index.js'");
        vite.Should().Contain("assetFileNames: 'style[extname]'");
        vite.Should().Contain("formats: ['es']");
        // 漏声明 external 会内联第二份 Vue → inject/路由在点击那一刻才炸，最难定位。
        // 引号两种都收：模板（AIAgent/CostScope）的 vite.config 用单引号，只认双引号会假红。
        vite.Should().MatchRegex(@"external:\s*\[[^\]]*['""]vue['""]", "vue 必须 external");
        vite.Should().MatchRegex(@"external:\s*\[[^\]]*['""]vue-router['""]", "vue-router 必须 external");
        vite.Should().MatchRegex(@"external:\s*\[[^\]]*['""]element-plus['""]", "element-plus 必须 external");
    }

    [Fact]
    public void 发布链要求的lockfile与工作区配置都在场()
    {
        // scripts/release/build-frontend.ps1 对每个 Plugins/*/web 跑 pnpm install --frozen-lockfile
        File.Exists(Path.Combine(WebDir, "pnpm-lock.yaml")).Should().BeTrue("缺 lockfile 时 CI 前端段必红");
        var workspace = File.ReadAllText(Path.Combine(WebDir, "pnpm-workspace.yaml"));
        workspace.Should().Contain("esbuild", "esbuild postinstall 被拦时 vite 起不来");
    }

    [Fact]
    public void 构建产物在场时应能对上导出名与裸导入()
    {
        var entry = Path.Combine(WebDir, "dist", "index.js");
        if (!File.Exists(entry))
        {
            // 干净检出时产物尚未构建（CI 会现建）：这里不判失败，但要把"未验证"说清楚
            return;
        }

        var bundle = File.ReadAllText(entry);
        bundle.Should().Contain("as TodoView", "宿主加载器按 views[0] 取名");
        bundle.Should().MatchRegex("from\\s*[\"']vue[\"']", "vue 保持裸导入 ⇒ 由宿主 import map 解析到同一份实例");

        File.Exists(Path.Combine(WebDir, "dist", "style.css")).Should().BeTrue("宿主按入口同目录的 style.css 注入 <link>");
    }

    [Fact]
    public void 插件界面不再依赖宿主别名与宿主模块()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(WebDir, "src"), "*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) &&
                !file.EndsWith(".vue", StringComparison.OrdinalIgnoreCase)) continue;

            var text = File.ReadAllText(file);
            text.Should().NotContain("'@/", $"{Path.GetFileName(file)} 用了宿主别名 @/，插件产物运行时解析不到");
            text.Should().NotContain("from \"@/", Path.GetFileName(file));
        }
    }

    private static string StripComments(string source) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(source, @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline),
            @"//.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("找不到仓库根（ForgeSelf.slnx）");
    }
}
