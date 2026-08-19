using System.IO.Compression;
using System.Text;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.Services;

public class PluginScaffolderService
{
    public PluginScaffolderService()
    {
    }

    public List<PluginTemplateInfo> GetPluginTemplates()
    {
        XTrace.Log.Info("获取插件模板列表");

        return new List<PluginTemplateInfo>
        {
            new()
            {
                Id = "tool",
                Name = "工具插件",
                Description = "创建一个提供工具函数的插件，适合开发开发者工具类扩展",
                Icon = "🔧",
                PluginType = "Tool"
            },
            new()
            {
                Id = "ai",
                Name = "AI扩展插件",
                Description = "创建一个AI能力扩展插件，可注册AI工具函数和提示词模板",
                Icon = "🤖",
                PluginType = "AI"
            },
            new()
            {
                Id = "system",
                Name = "系统集成插件",
                Description = "创建一个系统级集成插件，适合扩展核心功能",
                Icon = "⚙️",
                PluginType = "System"
            }
        };
    }

    public byte[] GeneratePlugin(ScaffoldOptions options)
    {
        XTrace.Log.Info("生成插件脚手架: {0}, 类型: {1}", options.Name, options.PluginType);

        if (string.IsNullOrWhiteSpace(options.Name))
        {
            throw new ArgumentException("插件名称不能为空");
        }

        var pluginId = string.IsNullOrWhiteSpace(options.Id) 
            ? GeneratePluginId(options.Name) 
            : options.Id;

        var version = string.IsNullOrWhiteSpace(options.Version) ? "1.0.0" : options.Version;
        var author = string.IsNullOrWhiteSpace(options.Author) ? "Anonymous" : options.Author;
        var description = string.IsNullOrWhiteSpace(options.Description) 
            ? $"{options.Name} 插件" 
            : options.Description;

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var pluginName = options.Name.Replace(" ", "");

            var backendFiles = GenerateBackendFiles(pluginId, pluginName, version, author, description, options.PluginType);
            foreach (var (filePath, content) in backendFiles)
            {
                var entry = archive.CreateEntry($"src/backend/{filePath}");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(content);
            }

            var frontendFiles = GenerateFrontendFiles(pluginId, pluginName, description, options.PluginType);
            foreach (var (filePath, content) in frontendFiles)
            {
                var entry = archive.CreateEntry($"src/frontend/{filePath}");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(content);
            }

            var readme = GenerateReadme(pluginName, description, author, version);
            var readmeEntry = archive.CreateEntry("README.md");
            using (var entryStream = readmeEntry.Open())
            using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
            {
                writer.Write(readme);
            }
        }

