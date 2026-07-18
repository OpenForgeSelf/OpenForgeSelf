using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using OpenForgeSelf.Backend.Models.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins;

/// <summary>
/// 插件管理器，负责插件的发现、加载、生命周期管理
/// </summary>
public class PluginManager
{
    private readonly ConcurrentDictionary<string, PluginLoadContext> _loadContexts = new();
    private readonly ConcurrentDictionary<string, IPlugin> _plugins = new();
    private readonly ConcurrentDictionary<string, PluginMetadata> _metadatas = new();
    private readonly ConcurrentDictionary<string, PluginState> _pluginStates = new();
    private readonly IPermissionChecker _permissionChecker;
    private readonly IServiceProvider _serviceProvider;
    private string _pluginsDirectory = string.Empty;

    /// <summary>
    /// 插件目录
    /// </summary>
    public string PluginsDirectory => _pluginsDirectory;

    /// <summary>
    /// 所有已加载的插件ID列表
    /// </summary>
    public IEnumerable<string> LoadedPluginIds => _plugins.Keys;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="permissionChecker">权限校验器</param>
    public PluginManager(IServiceProvider serviceProvider, IPermissionChecker permissionChecker)
    {
        _serviceProvider = serviceProvider;
        _permissionChecker = permissionChecker;
    }

    /// <summary>
    /// 设置插件目录
    /// </summary>
    /// <param name="pluginsDirectory">插件目录路径</param>
    public void SetPluginsDirectory(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
        XTrace.Log.Info("插件目录设置为: {0}", pluginsDirectory);
    }

