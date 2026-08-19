using System.Text.Json;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Models.UsageStats;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using WorkflowUsageRecordEntity = OpenForgeSelf.Backend.Entities.WorkflowUsageRecord;
using UsageRecordEntity = OpenForgeSelf.Backend.Entities.UsageRecord;

namespace OpenForgeSelf.Backend.Services.UsageStats;

public class WorkflowRecommendationService : IWorkflowRecommendationService
{
    private readonly IScriptLibraryStatsProvider? _scriptStatsProvider;

    public WorkflowRecommendationService()
    {
    }

    /// <summary>
    /// 经共享契约读取脚本库统计（ADR D2），插件未加载时安全降级为空推荐。
    /// </summary>
    public WorkflowRecommendationService(IServiceProvider serviceProvider)
    {
        _scriptStatsProvider = serviceProvider.GetService<IScriptLibraryStatsProvider>();
    }

    public async Task<List<WorkflowRecommendationDto>> GetRecommendedWorkflowsAsync(
        string? userId = null,
        string? context = null,
        int limit = 5)
    {
        try
        {
            XTrace.Log.Debug("获取工作流推荐: userId={0}, context={1}, limit={2}", userId, context, limit);

            var recommendations = new List<WorkflowRecommendationDto>();

            var popularWorkflows = GetFrequencyBasedRecommendations(limit * 2);
            recommendations.AddRange(popularWorkflows);

            if (!string.IsNullOrWhiteSpace(context))
            {
                var contextBased = GetContextBasedRecommendations(context, limit);
                foreach (var wf in contextBased)
                {
                    if (!recommendations.Any(r => r.WorkflowId == wf.WorkflowId))
                    {
                        recommendations.Add(wf);
                    }
                }
            }

            var recentWorkflows = GetRecentUsedRecommendations(limit);
            foreach (var wf in recentWorkflows)
            {
                if (!recommendations.Any(r => r.WorkflowId == wf.WorkflowId))
                {
                    recommendations.Add(wf);
                }
            }

            var finalRecommendations = recommendations
                .OrderByDescending(r => r.MatchScore)
                .Take(limit)
                .ToList();

            XTrace.Log.Debug("工作流推荐完成，共 {0} 个推荐", finalRecommendations.Count);
            return finalRecommendations;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工作流推荐失败: {0}", ex.Message);
            throw;
        }
    }

    private List<WorkflowRecommendationDto> GetFrequencyBasedRecommendations(int limit)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddDays(-30);

            var exp = new WhereExpression();
            exp &= WorkflowUsageRecordEntity._.StartTime >= startDate;
            exp &= WorkflowUsageRecordEntity._.Status == (int)WorkflowExecutionStatus.Success;

            var records = WorkflowUsageRecordEntity.FindAll(exp);

            var popular = records
                .GroupBy(r => new { r.WorkflowId, r.WorkflowName })
                .Select(g => new WorkflowRecommendationDto
                {
                    WorkflowId = g.Key.WorkflowId,
                    WorkflowName = g.Key.WorkflowName,
                    UsageCount = g.Count(),
                    MatchScore = Math.Min(g.Count() * 10.0, 80.0),
                    MatchReason = "高频使用",
                    Tags = new List<string> { "热门" }
                })
                .OrderByDescending(r => r.UsageCount)
                .Take(limit)
                .ToList();

