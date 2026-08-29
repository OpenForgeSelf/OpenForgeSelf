using System.Text;
using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.MemorySystem.Data;
using OpenForgeSelf.Backend.Plugins.MemorySystem.Models;
using Microsoft.EntityFrameworkCore;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.MemorySystem.Services;

public interface IMemoryIntegrationService
{
    Task<string> GetRelevantMemoriesAsSystemPromptAsync(string userMessage, int limit = 5);
    Task<List<MemoryDto>> GetRelevantMemoriesAsync(string query, int limit = 5, double minScore = 0.1);
    Task AutoExtractAndSaveMemoriesAsync(string conversationSummary, string source);
    Task<bool> IsMemoryServiceAvailableAsync();
}

public class MemoryIntegrationService : IMemoryIntegrationService
{
    private readonly IMemoryService _memoryService;
    private readonly string _memoryDbDirectory;

    public MemoryIntegrationService(IMemoryService memoryService, string memoryDbDirectory)
    {
        _memoryService = memoryService;
        _memoryDbDirectory = memoryDbDirectory;
    }

    public async Task<string> GetRelevantMemoriesAsSystemPromptAsync(string userMessage, int limit = 5)
    {
        try
        {
            var memories = await _memoryService.GetRelevantMemoriesAsync(userMessage, limit, 0.15);

            if (memories.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("【相关记忆】");
            sb.AppendLine("以下是从你的长期记忆中检索到的与当前对话相关的信息，请在回答时参考这些记忆：");
            sb.AppendLine();

            for (int i = 0; i < memories.Count; i++)
            {
                var mem = memories[i];
                sb.AppendLine($"{i + 1}. [{mem.Type.ToString()}] {mem.Title}");
                sb.AppendLine($"   重要程度: {mem.Importance}");
                if (!string.IsNullOrEmpty(mem.CategoryName))
                    sb.AppendLine($"   分类: {mem.CategoryName}");
                if (mem.Tags != null && mem.Tags.Count > 0)
                    sb.AppendLine($"   标签: {string.Join(", ", mem.Tags)}");
                sb.AppendLine($"   内容: {mem.Content}");
                sb.AppendLine($"   相关度: {mem.RelevanceScore:F2}");
                sb.AppendLine();
            }

            sb.AppendLine("请根据这些记忆提供更个性化、更准确的回答。如果记忆中的信息与当前问题无关，可以忽略。");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemoryIntegration] 检索相关记忆失败: {0}", ex.Message);
            return string.Empty;
        }
    }

    public async Task<List<MemoryDto>> GetRelevantMemoriesAsync(string query, int limit = 5, double minScore = 0.1)
    {
        try
        {
            return await _memoryService.GetRelevantMemoriesAsync(query, limit, minScore);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemoryIntegration] 获取相关记忆失败: {0}", ex.Message);
            return new List<MemoryDto>();
        }
    }

    public async Task AutoExtractAndSaveMemoriesAsync(string conversationSummary, string source)
    {
        try
        {
            XTrace.Log.Info("[MemoryIntegration] 开始自动提取记忆，来源: {0}", source);

            var prompt = BuildExtractionPrompt(conversationSummary);

            var extracted = await ExtractMemoriesWithAIAsync(prompt);

            if (extracted.Count == 0)
            {
                XTrace.Log.Info("[MemoryIntegration] 未提取到有价值的记忆");
                return;
            }

            int savedCount = 0;
            foreach (var mem in extracted)
            {
                try
                {
                    var request = new CreateMemoryRequest
                    {
                        Title = mem.Title,
                        Content = mem.Content,
                        Type = mem.Type,
                        Importance = mem.Importance,
                        Tags = mem.Tags,
                        Source = source
                    };

                    await _memoryService.CreateAsync(request);
                    savedCount++;
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[MemoryIntegration] 保存记忆失败: {0}", ex.Message);
                }
            }

            XTrace.Log.Info("[MemoryIntegration] 自动提取记忆完成，成功保存 {0} 条", savedCount);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemoryIntegration] 自动提取记忆失败: {0}", ex.Message);
        }
    }

    public async Task<bool> IsMemoryServiceAvailableAsync()
    {
        try
        {
            var stats = await _memoryService.GetStatsAsync();
            return stats != null;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildExtractionPrompt(string conversation)
    {
        return $@"请从以下对话内容中提取有价值的、值得长期保存的信息。

对话内容：
{conversation}

请分析并提取以下类型的信息（如果存在）：
1. 用户的个人偏好、习惯、喜好
2. 重要的事实信息
3. 学到的新知识和技能
4. 工作相关的重要信息
5. 项目相关的信息

输出格式要求（JSON数组）：
[
  {{
    ""title"": ""记忆标题，简洁概括"",
    ""content"": ""记忆的详细内容"",
    ""type"": ""fact/preference/project/personal/workflow/skill/other"",
    ""importance"": ""low/medium/high/critical"",
    ""tags"": [""标签1"", ""标签2""]
  }}
]

注意：
- 只提取真正有长期保存价值的信息
- 不要提取无关紧要的闲聊内容
- 确保 JSON 格式正确
- 如果没有值得保存的信息，返回空数组 []";
    }

    private async Task<List<ExtractedMemory>> ExtractMemoriesWithAIAsync(string prompt)
    {
        try
        {
            var dbPath = Path.Combine(_memoryDbDirectory, "memory-system.db");
            var optionsBuilder = new DbContextOptionsBuilder<MemoryDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new List<ExtractedMemory>();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[MemoryIntegration] AI提取记忆失败: {0}", ex.Message);
            return new List<ExtractedMemory>();
        }
    }

    private class ExtractedMemory
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public MemoryType Type { get; set; } = MemoryType.Fact;
        public MemoryImportance Importance { get; set; } = MemoryImportance.Medium;
        public List<string>? Tags { get; set; }
    }
}
