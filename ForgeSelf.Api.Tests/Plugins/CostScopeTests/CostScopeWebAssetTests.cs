using System.IO;
using System.Text.Json;

namespace ForgeSelf.Api.Tests.Plugins.CostScopeTests;

/// <summary>
/// A12 原子任务：插件前端资产守卫测试。
/// </summary>
/// <remarks>
/// <b>为什么需要它</b>：前端产物由 Vite 单独构建、不参与 .NET 编译，
/// 「调错端点」「视图名与 plugin.json 不一致」这类问题<b>不会让任何编译失败</b>，
/// 只能靠静态扫描守住。
/// <b>数据驱动</b>：视图名与入口从 <c>plugin.json</c> 读取（SSOT），
/// 而不是把 <c>CostScopeView</c>/<c>web/dist/index.js</c> 写死在断言里——
/// 否则改了 plugin.json 而忘了改前端，测试反而全绿。
/// </remarks>
public class CostScopeWebAssetTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Plugins"))
                && File.Exists(Path.Combine(dir.FullName, "ForgeSelf.Api", "ForgeSelf.Api.csproj")))
                return dir.FullName;

            // 必须向上推进，否则永远停在同一层 ⇒ 死循环（曾导致测试挂起 10 分钟）
            dir = dir.Parent;
        }

        throw new InvalidOperationException("未能定位仓库根目录");
    }

    private static string WebDir => Path.Combine(RepoRoot(), "Plugins", "CostScope", "web");

    private static string PluginJsonPath => Path.Combine(RepoRoot(), "Plugins", "CostScope", "plugin.json");

    private static JsonElement Frontend()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(PluginJsonPath));
        return doc.RootElement.GetProperty("frontend").Clone();
    }

    private static IEnumerable<string> SourceFiles()
    {
        var src = Path.Combine(WebDir, "src");
        if (!Directory.Exists(src)) yield break;
        foreach (var f in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories))
        {
            if (f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".vue", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                yield return f;
        }
    }

    // ---------- 端点边界（FR-4.6 / 铁律 14） ----------

    [Fact]
    public void 前端源码_禁止调用宿主观测端点()
    {
        // 本插件只允许调自己的 api/cost-scope；混用会出现「两套数字对不上」
        var forbidden = new[] { "api/usage", "api/usage-stats", "api/chat-records", "api/llm-observability" };
        var offenders = new List<string>();

        foreach (var file in SourceFiles())
        {
            // 先剥注释：本守卫要拦的是「真的调了」，而文件里为了说明「为什么不能调」
            // 必然会出现这些端点名（否则读者莫名其妙）——扫注释必然误报。
            var code = StripComments(File.ReadAllText(file));
            foreach (var f in forbidden)
            {
                if (code.Contains(f, StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{Path.GetFileName(file)} → {f}");
            }
        }

        offenders.Should().BeEmpty("成本观测前端只能调用插件自己的端点（FR-4.6 / 铁律 14）");
    }

    /// <summary>去掉行注释、块注释与 Vue 模板注释，只留代码。</summary>
    private static string StripComments(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i = Math.Min(i + 2, text.Length);
            }
            else if (text[i] == '<' && i + 3 < text.Length
                && text[i + 1] == '!' && text[i + 2] == '-' && text[i + 3] == '-')
            {
                while (i + 2 < text.Length && !(text[i] == '-' && text[i + 1] == '-' && text[i + 2] == '>')) i++;
                i = Math.Min(i + 3, text.Length);
            }
            else
            {
                sb.Append(text[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    [Fact]
    public void 前端源码_只调用本插件端点前缀()
    {
        var files = SourceFiles().ToList();
        files.Should().NotBeEmpty("应存在前端源码");

        var http = File.ReadAllText(Path.Combine(WebDir, "src", "http.ts"));
        http.Should().Contain("/api/cost-scope", "端点前缀必须是插件自己的");
    }

    // ---------- 与 plugin.json 的契约对齐（数据驱动） ----------

    [Fact]
    public void 入口_导出plugin_json声明的视图名()
    {
        var view = Frontend().GetProperty("views")[0].GetString();
        view.Should().NotBeNullOrWhiteSpace();

        var index = File.ReadAllText(Path.Combine(WebDir, "src", "index.ts"));
        index.Should().Contain($"export {{ {view} }}", $"index.ts 必须具名导出 {view}（宿主加载器按此名取组件）");
    }

    [Fact]
    public void 入口_默认导出与具名导出指向同一组件()
    {
        var view = Frontend().GetProperty("views")[0].GetString()!;
        var index = File.ReadAllText(Path.Combine(WebDir, "src", "index.ts"));

        index.Should().Contain($"import {view} from './{view}.vue'");
        index.Should().Contain($"export default {view}", "取不到具名导出时宿主会回退 default 导出");
    }

    [Fact]
    public void 构建入口_与plugin_json的entry一致()
    {
        var entry = Frontend().GetProperty("entry").GetString();
        entry.Should().Be("web/dist/index.js");

        // vite 产物文件名必须与 entry 对得上，否则宿主 404
        var vite = File.ReadAllText(Path.Combine(WebDir, "vite.config.ts"));
        vite.Should().Contain("entryFileNames: 'index.js'");
    }

    [Fact]
    public void 路由与菜单元数据已登记()
    {
        var frontend = Frontend();
        frontend.GetProperty("route").GetString().Should().Be("/cost-scope");
        frontend.GetProperty("menu").GetString().Should().Be("成本观测");
    }

    // ---------- 关键组件与诚实性标记 ----------

    [Fact]
    public void 关键资产_齐备()
    {
        foreach (var rel in new[]
        {
            "package.json",
            "vite.config.ts",
            "src/index.ts",
            "src/http.ts",
            "src/CostScopeView.vue",
            "src/components/DashboardPanel.vue",
            "src/components/PricePanel.vue",
            "src/components/BudgetPanel.vue",
            "src/components/TracePanel.vue",
        })
        {
            File.Exists(Path.Combine(WebDir, rel)).Should().BeTrue($"缺少前端资产 {rel}");
        }
    }

    [Fact]
    public void 界面_必须展示覆盖度与关联口径_不许静默隐藏()
    {
        var dashboard = File.ReadAllText(Path.Combine(WebDir, "src", "components", "DashboardPanel.vue"));
        var trace = File.ReadAllText(Path.Combine(WebDir, "src", "components", "TracePanel.vue"));

        // 主聊天不可观测 / 未配单价下界 / 关联为近似——三条都必须显式呈现
        dashboard.Should().Contain("mainChatObservable");
        dashboard.Should().Contain("costIsLowerBound");
        trace.Should().Contain("correlationNote");
    }

    [Fact]
    public void 界面_超支只做页面横幅_不发通知()
    {
        var budget = File.ReadAllText(Path.Combine(WebDir, "src", "components", "BudgetPanel.vue"));

        budget.Should().Contain("shouldAlert");
        budget.Should().NotContain("Notification", "超支只做页面内横幅，不发通知（FR-3.6）");
        budget.Should().NotContain("ElMessage", "超支不弹通知打扰用户");
    }
}
