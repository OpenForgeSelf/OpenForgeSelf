using ForgeSelf.Api.Plugins.Services;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// 守卫（G1-G6）：独立插件骨架模板（<see cref="PluginSrcScaffold"/>）产出的仓库必须「GitHub Actions 可用、宿主可安装」。
/// 背景（2026-11-06 验收发现，模板与 v2 骨架分叉，模板缺陷要 push 到真实 GitHub 才暴露）：
/// ① `secrets.*` 用于 `if:` 是 GitHub Actions 非法上下文（workflow 解析即失败）；密钥只能经 step `env` 映射；
/// ② 仓库无 sln 时裸 `dotnet test/publish` 必挂（MSB1003），必须显式工程路径；
/// ③ publish 产物不含根 plugin.json → Release zip 缺清单，宿主拒装；
/// ④ 8 级相对 Exists ProjectReference 只在宿主仓布局成立，runner 上 CS0246 —— 引用必须走仓内 lib/ 提交程序集；
/// ⑤ 宿主共享程序集（ForgeSelf.*/NewLife.*/Microsoft.Extensions.* 等）若随 publish 进包，会造成类型身份分裂 + 违反发行契约；
/// ⑥ manifest 必须写进工程目录（EmbeddedResource 相对 csproj 解析），且仓库需要 .gitignore 防 bin/obj 入库。
/// 本守卫对 PluginSrcScaffold.Write 的真实产物做功能级断言，不扫源码字符串。
/// </summary>
public class PluginScaffoldReleaseTemplateTests
{
    private sealed record ScaffoldOut(string Src, string Workflow, string Csproj, string TestsCsproj, string ProjectDir, string LibDir);

    private static ScaffoldOut ScaffoldFresh()
    {
        var src = Path.Combine(Path.GetTempPath(), "ofs-scaffold-guard-" + Guid.NewGuid().ToString("N"));
        try
        {
            PluginSrcScaffold.Write(src, "guard-plugin", "Plugin.Guard");
            var projectDir = Path.Combine(src, "src", "Plugin.Guard");
            return new ScaffoldOut(
                src,
                File.ReadAllText(Path.Combine(src, ".github", "workflows", "release.yml")),
                File.ReadAllText(Path.Combine(projectDir, "Plugin.Guard.csproj")),
                File.ReadAllText(Path.Combine(src, "tests", "Plugin.Guard.Tests", "Plugin.Guard.Tests.csproj")),
                projectDir,
                Path.Combine(src, "lib"));
        }
        catch
        {
            TryDelete(src);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 清理失败不影响判定
        }
    }

    [Fact]
    public void Workflow_密钥只允许经env映射_禁止进if()
    {
        var scaffold = ScaffoldFresh();
        try
        {
            var badIf = scaffold.Workflow.Split('\n')
                .Where(l => l.TrimStart().StartsWith("if:") && l.Contains("secrets."))
                .ToList();
            Assert.Empty(badIf);
            // 新仓库默认只读 workflow token：发 Release 必须显式声明 contents:write（实测 403 教训）。
            Assert.Contains("permissions:", scaffold.Workflow);
            Assert.Contains("contents: write", scaffold.Workflow);
            // NuGet 密钥的合法形态：step 级 env 映射；未配置时打包后跳过上传，CI 保持绿。
            Assert.Contains("NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}", scaffold.Workflow);
            Assert.Contains("IsNullOrWhiteSpace($env:NUGET_API_KEY)", scaffold.Workflow);
        }
        finally
        {
            TryDelete(scaffold.Src);
        }
    }

    [Fact]
    public void Workflow_test_publish_pack必须显式工程路径_仓库无sln()
    {
        var scaffold = ScaffoldFresh();
        try
        {
            Assert.Contains("dotnet test tests/", scaffold.Workflow);
            Assert.Contains("dotnet publish src/", scaffold.Workflow);
            Assert.Contains("dotnet pack src/", scaffold.Workflow);
            Assert.DoesNotContain("run: dotnet test --nologo", scaffold.Workflow);
        }
        finally
        {
            TryDelete(scaffold.Src);
        }
    }

