using OpenForgeSelf.Abstractions;
using ScriptEntity = OpenForgeSelf.Backend.Plugins.ScriptRunner.Entities.Script;
using CodeSnippetEntity = OpenForgeSelf.Backend.Plugins.ScriptRunner.Entities.CodeSnippet;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

/// <summary>
/// 脚本库统计读取实现（ADR D2）：供宿主 UsageStats / Recommendation 服务跨程序集消费。
/// 使用本插件 XCode 实体读取，宿主不再直接依赖 ScriptRunner 程序集。
/// 各方法防御式捕获异常并返回空默认值，与拆分前宿主行为一致（脚本库不可用时统计为空）。
/// </summary>
public class ScriptLibraryStatsProvider : IScriptLibraryStatsProvider
{
    public Task<ScriptLibraryStats> GetLibraryStatsAsync()
    {
        try
        {
            var scripts = ScriptEntity.FindAll();
            var snippets = CodeSnippetEntity.FindAll();

            var stats = new ScriptLibraryStats
            {
                ScriptCount = scripts.Count,
                CodeSnippetCount = snippets.Count,
                FavoriteCount = scripts.Count(s => s.IsFavorite) + snippets.Count(s => s.IsFavorite),
                TopScripts = scripts
                    .OrderByDescending(s => s.UsageCount)
                    .Take(10)
                    .Select(s => new TopScriptItem
                    {
                        ScriptId = s.Id,
                        ScriptName = s.Name,
                        Language = ((ScriptLanguage)s.Language).ToString(),
                        UsageCount = s.UsageCount,
                        LastUsedAt = s.LastUsedAt > DateTime.MinValue ? s.LastUsedAt : (DateTime?)null
                    })
                    .ToList()
            };
            return Task.FromResult(stats);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ScriptRunner] 获取脚本库统计失败: {0}", ex.Message);
            return Task.FromResult(new ScriptLibraryStats());
        }
    }

    public Task<ScriptCreationStats> GetCreationStatsAsync(DateTime start, DateTime end)
    {
        try
        {
            var scripts = ScriptEntity.FindAll(ScriptEntity._.CreatedAt >= start & ScriptEntity._.CreatedAt < end);
            var snippets = CodeSnippetEntity.FindAll(CodeSnippetEntity._.CreatedAt >= start & CodeSnippetEntity._.CreatedAt < end);

            var stats = new ScriptCreationStats
            {
                NewScriptsByDate = scripts
                    .GroupBy(s => s.CreatedAt.Date)
                    .ToDictionary(g => g.Key, g => g.Count()),
                NewCodeSnippetsByDate = snippets
                    .GroupBy(s => s.CreatedAt.Date)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
            return Task.FromResult(stats);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ScriptRunner] 获取脚本/片段创建统计失败: {0}", ex.Message);
            return Task.FromResult(new ScriptCreationStats());
        }
    }

    public Task<ScriptUsageTotals> GetUsageTotalsAsync()
    {
        try
        {
            var totals = new ScriptUsageTotals
            {
                ScriptUsageCount = ScriptEntity.FindAll().Sum(s => s.UsageCount),
                CodeSnippetUsageCount = CodeSnippetEntity.FindAll().Sum(s => s.UsageCount)
            };
            return Task.FromResult(totals);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ScriptRunner] 获取脚本使用统计失败: {0}", ex.Message);
            return Task.FromResult(new ScriptUsageTotals());
        }
    }

    public Task<List<ScriptRecommendationItem>> GetTopScriptsAsync(int take)
    {
        try
        {
            var items = ScriptEntity.FindAll()
                .OrderByDescending(s => s.UsageCount)
                .Take(take)
                .Select(s => new ScriptRecommendationItem
                {
                    ScriptId = s.Id,
                    ScriptName = s.Name,
                    Description = s.Description,
                    Language = ((ScriptLanguage)s.Language).ToString(),
                    UsageCount = s.UsageCount
                })
                .ToList();
            return Task.FromResult(items);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ScriptRunner] 获取推荐脚本失败: {0}", ex.Message);
            return Task.FromResult(new List<ScriptRecommendationItem>());
        }
    }

    public Task<List<CodeSnippetRecommendationItem>> GetTopCodeSnippetsAsync(int take)
    {
        try
        {
            var items = CodeSnippetEntity.FindAll()
                .OrderByDescending(s => s.UsageCount)
                .Take(take)
                .Select(s => new CodeSnippetRecommendationItem
                {
                    SnippetId = s.Id,
                    Title = s.Title,
                    Description = s.Description,
                    Language = s.Language,
                    UsageCount = s.UsageCount
                })
                .ToList();
            return Task.FromResult(items);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ScriptRunner] 获取推荐代码片段失败: {0}", ex.Message);
            return Task.FromResult(new List<CodeSnippetRecommendationItem>());
        }
    }
}
