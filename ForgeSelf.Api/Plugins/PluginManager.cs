using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Dev;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins;

/// <summary>
/// 插件管理器，负责插件的发现、加载、生命周期管理
/// </summary>
public class PluginManager
{
    private readonly ConcurrentDictionary<string, PluginLoadContext> _loadContexts = new();
    private readonly ConcurrentDictionary<string, IPlugin> _plugins = new();
    private readonly ConcurrentDictionary<string, PluginMetadata> _metadatas = new();
    private readonly ConcurrentDictionary<string, PluginState> _pluginStates = new();
    private readonly ConcurrentDictionary<string, Fiber> _fibers = new();
    private readonly ConcurrentDictionary<string, Assembly> _pluginAssemblies = new();
    private readonly IPermissionChecker _permissionChecker;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPluginServiceRegistry _serviceRegistry;
    private readonly Context _rootContext = new();
    private readonly ConcurrentDictionary<string, IServiceCollection> _pluginServices = new();
    private string _pluginsDirectory = string.Empty;

    // 插件根列表（按优先级）：内置根（随版本发布，BaseDirectory/plugins）在前，数据目录根（用户安装）在后。
    // 2026-10-04 输入18：插件从"单根"改为"两路合并扫描"，同 Id 冲突由版本号裁决。
    private readonly List<string> _pluginRoots = new();

    // 动态端点移除（卸载插件时移除其控制器 AssemblyPart）：
    // _partManager 为宿主 MVC ApplicationPartManager（启动接线时设置，可选为 null）；
    // _registeredApplicationParts 记录「插件 → 已注册的 AssemblyPart」，保证注册/移除幂等；
    // _applicationPartsChanged 为部件集合实际变更后的通知回调（宿主用于触发 ActionDescriptor 刷新），可选为 null。
    private ApplicationPartManager? _partManager;
    private readonly ConcurrentDictionary<string, AssemblyPart> _registeredApplicationParts = new();
    private Action? _applicationPartsChanged;

    private ForgeSelf.Core.IEventBus? _eventBus;

    /// <summary>
    /// 事件总线（P3 事件总线贯穿）：宿主启动时注入平台 <c>IEventBus</c> 单例。
    /// 为 null 时不发事件（保持单元测试与独立构造场景兼容）。
    /// </summary>
    /// <remarks>
    /// B3（040）：根 Context 的 <see cref="EventBus"/> 是其自建实例，与宿主 DI 单例并非同一对象，
    /// 仅靠「Context 树内冒泡」无法让插件 Fiber 内的 emit 触达平台级 <c>tools/*</c> 监听器。
    /// 故赋值时把宿主单例总线挂为根 Context 总线的父总线（事后挂载，最小侵入：
    /// 不改 <see cref="PluginManager"/> 构造签名、不改 <see cref="ForgeSelf.Core.IEventBus"/> 公开签名）。
    /// </remarks>
    public ForgeSelf.Core.IEventBus? EventBus
    {
        get => _eventBus;
        set
        {
            _eventBus = value;
            // 只有具体 EventBus 支持挂父指针；其它 IEventBus 实现（测试替身）静默跳过。
            if (value is ForgeSelf.Core.EventBus hostBus)
            {
                _rootContext.AttachParentBus(hostBus);
            }
        }
    }

    /// <summary>
    /// 发插件生命周期事件（plugin/loaded / plugin/unloaded）。EventBus 为 null 时静默跳过。
    /// </summary>
    private void EmitLifecycleEvent(PluginMetadata metadata, string action)
    {
        if (EventBus == null)
        {
            return;
        }

        try
        {
            var evt = new ForgeSelf.Abstractions.PluginLifecycleEvent
            {
                PluginId = metadata.Id,
                Name = metadata.Name,
                Version = metadata.Version,
                Action = action
            };
            EventBus.EmitAsync(action == "loaded" ? "plugin/loaded" : "plugin/unloaded", evt);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("发插件生命周期事件失败 [{0}]: {1}", metadata.Id, ex.Message);
        }
    }

    /// <summary>
    /// 插件目录（第一路＝内置根：随宿主版本发布，位于业务层旁边 <c>BaseDirectory/plugins</c>）
    /// </summary>
    public string PluginsDirectory => _pluginsDirectory;

    /// <summary>
    /// 全部插件根（按扫描优先级排序）。2026-10-04 输入18 起为两路：内置根 + 数据目录根（用户安装的插件包）。
    /// </summary>
    public IReadOnlyList<string> PluginRoots => _pluginRoots;

    /// <summary>
    /// 所有已加载的插件ID列表
    /// </summary>
    public IEnumerable<string> LoadedPluginIds => _plugins.Keys;

    /// <summary>
    /// 构造函数（无注册表时自建默认注册表，供单元测试与独立构造场景）。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="permissionChecker">权限校验器</param>
    public PluginManager(IServiceProvider serviceProvider, IPermissionChecker permissionChecker)
        : this(serviceProvider, permissionChecker, new PluginServiceRegistry())
    {
    }

    /// <summary>
    /// 构造函数（注入宿主单例注册表，保证 AppBuilder 与热插拔共享同一份可变 DI 容器）。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="permissionChecker">权限校验器</param>
    /// <param name="serviceRegistry">插件服务注册表（宿主单例）</param>
    public PluginManager(IServiceProvider serviceProvider, IPermissionChecker permissionChecker, IPluginServiceRegistry serviceRegistry)
    {
        _serviceProvider = serviceProvider;
        _permissionChecker = permissionChecker;
        _serviceRegistry = serviceRegistry;
    }

    /// <summary>
    /// 设置插件目录（内置根，随宿主版本发布）
    /// </summary>
    /// <param name="pluginsDirectory">插件目录路径</param>
    public void SetPluginsDirectory(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
        _pluginRoots.Clear();
        if (!string.IsNullOrWhiteSpace(pluginsDirectory)) _pluginRoots.Add(pluginsDirectory);
        XTrace.Log.Info("插件目录设置为: {0}", pluginsDirectory);
    }