    /// <summary>
    /// 扫描并发现所有插件
    /// </summary>
    /// <returns>发现的插件元数据列表</returns>
    public List<PluginMetadata> DiscoverPlugins()
    {
        var metadatas = new List<PluginMetadata>();

        if (!Directory.Exists(_pluginsDirectory))
        {
            XTrace.Log.Warn("插件目录不存在: {0}", _pluginsDirectory);
            return metadatas;
        }

        XTrace.Log.Info("开始扫描插件目录: {0}", _pluginsDirectory);

        var pluginDirectories = Directory.GetDirectories(_pluginsDirectory);
        foreach (var pluginDir in pluginDirectories)
        {
            try
            {
                var manifestPath = Path.Combine(pluginDir, "plugin.json");
                if (!File.Exists(manifestPath))
                {
                    XTrace.Log.Debug("跳过目录（无plugin.json）: {0}", pluginDir);
                    continue;
                }

                var metadata = LoadPluginManifest(manifestPath);
                if (metadata != null)
                {
                    metadata.PluginDirectory = pluginDir;
                    _metadatas.TryAdd(metadata.Id, metadata);
                    _pluginStates.TryAdd(metadata.Id, PluginState.NotLoaded);
                    metadatas.Add(metadata);
                    XTrace.Log.Info("发现插件: {0} v{1} - {2}", metadata.Name, metadata.Version, metadata.Id);
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("扫描插件目录失败 [{0}]: {1}", pluginDir, ex.Message);
            }
        }

        XTrace.Log.Info("插件扫描完成，共发现 {0} 个插件", metadatas.Count);
        return metadatas;
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
            XTrace.Log.Info("开始加载插件: {0}", pluginId);
            _pluginStates[pluginId] = PluginState.Loaded;

            var assemblyPath = Path.Combine(metadata.PluginDirectory, metadata.EntryAssembly);
            if (!File.Exists(assemblyPath))
            {
                XTrace.Log.Error("插件程序集不存在: {0}", assemblyPath);
                _pluginStates[pluginId] = PluginState.Error;
                return false;
            }

            var loadContext = new PluginLoadContext(assemblyPath, pluginId);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            var pluginType = assembly.GetType(metadata.EntryType);
            if (pluginType == null)
            {
                XTrace.Log.Error("插件入口类型不存在: {0}", metadata.EntryType);
                _pluginStates[pluginId] = PluginState.Error;
                return false;
            }

            if (!typeof(IPlugin).IsAssignableFrom(pluginType))
            {
                XTrace.Log.Error("插件类型未实现 IPlugin 接口: {0}", metadata.EntryType);
                _pluginStates[pluginId] = PluginState.Error;
                return false;
            }

            var plugin = (IPlugin?)Activator.CreateInstance(pluginType);
            if (plugin == null)
            {
                XTrace.Log.Error("创建插件实例失败: {0}", metadata.EntryType);
                _pluginStates[pluginId] = PluginState.Error;
                return false;
            }

            _loadContexts.TryAdd(pluginId, loadContext);
            _plugins.TryAdd(pluginId, plugin);
            _pluginStates[pluginId] = PluginState.Loaded;

            XTrace.Log.Info("插件加载成功: {0} v{1}", plugin.Name, plugin.Version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("加载插件失败 [{0}]: {1}", pluginId, ex.Message);
            _pluginStates[pluginId] = PluginState.Error;
            return false;
        }
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

        try
        {
            XTrace.Log.Info("开始初始化插件: {0}", pluginId);
            _pluginStates[pluginId] = PluginState.Initializing;

            plugin.Initialize(_serviceProvider);

            _pluginStates[pluginId] = PluginState.Initialized;
            XTrace.Log.Info("插件初始化成功: {0}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("初始化插件失败 [{0}]: {1}", pluginId, ex.Message);
            _pluginStates[pluginId] = PluginState.Error;
            return false;
        }
    }

    /// <summary>
    /// 启动指定插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否启动成功</returns>
    public bool StartPlugin(string pluginId)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
        {
            XTrace.Log.Error("插件未加载，无法启动: {0}", pluginId);
            return false;
        }

        if (_pluginStates[pluginId] == PluginState.Running)
        {
            XTrace.Log.Warn("插件已在运行: {0}", pluginId);
            return true;
        }

        if (_pluginStates[pluginId] != PluginState.Initialized &&
            _pluginStates[pluginId] != PluginState.Stopped)
        {
            XTrace.Log.Error("插件状态不允许启动: {0} (当前状态: {1})", pluginId, _pluginStates[pluginId]);
            return false;
        }

        try
        {
            XTrace.Log.Info("启动插件: {0}", pluginId);
            _pluginStates[pluginId] = PluginState.Starting;

            plugin.Start();

            _pluginStates[pluginId] = PluginState.Running;
            XTrace.Log.Info("插件启动成功: {0}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("启动插件失败 [{0}]: {1}", pluginId, ex.Message);
            _pluginStates[pluginId] = PluginState.Error;
            return false;
        }
    }

    /// <summary>
    /// 停止指定插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否停止成功</returns>
    public bool StopPlugin(string pluginId)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
        {
            XTrace.Log.Error("插件未加载，无法停止: {0}", pluginId);
            return false;
        }

        if (_pluginStates[pluginId] != PluginState.Running)
        {
            XTrace.Log.Warn("插件未在运行，无需停止: {0} (当前状态: {1})", pluginId, _pluginStates[pluginId]);
            return true;
        }

        try
        {
            XTrace.Log.Info("停止插件: {0}", pluginId);
            _pluginStates[pluginId] = PluginState.Stopping;

            plugin.Stop();

            _pluginStates[pluginId] = PluginState.Stopped;
            XTrace.Log.Info("插件停止成功: {0}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("停止插件失败 [{0}]: {1}", pluginId, ex.Message);
            _pluginStates[pluginId] = PluginState.Error;
            return false;
        }
    }

    /// <summary>
    /// 销毁指定插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否销毁成功</returns>
    public bool DestroyPlugin(string pluginId)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
        {
            XTrace.Log.Error("插件未加载，无法销毁: {0}", pluginId);
            return false;
        }

        try
        {
            XTrace.Log.Info("销毁插件: {0}", pluginId);
            var currentState = _pluginStates[pluginId];
            _pluginStates[pluginId] = PluginState.Destroying;

            if (currentState == PluginState.Running)
            {
                plugin.Stop();
            }

            plugin.Destroy();

            _pluginStates[pluginId] = PluginState.Destroyed;

            _plugins.TryRemove(pluginId, out _);

            if (_loadContexts.TryRemove(pluginId, out var loadContext))
            {
                loadContext.Unload();
                XTrace.Log.Debug("插件加载上下文已卸载: {0}", pluginId);
            }

            XTrace.Log.Info("插件销毁成功: {0}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("销毁插件失败 [{0}]: {1}", pluginId, ex.Message);
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
                    StartPlugin(metadata.Id);
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

        if (!InitializePlugin(pluginId))
            return false;

        return StartPlugin(pluginId);
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
