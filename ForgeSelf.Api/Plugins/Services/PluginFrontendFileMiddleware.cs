using ForgeSelf.Api.Plugins;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Services;

/// <summary>
/// 插件自带界面资源服务中间件。
/// <para>
/// 以只读方式把插件目录下的 <c>web/</c> 资源暴露到
/// <c>/plugins/{插件id}/web/{资源路径}</c>，供宿主前端在运行时按需远程加载插件界面
/// （对应 FR-002，契约见 <c>specs/010-plugin-frontend-runtime/contracts/plugin-frontend-assets.md</c>）。
/// </para>
/// <para>
/// 目录结构：<c>web/</c> 是插件前端的唯一归属，其下 <c>src/</c> 为源码、<c>dist/</c> 为构建产物；
/// 对外只提供 <c>dist/</c> 内的文件（清单 <c>Entry</c> 形如 <c>web/dist/index.js</c>）。
/// 结构设计详见 <c>specs/010-plugin-frontend-runtime/design/plugin-directory-layout.md</c>。
/// </para>
/// <para>
/// 安全约束（契约 A-2）：访问范围严格限定在各插件自身的 <c>web/</c> 目录内，
/// 拒绝路径穿越（<c>..</c>）与越权读取插件后端产物（DLL 等）；未知扩展名一律不提供。
/// </para>
/// <para>
/// 插件目录在<b>请求时</b>通过 <see cref="PluginManager"/> 解析（而非启动时快照），
/// 因此运行时新增/热更新的插件其界面资源可立即被访问。
/// </para>
/// </summary>
public class PluginFrontendFileMiddleware
{
    /// <summary>插件界面资源的 URL 前缀。</summary>
    public const string PathPrefix = "/plugins";

    /// <summary>插件目录内唯一允许对外提供的界面资源目录名。</summary>
    public const string FrontendDirectoryName = "web";

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".css"] = "text/css",
        [".json"] = "application/json",
        [".map"] = "application/json",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
    };

    private readonly RequestDelegate _next;

    /// <summary>初始化中间件。</summary>
    /// <param name="next">管道中的下一个中间件。</param>
    public PluginFrontendFileMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>处理请求：命中插件界面资源则直接返回文件，否则交给后续中间件。</summary>
    /// <param name="context">当前 HTTP 上下文。</param>
    /// <param name="pluginManager">插件管理器，用于按插件 id 解析插件目录。</param>
    public async Task InvokeAsync(HttpContext context, PluginManager pluginManager)
    {
        if (!context.Request.Path.StartsWithSegments(PathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 期望形态：/plugins/{插件id}/web/{资源路径}
        var segments = context.Request.Path.Value![PathPrefix.Length..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 3
            || !string.Equals(segments[1], FrontendDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var pluginId = segments[0];
        var assetSegments = segments.Skip(2).ToArray();

        // 路径穿越防护：拒绝任何 .. 段与反斜杠分隔符。
        if (assetSegments.Any(s => s == ".." || s.Contains('\\')) || pluginId == ".." || pluginId.Contains('\\'))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var frontendRoot = ResolveFrontendRoot(pluginManager, pluginId);
        if (frontendRoot is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // 归一化为绝对路径并二次校验仍未越出 frontend/ 根目录（纵深防御）。
        var fullPath = Path.GetFullPath(Path.Combine(frontendRoot, Path.Combine(assetSegments)));
        var rootFull = Path.GetFullPath(frontendRoot);
        var rootWithSeparator = rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal) || !File.Exists(fullPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var extension = Path.GetExtension(fullPath);
        if (string.IsNullOrEmpty(extension) || !ContentTypes.TryGetValue(extension, out var contentType))
        {
            // 未知类型不提供（不猜测 MIME），避免插件后端产物被误当作静态资源下发。
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = contentType;

        // 缓存策略：入口 URL 携带 ?v={版本} 时视为不可变资源长缓存；
        // 未带版本号时走协商缓存，保证版本提升前也能及时取到新内容（FR-008）。
        var version = context.Request.Query["v"].ToString();
        context.Response.Headers.CacheControl = string.IsNullOrWhiteSpace(version)
            ? "no-cache"
            : "public, max-age=31536000, immutable";

        try
        {
            await context.Response.SendFileAsync(fullPath);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("提供插件界面资源失败: {0}，原因: {1}", fullPath, ex.Message);
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }
    }

    /// <summary>
    /// 解析指定插件的界面资源根目录。
    /// <para>
    /// 版本化布局优先：插件存在 <c>current</c> 指针且
    /// <c>versions/&lt;current&gt;/web</c> 存在时，从版本快照目录读取
    /// （版本化发布/回滚后前端资源随版本立即生效，不依赖覆盖活动目录）；
    /// 否则回退扁平布局 <c>{插件目录}/web</c>（存量插件兼容）。
    /// </para>
    /// 返回 null 表示插件不存在、插件目录缺失或该插件尚未提供界面资源目录。
    /// </summary>
    /// <param name="pluginManager">插件管理器。</param>
    /// <param name="pluginId">插件标识（kebab-case）。</param>
    /// <returns>界面资源根目录绝对路径；不可用时返回 null。</returns>
    private static string? ResolveFrontendRoot(PluginManager pluginManager, string pluginId)
    {
        var metadata = pluginManager.GetPluginMetadata(pluginId);
        var pluginDirectory = metadata?.PluginDirectory;
        if (string.IsNullOrWhiteSpace(pluginDirectory) || !Directory.Exists(pluginDirectory))
        {
            return null;
        }

        var current = PluginVersionLayout.ReadCurrentVersion(pluginDirectory);
        if (!string.IsNullOrWhiteSpace(current))
        {
            var versionedRoot = Path.Combine(
                PluginVersionLayout.VersionDirectory(pluginDirectory, current), FrontendDirectoryName);
            if (Directory.Exists(versionedRoot))
                return versionedRoot;
        }

        var flatRoot = Path.Combine(pluginDirectory, FrontendDirectoryName);
        return Directory.Exists(flatRoot) ? flatRoot : null;
    }

    /// <summary>判断指定插件是否已提供界面资源目录（供宿主探测资源是否存在）。</summary>
    /// <param name="pluginManager">插件管理器。</param>
    /// <param name="pluginId">插件标识。</param>
    /// <returns>存在界面资源目录时返回 true。</returns>
    public static bool HasFrontendAssets(PluginManager pluginManager, string pluginId)
        => ResolveFrontendRoot(pluginManager, pluginId) is not null;
}