    /// <summary>
    /// 追加一路插件根（2026-10-04 输入18：数据目录根 <c>{数据根}/plugins</c>，承载用户自行安装的插件包）。
    /// 与已有根同路径（忽略大小写）时不重复追加；追加顺序＝扫描优先级（内置根在前）。
    /// </summary>
    /// <param name="pluginsDirectory">要一并扫描的插件根</param>
    /// <param name="source">日志里标识这一路来源的名字（如"数据目录"）</param>
    public void AddPluginRoot(string? pluginsDirectory, string source = "附加根")
    {
        if (string.IsNullOrWhiteSpace(pluginsDirectory)) return;
        if (_pluginRoots.Any(r => string.Equals(r, pluginsDirectory, StringComparison.OrdinalIgnoreCase)))
        {
            XTrace.Log.Info("插件根已在本路，跳过追加（{0}）: {1}", source, pluginsDirectory);
            return;
        }
        _pluginRoots.Add(pluginsDirectory);
        XTrace.Log.Info("追加插件根（{0}）: {1}，存在={2}", source, pluginsDirectory, Directory.Exists(pluginsDirectory));
    }

    /// <summary>
    /// 把宿主应提供给插件的服务（能力接缝契约）在初始化阶段 seed 进插件根上下文。
    /// 对标 Cordis 的 <c>app.service(name, instance)</c>：宿主显式「提供」契约实例，
    /// 各 Fiber 派生上下文经父级链继承消费，无需回落宿主 MS DI 容器。
    /// 调用时机：宿主应用构建完成后（<c>app.Services</c>）一次。
    /// </summary>
    /// <remarks>
    /// 仅 seed 精选的「宿主 → 插件」契约集（<see cref="HostProvidedServiceContracts"/>），而非把宿主全部服务透传。
    /// 其中 Scoped 契约（如 <see cref="ILlmRuntime"/>）在上下文中以单一稳定实例存在（Cordis 语义：能力即实例），
    /// 不再随每次解析创建新实例，消除了此前宿主透传对 Scoped 语义的失真。
    /// </remarks>
    /// <param name="hostServices">宿主应用的服务提供程序。</param>
    public void ProvideHostServices(IServiceProvider hostServices)
    {
        ArgumentNullException.ThrowIfNull(hostServices);

        // Scoped 契约（如 ILlmRuntime）不能从 root provider 直接解析，须经 scope 获取；
        // Cordis 语义「能力即实例」：Scoped 接缝以单一稳定实例 seed 进根 Context（见类注释与契约清单注释）。
        using var scope = hostServices.CreateScope();
        foreach (var contract in HostProvidedServiceContracts)
        {
            // 已在插件注册期提前 seed 的契约（见 ProvideHostService）不再重复注册，
            // 否则根上下文会为同一契约累积冗余 effect。
            if (_rootContext.GetService(contract) is not null) continue;

            // IServiceProvider 特判：seed 宿主根 provider 本体（而非本次 scope 的 provider——
            // scope 在方法返回即释放，其 provider 后续解析会抛 ObjectDisposedException）。
            // 插件经 ctx.Get<IServiceProvider>() 回落宿主容器（如解析 SignalR 的 IHubContext<T>），
            // 需要长期有效的根 provider。
            if (contract == typeof(IServiceProvider))
            {
                _rootContext.Register(typeof(IServiceProvider), hostServices);
                continue;
            }

            var instance = scope.ServiceProvider.GetService(contract);
            if (instance is not null)
            {
                _rootContext.Register(contract, instance);
            }
            else
            {
                // 清单中的契约若未在宿主 DI 注册，seed 会静默跳过。输出警告以便发现漏注册。
                XTrace.Log.Warn("宿主 seed 契约未在宿主 DI 注册（已跳过）：{0}", contract.FullName);
            }
        }
    }

