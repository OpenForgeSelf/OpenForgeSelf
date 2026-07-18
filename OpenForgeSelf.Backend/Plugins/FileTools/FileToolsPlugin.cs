using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.FileTools.Models;
using OpenForgeSelf.Backend.Plugins.FileTools.Services;
using OpenForgeSelf.Backend.Services.UsageStats;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.FileTools;

public class FileToolsPlugin : IPlugin
{
    public string Id => "filetools.plugin";
    public string Name => "文件工具插件";
    public string Version => "1.0.0";
    public string Author => "OpenForgeSelf Team";
    public string Description => "提供批量重命名、文件清理、压缩解压、文件统计等常用文件处理工具。";
    public string IconUrl => "https://example.com/file-tools-icon.png";

    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    private IServiceProvider? _serviceProvider;

    public void Initialize(IServiceProvider services)
    {
        _serviceProvider = services;
        XTrace.Log.Info("[FileToolsPlugin] 初始化文件工具插件");

        RegisterServices();
        RegisterMenuExtensions();
        RegisterToolFunctionExtensions();

        XTrace.Log.Info("[FileToolsPlugin] 文件工具插件初始化完成");
    }

    public void Start()
    {
        XTrace.Log.Info("[FileToolsPlugin] 启动文件工具插件");
        XTrace.Log.Info("[FileToolsPlugin] 文件工具插件启动完成");
    }

    public void Stop()
    {
        XTrace.Log.Info("[FileToolsPlugin] 停止文件工具插件");
        XTrace.Log.Info("[FileToolsPlugin] 文件工具插件已停止");
    }

    public void Destroy()
    {
        XTrace.Log.Info("[FileToolsPlugin] 销毁文件工具插件");
        MenuExtensions.Clear();
        ToolExtensions.Clear();
        XTrace.Log.Info("[FileToolsPlugin] 文件工具插件已销毁");
    }

    private void RegisterServices()
    {
        XTrace.Log.Debug("[FileToolsPlugin] 注册服务");
    }

    private void RegisterMenuExtensions()
    {
        XTrace.Log.Debug("[FileToolsPlugin] 注册菜单扩展点");

        var fileToolsMenu = new FileToolsMenuExtension
        {
            Id = "filetools.menu.main",
            Name = "文件工具",
            PluginId = Id,
            Icon = "fa-solid fa-folder-open",
            Path = "/file-tools",
            Order = 300,
            ParentId = null,
            Children = new List<IMenuExtension>
            {
                new FileToolsMenuExtension
                {
                    Id = "filetools.menu.rename",
                    Name = "批量重命名",
                    PluginId = Id,
                    Icon = "fa-solid fa-pen-to-square",
                    Path = "/file-tools/rename",
                    Order = 1,
                    ParentId = "filetools.menu.main"
                },
                new FileToolsMenuExtension
                {
                    Id = "filetools.menu.cleanup",
                    Name = "批量清理",
                    PluginId = Id,
                    Icon = "fa-solid fa-broom",
                    Path = "/file-tools/cleanup",
                    Order = 2,
                    ParentId = "filetools.menu.main"
                },
                new FileToolsMenuExtension
                {
                    Id = "filetools.menu.archive",
                    Name = "压缩解压",
                    PluginId = Id,
                    Icon = "fa-solid fa-file-zipper",
                    Path = "/file-tools/archive",
                    Order = 3,
                    ParentId = "filetools.menu.main"
                },
                new FileToolsMenuExtension
                {
                    Id = "filetools.menu.stats",
                    Name = "文件统计",
                    PluginId = Id,
                    Icon = "fa-solid fa-chart-pie",
                    Path = "/file-tools/stats",
                    Order = 4,
                    ParentId = "filetools.menu.main"
                }
            }
        };

        MenuExtensions.Add(fileToolsMenu);
        XTrace.Log.Debug("[FileToolsPlugin] 菜单扩展点注册完成，共 {0} 个菜单项", MenuExtensions.Count);
    }

    private void RegisterToolFunctionExtensions()
    {
        XTrace.Log.Debug("[FileToolsPlugin] 注册AI工具函数扩展点");

        ToolExtensions.Add(new FileRenameToolFunction(Id, _serviceProvider));
        ToolExtensions.Add(new FileCleanupToolFunction(Id, _serviceProvider));
        ToolExtensions.Add(new FileCompressToolFunction(Id, _serviceProvider));
        ToolExtensions.Add(new FileExtractToolFunction(Id, _serviceProvider));
        ToolExtensions.Add(new FileStatsToolFunction(Id, _serviceProvider));

        XTrace.Log.Debug("[FileToolsPlugin] AI工具函数扩展点注册完成，共 {0} 个工具函数", ToolExtensions.Count);
    }
}