        XTrace.Log.Info("插件脚手架生成成功: {0}", options.Name);
        return ms.ToArray();
    }

    private Dictionary<string, string> GenerateBackendFiles(
        string pluginId, string pluginName, string version, 
        string author, string description, string pluginType)
    {
        var files = new Dictionary<string, string>();
        var namespaceName = $"OpenForgeSelf.Backend.Plugins.{pluginName}";

        files["plugin.json"] = GeneratePluginJson(pluginId, pluginName, version, author, description, pluginType);
        files[$"{pluginName}Plugin.cs"] = GeneratePluginMainClass(pluginId, pluginName, namespaceName, description, pluginType);

        if (pluginType == "Tool")
        {
            files["Services/IExampleService.cs"] = GenerateServiceInterface(pluginName, namespaceName);
            files["Services/ExampleService.cs"] = GenerateServiceImplementation(pluginName, namespaceName);
            files["Controllers/ExampleController.cs"] = GenerateController(pluginName, namespaceName);
            files["Models/ExampleModels.cs"] = GenerateModels(pluginName, namespaceName);
        }
        else if (pluginType == "AI")
        {
            files["Services/IToolFunctions.cs"] = GenerateAIToolInterface(pluginName, namespaceName);
            files["Services/ToolFunctions.cs"] = GenerateAIToolImplementation(pluginName, namespaceName);
            files["Controllers/AIController.cs"] = GenerateAIController(pluginName, namespaceName);
        }
        else if (pluginType == "System")
        {
            files["Services/ISystemService.cs"] = GenerateSystemServiceInterface(pluginName, namespaceName);
            files["Services/SystemService.cs"] = GenerateSystemServiceImplementation(pluginName, namespaceName);
        }

        return files;
    }

    private string GeneratePluginJson(string pluginId, string pluginName, string version, 
        string author, string description, string pluginType)
    {
        var category = pluginType switch
        {
            "Tool" => "工具",
            "AI" => "AI",
            "System" => "系统",
            _ => "工具"
        };

        return $$$"""
        {
          "Id": "{{{pluginId}}}",
          "Name": "{{{pluginName}}}",
          "Version": "{{{version}}}",
          "Author": "{{{author}}}",
          "Description": "{{{description}}}",
          "IconUrl": "",
          "Category": "{{{category}}}",
          "Tags": ["{{{category}}}"],
          "EntryAssembly": "OpenForgeSelf.dll",
          "EntryType": "OpenForgeSelf.Backend.Plugins.{{{pluginName}}}.{{{pluginName}}}Plugin",
          "Dependencies": [],
          "Permissions": [],
          "Screenshots": [],
          "HomepageUrl": "",
          "RepositoryUrl": "",
          "License": "MIT",
          "ReleaseNotes": "初始版本发布"
        }
        """;
    }

    private string GeneratePluginMainClass(string pluginId, string pluginName, 
        string namespaceName, string description, string pluginType)
    {
        return $$$"""
        using OpenForgeSelf.Abstractions;
        using OpenForgeSelf.Core;

        namespace {{{namespaceName}}};

        public class {{{pluginName}}}Plugin : IPlugin
        {
            public void Apply(IContext ctx)
            {
                // 插件元数据单一真源在 plugin.json，插件 Id 通过 ctx.Get<PluginMetadata>()?.Id 获取
                // var pluginId = ctx.Get<PluginMetadata>()?.Id;

                // 注册 DI 服务：var services = ctx.Get<Microsoft.Extensions.DependencyInjection.IServiceCollection>();
                // services?.AddScoped<...>();

                // 注册菜单/工具扩展、ctx.Effect(...) 等
            }
        }
        """;
    }

    private string GenerateServiceInterface(string pluginName, string namespaceName)
    {
        return $$$"""
        namespace {{{namespaceName}}}.Services;

        public interface IExampleService
        {
            string GetExampleData();
            Task<string> ProcessExampleAsync(string input);
        }
        """;
    }

    private string GenerateServiceImplementation(string pluginName, string namespaceName)
    {
        return $$$"""
        using NewLife.Log;

        namespace {{{namespaceName}}}.Services;

        public class ExampleService : IExampleService
        {
            public string GetExampleData()
            {
                XTrace.Log.Debug("ExampleService.GetExampleData 被调用");
                return "Hello from {{{pluginName}}}!";
            }

            public Task<string> ProcessExampleAsync(string input)
            {
                XTrace.Log.Debug("ExampleService.ProcessExampleAsync 被调用，input: {0}", input);
                return Task.FromResult($"Processed: {input}");
            }
        }
        """;
    }

    private string GenerateController(string pluginName, string namespaceName)
    {
        var routeName = pluginName.ToLower();
        return $$$"""
        using Microsoft.AspNetCore.Mvc;
        using OpenForgeSelf.Abstractions;
        using {{{namespaceName}}}.Services;
        using NewLife.Log;

        namespace {{{namespaceName}}}.Controllers;

        [ApiController]
        [Route("api/plugins/{{{routeName}}}")]
        public class ExampleController : ControllerBase
        {
            private readonly IExampleService _exampleService;

            public ExampleController(IExampleService exampleService)
            {
                _exampleService = exampleService;
            }

            [HttpGet("example")]
            public ActionResult<ApiResponse<string>> GetExample()
            {
                try
                {
                    var result = _exampleService.GetExampleData();
                    return Ok(ApiResponse<string>.Ok(result, "获取成功"));
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("获取示例数据失败: {0}", ex.Message);
                    return StatusCode(500, ApiResponse<string>.Error("获取失败: " + ex.Message));
                }
            }

            [HttpPost("process")]
            public async Task<ActionResult<ApiResponse<string>>> ProcessExample([FromBody] string input)
            {
                try
                {
                    var result = await _exampleService.ProcessExampleAsync(input);
                    return Ok(ApiResponse<string>.Ok(result, "处理成功"));
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("处理示例数据失败: {0}", ex.Message);
                    return StatusCode(500, ApiResponse<string>.Error("处理失败: " + ex.Message));
                }
            }
        }
        """;
    }

    private string GenerateModels(string pluginName, string namespaceName)
    {
        return $$$"""
        namespace {{{namespaceName}}}.Models;

        public class ExampleRequest
        {
            public string Input { get; set; } = string.Empty;
            public int Option { get; set; }
        }

        public class ExampleResponse
        {
            public string Result { get; set; } = string.Empty;
            public bool Success { get; set; }
            public DateTime ProcessedAt { get; set; }
        }
        """;
    }

    private string GenerateAIToolInterface(string pluginName, string namespaceName)
    {
        return $$$"""
        namespace {{{namespaceName}}}.Services;

        public interface IToolFunctions
        {
            Task<string> ExecuteExampleAsync(string parametersJson);
            Task<string> AnotherToolFunctionAsync(string parametersJson);
        }
        """;
    }

    private string GenerateAIToolImplementation(string pluginName, string namespaceName)
    {
        return $$$"""
        using System.Text.Json;
        using NewLife.Log;

        namespace {{{namespaceName}}}.Services;

        public class ToolFunctions : IToolFunctions
        {
            public Task<string> ExecuteExampleAsync(string parametersJson)
            {
                XTrace.Log.Info("执行示例工具函数，参数: {0}", parametersJson);
                try
                {
                    var result = new
                    {
                        success = true,
                        message = "示例工具执行成功",
                        timestamp = DateTime.Now
                    };
                    return Task.FromResult(JsonSerializer.Serialize(result));
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("示例工具执行失败: {0}", ex.Message);
                    return Task.FromResult($"{{\"success\":false,\"error\":\"{ex.Message}\"}}");
                }
            }

            public Task<string> AnotherToolFunctionAsync(string parametersJson)
            {
                XTrace.Log.Info("执行另一个工具函数");
                return Task.FromResult("{\"success\":true,\"data\":\"another result\"}");
            }
        }
        """;
    }

    private string GenerateAIController(string pluginName, string namespaceName)
    {
        var routeName = pluginName.ToLower();
        return $$$"""
        using Microsoft.AspNetCore.Mvc;
        using OpenForgeSelf.Abstractions;
        using {{{namespaceName}}}.Services;
        using NewLife.Log;

        namespace {{{namespaceName}}}.Controllers;

        [ApiController]
        [Route("api/plugins/{{{routeName}}}")]
        public class AIController : ControllerBase
        {
            private readonly IToolFunctions _toolFunctions;

            public AIController(IToolFunctions toolFunctions)
            {
                _toolFunctions = toolFunctions;
            }

            [HttpPost("execute")]
            public async Task<ActionResult<ApiResponse<string>>> ExecuteTool([FromBody] ToolExecuteRequest request)
            {
                try
                {
                    var result = await _toolFunctions.ExecuteExampleAsync(request.ParametersJson ?? "{}");
                    return Ok(ApiResponse<string>.Ok(result, "执行成功"));
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("执行工具失败: {0}", ex.Message);
                    return StatusCode(500, ApiResponse<string>.Error("执行失败: " + ex.Message));
                }
            }
        }

        public class ToolExecuteRequest
        {
            public string ParametersJson { get; set; } = "{}";
        }
        """;
    }

    private string GenerateSystemServiceInterface(string pluginName, string namespaceName)
    {
        return $$$"""
        namespace {{{namespaceName}}}.Services;

        public interface ISystemService
        {
            string GetSystemInfo();
            bool PerformSystemCheck();
        }
        """;
    }

    private string GenerateSystemServiceImplementation(string pluginName, string namespaceName)
    {
        return $$$"""
        using System.Diagnostics;
        using NewLife.Log;

        namespace {{{namespaceName}}}.Services;

        public class SystemService : ISystemService
        {
            public string GetSystemInfo()
            {
                XTrace.Log.Debug("获取系统信息");
                return $"OS: {Environment.OSVersion}, Machine: {Environment.MachineName}, Processors: {Environment.ProcessorCount}";
            }

            public bool PerformSystemCheck()
            {
                XTrace.Log.Info("执行系统检查");
                try
                {
                    var memory = GC.GetTotalMemory(false);
                    XTrace.Log.Debug("当前内存使用: {0} bytes", memory);
                    return true;
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("系统检查失败: {0}", ex.Message);
                    return false;
                }
            }
        }
        """;
    }

    private Dictionary<string, string> GenerateFrontendFiles(
        string pluginId, string pluginName, string description, string pluginType)
    {
        var files = new Dictionary<string, string>();
        var componentName = pluginName;

        files["types/plugin.ts"] = GenerateFrontendTypes(pluginName);
        files["services/api.ts"] = GenerateFrontendApi(pluginId, pluginName);
        files["stores/plugin.ts"] = GenerateFrontendStore(pluginName);
        files["views/PluginView.vue"] = GenerateFrontendView(pluginName, description, pluginType);
        files["components/ExampleComponent.vue"] = GenerateFrontendComponent(pluginName);

        return files;
    }

    private string GenerateFrontendTypes(string pluginName)
    {
        return $$$"""
        export interface {{{pluginName}}}Config {
          enabled: boolean
          theme: string
        }

        export interface ExampleData {
          id: string
          name: string
          value: string
        }
        """;
    }

    private string GenerateFrontendApi(string pluginId, string pluginName)
    {
        return $$$"""
        const API_BASE = import.meta.env.VITE_API_BASE_URL || '/api'

        export const {{pluginId}}Api = {
          async getExample(): Promise<string> {
            const response = await fetch(`${API_BASE}/plugins/{{{pluginId}}}/example`)
            if (!response.ok) {
              throw new Error('获取示例数据失败')
            }
            return response.json()
          },

          async processExample(input: string): Promise<string> {
            const response = await fetch(`${API_BASE}/plugins/{{{pluginId}}}/process`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(input)
            })
            if (!response.ok) {
              throw new Error('处理示例数据失败')
            }
            return response.json()
          }
        }
        """;
    }

    private string GenerateFrontendStore(string pluginName)
    {
        var storeName = pluginName.ToLower();
        return $$$"""
        import { ref, computed } from 'vue'
        import { defineStore } from 'pinia'

        export const use{{{pluginName}}}Store = defineStore('{{{storeName}}}', () => {
          const isLoading = ref(false)
          const exampleData = ref<string>('')
          const error = ref<string | null>(null)

          async function loadExample() {
            try {
              isLoading.value = true
              error.value = null
              // 调用API获取数据
              exampleData.value = '示例数据'
            } catch (e) {
              error.value = e instanceof Error ? e.message : '加载失败'
            } finally {
              isLoading.value = false
            }
          }

          return {
            isLoading,
            exampleData,
            error,
            loadExample
          }
        })
        """;
    }

    private string GenerateFrontendView(string pluginName, string description, string pluginType)
    {
        return $$$"""
        <script setup lang="ts">
        import { ref, onMounted } from 'vue'
        import ExampleComponent from '@/components/ExampleComponent.vue'

        const isLoading = ref(false)
        const exampleData = ref('')

        async function loadData() {
          isLoading.value = true
          try {
            exampleData.value = 'Hello from {{{pluginName}}}!'
          } finally {
            isLoading.value = false
          }
        }

        onMounted(() => {
          loadData()
        })
        </script>

        <template>
          <div class="plugin-view">
            <header class="view-header">
              <h1>{{{pluginName}}}</h1>
              <p class="description">{{{description}}}</p>
            </header>

            <div v-if="isLoading" class="loading">
              加载中...
            </div>

            <div v-else class="content">
              <ExampleComponent :data="exampleData" />
            </div>
          </div>
        </template>

        <style scoped>
        .plugin-view {
          padding: 24px;
        }

        .view-header {
          margin-bottom: 24px;
        }

        .view-header h1 {
          font-size: 24px;
          font-weight: 600;
          margin: 0 0 8px 0;
        }

        .description {
          color: #6c757d;
          margin: 0;
        }

        .loading {
          text-align: center;
          padding: 40px;
          color: #6c757d;
        }

        .content {
          background: #fff;
          border-radius: 12px;
          padding: 24px;
          border: 1px solid #e9ecef;
        }
        </style>
        """;
    }

    private string GenerateFrontendComponent(string pluginName)
    {
        return $$$"""
        <script setup lang="ts">
        import { computed } from 'vue'

        const props = defineProps<{
          data: string
        }>()

        const displayText = computed(() => {
          return props.data || '暂无数据'
        })
        </script>

        <template>
          <div class="example-component">
            <h3>示例组件</h3>
            <p class="data-text">{{ displayText }}</p>
          </div>
        </template>

        <style scoped>
        .example-component {
          padding: 16px;
        }

        .example-component h3 {
          font-size: 16px;
          margin: 0 0 12px 0;
        }

        .data-text {
          color: #495057;
          font-size: 14px;
          padding: 12px;
          background: #f8f9fa;
          border-radius: 8px;
        }
        </style>
        """;
    }

    private string GenerateReadme(string pluginName, string description, string author, string version)
    {
        return $$$"""
        # {{{pluginName}}} 插件

        > {{{description}}}

        ## 特性

        - 特性1
        - 特性2
        - 特性3

        ## 安装

        1. 将插件文件夹复制到 Plugins 目录
        2. 重启应用或在插件管理中启用
        3. 配置插件设置（如有需要）

        ## 使用方法

        描述插件的基本使用方法...

        ## 开发指南

        ### 后端开发

        后端代码位于 `src/backend/` 目录，使用 C# 开发。

        ### 前端开发

        前端代码位于 `src/frontend/` 目录，使用 Vue 3 + TypeScript 开发。

        ## 版本历史

        ### v{{{version}}}
        - 初始版本发布

        ## 作者

        {{{author}}}

        ## 许可证

        MIT License
        """;
    }

    private string GeneratePluginId(string name)
    {
        var id = name.ToLower()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-");
        return $"{id}.plugin";
    }
}

public class ScaffoldOptions
{
    public string Name { get; set; } = string.Empty;

    public string? Id { get; set; }

    public string? Description { get; set; }

    public string? Author { get; set; }

    public string? Version { get; set; }

    public string PluginType { get; set; } = "Tool";
}

public class PluginTemplateInfo
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string PluginType { get; set; } = string.Empty;
}
