using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.Sems.Services;

/// <summary>一条已声明的工程插件（只读扫描结果）。</summary>
public class EngineeringPluginItem
{
    /// <summary>包名（package.json 依赖键）。</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>声明版本字符串（如 ^5.0.0），不是已安装版本。</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>分组：构建 / 转译 / 代码质量 / 框架 / 其他。</summary>
    [JsonPropertyName("group")]
    public string Group { get; init; } = string.Empty;

    /// <summary>来源，本轮固定为 package.json。</summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = "package.json";
}

/// <summary>工程插件只读查询结果。项目存在时一律作为成功数据返回（扫描失败则列表为空并带原因）。</summary>
public class EngineeringPluginList
{
    /// <summary>识别到的插件数量。</summary>
    [JsonPropertyName("total")]
    public int Total { get; init; }

    /// <summary>插件列表（按名称稳定排序）。</summary>
    [JsonPropertyName("plugins")]
    public List<EngineeringPluginItem> Plugins { get; init; } = new();

    /// <summary>扫描失败时的中文原因；成功或空态为 null。</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// 项目工程插件只读查询：扫描项目根目录 <c>package.json</c> 的依赖声明，
/// 识别构建 / 转译 / 代码质量 / 框架类插件。不读 node_modules，不判断是否已安装，不执行包管理器。
/// </summary>
public interface IEngineeringPluginService
{
    /// <summary>
    /// 列出指定项目已声明的工程插件。
    /// 注册表不可用返回 Unavailable，项目不存在返回 NotFound；
    /// 项目存在时一律 Ok——扫描失败则列表为空并带中文原因，缺 package.json 视为空态而非失败。
    /// </summary>
    SemsResult<EngineeringPluginList> List(int projectId);
}

/// <summary>
/// 工程插件只读查询实现。每次调用经 <see cref="IContext.Get{T}"/> 重新解析
/// <see cref="IProjectRegistry"/>，不缓存接缝实例。
/// </summary>
public class EngineeringPluginService : IEngineeringPluginService
{
    private const string UnavailableMessage = "项目注册表不可用（宿主接缝未就绪）";
    private const string PackageFileName = "package.json";
    private const string SourcePackageJson = "package.json";

    private readonly IContext _ctx;

    public EngineeringPluginService(IContext ctx) => _ctx = ctx;

    public SemsResult<EngineeringPluginList> List(int projectId)
    {
        var registry = _ctx.Get<IProjectRegistry>();
        if (registry == null)
            return SemsResult<EngineeringPluginList>.Fail(SemsOpStatus.Unavailable, UnavailableMessage);

        var project = registry.Get(projectId);
        if (project == null)
            return SemsResult<EngineeringPluginList>.Fail(SemsOpStatus.NotFound, $"项目不存在：{projectId}");

        return SemsResult<EngineeringPluginList>.Ok(Scan(project.Root));
    }

