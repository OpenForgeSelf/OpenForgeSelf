using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;

namespace ForgeSelf.Api.Tests.Architecture;

/// <summary>
/// 插件依赖在宿主里解析不到（PC-020）。两条不变量：
/// 1. 插件独有的第三方 NuGet 必须 <c>CopyLocalLockFileAssemblies=true</c>，否则类库 bin 没有那份 DLL，宿主映射控制器即退出。
/// 2. 插件控制器不得构造注入宿主契约（<c>PluginManager.HostProvidedServiceContracts</c> 除去容器自带的 IServiceProvider），否则插件子容器激活即 500。
/// </summary>
public class PluginLoadDependencyGuardTests
{
    [Fact]
    public void 插件独有第三方包必须复制到输出目录()
    {
        var root = FindRepoRoot();
        var hostPackages = PackageIds(Path.Combine(root, "ForgeSelf.Api", "ForgeSelf.Api.csproj"));
        hostPackages.Should().NotBeEmpty("宿主 csproj 必须能解析出 PackageReference，否则本守卫是空转");

        var offenders = new List<string>();
        var thirdPartyPlugins = 0;
        foreach (var csproj in Directory.EnumerateFiles(Path.Combine(root, "Plugins"), "*.csproj", SearchOption.AllDirectories))
        {
            if (csproj.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = File.ReadAllText(csproj);
            var own = PackageIdsFromXml(text).Where(id => !HostProvides(id, hostPackages)).ToList();
            if (own.Count == 0)
                continue;

            thirdPartyPlugins++;
            if (!text.Contains("<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(root, csproj).Replace('\\', '/')
                    + " 独有包 " + string.Join(", ", own)
                    + " 但没有 CopyLocalLockFileAssemblies=true");
            }
        }

        thirdPartyPlugins.Should().BeGreaterThan(0, "仓内应至少有一个带独有第三方包的插件（BrowserUse/McpCenter/Sems），扫到 0 个说明包名解析失效");
        offenders.Should().BeEmpty("类库默认不复制 NuGet DLL，宿主 StageAllPlugins 只拷 bin，缺 DLL 会在 MapControllers 退出。违规：\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void 插件控制器不得构造注入宿主契约()
    {
        var root = FindRepoRoot();
        var contracts = HostContractNames(Path.Combine(root, "ForgeSelf.Api", "Plugins", "PluginManager.cs"));
        contracts.Should().Contain("IApprovalService");
        contracts.Remove("IServiceProvider");

        var offenders = new List<string>();
        var scanned = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "Plugins"), "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (!rel.Contains("/Controllers/", StringComparison.Ordinal) || rel.Contains("/obj/", StringComparison.Ordinal) || rel.Contains("/bin/", StringComparison.Ordinal))
                continue;

            var code = StripComments(File.ReadAllText(file));
            foreach (System.Text.RegularExpressions.Match ctor in ConstructorPattern.Matches(code))
            {
                scanned++;
                foreach (var param in ctor.Groups["params"].Value.Split(','))
                {
                    var type = ParamType(param);
                    if (type != null && contracts.Contains(type))
                    {
                        var line = code[..ctor.Index].Count(c => c == '\n') + 1;
                        offenders.Add($"{rel}:{line} 构造注入 {type}。改为构造只收 IContext，用时 ctx.Get<{type}>()。");
                    }
                }
            }
        }

        scanned.Should().BeGreaterThan(10, "应扫到一批插件控制器构造函数；0 个说明正则失效");
        offenders.Should().BeEmpty("插件子容器不含宿主契约，构造注入会在激活控制器时 500。违规：\n" + string.Join("\n", offenders));
    }

    private static readonly Regex ConstructorPattern = new(
        @"public\s+(?:\w+\.)*(?<name>\w*Controller)\s*\((?<params>[^)]*)\)",
        RegexOptions.Compiled);

    private static string? ParamType(string param)
    {
        var trimmed = param.Trim();
        if (trimmed.Length == 0)
            return null;
        var head = trimmed.Split('=', 2)[0].Trim();
        var parts = head.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;
        var type = parts[^2].TrimEnd('?');
        var dot = type.LastIndexOf('.');
        return dot >= 0 ? type[(dot + 1)..] : type;
    }

    private static HashSet<string> HostContractNames(string pluginManagerPath)
    {
        var text = File.ReadAllText(pluginManagerPath);
        var start = text.IndexOf("HostProvidedServiceContracts", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "PluginManager 必须保留宿主契约清单，守卫靠它枚举");
        var end = text.IndexOf("};", start, StringComparison.Ordinal);
        var block = text[start..end];
        return Regex.Matches(block, @"typeof\((\w+)\)").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    private static bool HostProvides(string packageId, HashSet<string> hostPackages)
    {
        if (packageId.StartsWith("NewLife.", StringComparison.OrdinalIgnoreCase)
            || packageId.StartsWith("ForgeSelf.", StringComparison.OrdinalIgnoreCase))
            return true;

        return hostPackages.Any(host =>
            host.Equals(packageId, StringComparison.OrdinalIgnoreCase)
            || host.StartsWith(packageId + ".", StringComparison.OrdinalIgnoreCase)
            || packageId.StartsWith(host + ".", StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> PackageIds(string csprojPath) => PackageIdsFromXml(File.ReadAllText(csprojPath));

    private static HashSet<string> PackageIdsFromXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string StripComments(string source) =>
        Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline),
            @"//.*$", "", RegexOptions.Multiline);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
            dir = dir.Parent;
        dir.Should().NotBeNull("测试需从 bin 向上找到 ForgeSelf.slnx");
        return dir!.FullName;
    }
}