            return popular;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("获取基于频率的推荐失败: {0}", ex.Message);
            return new List<WorkflowRecommendationDto>();
        }
    }

    private List<WorkflowRecommendationDto> GetRecentUsedRecommendations(int limit)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddDays(-7);

            var exp = WorkflowUsageRecordEntity._.StartTime >= startDate;
            var records = WorkflowUsageRecordEntity.FindAll(exp);

            var recent = records
                .GroupBy(r => new { r.WorkflowId, r.WorkflowName })
                .Select(g => new
                {
                    g.Key.WorkflowId,
                    g.Key.WorkflowName,
                    LastUsed = g.Max(r => r.StartTime),
                    UseCount = g.Count()
                })
                .OrderByDescending(r => r.LastUsed)
                .Take(limit)
                .ToList();

            var recommendations = recent.Select(r =>
            {
                var daysSinceLastUse = (DateTime.UtcNow - r.LastUsed).TotalDays;
                var recencyScore = Math.Max(0, 100 - daysSinceLastUse * 10);

                return new WorkflowRecommendationDto
                {
                    WorkflowId = r.WorkflowId,
                    WorkflowName = r.WorkflowName,
                    UsageCount = r.UseCount,
                    MatchScore = recencyScore * 0.6,
                    MatchReason = "最近使用",
                    Tags = new List<string> { "最近" }
                };
            }).ToList();

            return recommendations;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("获取基于最近使用的推荐失败: {0}", ex.Message);
            return new List<WorkflowRecommendationDto>();
        }
    }

    private List<WorkflowRecommendationDto> GetContextBasedRecommendations(string context, int limit)
    {
        try
        {
            var contextKeywords = ExtractKeywords(context);
            if (contextKeywords.Count == 0)
                return new List<WorkflowRecommendationDto>();

            var allWorkflows = WorkflowUsageRecordEntity.FindAll()
                .GroupBy(r => new { r.WorkflowId, r.WorkflowName })
                .Select(g => new
                {
                    g.Key.WorkflowId,
                    g.Key.WorkflowName,
                    UseCount = g.Count()
                })
                .ToList();

            var recommendations = new List<WorkflowRecommendationDto>();

            foreach (var wf in allWorkflows)
            {
                var nameKeywords = ExtractKeywords(wf.WorkflowName);
                var matchCount = contextKeywords.Count(k =>
                    nameKeywords.Any(nk => nk.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                           k.Contains(nk, StringComparison.OrdinalIgnoreCase)));

                if (matchCount > 0)
                {
                    var similarityScore = (double)matchCount / Math.Max(contextKeywords.Count, 1) * 100;

                    recommendations.Add(new WorkflowRecommendationDto
                    {
                        WorkflowId = wf.WorkflowId,
                        WorkflowName = wf.WorkflowName,
                        UsageCount = wf.UseCount,
                        MatchScore = similarityScore * 0.8,
                        MatchReason = "内容相似",
                        Tags = new List<string> { "推荐" }
                    });
                }
            }

            return recommendations
                .OrderByDescending(r => r.MatchScore)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("获取基于上下文的推荐失败: {0}", ex.Message);
            return new List<WorkflowRecommendationDto>();
        }
    }

    public async Task<ContextualRecommendationDto> GetContextualRecommendationsAsync(ContextualRecommendationRequest request)
    {
        try
        {
            XTrace.Log.Debug("获取上下文推荐: currentPage={0}, currentAction={1}", request.CurrentPage, request.CurrentAction);

            var result = new ContextualRecommendationDto();

            var context = $"{request.CurrentPage} {request.CurrentAction} {string.Join(" ", request.RecentTools ?? [])}";

            var workflowRecommendations = await GetRecommendedWorkflowsAsync(null, context, request.Limit);
            result.RecommendedWorkflows = workflowRecommendations;

            try
            {
                if (_scriptStatsProvider == null)
                    throw new InvalidOperationException("脚本库统计提供者未注册");

                var contextKeywords = ExtractKeywords(context);

                var allScripts = await _scriptStatsProvider.GetTopScriptsAsync(50);

                var recommendedScripts = new List<RecommendedScriptItem>();
                foreach (var script in allScripts)
                {
                    var nameKeywords = ExtractKeywords(script.ScriptName);
                    var descKeywords = ExtractKeywords(script.Description);
                    var allScriptKeywords = nameKeywords.Concat(descKeywords).ToList();

                    var matchCount = contextKeywords.Count(k =>
                        allScriptKeywords.Any(sk => sk.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                                     k.Contains(sk, StringComparison.OrdinalIgnoreCase)));

                    if (matchCount > 0 || script.UsageCount > 0)
                    {
                        var score = matchCount > 0
                            ? (double)matchCount / Math.Max(contextKeywords.Count, 1) * 100
                            : Math.Min(script.UsageCount * 5.0, 30.0);

                        recommendedScripts.Add(new RecommendedScriptItem
                        {
                            ScriptId = script.ScriptId,
                            ScriptName = script.ScriptName,
                            Language = script.Language,
                            MatchScore = score,
                            MatchReason = matchCount > 0 ? "内容相关" : "常用脚本",
                            UsageCount = script.UsageCount
                        });
                    }
                }

                result.RecommendedScripts = recommendedScripts
                    .OrderByDescending(s => s.MatchScore)
                    .Take(request.Limit)
                    .ToList();

                var allSnippets = await _scriptStatsProvider.GetTopCodeSnippetsAsync(50);

                var recommendedSnippets = new List<RecommendedSnippetItem>();
                foreach (var snippet in allSnippets)
                {
                    var titleKeywords = ExtractKeywords(snippet.Title);
                    var descKeywords = ExtractKeywords(snippet.Description);
                    var allSnippetKeywords = titleKeywords.Concat(descKeywords).ToList();

                    var matchCount = contextKeywords.Count(k =>
                        allSnippetKeywords.Any(sk => sk.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                                     k.Contains(sk, StringComparison.OrdinalIgnoreCase)));

                    if (matchCount > 0 || snippet.UsageCount > 0)
                    {
                        var score = matchCount > 0
                            ? (double)matchCount / Math.Max(contextKeywords.Count, 1) * 100
                            : Math.Min(snippet.UsageCount * 5.0, 30.0);

                        recommendedSnippets.Add(new RecommendedSnippetItem
                        {
                            SnippetId = snippet.SnippetId,
                            Title = snippet.Title,
                            Language = snippet.Language,
                            MatchScore = score,
                            MatchReason = matchCount > 0 ? "内容相关" : "常用片段",
                            UsageCount = snippet.UsageCount
                        });
                    }
                }

                result.RecommendedSnippets = recommendedSnippets
                    .OrderByDescending(s => s.MatchScore)
                    .Take(request.Limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取脚本/片段推荐失败: {0}", ex.Message);
            }

            try
            {
                var startDate = DateTime.UtcNow.AddDays(-30);
                var exp = UsageRecordEntity._.Timestamp >= startDate;
                var records = UsageRecordEntity.FindAll(exp);

                var topTools = records
                    .GroupBy(r => new { r.PluginId, r.ToolId })
                    .Select(g => new RecommendedToolItem
                    {
                        PluginId = g.Key.PluginId,
                        ToolId = g.Key.ToolId,
                        ToolName = g.Key.ToolId,
                        UsageCount = g.Count(),
                        MatchScore = Math.Min(g.Count() * 5.0, 50.0),
                        MatchReason = "常用工具"
                    })
                    .OrderByDescending(t => t.UsageCount)
                    .Take(request.Limit)
                    .ToList();

                result.RecommendedTools = topTools;
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取工具推荐失败: {0}", ex.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取上下文推荐失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<SaveAsSuggestionDto> GetSaveAsSuggestionAsync(SaveAsSuggestionRequest request)
    {
        try
        {
            XTrace.Log.Debug("获取保存建议: actionType={0}, usageFrequency={1}", request.ActionType, request.UsageFrequency);

            var suggestion = new SaveAsSuggestionDto
            {
                ShouldSave = false,
                SuggestedType = "none",
                Reason = string.Empty,
                Confidence = 0
            };

            if (string.IsNullOrWhiteSpace(request.ActionType))
            {
                return Task.FromResult(suggestion);
            }

            var shouldSave = false;
            var confidence = 0.0;
            var suggestedType = "script";
            var reason = string.Empty;

            if (request.UsageFrequency >= 3)
            {
                shouldSave = true;
                confidence = Math.Min(0.5 + request.UsageFrequency * 0.1, 0.95);
                reason = $"该操作已使用 {request.UsageFrequency} 次，建议保存以提高效率";
            }

            if (!string.IsNullOrWhiteSpace(request.Content) && request.Content.Length > 50)
            {
                shouldSave = true;
                confidence = Math.Max(confidence, 0.7);
                reason = string.IsNullOrWhiteSpace(reason)
                    ? "代码内容较长，建议保存为代码片段"
                    : reason + "；代码内容较长";
            }

            if (!string.IsNullOrWhiteSpace(request.Language))
            {
                suggestedType = "codeSnippet";
            }

            suggestion.ShouldSave = shouldSave;
            suggestion.SuggestedType = suggestedType;
            suggestion.Reason = reason;
            suggestion.Confidence = confidence;
            suggestion.SuggestedTitle = !string.IsNullOrWhiteSpace(request.Content)
                ? request.Content.Length > 30 ? request.Content.Substring(0, 30) + "..." : request.Content
                : null;

            if (shouldSave)
            {
                var keywords = ExtractKeywords(request.ActionType + " " + request.Content);
                suggestion.SuggestedTags = keywords.Take(5).ToList();
            }

            return Task.FromResult(suggestion);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取保存建议失败: {0}", ex.Message);
            throw;
        }
    }

    private static List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "的", "了", "和", "是", "就", "都", "而", "及", "与", "着",
            "或", "一个", "没有", "我们", "你们", "他们", "它们", "这个", "那个",
            "这些", "那些", "什么", "怎么", "如何", "为什么", "可以", "需要",
            "the", "a", "an", "and", "or", "but", "is", "are", "was", "were",
            "be", "been", "being", "have", "has", "had", "do", "does", "did",
            "will", "would", "could", "should", "may", "might", "must", "shall",
            "to", "of", "in", "for", "on", "with", "at", "by", "from", "as",
            "into", "through", "during", "before", "after", "above", "below",
            "between", "out", "off", "over", "under", "again", "further",
            "then", "once", "here", "there", "when", "where", "why", "how",
            "all", "each", "every", "both", "few", "more", "most", "other",
            "some", "such", "no", "nor", "not", "only", "own", "same", "so",
            "than", "too", "very", "just", "because", "if", "while"
        };

        var words = text.Split(
            new[] { ' ', ',', '.', '!', '?', ';', ':', '-', '_', '/', '\\', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2)
            .Where(w => !stopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return words;
    }
}
