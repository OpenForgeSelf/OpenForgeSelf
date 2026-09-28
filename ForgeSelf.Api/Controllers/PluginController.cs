using System.IO;
using System.Security.Cryptography;
using System.Text;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Services;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Controllers;

[ApiController]
[Authorize("ApiKeyPolicy")] // 铁律 17：插件管理面（列表/启停/安装/更新/回滚/设置）一律需宿主 API 令牌，裸 curl 无 token 401
[Route("api/[controller]")]
public class PluginController : ControllerBase
{
    private readonly PluginManager _pluginManager;
    private readonly ExtensionPointManager _extensionPointManager;
    private readonly PluginVersionService _versionService;
    private readonly PluginPackagerService _packagerService;
    private readonly PluginInstallerService _installerService;
    private readonly PluginScaffolderService _scaffolderService;
    private readonly PluginUpdateSettingsService _pluginUpdateSettings;

    public PluginController(
        PluginManager pluginManager,
        ExtensionPointManager extensionPointManager,
        PluginVersionService versionService,
        PluginPackagerService packagerService,
        PluginInstallerService installerService,
        PluginScaffolderService scaffolderService,
        PluginUpdateSettingsService pluginUpdateSettings)
    {
        _pluginManager = pluginManager;
        _extensionPointManager = extensionPointManager;
        _versionService = versionService;
        _packagerService = packagerService;
        _installerService = installerService;
        _scaffolderService = scaffolderService;
        _pluginUpdateSettings = pluginUpdateSettings;
    }