    /// <summary>
    /// 在插件注册期（<c>builder.Build()</c> 之前、宿主 DI 尚不可用时）提前把某个宿主契约实例 seed 进根上下文。
    /// </summary>
    /// <remarks>
    /// 时序缺口：<see cref="RegisterAllServices"/> 会立刻触发各插件 <c>Apply</c>，而 <c>Apply</c> 内就可能
    /// <c>ctx.Get&lt;T&gt;()</c>（例如取数据目录）；但 <see cref="ProvideHostServices"/> 必须等
    /// <c>app.Services</c> 就绪（Build 之后）才能解析 DI，届时 Apply 早已执行完毕。
    /// 对这些「Apply 期就要用」的契约，宿主须在 Build 之前用本方法手工 seed 同一实例。
    /// 实测踩坑：<c>IDataLocationService</c> 未提前 seed 时，MemorySystem/ProxyCapture 两个插件
    /// 因 <c>ctx.GetPluginDataDirectory()</c> 抛「未注册到插件上下文」而整体注册失败（发布版表现为插件消失）。
    /// </remarks>
    /// <param name="contract">契约类型。</param>
    /// <param name="instance">契约实例（应与宿主 DI 中的注册为同一实例）。</param>
    public void ProvideHostService(Type contract, object instance)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(instance);
        _rootContext.Register(contract, instance);
    }

    /// <summary>
    /// 宿主应提供给插件的契约集（对标 Cordis <c>app.service</c> 显式提供的能力）。
    /// 子插件经父级上下文链继承消费；新增「宿主 → 插件」接缝时在此登记。
    /// </summary>
    /// <remarks>
    /// 仅含<b>宿主拥有</b>的能力接缝（已在宿主 DI 注册者）。由插件自身提供的接缝（如 <see cref="ISchedulerHost"/>、
    /// <see cref="IWorkflowExecutor"/>、<see cref="IScriptTemplateService"/> 等）由各插件在 <c>Apply</c> 内
    /// <c>ctx.Register</c> 到自身上下文，不在此列表（宿主不拥有它们）。
    /// Scoped 接缝（<see cref="ILlmRuntime"/>、<see cref="ILogService"/>）按 Cordis 语义以<b>单实例</b> seed 进根 Context
    /// （已知代价：失去 per-request 作用域）。
    /// 新增「宿主 → 插件」接缝时在此登记；<see cref="ProvideHostServices"/> 会对未注册（解析为 null）的契约输出警告，防止漏注册。
    /// </remarks>
    private static readonly Type[] HostProvidedServiceContracts =
    {
        typeof(IConfigurationService),
        typeof(IToolRegistry),
        typeof(ICronParser),
        typeof(IRuntimeDetector),
        typeof(ISessionStore),
        // B5（041）：IAgentLoop 已删除（旧 P4 接缝，生产消费方 0）；
        // Agent 运行时注册表（IAgentRegistry）由 AIAgent 插件在 Apply 内 ctx.Register，不在此宿主清单内。
        typeof(IInbox),
        typeof(ILlmRuntime),
        typeof(ILogService),
        typeof(IWebSocketBroadcaster),
        typeof(IDataLocationService),
        typeof(IProjectRegistry),
        typeof(IAIProviderRegistry),
        // 配置密钥加密（AES-256）：ImGateway 等插件把 Secret 加密入库所需（第 1 步接口上移 Abstractions 后，
        // 宿主 DI 已注册，但插件经 ctx.Get 需要此接缝 seed 进根 Context）
        typeof(ISecretEncryptionService),
        // 宿主根 IServiceProvider：插件经 ctx.Get<IServiceProvider>() 回落宿主容器解析非精选契约
        // （如 SignalR 的 IHubContext<T>）。MemorySystem/SamplePlugin 均以「ctx.Get<IServiceProvider>()」
        // 为正确做法，此前依赖各插件自兜底，此处显式 seed 使之稳定可用（见 ProvideHostServices 特判）。
        typeof(IServiceProvider),
    };

    /// <summary>
    /// 对所有已加载插件发现并注册扩展点（启动路径接线，修复「启动后菜单/工具扩展为空」缺陷）。
    /// 幂等：扩展点注册使用 TryAdd，重复发现不会产生重复条目。
    /// 注意：<see cref="ExtensionPointManager"/> 构造依赖 <see cref="PluginManager"/>，
    /// 为避免 PluginManager ↔ ExtensionPointManager 构造循环依赖，此处由调用方注入 DI 单例实例。
    /// </summary>
    /// <param name="extensionPointManager">扩展点管理器（DI 单例）。</param>
    public void DiscoverAllExtensions(ExtensionPointManager extensionPointManager)
    {
        ArgumentNullException.ThrowIfNull(extensionPointManager);

        XTrace.Log.Info("开始发现所有已加载插件的扩展点");
        foreach (var pluginId in _plugins.Keys)
        {
            extensionPointManager.DiscoverExtensionsFromPlugin(pluginId);
        }
        XTrace.Log.Info("所有插件扩展点发现完成");
    }

    /// <summary>
    /// 扫描并发现所有插件（两路合并：内置根随版本发布 + 数据目录根用户安装；同 Id 由版本号裁决）
    /// </summary>
    /// <returns>发现的插件元数据列表</returns>
    public List<PluginMetadata> DiscoverPlugins()
    {
        var metadatas = new List<PluginMetadata>();
        var roots = _pluginRoots.Count > 0 ? _pluginRoots : new List<string> { _pluginsDirectory };

        // Id → 已生效的那份（含来源根），用于跨根去重与版本裁决
        var winners = new Dictionary<string, PluginMetadata>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;

            if (!Directory.Exists(root))
            {
                XTrace.Log.Warn("插件目录不存在: {0}", root);
                continue;
            }

            XTrace.Log.Info("开始扫描插件目录: {0}", root);

            foreach (var pluginDir in Directory.GetDirectories(root))
            {
                try
                {
                    var manifestPath = Path.Combine(pluginDir, "plugin.json");
                    if (!File.Exists(manifestPath))
                    {
                        // 数据目录根与「插件数据」目录同居一处（{数据根}/plugins/{插件Id}/*.db），
                        // 没有 plugin.json 的子目录一律不是插件包——这是两路合并的安全边界。
                        XTrace.Log.Debug("跳过目录（无plugin.json）: {0}", pluginDir);
                        continue;
                    }

                    var metadata = LoadPluginManifest(manifestPath);
                    if (metadata == null) continue;

                    metadata.PluginDirectory = pluginDir;

                    if (winners.TryGetValue(metadata.Id, out var kept))
                    {
                        var cmp = ComparePluginVersions(metadata.Version, kept.Version);
                        if (cmp <= 0)
                        {
                            XTrace.Log.Info("同名插件已发现更高或同版本，跳过: {0} v{1}（来源 {2}；生效版本 v{3} 来自 {4}）",
                                metadata.Id, metadata.Version, pluginDir, kept.Version, kept.PluginDirectory);
                            continue;
                        }
                        XTrace.Log.Warn("同名插件按版本覆盖生效: {0} v{1}（来源 {2}）取代 v{3}（来源 {4}）",
                            metadata.Id, metadata.Version, pluginDir, kept.Version, kept.PluginDirectory);
                        winners[metadata.Id] = metadata;
                        metadatas.RemoveAll(m => string.Equals(m.Id, metadata.Id, StringComparison.OrdinalIgnoreCase));
                        metadatas.Add(metadata);
                        _metadatas[metadata.Id] = metadata;
                        continue;
                    }

                    winners[metadata.Id] = metadata;
                    _metadatas.TryAdd(metadata.Id, metadata);
                    _pluginStates.TryAdd(metadata.Id, PluginState.NotLoaded);
                    metadatas.Add(metadata);
                    XTrace.Log.Info("发现插件: {0} v{1} - {2}（来源 {3}）", metadata.Name, metadata.Version, metadata.Id, pluginDir);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("扫描插件目录失败 [{0}]: {1}", pluginDir, ex.Message);
                }
            }
        }

        XTrace.Log.Info("插件扫描完成，共发现 {0} 个插件（插件根 {1} 个：{2}）",
            metadatas.Count, roots.Count(r => !string.IsNullOrWhiteSpace(r)), string.Join(" | ", roots));
        return metadatas;
    }

    /// <summary>
    /// 插件版本号比较：可解析为 <see cref="Version"/>（2~4 段）时按数值比；
    /// 任一不可解析则视为相等（返回 0 ⇒ 保留先扫到的内置根，避免被畸形版本号夺走）。
    /// </summary>
    internal static int ComparePluginVersions(string? candidate, string? kept)
    {
        if (Version.TryParse(candidate, out var a) && Version.TryParse(kept, out var b))
            return a.CompareTo(b);
        return 0;
    }

    /// <summary>
    /// 加载插件清单文件
    /// </summary>
    /// <param name="manifestPath">清单文件路径</param>
    /// <returns>插件元数据</returns>
    private PluginMetadata? LoadPluginManifest(string manifestPath)
    {
        try
        {
            var json = File.ReadAllText(manifestPath);
            var metadata = JsonSerializer.Deserialize<PluginMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (metadata == null || string.IsNullOrWhiteSpace(metadata.Id))
            {
                XTrace.Log.Error("插件清单无效（缺少Id）: {0}", manifestPath);
                return null;
            }

            return metadata;
        }
        catch (JsonException ex)
        {
            XTrace.Log.Error("解析插件清单失败 [{0}]: {1}", manifestPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 加载指定插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否加载成功</returns>
    public bool LoadPlugin(string pluginId)
    {
        if (!_metadatas.TryGetValue(pluginId, out var metadata))
        {
            XTrace.Log.Error("插件不存在: {0}", pluginId);
            return false;
        }

        if (_plugins.ContainsKey(pluginId))
        {
            XTrace.Log.Warn("插件已加载: {0}", pluginId);
            return true;
        }

        try
        {
            using var __logScope = PluginLogScope.Push(pluginId);

            XTrace.Log.Info("开始加载插件: {0}", pluginId);
            _pluginStates[pluginId] = PluginState.Loaded;

            var plugin = ResolvePluginInstance(metadata, out var loadContext);
            if (plugin == null)
            {
                _pluginStates[pluginId] = PluginState.Error;
                return false;
            }

            if (loadContext != null)
            {
                _loadContexts.TryAdd(pluginId, loadContext);
            }

            _plugins.TryAdd(pluginId, plugin);
            _pluginStates[pluginId] = PluginState.Loaded;

            XTrace.Log.Info("插件加载成功: {0} v{1}", metadata.Name, metadata.Version);
            PluginErrorStore.Clear(pluginId);
            EmitLifecycleEvent(metadata, "loaded");
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("加载插件失败 [{0}]: {1}", pluginId, ex.Message);
            PluginErrorStore.Set(pluginId, ex);
            _pluginStates[pluginId] = PluginState.Error;
            return false;
        }
    }

    /// <summary>
    /// 解析插件入口实例：优先从插件目录下的独立程序集加载；
    /// 若程序集文件不存在（内嵌插件），则从主程序集解析入口类型（T032）。
    /// </summary>
    /// <param name="metadata">插件元数据</param>
    /// <param name="loadContext">独立程序集加载上下文；内嵌插件时为 null</param>
    /// <returns>插件实例；解析失败时返回 null</returns>
    private IPlugin? ResolvePluginInstance(PluginMetadata metadata, out PluginLoadContext? loadContext)
    {
        loadContext = null;
        try
        {
            // side-by-side 版本目录 / 旧版扁平布局优先；均无独立 DLL 时回退主程序集（内嵌插件）。
            var assemblyPath = PluginVersionLayout.ResolveEntryAssemblyPath(metadata);

            // dev-only shadow-copy 装载（FORGESELF_DEV_SHADOWCOPY）：把产物复制到 %TEMP% 副本再从副本加载，
            // 源 DLL 不被锁 → 支持「重新编译 → 同版本热重载」。Production 不走此分支，行为不变。
            // 源码树布局（--plugins-dir 指向 Plugins/ 源码）下常规解析为 null，由 Prepare 回退 bin/<config>/<tfm> 解析。
            if (DevMode.ShadowCopyEnabled)
            {
                var shadowed = PluginShadowCopy.Prepare(metadata, assemblyPath);
                if (!string.IsNullOrWhiteSpace(shadowed))
                {
                    XTrace.Log.Debug("dev shadow 装载: {0} -> {1}", metadata.Id, shadowed);
                    assemblyPath = shadowed;
                }
            }

            Assembly assembly;
            if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
            {
                loadContext = new PluginLoadContext(assemblyPath, metadata.Id);
                assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
                _pluginAssemblies[metadata.Id] = assembly;
            }
            else
            {
                // 内嵌插件：从主程序集解析入口类型（解决 T032）
                assembly = typeof(PluginManager).Assembly;
            }

            var pluginType = assembly.GetType(metadata.EntryType);
            if (pluginType == null)
            {
                XTrace.Log.Error("插件入口类型不存在: {0}", metadata.EntryType);
                PluginErrorStore.Set(metadata.Id, new InvalidOperationException(
                    $"插件入口类型不存在: {metadata.EntryType}（检查 plugin.json 的 EntryType 与程序集实际类型是否一致）"));
                return null;
            }

            if (!typeof(IPlugin).IsAssignableFrom(pluginType))
            {
                XTrace.Log.Error("插件类型未实现 IPlugin 接口: {0}", metadata.EntryType);
                PluginErrorStore.Set(metadata.Id, new InvalidOperationException(
                    $"插件类型未实现 IPlugin 接口: {metadata.EntryType}"));
                return null;
            }

            var plugin = (IPlugin?)Activator.CreateInstance(pluginType);
            if (plugin == null)
            {
                XTrace.Log.Error("创建插件实例失败: {0}", metadata.EntryType);
                PluginErrorStore.Set(metadata.Id, new InvalidOperationException(
                    $"创建插件实例失败: {metadata.EntryType}（构造函数抛异常或返回 null）"));
                return null;
            }

            // 运行期加载/热重载路径（partManager 已设置）：把新程序集注册为 MVC ApplicationPart，
            // 使拆独立程序集插件的控制器在启用/重载后仍被路由发现；启动期（partManager 为 null）由
            // RegisterPluginApplicationParts 统一注册，此处自动跳过。
            RegisterApplicationPart(metadata.Id);

            return plugin;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("解析插件实例失败 [{0}]: {1}", metadata.Id, ex.Message);
            PluginErrorStore.Set(metadata.Id, ex);
            return null;
        }
    }

    /// <summary>
    /// 获取已从独立程序集加载的插件程序集（供宿主注册 MVC ApplicationPart，使其控制器被路由发现）。
    /// </summary>
    public IEnumerable<Assembly> GetLoadedPluginAssemblies() => _pluginAssemblies.Values;

    /// <summary>
    /// 设置 MVC <see cref="ApplicationPartManager"/> 引用与「部件集合变更」通知回调（动态端点移除）。
    /// 两者均可为 null：null 时跳过对应行为（不注册/不移除部件、不通知刷新）。
    /// </summary>
    /// <param name="partManager">宿主 MVC 部件管理器；null 时禁用动态部件注册/移除。</param>
    /// <param name="onPartsChanged">部件集合实际变更后的通知回调（宿主用于触发 ActionDescriptor 刷新）；null 时不通知。</param>
    public void SetApplicationPartManager(ApplicationPartManager? partManager, Action? onPartsChanged = null)
    {
        _partManager = partManager;
        _applicationPartsChanged = onPartsChanged;
    }

    /// <summary>
    /// 将当前所有「已从独立程序集加载」的插件程序集注册为 MVC ApplicationPart（启动路径接线）。
    /// 同时捕获 <paramref name="partManager"/> 引用与变更通知回调，供后续 <see cref="DestroyPlugin"/> 移除部件。
    /// 幂等：重复调用不会产生重复部件；未加载独立程序集的插件（内嵌插件）自动跳过。
    /// </summary>
    /// <param name="partManager">宿主 MVC 部件管理器。</param>
    /// <param name="onPartsChanged">部件集合实际变更后的通知回调（宿主用于触发 ActionDescriptor 刷新）；null 时不通知。</param>
    public void RegisterPluginApplicationParts(ApplicationPartManager partManager, Action? onPartsChanged = null)
    {
        ArgumentNullException.ThrowIfNull(partManager);

        SetApplicationPartManager(partManager, onPartsChanged);

        foreach (var pluginId in _pluginAssemblies.Keys)
        {
            RegisterApplicationPart(pluginId);
        }
    }

    /// <summary>
    /// 从追踪表中移除指定插件的独立程序集并返回之；未加载独立程序集的插件返回 null。
    /// 幂等：重复调用返回 null，不抛异常。
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>被移除的程序集；不存在时为 null。</returns>
    public Assembly? RemovePluginAssembly(string pluginId)
    {
        _pluginAssemblies.TryRemove(pluginId, out var assembly);
        return assembly;
    }

    /// <summary>
    /// 将指定插件的独立程序集注册为 MVC ApplicationPart（若尚未注册）。
    /// 未设置 partManager、插件无独立程序集或已注册时静默跳过（幂等）。
    /// </summary>
    private void RegisterApplicationPart(string pluginId)
    {
        if (_partManager == null || _registeredApplicationParts.ContainsKey(pluginId))
            return;

        if (!_pluginAssemblies.TryGetValue(pluginId, out var assembly))
            return;

        var part = new AssemblyPart(assembly);
        if (_registeredApplicationParts.TryAdd(pluginId, part))
        {
            _partManager.ApplicationParts.Add(part);
            _applicationPartsChanged?.Invoke();
        }
    }

    /// <summary>
    /// 移除指定插件的 MVC ApplicationPart（若已注册）。未注册/未设置 partManager 时静默跳过（幂等）。
    /// </summary>
    private void UnregisterApplicationPart(string pluginId)
    {
        if (_partManager == null)
            return;

        if (_registeredApplicationParts.TryRemove(pluginId, out var part))
        {
            _partManager.ApplicationParts.Remove(part);
            _applicationPartsChanged?.Invoke();
        }
    }

    /// <summary>
    /// 按依赖拓扑顺序注册所有插件的 DI 服务（插件自注册）。
    /// 每个插件使用独立的 <see cref="ServiceCollection"/>（不注入宿主集合），
    /// 插件 <see cref="IPlugin.Apply"/> 自注册进该子集合后，再挂载到可变 DI 注册表。
    /// </summary>
    /// <param name="services">应用服务集合（保留签名兼容；插件服务不再注入宿主集合）。</param>
    public void RegisterAllServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        XTrace.Log.Info("开始注册所有插件的服务");

        var metadatas = TopologicalSort(DiscoverPlugins());

        foreach (var metadata in metadatas)
        {
            try
            {
                using var __logScope = PluginLogScope.Push(metadata.Id);

                var plugin = ResolvePluginInstance(metadata, out var loadContext);
                if (plugin == null)
                {
                    _pluginStates[metadata.Id] = PluginState.Error;
                    continue;
                }

                var perPluginServices = new ServiceCollection();
                MountPlugin(metadata, plugin, loadContext, perPluginServices);
                _pluginServices[metadata.Id] = perPluginServices;
                _serviceRegistry.Mount(metadata.Id, perPluginServices);

                XTrace.Log.Info("插件服务注册成功: {0} v{1}", metadata.Name, metadata.Version);
                PluginErrorStore.Clear(metadata.Id);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("注册插件服务失败 [{0}]: {1}", metadata.Id, ex.Message);
                PluginErrorStore.Set(metadata.Id, ex);
                _pluginStates[metadata.Id] = PluginState.Error;
            }
        }

        XTrace.Log.Info("所有插件服务注册完成");
    }

    /// <summary>
    /// 初始化指定插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否初始化成功</returns>
    public bool InitializePlugin(string pluginId)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
        {
            XTrace.Log.Error("插件未加载，无法初始化: {0}", pluginId);
            return false;
        }

        if (_pluginStates[pluginId] == PluginState.Initialized ||
            _pluginStates[pluginId] == PluginState.Running)
        {
            XTrace.Log.Warn("插件已初始化: {0}", pluginId);
            return true;
        }

        if (!_metadatas.TryGetValue(pluginId, out var metadata))
        {
            XTrace.Log.Error("插件元数据缺失，无法初始化: {0}", pluginId);
            return false;
        }

        try
        {
            using var __logScope = PluginLogScope.Push(pluginId);

            XTrace.Log.Info("开始初始化插件: {0}", pluginId);
            _pluginStates[pluginId] = PluginState.Initializing;

            // 热启用路径不再二次 Apply：已挂载则复用（幂等）；否则用保存的 per-plugin 集合挂载；
            // 再否则（全新插件，此前从未 Apply）首次 Apply 并挂载。
            if (!_serviceRegistry.IsMounted(pluginId))
            {
                if (!_pluginServices.TryGetValue(pluginId, out var perPluginServices))
                {
                    perPluginServices = new ServiceCollection();
                    MountPlugin(metadata, plugin, null, perPluginServices);
                    _pluginServices[pluginId] = perPluginServices;
                }

                _serviceRegistry.Mount(pluginId, perPluginServices);
            }

            _pluginStates[pluginId] = PluginState.Running;
            XTrace.Log.Info("插件初始化成功: {0}", pluginId);
            PluginErrorStore.Clear(pluginId);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("初始化插件失败 [{0}]: {1}", pluginId, ex.Message);
            PluginErrorStore.Set(pluginId, ex);
            _pluginStates[pluginId] = PluginState.Error;
            return false;
        }
    }

    /// <summary>
    /// 统一 Fiber 装配：为插件创建 Fiber、注册元数据与该插件的独立服务集合、执行 Apply，并登记实例/状态/加载上下文。
    /// 插件在 Apply 中通过 <c>ctx.Get&lt;IServiceCollection&gt;()</c> 拿到的即此独立子集合，自注册其 DI 服务。
    /// </summary>
    /// <param name="metadata">插件元数据。</param>
    /// <param name="plugin">插件实例。</param>
    /// <param name="loadContext">独立程序集加载上下文；内嵌插件为 null。</param>
    /// <param name="services">该插件的独立服务集合（不注入宿主集合）。</param>
    private void MountPlugin(PluginMetadata metadata, IPlugin plugin, PluginLoadContext? loadContext, IServiceCollection? services)
    {
        var fiber = new Fiber(_rootContext);
        // PluginMetadata/IServiceCollection 为插件框架私有对象，注册为本地值（不进共享表，兄弟插件不可见；裁决 A）。
        fiber.Context.RegisterLocal(metadata);
        if (services != null)
        {
            fiber.Context.RegisterLocal(services);
            // 让插件 Service 构造函数可注入 IContext，运行期经 ctx.Get<T>() 取宿主 seed 契约（Cordis 模式）。
            services.AddSingleton<IContext>(fiber.Context);
        }

        fiber.Mount(plugin.Apply);

        // 把该插件程序集中的控制器类型注册进插件子容器（Transient）：
        // 由 PluginAwareControllerActivator 实时从注册表 Resolve，绕开宿主 DI 在热更新后的
        // 陈旧转发描述符（类型身份不匹配）导致插件端点 500。扫描仅在无法枚举类型时降级为已加载部分。
        RegisterControllerServices(plugin.GetType().Assembly, services);

        _plugins[metadata.Id] = plugin;
        _fibers[metadata.Id] = fiber;
        _pluginStates[metadata.Id] = PluginState.Running;

        if (loadContext != null)
        {
            _loadContexts.TryAdd(metadata.Id, loadContext);
        }
    }

    /// <summary>
    /// 把插件程序集中的控制器类型注册进该插件子容器（Transient）。
    /// 目标：<see cref="PluginAwareControllerActivator"/> 经 <see cref="IPluginServiceRegistry.Resolve"/> 解析控制器
    /// 时，控制器类型已在 <c>_index</c>（由 <see cref="IPluginServiceRegistry.Mount"/> 依据服务描述符填充），
    /// 且其构造依赖（插件自带服务 + IContext）由插件子 provider 提供。
    /// </summary>
    /// <param name="assembly">插件入口程序集。</param>
    /// <param name="services">该插件的独立服务集合；null 时静默跳过。</param>
    private static void RegisterControllerServices(Assembly assembly, IServiceCollection? services)
    {
        if (services == null)
        {
            return;
        }

        Type[] types;
        try
        {
            types = assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException)
        {
            // 依赖类型解析不全时无法枚举全部，仅尝试已加载部分（不阻断插件装配）。
            XTrace.Log.Warn("插件控制器类型扫描部分失败（已忽略）: {0}", assembly.GetName().Name);
            return;
        }

        foreach (var type in types)
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
            {
                continue;
            }

            if (typeof(ControllerBase).IsAssignableFrom(type))
            {
                services.AddTransient(type);
            }
        }
    }

    /// <summary>
    /// 销毁指定插件（释放 Fiber 副作用并卸载程序集加载上下文）
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否销毁成功</returns>
    public bool DestroyPlugin(string pluginId)
    {
        if (!_plugins.TryGetValue(pluginId, out _))
        {
            XTrace.Log.Error("插件未加载，无法销毁: {0}", pluginId);
            return false;
        }

        try
        {
            using var __logScope = PluginLogScope.Push(pluginId);

            XTrace.Log.Info("销毁插件: {0}", pluginId);
            _pluginStates[pluginId] = PluginState.Destroying;

            // 动态端点移除：先把该插件注册的 MVC ApplicationPart 摘除并触发 ActionDescriptor 刷新，
            // 再摘除独立程序集追踪。若保留 AssemblyPart，其控制器 [Route] 端点卸载后仍会被路由发现。
            UnregisterApplicationPart(pluginId);
            RemovePluginAssembly(pluginId);

            // 先摘除 DI 子容器索引（此后宿主解析该插件服务即失败），再释放 Fiber 副作用与卸载程序集。
            _serviceRegistry.Unmount(pluginId);
            _pluginServices.TryRemove(pluginId, out _);

            if (_fibers.TryRemove(pluginId, out var fiber))
            {
                fiber.Dispose();
                XTrace.Log.Debug("插件 Fiber 已释放: {0}", pluginId);
            }

            _pluginStates[pluginId] = PluginState.Destroyed;

            _plugins.TryRemove(pluginId, out _);

            if (_loadContexts.TryRemove(pluginId, out var loadContext))
            {
                loadContext.Unload();
                XTrace.Log.Debug("插件加载上下文已卸载: {0}", pluginId);
            }

            XTrace.Log.Info("插件销毁成功: {0}", pluginId);
            if (_metadatas.TryGetValue(pluginId, out var destroyedMetadata))
            {
                EmitLifecycleEvent(destroyedMetadata, "unloaded");
            }
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("销毁插件失败 [{0}]: {1}", pluginId, ex.Message);
            PluginErrorStore.Set(pluginId, ex);
            _pluginStates[pluginId] = PluginState.Error;
            return false;
        }
    }

    /// <summary>
    /// 加载并启动所有插件（按依赖拓扑排序）
    /// </summary>
    public void LoadAndStartAllPlugins()
    {
        XTrace.Log.Info("开始加载并启动所有插件");

        var metadatas = DiscoverPlugins();
        if (metadatas.Count == 0)
        {
            XTrace.Log.Info("没有发现可加载的插件");
            return;
        }

        var sortedPlugins = TopologicalSort(metadatas);
        XTrace.Log.Info("插件加载顺序: {0}", string.Join(" → ", sortedPlugins.Select(p => p.Id)));

        foreach (var metadata in sortedPlugins)
        {
            try
            {
                if (LoadPlugin(metadata.Id))
                {
                    InitializePlugin(metadata.Id);
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("加载启动插件失败 [{0}]: {1}", metadata.Id, ex.Message);
            }
        }

        XTrace.Log.Info("所有插件加载启动完成");
    }

    /// <summary>
    /// 停止并卸载所有插件（逆序）
    /// </summary>
    public void StopAndUnloadAllPlugins()
    {
        XTrace.Log.Info("开始停止并卸载所有插件");

        var pluginIds = _plugins.Keys.Reverse().ToList();

        foreach (var pluginId in pluginIds)
        {
            try
            {
                DestroyPlugin(pluginId);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("停止卸载插件失败 [{0}]: {1}", pluginId, ex.Message);
            }
        }

        XTrace.Log.Info("所有插件停止卸载完成");
    }

    /// <summary>
    /// 拓扑排序，按依赖关系排序插件
    /// </summary>
    /// <param name="metadatas">插件元数据列表</param>
    /// <returns>排序后的插件元数据列表</returns>
    private List<PluginMetadata> TopologicalSort(List<PluginMetadata> metadatas)
    {
        var metadataMap = metadatas.ToDictionary(m => m.Id);
        var sorted = new List<PluginMetadata>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var metadata in metadatas)
        {
            Visit(metadata.Id, metadataMap, sorted, visited, visiting);
        }

        return sorted;
    }

    private void Visit(string pluginId, Dictionary<string, PluginMetadata> metadataMap,
        List<PluginMetadata> sorted, HashSet<string> visited, HashSet<string> visiting)
    {
        if (visited.Contains(pluginId))
            return;

        if (visiting.Contains(pluginId))
        {
            XTrace.Log.Error("检测到插件依赖循环: {0}", pluginId);
            return;
        }

        if (!metadataMap.TryGetValue(pluginId, out var metadata))
            return;

        visiting.Add(pluginId);

        foreach (var depId in metadata.Dependencies)
        {
            Visit(depId, metadataMap, sorted, visited, visiting);
        }

        visiting.Remove(pluginId);
        visited.Add(pluginId);
        sorted.Add(metadata);
    }

    /// <summary>
    /// 获取插件实例
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>插件实例</returns>
    public IPlugin? GetPlugin(string pluginId)
    {
        _plugins.TryGetValue(pluginId, out var plugin);
        return plugin;
    }

    /// <summary>
    /// 获取插件元数据
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>插件元数据</returns>
    public PluginMetadata? GetPluginMetadata(string pluginId)
    {
        _metadatas.TryGetValue(pluginId, out var metadata);
        return metadata;
    }

    /// <summary>
    /// 解析指定插件当前生效的入口程序集磁盘路径；内嵌插件（无独立 DLL）返回 null。
    /// 供版本服务在切换 current 指针前后探测旧 DLL 句柄是否可释放。
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>入口程序集绝对路径；无独立程序集时为 null。</returns>
    public string? GetPluginEntryAssemblyPath(string pluginId)
    {
        if (!_metadatas.TryGetValue(pluginId, out var metadata))
            return null;

        return PluginVersionLayout.ResolveEntryAssemblyPath(metadata);
    }

    /// <summary>
    /// 从磁盘重新读取插件活动清单并刷新内存中的元数据。
    /// 优先读取 side-by-side 当前版本目录下的 <c>plugin.json</c>，回退到插件根目录清单。
    /// 显式重载入口：版本化 update/rollback 切换 current 指针后同步版本号与入口信息。
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>刷新后的元数据；插件不存在或清单不可读时返回原值。</returns>
    public PluginMetadata? RefreshMetadataFromDisk(string pluginId)
    {
        if (!_metadatas.TryGetValue(pluginId, out var existing))
            return null;

        var pluginDir = existing.PluginDirectory;
        if (string.IsNullOrWhiteSpace(pluginDir) || !Directory.Exists(pluginDir))
            return existing;

        var manifestPath = Path.Combine(pluginDir, "plugin.json");
        var current = PluginVersionLayout.ReadCurrentVersion(pluginDir);
        if (!string.IsNullOrWhiteSpace(current))
        {
            var versionManifest = Path.Combine(
                pluginDir, PluginVersionLayout.VersionsFolderName, current, "plugin.json");
            if (File.Exists(versionManifest))
                manifestPath = versionManifest;
        }

        if (!File.Exists(manifestPath))
            return existing;

        var metadata = LoadPluginManifest(manifestPath);
        if (metadata == null)
            return existing;

        metadata.PluginDirectory = pluginDir;
        _metadatas[pluginId] = metadata;
        _pluginStates.TryAdd(pluginId, PluginState.NotLoaded);
        return metadata;
    }

    /// <summary>
    /// 热重载插件：停用旧 Fiber → 卸载 ALC → 强制回收 → 刷新元数据 → 重载新版。
    /// 加载新版失败时回退上一可用版本（若存在 current 指针）并尽力重载旧版，不抛异常、
    /// 不影响宿主与其它插件。此方法为显式重载的可测单元（2026-09-24 起无自动热重载调用方，保留供显式操作与回退复用）。
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否重载成功（内嵌插件或未运行插件刷新元数据后返回 true）。</returns>
    public bool ReloadPlugin(string pluginId)
    {
        XTrace.Log.Info("热重载插件: {0}", pluginId);

        if (!_metadatas.TryGetValue(pluginId, out var metadata))
        {
            XTrace.Log.Error("热重载失败：插件不存在: {0}", pluginId);
            return false;
        }

        var previousVersion = PluginVersionLayout.ReadCurrentVersion(metadata.PluginDirectory);
        var wasRunning = _pluginStates.TryGetValue(pluginId, out var state) && state == PluginState.Running;

        if (wasRunning && !DestroyPlugin(pluginId))
        {
            XTrace.Log.Error("热重载失败：停用旧版失败: {0}", pluginId);
            return false;
        }

        if (wasRunning)
            PluginAssemblyUnloader.ForceCollect();

        RefreshMetadataFromDisk(pluginId);

        if (!wasRunning)
            return true;

        if (EnablePlugin(pluginId))
            return true;

        // 回退上一可用版本（若存在 current 指针），避免坏版本长期生效。
        XTrace.Log.Error("热重载失败：加载新版失败，回退到 {0}", previousVersion ?? "(内嵌主程序集)");
        if (!string.IsNullOrWhiteSpace(previousVersion))
            PluginVersionLayout.WriteCurrentVersion(metadata.PluginDirectory, previousVersion);

        RefreshMetadataFromDisk(pluginId);
        EnablePlugin(pluginId);
        return false;
    }

    /// <summary>
    /// 获取插件状态
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>插件状态</returns>
    public PluginState GetPluginState(string pluginId)
    {
        _pluginStates.TryGetValue(pluginId, out var state);
        return state;
    }

    /// <summary>
    /// 启用插件（热插拔）
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否成功</returns>
    public bool EnablePlugin(string pluginId)
    {
        XTrace.Log.Info("启用插件: {0}", pluginId);

        if (!LoadPlugin(pluginId))
            return false;

        return InitializePlugin(pluginId);
    }

    /// <summary>
    /// 禁用插件（热插拔）
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否成功</returns>
    public bool DisablePlugin(string pluginId)
    {
        XTrace.Log.Info("禁用插件: {0}", pluginId);
        return DestroyPlugin(pluginId);
    }

    /// <summary>
    /// 获取所有插件元数据
    /// </summary>
    /// <returns>所有插件元数据</returns>
    public IEnumerable<PluginMetadata> GetAllMetadatas()
    {
        return _metadatas.Values;
    }

    /// <summary>
    /// 搜索插件（按名称、描述、标签）
    /// </summary>
    /// <param name="keyword">关键词</param>
    /// <returns>匹配的插件元数据列表</returns>
    public List<PluginMetadata> SearchPlugins(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return GetAllMetadatas().ToList();

        var keywordLower = keyword.ToLower();
        return GetAllMetadatas()
            .Where(m =>
                m.Name.ToLower().Contains(keywordLower) ||
                m.Description.ToLower().Contains(keywordLower) ||
                m.Id.ToLower().Contains(keywordLower) ||
                m.Author.ToLower().Contains(keywordLower) ||
                m.Tags.Any(t => t.ToLower().Contains(keywordLower))
            )
            .ToList();
    }

    /// <summary>
    /// 按分类筛选插件
    /// </summary>
    /// <param name="category">分类名称</param>
    /// <returns>匹配的插件元数据列表</returns>
    public List<PluginMetadata> FilterByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return GetAllMetadatas().ToList();

        return GetAllMetadatas()
            .Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// 排序插件
    /// </summary>
    /// <param name="metadatas">插件元数据列表</param>
    /// <param name="sortBy">排序方式</param>
    /// <param name="ascending">是否升序</param>
    /// <returns>排序后的插件元数据列表</returns>
    public List<PluginMetadata> SortPlugins(List<PluginMetadata> metadatas, string sortBy, bool ascending = true)
    {
        var sortByEnum = Enum.TryParse<PluginSortBy>(sortBy, true, out var result)
            ? result
            : PluginSortBy.Name;

        return sortByEnum switch
        {
            PluginSortBy.InstallCount => ascending
                ? metadatas.OrderBy(m => m.InstallCount).ToList()
                : metadatas.OrderByDescending(m => m.InstallCount).ToList(),
            PluginSortBy.UpdatedAt => ascending
                ? metadatas.OrderBy(m => m.UpdatedAt ?? DateTime.MinValue).ToList()
                : metadatas.OrderByDescending(m => m.UpdatedAt ?? DateTime.MinValue).ToList(),
            PluginSortBy.Rating => ascending
                ? metadatas.OrderBy(m => m.Rating).ToList()
                : metadatas.OrderByDescending(m => m.Rating).ToList(),
            _ => ascending
                ? metadatas.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList()
                : metadatas.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    /// <summary>
    /// 获取推荐插件
    /// </summary>
    /// <param name="limit">返回数量限制</param>
    /// <returns>推荐插件列表</returns>
    public List<PluginMetadata> GetRecommendedPlugins(int limit = 10)
    {
        return GetAllMetadatas()
            .OrderByDescending(m => m.Rating)
            .ThenByDescending(m => m.InstallCount)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// 获取热门插件
    /// </summary>
    /// <param name="limit">返回数量限制</param>
    /// <returns>热门插件列表</returns>
    public List<PluginMetadata> GetPopularPlugins(int limit = 10)
    {
        return GetAllMetadatas()
            .OrderByDescending(m => m.InstallCount)
            .ThenByDescending(m => m.Rating)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// 获取所有插件分类
    /// </summary>
    /// <returns>分类列表及数量</returns>
    public List<PluginCategoryDto> GetCategories()
    {
        var categoryGroups = GetAllMetadatas()
            .GroupBy(m => m.Category)
            .Select(g => new PluginCategoryDto
            {
                Name = g.Key,
                DisplayName = g.Key,
                Count = g.Count(),
                Icon = GetCategoryIcon(g.Key)
            })
            .OrderBy(c => c.Name)
            .ToList();

        return categoryGroups;
    }

    /// <summary>
    /// 获取分类图标
    /// </summary>
    private string GetCategoryIcon(string category)
    {
        return category.ToLower() switch
        {
            "工具" => "🔧",
            "ai" => "🤖",
            "系统" => "⚙️",
            "开发" => "💻",
            "效率" => "⚡",
            "数据" => "📊",
            "安全" => "🔒",
            "娱乐" => "🎮",
            _ => "📦"
        };
    }
}