    /// <summary>只读扫描项目根目录的 package.json；不递归、不进入 node_modules。</summary>
    internal static EngineeringPluginList Scan(string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return Failed("项目目录不可读");

        try
        {
            if (!Directory.Exists(root))
                return Empty();

            // 能存在但无法枚举（权限不足）视为扫描失败，以便页面展示原因。
            _ = Directory.GetFileSystemEntries(root);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Failed("项目目录不可读");
        }

        var packagePath = Path.Combine(root, PackageFileName);
        bool exists;
        try
        {
            exists = File.Exists(packagePath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Failed("项目目录不可读");
        }

        if (!exists)
            return Empty();

        string text;
        try
        {
            text = File.ReadAllText(packagePath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Failed("无法读取 package.json");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return Failed("package.json 不是合法 JSON");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return Failed("package.json 不是合法 JSON");

            var plugins = Collect(doc.RootElement);
            return new EngineeringPluginList
            {
                Total = plugins.Count,
                Plugins = plugins,
                Error = null
            };
        }
    }

    private static EngineeringPluginList Empty() => new()
    {
        Total = 0,
        Plugins = new List<EngineeringPluginItem>(),
        Error = null
    };

    private static EngineeringPluginList Failed(string reason) => new()
    {
        Total = 0,
        Plugins = new List<EngineeringPluginItem>(),
        Error = reason
    };

    private static List<EngineeringPluginItem> Collect(JsonElement root)
    {
        // 同一包出现在多个依赖桶时只保留一条：dependencies > devDependencies > optionalDependencies。
        var chosen = new Dictionary<string, EngineeringPluginItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var (property, _) in new (string, int)[]
                 {
                     ("dependencies", 0),
                     ("devDependencies", 1),
                     ("optionalDependencies", 2)
                 })
        {
            if (!TryGetPropertyIgnoreCase(root, property, out var bucket))
                continue;
            if (bucket.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var dep in bucket.EnumerateObject())
            {
                var name = dep.Name.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;
                if (chosen.ContainsKey(name))
                    continue;
                if (!EngineeringPluginClassifier.TryClassify(name, out var group))
                    continue;

                chosen[name] = new EngineeringPluginItem
                {
                    Name = name,
                    Version = ReadDeclaredVersion(dep.Value),
                    Group = group,
                    Source = SourcePackageJson
                };
            }
        }

        return chosen.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ReadDeclaredVersion(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
            return true;

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

/// <summary>
/// 按包名识别工程插件并分组。匹配不区分大小写，遵循 npm 包名习惯。
/// 需求原文把 eslint-plugin 与 eslint-config 写在同一条并注「不计入（配置包不是插件）」；
/// 此处将「不计入」理解为只排除配置包（eslint-config-* / @*/eslint-config-*），eslint 插件计入。
/// </summary>
public static class EngineeringPluginClassifier
{
    private static readonly Regex ScopedEslintPlugin = new(
        @"^@[^/]+/eslint-plugin(?:-|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ScopedEslintConfig = new(
        @"^@[^/]+/eslint-config-",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// 若包名符合工程插件规则则给出分组，否则返回 false。
    /// 配置包、普通依赖、以及 sems_* / 宿主工具名一律不计入。
    /// </summary>
    public static bool TryClassify(string? packageName, out string group)
    {
        group = string.Empty;
        if (string.IsNullOrWhiteSpace(packageName))
            return false;

        var name = packageName.Trim();
        if (LooksLikeHostTool(name))
            return false;
        if (IsEslintConfig(name))
            return false;

        var n = name.ToLowerInvariant();

        if (StartsWith(n, "eslint-plugin-") || ScopedEslintPlugin.IsMatch(name) || StartsWith(n, "prettier-plugin-"))
        {
            group = "代码质量";
            return true;
        }

        if (StartsWith(n, "babel-plugin-") || StartsWith(n, "@babel/plugin-"))
        {
            group = "转译";
            return true;
        }

        if (StartsWith(n, "@vue/cli-plugin-") || StartsWith(n, "vue-cli-plugin-"))
        {
            group = "框架";
            return true;
        }

        if (StartsWith(n, "vite-plugin-") || n.EndsWith("-vite-plugin", StringComparison.Ordinal)
            || StartsWith(n, "@vitejs/plugin-")
            || StartsWith(n, "rollup-plugin-") || StartsWith(n, "unplugin-")
            || n.EndsWith("-webpack-plugin", StringComparison.Ordinal) || StartsWith(n, "webpack-plugin-"))
        {
            group = "构建";
            return true;
        }

        return false;
    }

    private static bool IsEslintConfig(string name)
    {
        var n = name.ToLowerInvariant();
        return StartsWith(n, "eslint-config-") || ScopedEslintConfig.IsMatch(name);
    }

    private static bool LooksLikeHostTool(string name)
    {
        var n = name.ToLowerInvariant();
        return StartsWith(n, "sems_") || StartsWith(n, "mcp_");
    }

    private static bool StartsWith(string lower, string prefix) =>
        lower.StartsWith(prefix, StringComparison.Ordinal);
}