    [HttpGet]
    public ActionResult<ApiResponse<List<PluginInfoDto>>> GetPlugins(
        [FromQuery] string? keyword = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] string? category = null)
    {
        try
        {
            XTrace.Log.Info("获取插件列表，keyword={0}, isEnabled={1}, category={2}", keyword, isEnabled, category);

            var metadatas = _pluginManager.GetAllMetadatas().ToList();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var keywordLower = keyword.ToLower();
                metadatas = metadatas.Where(m =>
                    m.Name.ToLower().Contains(keywordLower) ||
                    m.Description.ToLower().Contains(keywordLower) ||
                    m.Author.ToLower().Contains(keywordLower) ||
                    m.Id.ToLower().Contains(keywordLower)
                ).ToList();
            }

            var pluginInfos = metadatas.Select(m =>
            {
                var state = _pluginManager.GetPluginState(m.Id);
                var isRunning = state == PluginState.Running;

                if (isEnabled.HasValue && isEnabled.Value != isRunning)
                    return null;

                return new PluginInfoDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Version = m.Version,
                    Author = m.Author,
                    Description = m.Description,
                    IconUrl = m.IconUrl,
                    State = state,
                    IsEnabled = isRunning,
                    Category = "工具"
                };
            })
            .Where(p => p != null)
            .Cast<PluginInfoDto>()
            .OrderBy(p => p.Name)
            .ToList();

            return Ok(ApiResponse<List<PluginInfoDto>>.Ok(pluginInfos, "获取插件列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取插件列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PluginInfoDto>>.Error("获取插件列表失败: " + ex.Message));
        }
    }

    [HttpGet("{pluginId}")]
    public ActionResult<ApiResponse<PluginDetailDto>> GetPluginDetail(string pluginId)
    {
        try
        {
            XTrace.Log.Info("获取插件详情: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse<PluginDetailDto>.Error("插件不存在", 404));
            }

            var state = _pluginManager.GetPluginState(pluginId);
            var extensions = _extensionPointManager.GetPluginExtensions(pluginId);
            var extensionPointNames = extensions.Select(e => e.GetType().Name).Distinct().ToList();

            var detail = new PluginDetailDto
            {
                Id = metadata.Id,
                Name = metadata.Name,
                Version = metadata.Version,
                Author = metadata.Author,
                Description = metadata.Description,
                IconUrl = metadata.IconUrl,
                State = state,
                IsEnabled = state == PluginState.Running,
                Dependencies = metadata.Dependencies,
                Permissions = metadata.Permissions,
                ExtensionPoints = extensionPointNames,
                Category = "工具"
            };

            return Ok(ApiResponse<PluginDetailDto>.Ok(detail, "获取插件详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取插件详情失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse<PluginDetailDto>.Error("获取插件详情失败: " + ex.Message));
        }
    }

    [HttpPost("{pluginId}/enable")]
    public ActionResult<ApiResponse> EnablePlugin(string pluginId)
    {
        try
        {
            XTrace.Log.Info("启用插件: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse.Error("插件不存在", 404));
            }

            var state = _pluginManager.GetPluginState(pluginId);
            if (state == PluginState.Running)
            {
                return Ok(ApiResponse.Ok("插件已启用"));
            }

            var success = _pluginManager.EnablePlugin(pluginId);
            if (!success)
            {
                return BadRequest(ApiResponse.Error("启用插件失败", 400));
            }

            _extensionPointManager.DiscoverExtensionsFromPlugin(pluginId);

            return Ok(ApiResponse.Ok("插件启用成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("启用插件失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse.Error("启用插件失败: " + ex.Message));
        }
    }

    [HttpPost("{pluginId}/disable")]
    public ActionResult<ApiResponse> DisablePlugin(string pluginId)
    {
        try
        {
            XTrace.Log.Info("禁用插件: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse.Error("插件不存在", 404));
            }

            var state = _pluginManager.GetPluginState(pluginId);
            if (state == PluginState.NotLoaded || state == PluginState.Stopped)
            {
                return Ok(ApiResponse.Ok("插件已禁用"));
            }

            _extensionPointManager.RemovePluginExtensions(pluginId);

            var success = _pluginManager.DisablePlugin(pluginId);
            if (!success)
            {
                return BadRequest(ApiResponse.Error("禁用插件失败", 400));
            }

            return Ok(ApiResponse.Ok("插件禁用成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("禁用插件失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse.Error("禁用插件失败: " + ex.Message));
        }
    }

    [HttpGet("{pluginId}/settings")]
    public ActionResult<ApiResponse<List<PluginSettingDto>>> GetPluginSettings(string pluginId)
    {
        try
        {
            XTrace.Log.Info("获取插件设置: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse<List<PluginSettingDto>>.Error("插件不存在", 404));
            }

            var settings = new List<PluginSettingDto>();

            return Ok(ApiResponse<List<PluginSettingDto>>.Ok(settings, "获取插件设置成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取插件设置失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse<List<PluginSettingDto>>.Error("获取插件设置失败: " + ex.Message));
        }
    }

    [HttpPut("{pluginId}/settings")]
    public ActionResult<ApiResponse> UpdatePluginSettings(
        string pluginId,
        [FromBody] List<PluginSettingDto> settings)
    {
        try
        {
            XTrace.Log.Info("更新插件设置: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse.Error("插件不存在", 404));
            }

            return Ok(ApiResponse.Ok("插件设置更新成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新插件设置失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse.Error("更新插件设置失败: " + ex.Message));
        }
    }

    [HttpGet("menu-items")]
    [HttpGet("menus")]
    public ActionResult<ApiResponse<List<PluginMenuItemDto>>> GetMenuItems()
    {
        try
        {
            XTrace.Log.Info("获取所有插件菜单项");

            var menuExtensions = _extensionPointManager.GetExtensions<IMenuExtension>();

            var menuItems = menuExtensions.Select(m => new PluginMenuItemDto
            {
                Id = m.Id,
                Name = m.Name,
                Icon = m.Icon,
                Path = m.Path,
                Order = m.Order,
                ParentId = m.ParentId,
                PluginId = m.PluginId,
                Children = m.Children?.Select(c => new PluginMenuItemDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Icon = c.Icon,
                    Path = c.Path,
                    Order = c.Order,
                    ParentId = c.ParentId,
                    PluginId = c.PluginId
                }).ToList()
            }).ToList();

            // 批次A-D2：已启用且声明 frontend.menu 的插件按 manifest 机械派生补发（plugin.json 为界面贡献真源）；
            // 同插件已有 IMenuExtension 项则跳过 manifest 项防双发；menu/route 任一为空不派生（Path 必须真实可导航）。
            var extensionPluginIds = menuItems.Select(i => i.PluginId).ToHashSet();
            menuItems.AddRange(_pluginManager.GetAllMetadatas()
                .Where(m => !string.IsNullOrWhiteSpace(m.Frontend?.Menu)
                            && !string.IsNullOrWhiteSpace(m.Frontend?.Route)
                            && _pluginManager.GetPluginState(m.Id) == PluginState.Running
                            && !extensionPluginIds.Contains(m.Id))
                .Select(m => new PluginMenuItemDto
                {
                    Id = $"{m.Id}.menu.manifest",
                    Name = m.Frontend!.Menu!,
                    Icon = m.Frontend.Icon ?? string.Empty,
                    Path = m.Frontend.Route!,
                    PluginId = m.Id
                }));

            menuItems = menuItems.OrderBy(m => m.Order).ToList();

            return Ok(ApiResponse<List<PluginMenuItemDto>>.Ok(menuItems, "获取菜单项成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取菜单项失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PluginMenuItemDto>>.Error("获取菜单项失败: " + ex.Message));
        }
    }

    [HttpGet("frontend-manifest")]
    public ActionResult<ApiResponse<List<PluginFrontendManifestDto>>> GetFrontendManifest()
    {
        try
        {
            XTrace.Log.Info("获取前端插件清单");

            var metadatas = _pluginManager.GetAllMetadatas().ToList();

            var manifest = metadatas.Select(m =>
            {
                var state = _pluginManager.GetPluginState(m.Id);
                return new PluginFrontendManifestDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Version = m.Version,
                    WebVersion = ComputeWebVersion(m.PluginDirectory, m.Frontend?.Entry),
                    Frontend = m.Frontend,
                    IsEnabled = state == PluginState.Running
                };
            })
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

            return Ok(ApiResponse<List<PluginFrontendManifestDto>>.Ok(manifest, "获取前端插件清单成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取前端插件清单失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PluginFrontendManifestDto>>.Error("获取前端插件清单失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 计算插件界面资源的缓存标识（内容指纹）。
    /// </summary>
    /// <para>
    /// 基于插件界面入口脚本与同目录 <c>style.css</c> 的内容拼接计算短哈希。
    /// 资源读取基准目录与 <see cref="ForgeSelf.Api.Plugins.Services.PluginFrontendFileMiddleware"/>
    /// 保持一致：版本化插件优先 <c>versions/&lt;current&gt;/</c>（web/dist 随版本快照），
    /// 否则回退插件根目录（扁平布局）。任一文件内容变化，指纹即变化，前端据此拼装的
    /// <c>?v=</c> 资源 URL 随之变化，浏览器（对带 <c>?v=</c> 的资源设 <c>immutable</c> 长缓存）
    /// 即会重新拉取新界面，无需手动提升 <c>plugin.json</c> 版本即可在重发插件后刷新生效。
    /// </para>
    /// <param name="pluginDirectory">插件目录绝对路径（含 plugin.json）。</param>
    /// <param name="entry">界面入口相对路径（如 <c>web/dist/index.js</c>），为空则不计算。</param>
    /// <returns>短哈希指纹；无入口、文件缺失或计算异常时为 <see cref="string.Empty"/>。</returns>
    private static string ComputeWebVersion(string? pluginDirectory, string? entry)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory) || string.IsNullOrWhiteSpace(entry))
            return string.Empty;

        try
        {
            var webBase = pluginDirectory;
            var current = PluginVersionLayout.ReadCurrentVersion(pluginDirectory);
            if (!string.IsNullOrWhiteSpace(current))
            {
                var versioned = PluginVersionLayout.VersionDirectory(pluginDirectory, current);
                if (Directory.Exists(versioned))
                    webBase = versioned;
            }

            var entryPath = Path.Combine(webBase, entry.Replace('/', Path.DirectorySeparatorChar));
            var files = new List<string> { entryPath };

            var dir = Path.GetDirectoryName(entryPath);
            if (!string.IsNullOrEmpty(dir))
            {
                var stylePath = Path.Combine(dir, "style.css");
                if (System.IO.File.Exists(stylePath)) files.Add(stylePath);
            }

            var fingerprint = new StringBuilder();
            foreach (var file in files.Where(System.IO.File.Exists))
            {
                var hash = SHA256.HashData(System.IO.File.ReadAllBytes(file));
                // 每个文件取前 6 字节（12 位 hex）作为指纹片段，拼接保证整体随内容变化。
                foreach (var b in hash.AsSpan(0, 6))
                    fingerprint.Append(b.ToString("x2"));
            }

            return fingerprint.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    [HttpGet("tool-functions")]
    public ActionResult<ApiResponse<List<PluginToolFunctionDto>>> GetToolFunctions()
    {
        try
        {
            XTrace.Log.Info("获取所有插件AI工具函数");

            var toolExtensions = _extensionPointManager.GetExtensions<IToolFunctionExtension>();

            var toolFunctions = toolExtensions.Select(t => new PluginToolFunctionDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                PluginId = t.PluginId,
                ParametersJsonSchema = t.ParametersJsonSchema
            }).ToList();

            return Ok(ApiResponse<List<PluginToolFunctionDto>>.Ok(toolFunctions, "获取工具函数成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工具函数失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PluginToolFunctionDto>>.Error("获取工具函数失败: " + ex.Message));
        }
    }

    [HttpGet("detail/{pluginId}")]
    public ActionResult<ApiResponse<PluginDetailDto>> GetPluginFullDetail(string pluginId)
    {
        try
        {
            XTrace.Log.Info("获取插件完整详情: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse<PluginDetailDto>.Error("插件不存在", 404));
            }

            var state = _pluginManager.GetPluginState(pluginId);
            var extensions = _extensionPointManager.GetPluginExtensions(pluginId);
            var extensionPointNames = extensions.Select(e => e.GetType().Name).Distinct().ToList();

            var detail = new PluginDetailDto
            {
                Id = metadata.Id,
                Name = metadata.Name,
                Version = metadata.Version,
                Author = metadata.Author,
                Description = metadata.Description,
                IconUrl = metadata.IconUrl,
                State = state,
                IsEnabled = state == PluginState.Running,
                Dependencies = metadata.Dependencies,
                Permissions = metadata.Permissions,
                ExtensionPoints = extensionPointNames,
                Category = metadata.Category,
                Tags = metadata.Tags,
                Screenshots = metadata.Screenshots,
                HomepageUrl = metadata.HomepageUrl,
                RepositoryUrl = metadata.RepositoryUrl,
                License = metadata.License,
                ReleaseNotes = metadata.ReleaseNotes,
                UpdatedAt = metadata.UpdatedAt,
                InstallCount = metadata.InstallCount,
                Rating = metadata.Rating
            };

            return Ok(ApiResponse<PluginDetailDto>.Ok(detail, "获取插件详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取插件详情失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse<PluginDetailDto>.Error("获取插件详情失败: " + ex.Message));
        }
    }

    [HttpGet("categories")]
    public ActionResult<ApiResponse<List<PluginCategoryDto>>> GetCategories()
    {
        try
        {
            XTrace.Log.Info("获取插件分类列表");

            var categories = _pluginManager.GetCategories();

            return Ok(ApiResponse<List<PluginCategoryDto>>.Ok(categories, "获取分类列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取分类列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PluginCategoryDto>>.Error("获取分类列表失败: " + ex.Message));
        }
    }

    [HttpGet("search")]
    public ActionResult<ApiResponse<PluginSearchResult>> SearchPlugins(
        [FromQuery] string? keyword = null,
        [FromQuery] string? category = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("搜索插件: keyword={0}, category={1}, sortBy={2}, page={3}", keyword, category, sortBy, page);

            var metadatas = _pluginManager.SearchPlugins(keyword ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(category))
            {
                metadatas = metadatas
                    .Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                metadatas = _pluginManager.SortPlugins(metadatas, sortBy, false);
            }

            var total = metadatas.Count;
            var pagedItems = metadatas
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m =>
                {
                    var state = _pluginManager.GetPluginState(m.Id);
                    return new PluginInfoDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Version = m.Version,
                        Author = m.Author,
                        Description = m.Description,
                        IconUrl = m.IconUrl,
                        State = state,
                        IsEnabled = state == PluginState.Running,
                        Category = m.Category,
                        Tags = m.Tags,
                        InstallCount = m.InstallCount,
                        Rating = m.Rating,
                        UpdatedAt = m.UpdatedAt
                    };
                })
                .ToList();

            var result = new PluginSearchResult
            {
                Items = pagedItems,
                Total = total,
                Page = page,
                PageSize = pageSize
            };

            return Ok(ApiResponse<PluginSearchResult>.Ok(result, "搜索插件成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("搜索插件失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<PluginSearchResult>.Error("搜索插件失败: " + ex.Message));
        }
    }

    [HttpGet("recommended")]
    public ActionResult<ApiResponse<List<PluginInfoDto>>> GetRecommendedPlugins([FromQuery] int limit = 10)
    {
        try
        {
            XTrace.Log.Info("获取推荐插件，limit={0}", limit);

            var metadatas = _pluginManager.GetRecommendedPlugins(limit);
            var pluginInfos = metadatas.Select(m =>
            {
                var state = _pluginManager.GetPluginState(m.Id);
                return new PluginInfoDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Version = m.Version,
                    Author = m.Author,
                    Description = m.Description,
                    IconUrl = m.IconUrl,
                    State = state,
                    IsEnabled = state == PluginState.Running,
                    Category = m.Category,
                    Tags = m.Tags,
                    InstallCount = m.InstallCount,
                    Rating = m.Rating,
                    UpdatedAt = m.UpdatedAt
                };
            }).ToList();

            return Ok(ApiResponse<List<PluginInfoDto>>.Ok(pluginInfos, "获取推荐插件成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取推荐插件失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PluginInfoDto>>.Error("获取推荐插件失败: " + ex.Message));
        }
    }

    [HttpGet("popular")]
    public ActionResult<ApiResponse<List<PluginInfoDto>>> GetPopularPlugins([FromQuery] int limit = 10)
    {
        try
        {
            XTrace.Log.Info("获取热门插件，limit={0}", limit);

            var metadatas = _pluginManager.GetPopularPlugins(limit);
            var pluginInfos = metadatas.Select(m =>
            {
                var state = _pluginManager.GetPluginState(m.Id);
                return new PluginInfoDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Version = m.Version,
                    Author = m.Author,
                    Description = m.Description,
                    IconUrl = m.IconUrl,
                    State = state,
                    IsEnabled = state == PluginState.Running,
                    Category = m.Category,
                    Tags = m.Tags,
                    InstallCount = m.InstallCount,
                    Rating = m.Rating,
                    UpdatedAt = m.UpdatedAt
                };
            }).ToList();

            return Ok(ApiResponse<List<PluginInfoDto>>.Ok(pluginInfos, "获取热门插件成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取热门插件失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PluginInfoDto>>.Error("获取热门插件失败: " + ex.Message));
        }
    }

    [HttpPost("package/{pluginId}")]
    public IActionResult PackagePlugin(string pluginId)
    {
        try
        {
            XTrace.Log.Info("打包插件: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse.Error("插件不存在", 404));
            }

            var packageBytes = _packagerService.PackagePlugin(pluginId);
            var fileName = $"{metadata.Name}-{metadata.Version}.forgeself-plugin";

            return File(packageBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("打包插件失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse.Error("打包插件失败: " + ex.Message));
        }
    }

    [HttpPost("install")]
    public async Task<ActionResult<ApiResponse<PluginDetailDto>>> InstallPlugin()
    {
        try
        {
            XTrace.Log.Info("安装插件");

            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse.Error("请上传插件文件", 400));
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.forgeself-plugin");
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            if (!_packagerService.ValidatePackage(tempPath))
            {
                System.IO.File.Delete(tempPath);
                return BadRequest(ApiResponse.Error("插件包校验失败", 400));
            }

            var metadata = _installerService.InstallFromPackage(tempPath);
            System.IO.File.Delete(tempPath);

            if (metadata == null)
            {
                return BadRequest(ApiResponse.Error("插件安装失败", 400));
            }

            _pluginManager.DiscoverPlugins();

            var state = _pluginManager.GetPluginState(metadata.Id);
            var detail = new PluginDetailDto
            {
                Id = metadata.Id,
                Name = metadata.Name,
                Version = metadata.Version,
                Author = metadata.Author,
                Description = metadata.Description,
                IconUrl = metadata.IconUrl,
                State = state,
                IsEnabled = state == PluginState.Running,
                Category = metadata.Category,
                Tags = metadata.Tags,
                InstallCount = metadata.InstallCount,
                Rating = metadata.Rating,
                UpdatedAt = metadata.UpdatedAt
            };

            return Ok(ApiResponse<PluginDetailDto>.Ok(detail, "插件安装成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("安装插件失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<PluginDetailDto>.Error("安装插件失败: " + ex.Message));
        }
    }

    [HttpPost("uninstall/{pluginId}")]
    public ActionResult<ApiResponse> UninstallPlugin(string pluginId)
    {
        try
        {
            XTrace.Log.Info("卸载插件: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse.Error("插件不存在", 404));
            }

            var success = _installerService.UninstallPlugin(pluginId);
            if (!success)
            {
                return BadRequest(ApiResponse.Error("卸载插件失败", 400));
            }

            return Ok(ApiResponse.Ok("插件卸载成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("卸载插件失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse.Error("卸载插件失败: " + ex.Message));
        }
    }

    [HttpGet("updates")]
    public ActionResult<ApiResponse<List<PluginUpdateInfo>>> CheckForUpdates()
    {
        try
        {
            XTrace.Log.Info("检查插件更新");

            var updates = _versionService.CheckForUpdates();

            return Ok(ApiResponse<List<PluginUpdateInfo>>.Ok(updates, "检查更新完成"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("检查更新失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PluginUpdateInfo>>.Error("检查更新失败: " + ex.Message));
        }
    }

    [HttpGet("update-settings")]
    public ActionResult<ApiResponse<PluginUpdateSettings>> GetPluginUpdateSettings()
    {
        try
        {
            XTrace.Log.Info("获取插件更新源配置");
            var settings = _pluginUpdateSettings.Current;
            return Ok(ApiResponse<PluginUpdateSettings>.Ok(settings, "获取插件更新源配置成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取插件更新源配置失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<PluginUpdateSettings>.Error("获取插件更新源配置失败: " + ex.Message));
        }
    }

    [HttpPut("update-settings")]
    public ActionResult<ApiResponse<PluginUpdateSettings>> SavePluginUpdateSettings([FromBody] PluginUpdateSettings request)
    {
        try
        {
            XTrace.Log.Info("保存插件更新源配置");

            var localDir = request?.LocalDir?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(localDir) && !Directory.Exists(localDir))
            {
                return BadRequest(ApiResponse.Error("插件更新源目录不存在", 400));
            }

            _pluginUpdateSettings.Update(cfg => cfg.LocalDir = localDir);
            return Ok(ApiResponse<PluginUpdateSettings>.Ok(_pluginUpdateSettings.Current, "插件更新源配置已保存"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("保存插件更新源配置失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<PluginUpdateSettings>.Error("保存插件更新源配置失败: " + ex.Message));
        }
    }
    [HttpPost("update/{pluginId}")]
    public ActionResult<ApiResponse> UpdatePlugin(string pluginId)
    {
        try
        {
            XTrace.Log.Info("更新插件: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse.Error("插件不存在", 404));
            }

            var success = _versionService.UpdatePlugin(pluginId);
            if (!success)
            {
                return BadRequest(ApiResponse.Error("更新插件失败", 400));
            }

            _pluginManager.DiscoverPlugins();

            return Ok(ApiResponse.Ok("插件更新成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新插件失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse.Error("更新插件失败: " + ex.Message));
        }
    }

    [HttpPost("rollback/{pluginId}")]
    public ActionResult<ApiResponse> RollbackPlugin(string pluginId, [FromBody] RollbackRequest request)
    {
        try
        {
            XTrace.Log.Info("回滚插件: {0} 到版本 {1}", pluginId, request.Version);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse.Error("插件不存在", 404));
            }

            var success = _versionService.RollbackPlugin(pluginId, request.Version);
            if (!success)
            {
                return BadRequest(ApiResponse.Error("回滚插件失败", 400));
            }

            _pluginManager.DiscoverPlugins();

            return Ok(ApiResponse.Ok("插件回滚成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("回滚插件失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse.Error("回滚插件失败: " + ex.Message));
        }
    }

    [HttpGet("{pluginId}/versions")]
    public ActionResult<ApiResponse<List<PluginVersionInfo>>> GetPluginVersions(string pluginId)
    {
        try
        {
            XTrace.Log.Info("获取插件版本历史: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                return NotFound(ApiResponse<List<PluginVersionInfo>>.Error("插件不存在", 404));
            }

            var versions = _versionService.GetPluginVersions(pluginId);

            return Ok(ApiResponse<List<PluginVersionInfo>>.Ok(versions, "获取版本历史成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取版本历史失败 [{0}]: {1}", pluginId, ex.Message);
            return StatusCode(500, ApiResponse<List<PluginVersionInfo>>.Error("获取版本历史失败: " + ex.Message));
        }
    }

    [HttpGet("scaffolder/templates")]
    public ActionResult<ApiResponse<List<PluginTemplateInfo>>> GetScaffoldTemplates()
    {
        try
        {
            XTrace.Log.Info("获取插件模板列表");

            var templates = _scaffolderService.GetPluginTemplates();

            return Ok(ApiResponse<List<PluginTemplateInfo>>.Ok(templates, "获取模板列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取模板列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PluginTemplateInfo>>.Error("获取模板列表失败: " + ex.Message));
        }
    }

    [HttpPost("scaffolder/generate")]
    public IActionResult GeneratePluginScaffold([FromBody] ScaffoldRequest request)
    {
        try
        {
            XTrace.Log.Info("生成插件脚手架: {0}", request.Name);

            var options = new ScaffoldOptions
            {
                Name = request.Name,
                Id = request.Id,
                Description = request.Description,
                Author = request.Author,
                Version = request.Version,
                PluginType = request.PluginType
            };

            var zipBytes = _scaffolderService.GeneratePlugin(options);
            var fileName = $"{request.Name}-scaffold.zip";

            return File(zipBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("生成插件脚手架失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse.Error("生成插件脚手架失败: " + ex.Message));
        }
    }
}

public class RollbackRequest
{
    public string Version { get; set; } = string.Empty;
}

public class ScaffoldRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Id { get; set; }

    public string? Description { get; set; }

    public string? Author { get; set; }

    public string? Version { get; set; }

    public string PluginType { get; set; } = "Tool";
}