    [Fact]
    public void Workflow_打包必须把根清单拷进产物_并剔除宿主共享程序集()
    {
        var scaffold = ScaffoldFresh();
        try
        {
            Assert.Contains("Copy-Item plugin.json", scaffold.Workflow);
            foreach (var shared in new[] { "ForgeSelf.", "NewLife.", "Microsoft.Extensions.", "XCode", "MX." })
                Assert.Contains(shared, scaffold.Workflow);
            Assert.Contains("Compress-Archive", scaffold.Workflow);
            Assert.Contains("SHA256SUMS.txt", scaffold.Workflow);
        }
        finally
        {
            TryDelete(scaffold.Src);
        }
    }

    [Fact]
    public void Csproj_引用仓内lib程序集_禁止8级相对Exists引用()
    {
        var scaffold = ScaffoldFresh();
        try
        {
            Assert.Contains("<Reference Include=\"ForgeSelf.Abstractions\"", scaffold.Csproj);
            Assert.Contains("<HintPath>..\\..\\lib\\ForgeSelf.Abstractions.dll</HintPath>", scaffold.Csproj);
            Assert.Contains("Private=\"false\"", scaffold.Csproj);
            Assert.DoesNotContain("Exists(", scaffold.Csproj);
            Assert.DoesNotContain("<ProjectReference", scaffold.Csproj);
            // Private=false 的 Reference 不跨工程传递：测试工程必须自带 lib 引用，否则 runner 上 CS0246。
            Assert.Contains("<Reference Include=\"ForgeSelf.Abstractions\"", scaffold.TestsCsproj);
            Assert.Contains("<HintPath>..\\..\\lib\\ForgeSelf.Abstractions.dll</HintPath>", scaffold.TestsCsproj);
        }
        finally
        {
            TryDelete(scaffold.Src);
        }
    }

    [Fact]
    public void Scaffold_manifest必须写进工程目录_仓库根不得出现第二份()
    {
        var scaffold = ScaffoldFresh();
        try
        {
            Assert.True(File.Exists(Path.Combine(scaffold.ProjectDir, "ForgeSelf.PluginManifest.json")),
                "内嵌清单必须放在 csproj 旁（EmbeddedResource 相对路径解析）");
            Assert.False(File.Exists(Path.Combine(scaffold.Src, "ForgeSelf.PluginManifest.json")),
                "仓库根不应出现第二份内嵌清单");
            Assert.True(File.Exists(Path.Combine(scaffold.Src, "plugin.json")), "仓库根必须有 plugin.json");
        }
        finally
        {
            TryDelete(scaffold.Src);
        }
    }

    [Fact]
    public void Scaffold_必须有gitignore_排除构建产物()
    {
        var scaffold = ScaffoldFresh();
        try
        {
            var gitignore = File.ReadAllText(Path.Combine(scaffold.Src, ".gitignore"));
            Assert.Contains("bin/", gitignore);
            Assert.Contains("obj/", gitignore);
        }
        finally
        {
            TryDelete(scaffold.Src);
        }
    }

    [Fact]
    public void Scaffold_lib目录携带引用程序集_当宿主存在时()
    {
        var scaffold = ScaffoldFresh();
        try
        {
            foreach (var dll in new[] { "ForgeSelf.Abstractions.dll", "ForgeSelf.Core.dll" })
            {
                var hostCopy = Path.Combine(AppContext.BaseDirectory, dll);
                var libCopy = Path.Combine(scaffold.LibDir, dll);
                if (File.Exists(hostCopy))
                    Assert.True(File.Exists(libCopy), $"宿主存在 {dll} 时，骨架 lib/ 必须携带（CI 编译依赖）");
                // 测试宿主 bin 缺该 dll 时不判死：脚手架实际运行在宿主进程内，BaseDirectory 必有。
            }
        }
        finally
        {
            TryDelete(scaffold.Src);
        }
    }
}
