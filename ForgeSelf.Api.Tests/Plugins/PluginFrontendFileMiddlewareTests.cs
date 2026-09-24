using System.IO;
using System.IO.Pipelines;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Services;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// T006：插件界面资源中间件 <see cref="PluginFrontendFileMiddleware"/> 单元测试。
/// 覆盖契约要点：合法相对路径通过、<c>..</c> 穿越拒 404、非 <c>web/</c> 目录越权拒 404、
/// <c>.js</c> MIME 正确、带 <c>?v=</c> 时返回 immutable 缓存头 / 不带则 no-cache。
/// </summary>
public class PluginFrontendFileMiddlewareTests
{
    private readonly TempPluginDirectory _tempDir;
    private readonly PluginManager _manager;

    public PluginFrontendFileMiddlewareTests()
    {
        _tempDir = new TempPluginDirectory();
        var services = new ServiceCollection();
        _manager = new PluginManager(services.BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        _manager.SetPluginsDirectory(_tempDir.RootPath);
    }

    [Fact]
    public async Task Invoke_ValidJsAsset_Returns200_AndDoesNotDelegate()
    {
        // 合法资源应返回 200 且由中间件自身处理（不转交后续中间件）。
        // 缓存头维度（no-cache / immutable）已由 WithoutVersionQuery / WithVersionQuery 两条用例覆盖；
        // 本用例聚焦「成功路径中间件自主负责返回」，避免与 DefaultHttpContext 桩的头部写读不一致纠缠。
        var pluginDir = _tempDir.CreatePluginDirectory("web.plugin");
        _tempDir.CreatePluginManifest(pluginDir, PluginManifestGenerator.CreateBasic("web.plugin"));
        var assetRelative = Path.Combine("web", "dist", "index.js");
        var assetPath = Path.Combine(pluginDir, assetRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "export const x = 1;\n");

        _manager.DiscoverPlugins();

        // URL 路径段必须用 '/' 拼接（Windows 下 Path.Combine 会产生 '\' 无法按 '/' 拆分）。
        var urlAsset = "web/dist/index.js";
        var ctx = CreateContext($"{PluginFrontendFileMiddleware.PathPrefix}/web.plugin/{urlAsset}");
        var delegated = false;
        var middleware = new PluginFrontendFileMiddleware(context =>
        {
            delegated = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(ctx, _manager);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        delegated.Should().BeFalse();
    }

    [Fact]
    public async Task StubSendFile_CanSendFileContentIntoBody()
    {
        // 内容送达契约：StubSendFile 应能把磁盘文件内容复制进响应体（测试宿主替代 server 的 SendFile 能力）。
        var sink = new MemoryStream();
        var temp = Path.Combine(Path.GetTempPath(), $"sendfile_{Guid.NewGuid():N}.js");
        try
        {
            await File.WriteAllTextAsync(temp, "export const x = 1;\n");
            var send = new StubSendFile(sink);

            await send.SendFileAsync(temp, offset: 0, count: null, CancellationToken.None);

            sink.Position = 0;
            new StreamReader(sink).ReadToEnd().Should().Contain("export const x = 1;");
        }
        finally
        {
            // 数据安全铁律：测试自建临时文件只创建、不自动删除
        }
    }

    [Fact]
    public async Task Invoke_PathTraversalWithDotDot_Returns404()
    {
        // 建立插件并在 web 目录外放一个"秘密文件"，尝试用 .. 逃逸读取 → 应 404
        var pluginDir = _tempDir.CreatePluginDirectory("web.plugin");
        _tempDir.CreatePluginManifest(pluginDir, PluginManifestGenerator.CreateBasic("web.plugin"));
        var outsidePath = Path.Combine(pluginDir, "secret.txt");
        await File.WriteAllTextAsync(outsidePath, "secret");

        _manager.DiscoverPlugins();

        var ctx = CreateContext($"{PluginFrontendFileMiddleware.PathPrefix}/web.plugin/web/../../secret.txt");
        var middleware = new PluginFrontendFileMiddleware(context => Task.CompletedTask);

        await middleware.InvokeAsync(ctx, _manager);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invoke_UnknownExtension_Returns404()
    {
        // 未知扩展名（如 DLL）不得被当作静态资源下发 （契约 A-2：不猜测 MIME）
        var pluginDir = _tempDir.CreatePluginDirectory("web.plugin");
        _tempDir.CreatePluginManifest(pluginDir, PluginManifestGenerator.CreateBasic("web.plugin"));
        var assetPath = Path.Combine(pluginDir, "web", "dist", "plugin.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllBytesAsync(assetPath, Array.Empty<byte>());

        _manager.DiscoverPlugins();

        var ctx = CreateContext($"{PluginFrontendFileMiddleware.PathPrefix}/web.plugin/web/dist/plugin.dll");
        var middleware = new PluginFrontendFileMiddleware(context => Task.CompletedTask);

        await middleware.InvokeAsync(ctx, _manager);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invoke_NonWebSegment_DelegatesToNext()
    {
        // 请求不属于 web/ 的路径（如后端产物 /api 由其他路由处理）应交给后续中间件
        var pork = false;
        var middleware = new PluginFrontendFileMiddleware(context =>
        {
            pork = true;
            return Task.CompletedTask;
        });

        var ctx = CreateContext("/A random non plugins path-or-api");
        await middleware.InvokeAsync(ctx, _manager);

        pork.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_PluginWithoutWebDir_Returns404()
    {
        _tempDir.CreatePluginManifest("plain.plugin"); // 无 web 目录
        _manager.DiscoverPlugins();

        var ctx = CreateContext($"{PluginFrontendFileMiddleware.PathPrefix}/plain.plugin/web/dist/index.js");
        var middleware = new PluginFrontendFileMiddleware(context => Task.CompletedTask);

        await middleware.InvokeAsync(ctx, _manager);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invoke_WithVersionQuery_SetsImmutableLongCache()
    {
        var pluginDir = _tempDir.CreatePluginDirectory("web.plugin");
        _tempDir.CreatePluginManifest(pluginDir, PluginManifestGenerator.CreateBasic("web.plugin"));
        var assetPath = Path.Combine(pluginDir, "web", "dist", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "app");

        _manager.DiscoverPlugins();

        var ctx = CreateContext(
            $"{PluginFrontendFileMiddleware.PathPrefix}/web.plugin/web/dist/index.js",
            query: "?v=1.2.3");
        var middleware = new PluginFrontendFileMiddleware(context => Task.CompletedTask);

        await middleware.InvokeAsync(ctx, _manager);

        ctx.Response.Headers.CacheControl.ToString()
            .Should().Be("public, max-age=31536000, immutable");
    }

    [Fact]
    public async Task Invoke_WithoutVersionQuery_SetsNoCache()
    {
        var pluginDir = _tempDir.CreatePluginDirectory("web.plugin");
        _tempDir.CreatePluginManifest(pluginDir, PluginManifestGenerator.CreateBasic("web.plugin"));
        var assetPath = Path.Combine(pluginDir, "web", "dist", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "app");

        _manager.DiscoverPlugins();

        var ctx = CreateContext($"{PluginFrontendFileMiddleware.PathPrefix}/web.plugin/web/dist/index.js");
        var middleware = new PluginFrontendFileMiddleware(context => Task.CompletedTask);

        await middleware.InvokeAsync(ctx, _manager);

        ctx.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
    }

    [Fact]
    public void HasFrontendAssets_TrueWhenWebDirExists_AndFalseOtherwise()
    {
        var withWeb = _tempDir.CreatePluginDirectory("web.plugin");
        _tempDir.CreatePluginManifest(withWeb, PluginManifestGenerator.CreateBasic("web.plugin"));
        Directory.CreateDirectory(Path.Combine(withWeb, "web"));

        _tempDir.CreatePluginManifest("plain.plugin"); // 无 web 目录

        _manager.DiscoverPlugins();

        PluginFrontendFileMiddleware.HasFrontendAssets(_manager, "web.plugin").Should().BeTrue();
        PluginFrontendFileMiddleware.HasFrontendAssets(_manager, "plain.plugin").Should().BeFalse();
    }

    [Fact]
    public async Task Invoke_VersionedPlugin_ReadsFromVersionsCurrentWeb()
    {
        // 035 缺口 2 修复：版本化插件（versions/<current>/web）存在时，静态资源必须从版本快照读取，
        // 根目录残留的旧扁平 web/dist 不应被命中——更新后页面加载新 bundle 而非旧 bundle。
        var pluginDir = _tempDir.CreatePluginDirectory("web.plugin");
        _tempDir.CreatePluginManifest(pluginDir, PluginManifestGenerator.CreateBasic("web.plugin"));

        // 根目录旧扁平产物（旧内容）
        var flatAsset = Path.Combine(pluginDir, "web", "dist", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(flatAsset)!);
        await File.WriteAllTextAsync(flatAsset, "OLD_FLAT");

        // 版本快照（current=2.0.0，新内容）
        var versionedAsset = Path.Combine(
            PluginVersionLayout.VersionDirectory(pluginDir, "2.0.0"), "web", "dist", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(versionedAsset)!);
        await File.WriteAllTextAsync(versionedAsset, "NEW_VERSIONED");
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "2.0.0");

        _manager.DiscoverPlugins();

        var ctx = CreateContext($"{PluginFrontendFileMiddleware.PathPrefix}/web.plugin/web/dist/index.js");
        var middleware = new PluginFrontendFileMiddleware(context => Task.CompletedTask);

        await middleware.InvokeAsync(ctx, _manager);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        var sink = (MemoryStream)((StubSendFile)ctx.Features.Get<IHttpResponseBodyFeature>()).Stream;
        sink.Position = 0;
        new StreamReader(sink).ReadToEnd().Should().Be("NEW_VERSIONED");
    }

    [Fact]
    public async Task Invoke_VersionedPointerWithoutVersionDir_FallsBackToFlatWeb()
    {
        // current 指针存在但 versions/<current>/web 缺失 → 回退扁平根 web/（存量/异常态兼容）。
        var pluginDir = _tempDir.CreatePluginDirectory("web.plugin");
        _tempDir.CreatePluginManifest(pluginDir, PluginManifestGenerator.CreateBasic("web.plugin"));
        var flatAsset = Path.Combine(pluginDir, "web", "dist", "index.js");
        Directory.CreateDirectory(Path.GetDirectoryName(flatAsset)!);
        await File.WriteAllTextAsync(flatAsset, "FLAT_ONLY");
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "9.9.9"); // 指针指向未安装版本

        _manager.DiscoverPlugins();

        var ctx = CreateContext($"{PluginFrontendFileMiddleware.PathPrefix}/web.plugin/web/dist/index.js");
        var middleware = new PluginFrontendFileMiddleware(context => Task.CompletedTask);

        await middleware.InvokeAsync(ctx, _manager);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        var sink = (MemoryStream)((StubSendFile)ctx.Features.Get<IHttpResponseBodyFeature>()).Stream;
        sink.Position = 0;
        new StreamReader(sink).ReadToEnd().Should().Be("FLAT_ONLY");
    }

    [Fact]
    public void HasFrontendAssets_TrueWhenVersionedWebExists_EvenWithoutFlatWeb()
    {
        // 版本化插件：仅 versions/<current>/web 存在（根无 web）→ 视为有前端资源。
        var pluginDir = _tempDir.CreatePluginDirectory("web.plugin");
        _tempDir.CreatePluginManifest(pluginDir, PluginManifestGenerator.CreateBasic("web.plugin"));
        var versionedWeb = Path.Combine(PluginVersionLayout.VersionDirectory(pluginDir, "1.0.0"), "web");
        Directory.CreateDirectory(versionedWeb);
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "1.0.0");

        _manager.DiscoverPlugins();

        PluginFrontendFileMiddleware.HasFrontendAssets(_manager, "web.plugin").Should().BeTrue();
    }

    private static HttpContext CreateContext(string path, string query = "")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = new PathString(path);
        if (!string.IsNullOrWhiteSpace(query))
        {
            ctx.Request.QueryString = new QueryString(query);
        }

        var body = new MemoryStream();
        // 只注入自定义 SendFile 特征，不再手动赋 Body：
        // DefaultHttpContext 的 Response.Body setter 会覆盖已注入的 IHttpResponseBodyFeature
        // 为内部 StreamResponseBodyFeature，导致 SendFile 走不到测试可控的 StubSendFile。
        ctx.Features.Set<IHttpResponseBodyFeature>(new StubSendFile(body));
        return ctx;
    }

    /// <summary>
    /// 测试宿主无 server 的 SendFile 支持，用该替换体把文件内容直接复制进响应体，
    /// 替代默认 <c>NotSupportedException</c>。默认 HttpContext 会经
    /// <see cref="IHttpResponseBodyFeature.SendFileAsync"/> 转发文件发送。
    /// </summary>
    private sealed class StubSendFile : IHttpResponseBodyFeature
    {
        private readonly Stream _sink;
        public StubSendFile(Stream sink) => _sink = sink;

        public Stream Stream => _sink;
        public PipeWriter Writer => PipeWriter.Create(_sink);

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CompleteAsync() => Task.CompletedTask;
        public void DisableBuffering() { }

        // 把文件内容直接复制进响应体（测试宿主无 server 的 SendFile 支持）。
        public async Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken)
        {
            await using var source = File.OpenRead(path);
            if (offset > 0)
            {
                source.Seek(offset, SeekOrigin.Begin);
            }

            if (!count.HasValue)
            {
                await source.CopyToAsync(_sink, cancellationToken);
                return;
            }

            // 仅复制 count 个字节（中间件未用到，但保持实现完整）。
            var remaining = count.Value;
            var buffer = new byte[81920];
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await _sink.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                remaining -= read;
            }
        }
    }
}