public class FileToolsMenuExtension : IMenuExtension
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? ParentId { get; set; }
    public IReadOnlyList<IMenuExtension>? Children { get; set; }
}

public class FileRenameToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "filetools.file_rename";
    public string Name => "file_rename";
    public string PluginId { get; }
    public string Description => "批量重命名文件和文件夹，支持序号、日期、查找替换、正则、前缀、后缀、扩展名修改等多种规则组合。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""files"": {
            ""type"": ""array"",
            ""items"": { ""type"": ""string"" },
            ""description"": ""要重命名的文件或文件夹路径列表""
        },
        ""rules"": {
            ""type"": ""array"",
            ""description"": ""重命名规则列表，按顺序应用"",
            ""items"": {
                ""type"": ""object"",
                ""properties"": {
                    ""ruleType"": {
                        ""type"": ""string"",
                        ""enum"": [""Sequence"", ""Date"", ""FindReplace"", ""Regex"", ""Prefix"", ""Suffix"", ""ExtensionChange""],
                        ""description"": ""规则类型""
                    },
                    ""order"": {
                        ""type"": ""integer"",
                        ""description"": ""规则应用顺序""
                    },
                    ""enabled"": {
                        ""type"": ""boolean"",
                        ""description"": ""是否启用该规则""
                    },
                    ""parameters"": {
                        ""type"": ""object"",
                        ""description"": ""规则参数，根据ruleType不同而不同""
                    }
                }
            }
        },
        ""execute"": {
            ""type"": ""boolean"",
            ""description"": ""是否执行重命名，false为预览，默认为false"",
            ""default"": false
        }
    },
    ""required"": [""files"", ""rules""]
}";

    public FileRenameToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[FileToolsPlugin] 执行 file_rename 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var files = new List<string>();
            if (paramsDoc.RootElement.TryGetProperty("files", out var filesProp))
            {
                foreach (var item in filesProp.EnumerateArray())
                {
                    files.Add(item.GetString() ?? string.Empty);
                }
            }

            var rules = new List<RenameRule>();
            if (paramsDoc.RootElement.TryGetProperty("rules", out var rulesProp))
            {
                foreach (var item in rulesProp.EnumerateArray())
                {
                    var rule = new RenameRule
                    {
                        Order = item.TryGetProperty("order", out var orderProp) ? orderProp.GetInt32() : 0,
                        Enabled = !item.TryGetProperty("enabled", out var enabledProp) || enabledProp.GetBoolean()
                    };

                    if (item.TryGetProperty("ruleType", out var typeProp))
                    {
                        var typeStr = typeProp.GetString();
                        if (Enum.TryParse<RenameRuleType>(typeStr, out var ruleType))
                        {
                            rule.RuleType = ruleType;
                        }
                    }

                    if (item.TryGetProperty("parameters", out var paramsProp))
                    {
                        var paramsJson = paramsProp.GetRawText();
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson);
                        if (dict != null)
                        {
                            rule.Parameters = dict;
                        }
                    }

                    rules.Add(rule);
                }
            }

            var execute = false;
            if (paramsDoc.RootElement.TryGetProperty("execute", out var execProp))
            {
                execute = execProp.GetBoolean();
            }

            var service = new RenameService();

            if (execute)
            {
                var result = await service.ExecuteRenameAsync(files, rules);
                var response = new
                {
                    success = result.Success,
                    message = result.Message,
                    totalCount = result.TotalCount,
                    successCount = result.SuccessCount,
                    failedCount = result.FailedCount,
                    operationId = result.OperationId
                };

                var json = JsonSerializer.Serialize(response);

                stopwatch.Stop();
                await RecordUsageAsync("file_rename", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    ["fileCount"] = files.Count,
                    ["ruleCount"] = rules.Count,
                    ["execute"] = execute,
                    ["successCount"] = result.SuccessCount
                });

                return json;
            }
            else
            {
                var preview = await service.PreviewRenameAsync(files, rules);
                var response = new
                {
                    success = true,
                    totalCount = preview.Count,
                    previewItems = preview
                };

                var json = JsonSerializer.Serialize(response);

                stopwatch.Stop();
                await RecordUsageAsync("file_rename", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    ["fileCount"] = files.Count,
                    ["ruleCount"] = rules.Count,
                    ["execute"] = false
                });

                return json;
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileToolsPlugin] file_rename 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("file_rename", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[FileToolsPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class FileCleanupToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "filetools.file_cleanup";
    public string Name => "file_cleanup";
    public string PluginId { get; }
    public string Description => "批量清理文件，支持按扩展名、文件大小、日期筛选，可查找空文件夹和重复文件。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""directory"": {
            ""type"": ""string"",
            ""description"": ""要清理的目录路径""
        },
        ""rules"": {
            ""type"": ""array"",
            ""description"": ""清理规则列表"",
            ""items"": {
                ""type"": ""object"",
                ""properties"": {
                    ""ruleType"": {
                        ""type"": ""string"",
                        ""enum"": [""ByExtension"", ""BySize"", ""ByDate""],
                        ""description"": ""规则类型""
                    },
                    ""enabled"": {
                        ""type"": ""boolean"",
                        ""description"": ""是否启用该规则""
                    },
                    ""parameters"": {
                        ""type"": ""object"",
                        ""description"": ""规则参数""
                    }
                }
            }
        },
        ""recursive"": {
            ""type"": ""boolean"",
            ""description"": ""是否递归子目录"",
            ""default"": true
        },
        ""action"": {
            ""type"": ""string"",
            ""enum"": [""preview"", ""execute"", ""empty_folders"", ""duplicates""],
            ""description"": ""操作类型：preview预览，execute执行清理，empty_folders查找空文件夹，duplicates查找重复文件"",
            ""default"": ""preview""
        },
        ""deletePermanently"": {
            ""type"": ""boolean"",
            ""description"": ""是否永久删除（false则移到回收站）"",
            ""default"": false
        }
    },
    ""required"": [""directory""]
}";

    public FileCleanupToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[FileToolsPlugin] 执行 file_cleanup 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var directory = paramsDoc.RootElement.GetProperty("directory").GetString() ?? string.Empty;
            var recursive = true;
            if (paramsDoc.RootElement.TryGetProperty("recursive", out var recursiveProp))
            {
                recursive = recursiveProp.GetBoolean();
            }

            var action = "preview";
            if (paramsDoc.RootElement.TryGetProperty("action", out var actionProp))
            {
                action = actionProp.GetString() ?? "preview";
            }

            var deletePermanently = false;
            if (paramsDoc.RootElement.TryGetProperty("deletePermanently", out var delProp))
            {
                deletePermanently = delProp.GetBoolean();
            }

            var rules = new List<CleanupRule>();
            if (paramsDoc.RootElement.TryGetProperty("rules", out var rulesProp))
            {
                foreach (var item in rulesProp.EnumerateArray())
                {
                    var rule = new CleanupRule
                    {
                        Enabled = !item.TryGetProperty("enabled", out var enabledProp) || enabledProp.GetBoolean()
                    };

                    if (item.TryGetProperty("ruleType", out var typeProp))
                    {
                        var typeStr = typeProp.GetString();
                        if (Enum.TryParse<CleanupRuleType>(typeStr, out var ruleType))
                        {
                            rule.RuleType = ruleType;
                        }
                    }

                    if (item.TryGetProperty("parameters", out var paramsProp))
                    {
                        var paramsJson = paramsProp.GetRawText();
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson);
                        if (dict != null)
                        {
                            rule.Parameters = dict;
                        }
                    }

                    rules.Add(rule);
                }
            }

            var service = new CleanupService();

            object response;
            string metadataAction;

            switch (action.ToLowerInvariant())
            {
                case "execute":
                    var execResult = await service.ExecuteCleanupAsync(directory, rules, recursive, deletePermanently);
                    response = new
                    {
                        success = execResult.Success,
                        message = execResult.Message,
                        totalCount = execResult.TotalCount,
                        successCount = execResult.SuccessCount,
                        failedCount = execResult.FailedCount,
                        totalSizeBytes = execResult.TotalSizeBytes,
                        totalSizeFormatted = execResult.TotalSizeFormatted,
                        operationId = execResult.OperationId
                    };
                    metadataAction = "execute";
                    break;

                case "empty_folders":
                    var emptyResult = await service.FindEmptyFoldersAsync(directory, recursive);
                    response = new
                    {
                        success = true,
                        folderCount = emptyResult.FolderCount,
                        folders = emptyResult.Folders
                    };
                    metadataAction = "empty_folders";
                    break;

                case "duplicates":
                    var dupResult = await service.FindDuplicateFilesAsync(directory, recursive);
                    response = new
                    {
                        success = true,
                        groupCount = dupResult.GroupCount,
                        totalDuplicateFiles = dupResult.TotalDuplicateFiles,
                        totalWastedSpaceBytes = dupResult.TotalWastedSpaceBytes,
                        totalWastedSpaceFormatted = dupResult.TotalWastedSpaceFormatted,
                        groups = dupResult.Groups
                    };
                    metadataAction = "duplicates";
                    break;

                case "preview":
                default:
                    var previewResult = await service.PreviewCleanupAsync(directory, rules, recursive);
                    response = new
                    {
                        success = true,
                        fileCount = previewResult.FileCount,
                        totalSizeBytes = previewResult.TotalSizeBytes,
                        totalSizeFormatted = previewResult.TotalSizeFormatted,
                        files = previewResult.Files
                    };
                    metadataAction = "preview";
                    break;
            }

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("file_cleanup", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["directory"] = directory,
                ["action"] = metadataAction,
                ["recursive"] = recursive
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileToolsPlugin] file_cleanup 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("file_cleanup", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[FileToolsPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class FileCompressToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "filetools.file_compress";
    public string Name => "file_compress";
    public string PluginId { get; }
    public string Description => "压缩和解压文件，支持ZIP格式，可设置压缩级别。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""action"": {
            ""type"": ""string"",
            ""enum"": [""compress"", ""extract"", ""info""],
            ""description"": ""操作类型：compress压缩，extract解压，info获取压缩包信息""
        },
        ""files"": {
            ""type"": ""array"",
            ""items"": { ""type"": ""string"" },
            ""description"": ""要压缩的文件或文件夹路径列表（compress时使用）""
        },
        ""outputPath"": {
            ""type"": ""string"",
            ""description"": ""压缩包输出路径（compress时）或解压输出目录（extract时）""
        },
        ""archivePath"": {
            ""type"": ""string"",
            ""description"": ""压缩包路径（extract或info时使用）""
        },
        ""format"": {
            ""type"": ""string"",
            ""enum"": [""Zip""],
            ""description"": ""压缩格式"",
            ""default"": ""Zip""
        },
        ""compressionLevel"": {
            ""type"": ""integer"",
            ""description"": ""压缩级别 0-9，0为不压缩，9为最小"",
            ""default"": 5
        },
        ""overwrite"": {
            ""type"": ""boolean"",
            ""description"": ""解压时是否覆盖已有文件"",
            ""default"": false
        }
    },
    ""required"": [""action""]
}";

    public FileCompressToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[FileToolsPlugin] 执行 file_compress 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var action = paramsDoc.RootElement.GetProperty("action").GetString() ?? "compress";

            var service = new ArchiveService();
            object response;

            switch (action.ToLowerInvariant())
            {
                case "extract":
                    var archivePath = paramsDoc.RootElement.TryGetProperty("archivePath", out var arcProp)
                        ? arcProp.GetString() ?? string.Empty
                        : string.Empty;
                    var outputPath = paramsDoc.RootElement.TryGetProperty("outputPath", out var outProp)
                        ? outProp.GetString() ?? string.Empty
                        : string.Empty;
                    var overwrite = paramsDoc.RootElement.TryGetProperty("overwrite", out var owProp) && owProp.GetBoolean();

                    var extractResult = await service.ExtractAsync(archivePath, outputPath, null, overwrite);
                    response = new
                    {
                        success = extractResult.Success,
                        message = extractResult.Message,
                        outputPath = extractResult.OutputPath,
                        fileCount = extractResult.FileCount,
                        totalExtractedSizeBytes = extractResult.TotalExtractedSizeBytes,
                        totalExtractedSizeFormatted = extractResult.TotalExtractedSizeFormatted
                    };
                    break;

                case "info":
                    var infoArchivePath = paramsDoc.RootElement.TryGetProperty("archivePath", out var infoArcProp)
                        ? infoArcProp.GetString() ?? string.Empty
                        : string.Empty;

                    var infoResult = await service.GetArchiveInfoAsync(infoArchivePath);
                    response = new
                    {
                        success = infoResult.Success,
                        message = infoResult.Message,
                        format = infoResult.Format.ToString(),
                        fileCount = infoResult.FileCount,
                        directoryCount = infoResult.DirectoryCount,
                        totalSizeBytes = infoResult.TotalSizeBytes,
                        totalSizeFormatted = infoResult.TotalSizeFormatted,
                        uncompressedSizeBytes = infoResult.UncompressedSizeBytes,
                        uncompressedSizeFormatted = infoResult.UncompressedSizeFormatted,
                        compressionRatio = infoResult.CompressionRatio
                    };
                    break;

                case "compress":
                default:
                    var files = new List<string>();
                    if (paramsDoc.RootElement.TryGetProperty("files", out var filesProp))
                    {
                        foreach (var item in filesProp.EnumerateArray())
                        {
                            files.Add(item.GetString() ?? string.Empty);
                        }
                    }
                    var compOutputPath = paramsDoc.RootElement.TryGetProperty("outputPath", out var compOutProp)
                        ? compOutProp.GetString() ?? string.Empty
                        : string.Empty;
                    var compressionLevel = 5;
                    if (paramsDoc.RootElement.TryGetProperty("compressionLevel", out var levelProp))
                    {
                        compressionLevel = levelProp.GetInt32();
                    }

                    var compressResult = await service.CompressAsync(files, compOutputPath, ArchiveFormat.Zip, null, null, compressionLevel);
                    response = new
                    {
                        success = compressResult.Success,
                        message = compressResult.Message,
                        outputPath = compressResult.OutputPath,
                        fileCount = compressResult.FileCount,
                        outputSizeBytes = compressResult.OutputSizeBytes,
                        outputSizeFormatted = compressResult.OutputSizeFormatted,
                        originalSizeBytes = compressResult.OriginalSizeBytes,
                        originalSizeFormatted = compressResult.OriginalSizeFormatted,
                        compressionRatio = compressResult.CompressionRatio
                    };
                    break;
            }

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("file_compress", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["action"] = action
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileToolsPlugin] file_compress 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("file_compress", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[FileToolsPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class FileStatsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "filetools.file_stats";
    public string Name => "file_stats";
    public string PluginId { get; }
    public string Description => "文件统计功能，包括目录统计、大文件列表、文件类型分布、文件排序等。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""action"": {
            ""type"": ""string"",
            ""enum"": [""directory"", ""large_files"", ""types"", ""sort""],
            ""description"": ""操作类型：directory目录统计，large_files大文件列表，types文件类型分布，sort文件排序""
        },
        ""directory"": {
            ""type"": ""string"",
            ""description"": ""要统计的目录路径""
        },
        ""files"": {
            ""type"": ""array"",
            ""items"": { ""type"": ""string"" },
            ""description"": ""要排序的文件路径列表（sort时使用）""
        },
        ""recursive"": {
            ""type"": ""boolean"",
            ""description"": ""是否递归子目录"",
            ""default"": true
        },
        ""limit"": {
            ""type"": ""integer"",
            ""description"": ""大文件列表返回数量限制"",
            ""default"": 20
        },
        ""sortBy"": {
            ""type"": ""string"",
            ""enum"": [""Size"", ""Name"", ""CreatedTime"", ""ModifiedTime"", ""Extension""],
            ""description"": ""排序方式"",
            ""default"": ""Size""
        },
        ""ascending"": {
            ""type"": ""boolean"",
            ""description"": ""是否升序"",
            ""default"": false
        }
    },
    ""required"": [""action""]
}";

    public FileStatsToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[FileToolsPlugin] 执行 file_stats 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var action = paramsDoc.RootElement.GetProperty("action").GetString() ?? "directory";

            var service = new FileStatsService();
            object response;

            var directory = paramsDoc.RootElement.TryGetProperty("directory", out var dirProp)
                ? dirProp.GetString() ?? string.Empty
                : string.Empty;
            var recursive = !paramsDoc.RootElement.TryGetProperty("recursive", out var recProp) || recProp.GetBoolean();

            switch (action.ToLowerInvariant())
            {
                case "large_files":
                    var limit = 20;
                    if (paramsDoc.RootElement.TryGetProperty("limit", out var limitProp))
                    {
                        limit = limitProp.GetInt32();
                    }
                    var largeResult = await service.GetLargeFilesAsync(directory, limit, recursive);
                    response = new
                    {
                        success = true,
                        totalCount = largeResult.TotalCount,
                        totalSizeBytes = largeResult.TotalSizeBytes,
                        totalSizeFormatted = largeResult.TotalSizeFormatted,
                        files = largeResult.Files
                    };
                    break;

                case "types":
                    var typesResult = await service.GetFileTypesBreakdownAsync(directory, recursive);
                    response = new
                    {
                        success = true,
                        totalFileCount = typesResult.TotalFileCount,
                        totalSizeBytes = typesResult.TotalSizeBytes,
                        totalSizeFormatted = typesResult.TotalSizeFormatted,
                        types = typesResult.Types
                    };
                    break;

                case "sort":
                    var files = new List<string>();
                    if (paramsDoc.RootElement.TryGetProperty("files", out var filesProp))
                    {
                        foreach (var item in filesProp.EnumerateArray())
                        {
                            files.Add(item.GetString() ?? string.Empty);
                        }
                    }
                    var sortBy = FileSortBy.Size;
                    if (paramsDoc.RootElement.TryGetProperty("sortBy", out var sortProp))
                    {
                        var sortStr = sortProp.GetString();
                        if (Enum.TryParse<FileSortBy>(sortStr, out var sortType))
                        {
                            sortBy = sortType;
                        }
                    }
                    var ascending = paramsDoc.RootElement.TryGetProperty("ascending", out var ascProp) && ascProp.GetBoolean();

                    var sortResult = await service.SortFilesAsync(files, sortBy, ascending);
                    response = new
                    {
                        success = true,
                        files = sortResult.Files
                    };
                    break;

                case "directory":
                default:
                    var dirResult = await service.GetDirectoryStatsAsync(directory, recursive);
                    response = new
                    {
                        success = true,
                        directory = dirResult.Directory,
                        fileCount = dirResult.FileCount,
                        directoryCount = dirResult.DirectoryCount,
                        totalSizeBytes = dirResult.TotalSizeBytes,
                        totalSizeFormatted = dirResult.TotalSizeFormatted,
                        fileTypeBreakdown = dirResult.FileTypeBreakdown,
                        oldestFileTime = dirResult.OldestFileTime,
                        newestFileTime = dirResult.NewestFileTime
                    };
                    break;
            }

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("file_stats", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["action"] = action,
                ["directory"] = directory
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileToolsPlugin] file_stats 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("file_stats", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[FileToolsPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class FileExtractToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "filetools.file_extract";
    public string Name => "file_extract";
    public string PluginId { get; }
    public string Description => "解压压缩文件，支持ZIP格式，可指定解压目录和是否覆盖已有文件。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""archivePath"": {
            ""type"": ""string"",
            ""description"": ""压缩包文件路径""
        },
        ""outputPath"": {
            ""type"": ""string"",
            ""description"": ""解压输出目录路径""
        },
        ""overwrite"": {
            ""type"": ""boolean"",
            ""description"": ""是否覆盖已有的文件"",
            ""default"": false
        },
        ""password"": {
            ""type"": ""string"",
            ""description"": ""压缩包密码（如果有）""
        },
        ""format"": {
            ""type"": ""string"",
            ""enum"": [""Zip""],
            ""description"": ""压缩格式"",
            ""default"": ""Zip""
        }
    },
    ""required"": [""archivePath"", ""outputPath""]
}";

    public FileExtractToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[FileToolsPlugin] 执行 file_extract 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var archivePath = paramsDoc.RootElement.TryGetProperty("archivePath", out var arcProp)
                ? arcProp.GetString() ?? string.Empty
                : string.Empty;
            var outputPath = paramsDoc.RootElement.TryGetProperty("outputPath", out var outProp)
                ? outProp.GetString() ?? string.Empty
                : string.Empty;
            var overwrite = paramsDoc.RootElement.TryGetProperty("overwrite", out var owProp) && owProp.GetBoolean();

            if (string.IsNullOrWhiteSpace(archivePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "压缩包路径不能为空" });
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "解压输出目录不能为空" });
            }

            var service = new ArchiveService();
            var extractResult = await service.ExtractAsync(archivePath, outputPath, null, overwrite);
            var response = new
            {
                success = extractResult.Success,
                message = extractResult.Message,
                outputPath = extractResult.OutputPath,
                fileCount = extractResult.FileCount,
                totalExtractedSizeBytes = extractResult.TotalExtractedSizeBytes,
                totalExtractedSizeFormatted = extractResult.TotalExtractedSizeFormatted
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("file_extract", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["fileCount"] = extractResult.FileCount,
                ["outputPath"] = outputPath
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileToolsPlugin] file_extract 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("file_extract", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[FileToolsPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}
