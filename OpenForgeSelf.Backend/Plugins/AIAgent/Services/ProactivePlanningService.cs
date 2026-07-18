using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.AIAgent.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Services;

public interface IProactivePlanningService
{
    Task<List<SuggestionEntity>> GenerateSuggestionsAsync(string userId, int limit = 10);
    Task<List<UsagePatternEntity>> AnalyzePatternsAsync(string userId, int days = 30);
    Task<UserProfileSummary> GetUserProfileAsync(string userId);
    Task RecordUsageEventAsync(UsageEventEntity eventEntity);
    Task<List<SuggestionEntity>> GetPendingSuggestionsAsync(string userId, int limit = 20);
    Task MarkSuggestionActionedAsync(long suggestionId);
    Task MarkSuggestionDismissedAsync(long suggestionId);
    Task<List<UserSkillEntity>> GetUserSkillsAsync(string userId);
    Task<List<UserPreferenceEntity>> GetUserPreferencesAsync(string userId);
}

public class ProactivePlanningService : IProactivePlanningService
{
    private readonly List<UsageEventEntity> _events = new();
    private readonly List<UsagePatternEntity> _patterns = new();
    private readonly List<SuggestionEntity> _suggestions = new();
    private readonly List<UserPreferenceEntity> _preferences = new();
    private readonly List<UserSkillEntity> _skills = new();

    public ProactivePlanningService()
    {
    }

    public async Task<List<SuggestionEntity>> GenerateSuggestionsAsync(string userId, int limit = 10)
    {
        try
        {
            XTrace.Log.Info("[ProactivePlanning] 为用户 {0} 生成建议", userId);

            var suggestions = new List<SuggestionEntity>();
            var patterns = await AnalyzePatternsAsync(userId, 30);
            var profile = await GetUserProfileAsync(userId);

            suggestions.AddRange(GenerateToolSuggestions(userId, profile, patterns));
            suggestions.AddRange(GenerateWorkflowSuggestions(userId, profile, patterns));
            suggestions.AddRange(GenerateMemorySuggestions(userId, profile, patterns));
            suggestions.AddRange(GenerateAgentSuggestions(userId, profile, patterns));
            suggestions.AddRange(GenerateOptimizationTips(userId, profile, patterns));

            var scored = suggestions
                .OrderByDescending(s => s.RelevanceScore)
                .ThenByDescending(s => s.Priority)
                .Take(limit)
                .ToList();

            foreach (var s in scored)
            {
                if (s.Id == 0)
                {
                    s.Id = DateTime.Now.Ticks + new Random().Next(1000, 9999);
                    _suggestions.Add(s);
                }
            }

            XTrace.Log.Info("[ProactivePlanning] 生成了 {0} 条建议", scored.Count);
            return scored;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProactivePlanning] 生成建议失败: {0}", ex.Message);
            return new List<SuggestionEntity>();
        }
    }

    public Task<List<UsagePatternEntity>> AnalyzePatternsAsync(string userId, int days = 30)
    {
        try
        {
            XTrace.Log.Debug("[ProactivePlanning] 分析用户 {0} 的使用模式（{1}天）", userId, days);

            var userEvents = _events
                .Where(e => e.UserId == userId && e.CreatedAt >= DateTime.Now.AddDays(-days))
                .ToList();

            var patterns = new List<UsagePatternEntity>();

            if (userEvents.Count == 0)
            {
                patterns.AddRange(GetDefaultPatterns(userId));
                return Task.FromResult(patterns);
            }

            patterns.Add(AnalyzeTemporalPattern(userId, userEvents));
            patterns.Add(AnalyzeFrequencyPattern(userId, userEvents));
            patterns.Add(AnalyzeSequentialPattern(userId, userEvents));
            patterns.Add(AnalyzeBehavioralPattern(userId, userEvents));

            foreach (var pattern in patterns)
            {
                var existing = _patterns.FirstOrDefault(p =>
                    p.UserId == userId && p.PatternName == pattern.PatternName);
                if (existing != null)
                {
                    existing.Confidence = pattern.Confidence;
                    existing.OccurrenceCount++;
                    existing.LastObservedAt = DateTime.Now;
                }
                else
                {
                    pattern.Id = DateTime.Now.Ticks + new Random().Next(1000, 9999);
                    _patterns.Add(pattern);
                }
            }

            return Task.FromResult(patterns);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProactivePlanning] 模式分析失败: {0}", ex.Message);
            return Task.FromResult(new List<UsagePatternEntity>());
        }
    }

    public Task<UserProfileSummary> GetUserProfileAsync(string userId)
    {
        try
        {
            var userEvents = _events.Where(e => e.UserId == userId).ToList();
            var userSkills = _skills.Where(s => s.UserId == userId).ToList();
            var userPreferences = _preferences.Where(p => p.UserId == userId).ToList();

            var profile = new UserProfileSummary
            {
                UserId = userId,
                TotalUsageDays = userEvents.Select(e => e.CreatedAt.Date).Distinct().Count(),
                TotalActions = userEvents.Count,
                TopTools = userEvents
                    .Where(e => e.EventType == UsageEventType.ToolCall)
                    .GroupBy(e => e.EventName)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList(),
                TopCategories = userEvents
                    .Where(e => !string.IsNullOrEmpty(e.Category))
                    .GroupBy(e => e.Category!)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList(),
                Skills = userSkills
                    .OrderByDescending(s => s.ProficiencyLevel)
                    .ToList(),
                Preferences = userPreferences
                    .OrderByDescending(p => p.Confidence)
                    .Select(p => $"{p.PreferenceKey}: {p.PreferenceValue}")
                    .ToList(),
                PrimaryUseTime = AnalyzePrimaryUseTime(userEvents),
                UsageStyle = AnalyzeUsageStyle(userEvents),
                EfficiencyScore = CalculateEfficiencyScore(userEvents),
                LearningRate = CalculateLearningRate(userEvents, userSkills)
            };

            if (profile.TotalUsageDays == 0)
            {
                profile.UsageStyle = "探索型";
                profile.PrimaryUseTime = "不固定";
            }

            return Task.FromResult(profile);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProactivePlanning] 获取用户画像失败: {0}", ex.Message);
            return Task.FromResult(new UserProfileSummary { UserId = userId });
        }
    }

    public Task RecordUsageEventAsync(UsageEventEntity eventEntity)
    {
        try
        {
            eventEntity.Id = DateTime.Now.Ticks + new Random().Next(1000, 9999);
            eventEntity.CreatedAt = DateTime.Now;
            _events.Add(eventEntity);

            UpdateSkillsFromEvent(eventEntity);
            UpdatePreferencesFromEvent(eventEntity);

            XTrace.Log.Debug("[ProactivePlanning] 记录使用事件: {0} - {1}",
                eventEntity.EventType, eventEntity.EventName);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProactivePlanning] 记录使用事件失败: {0}", ex.Message);
        }

        return Task.CompletedTask;
    }

    public Task<List<SuggestionEntity>> GetPendingSuggestionsAsync(string userId, int limit = 20)
    {
        var pending = _suggestions
            .Where(s => s.UserId == userId && !s.IsDismissed && !s.IsActioned && s.ExpiresAt > DateTime.Now)
            .OrderByDescending(s => s.Priority)
            .ThenByDescending(s => s.RelevanceScore)
            .Take(limit)
            .ToList();

        return Task.FromResult(pending);
    }

    public Task MarkSuggestionActionedAsync(long suggestionId)
    {
        var suggestion = _suggestions.FirstOrDefault(s => s.Id == suggestionId);
        if (suggestion != null)
        {
            suggestion.IsActioned = true;
            suggestion.ActionedAt = DateTime.Now;
            XTrace.Log.Info("[ProactivePlanning] 建议已采纳: {0}", suggestion.Title);
        }
        return Task.CompletedTask;
    }

    public Task MarkSuggestionDismissedAsync(long suggestionId)
    {
        var suggestion = _suggestions.FirstOrDefault(s => s.Id == suggestionId);
        if (suggestion != null)
        {
            suggestion.IsDismissed = true;
            suggestion.DismissedAt = DateTime.Now;
            XTrace.Log.Info("[ProactivePlanning] 建议已忽略: {0}", suggestion.Title);
        }
        return Task.CompletedTask;
    }

    public Task<List<UserSkillEntity>> GetUserSkillsAsync(string userId)
    {
        var skills = _skills
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.ProficiencyLevel)
            .ToList();
        return Task.FromResult(skills);
    }

    public Task<List<UserPreferenceEntity>> GetUserPreferencesAsync(string userId)
    {
        var prefs = _preferences
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Confidence)
            .ToList();
        return Task.FromResult(prefs);
    }

    private static List<UsagePatternEntity> GetDefaultPatterns(string userId)
    {
        return new List<UsagePatternEntity>
        {
            new()
            {
                UserId = userId,
                PatternType = PatternType.Behavioral,
                PatternName = "新用户探索模式",
                PatternDescription = "用户处于探索阶段，建议引导了解核心功能",
                Confidence = 0.5,
                OccurrenceCount = 0,
                IsActive = true
            }
        };
    }

    private static UsagePatternEntity AnalyzeTemporalPattern(string userId, List<UsageEventEntity> events)
    {
        var hourlyCounts = events
            .GroupBy(e => e.CreatedAt.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        var peakHour = hourlyCounts.OrderByDescending(kv => kv.Value).FirstOrDefault();
        var isMorning = hourlyCounts.Where(kv => kv.Key >= 6 && kv.Key < 12).Sum(kv => kv.Value);
        var isAfternoon = hourlyCounts.Where(kv => kv.Key >= 12 && kv.Key < 18).Sum(kv => kv.Value);
        var isEvening = hourlyCounts.Where(kv => kv.Key >= 18 || kv.Key < 6).Sum(kv => kv.Value);

        string patternName;
        string description;
        double confidence;

        var total = events.Count;
        if (total == 0)
        {
            patternName = "使用时间不固定";
            description = "用户使用时间分布较为分散";
            confidence = 0.3;
        }
        else if ((double)isMorning / total > 0.5)
        {
            patternName = "晨间使用偏好";
            description = "用户主要在上午使用系统，适合安排规划类任务";
            confidence = 0.7;
        }
        else if ((double)isAfternoon / total > 0.5)
        {
            patternName = "下午使用偏好";
            description = "用户主要在下午使用系统，适合安排深度工作";
            confidence = 0.7;
        }
        else if ((double)isEvening / total > 0.5)
        {
            patternName = "晚间使用偏好";
            description = "用户主要在晚上使用系统，适合安排学习和探索";
            confidence = 0.7;
        }
        else
        {
            patternName = "全天候使用";
            description = "用户在多个时段都有使用，时间分布均匀";
            confidence = 0.5;
        }

        return new UsagePatternEntity
        {
            UserId = userId,
            PatternType = PatternType.Temporal,
            PatternName = patternName,
            PatternDescription = description,
            Confidence = confidence,
            OccurrenceCount = events.Count,
            PatternData = JsonSerializer.Serialize(new { peakHour = peakHour.Key, peakCount = peakHour.Value })
        };
    }

    private static UsagePatternEntity AnalyzeFrequencyPattern(string userId, List<UsageEventEntity> events)
    {
        var dayCounts = events
            .GroupBy(e => e.CreatedAt.Date)
            .Count();

        string patternName;
        string description;
        double confidence;

        if (dayCounts <= 1)
        {
            patternName = "低频使用";
            description = "用户使用频率较低，建议增加功能发现引导";
            confidence = 0.8;
        }
        else if (dayCounts <= 5)
        {
            patternName = "中频使用";
            description = "用户每周使用几次，属于正常使用范围";
            confidence = 0.6;
        }
        else
        {
            patternName = "高频使用";
            description = "用户每天都在使用，是活跃用户";
            confidence = 0.8;
        }

        return new UsagePatternEntity
        {
            UserId = userId,
            PatternType = PatternType.Frequency,
            PatternName = patternName,
            PatternDescription = description,
            Confidence = confidence,
            OccurrenceCount = dayCounts,
            PatternData = JsonSerializer.Serialize(new { activeDays = dayCounts, totalEvents = events.Count })
        };
    }

    private static UsagePatternEntity AnalyzeSequentialPattern(string userId, List<UsageEventEntity> events)
    {
        var toolCalls = events
            .Where(e => e.EventType == UsageEventType.ToolCall)
            .OrderBy(e => e.CreatedAt)
            .ToList();

        string patternName;
        string description;
        double confidence;

        if (toolCalls.Count < 3)
        {
            patternName = "工具探索阶段";
            description = "用户还在探索各种工具的使用";
            confidence = 0.5;
        }
        else
        {
            patternName = "多工具组合使用";
            description = "用户倾向于组合使用多种工具完成任务";
            confidence = 0.6;
        }

        return new UsagePatternEntity
        {
            UserId = userId,
            PatternType = PatternType.Sequential,
            PatternName = patternName,
            PatternDescription = description,
            Confidence = confidence,
            OccurrenceCount = Math.Max(1, toolCalls.Count / 3)
        };
    }

    private static UsagePatternEntity AnalyzeBehavioralPattern(string userId, List<UsageEventEntity> events)
    {
        var toolRatio = (double)events.Count(e => e.EventType == UsageEventType.ToolCall) / Math.Max(1, events.Count);
        var chatRatio = (double)events.Count(e => e.EventType == UsageEventType.Chat) / Math.Max(1, events.Count);
        var workflowRatio = (double)events.Count(e => e.EventType == UsageEventType.WorkflowRun) / Math.Max(1, events.Count);

        string patternName;
        string description;
        double confidence;

        if (workflowRatio > 0.3)
        {
            patternName = "自动化驱动型";
            description = "用户偏好使用工作流自动化处理任务，效率导向";
            confidence = 0.7;
        }
        else if (toolRatio > 0.5)
        {
            patternName = "工具导向型";
            description = "用户主要使用工具函数完成具体任务，务实高效";
            confidence = 0.7;
        }
        else if (chatRatio > 0.5)
        {
            patternName = "对话互动型";
            description = "用户偏好通过对话方式交互，喜欢探索和学习";
            confidence = 0.7;
        }
        else
        {
            patternName = "均衡使用型";
            description = "用户在对话、工具、工作流之间均衡使用";
            confidence = 0.5;
        }

        return new UsagePatternEntity
        {
            UserId = userId,
            PatternType = PatternType.Behavioral,
            PatternName = patternName,
            PatternDescription = description,
            Confidence = confidence,
            OccurrenceCount = events.Count,
            PatternData = JsonSerializer.Serialize(new { toolRatio, chatRatio, workflowRatio })
        };
    }

    private static string AnalyzePrimaryUseTime(List<UsageEventEntity> events)
    {
        if (events.Count == 0) return "不固定";

        var hourlyCounts = events
            .GroupBy(e => e.CreatedAt.Hour)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        var hour = hourlyCounts.Key;
        if (hour >= 6 && hour < 12) return "上午";
        if (hour >= 12 && hour < 18) return "下午";
        return "晚间";
    }

    private static string AnalyzeUsageStyle(List<UsageEventEntity> events)
    {
        if (events.Count < 10) return "探索型";

        var toolCalls = events.Count(e => e.EventType == UsageEventType.ToolCall);
        var workflows = events.Count(e => e.EventType == UsageEventType.WorkflowRun);

        if (workflows > 5) return "自动化专家";
        if (toolCalls > 20) return "工具达人";
        if (toolCalls > 10) return "进阶用户";
        return "成长型";
    }

    private static double CalculateEfficiencyScore(List<UsageEventEntity> events)
    {
        if (events.Count == 0) return 0.5;

        var successRate = (double)events.Count(e => e.Success) / events.Count;
        var avgDuration = events.Average(e => e.DurationMs);
        var durationScore = Math.Max(0, Math.Min(1, 1 - avgDuration / 30000));

        return 0.6 * successRate + 0.4 * durationScore;
    }

    private static double CalculateLearningRate(List<UsageEventEntity> events, List<UserSkillEntity> skills)
    {
        if (events.Count < 5) return 0.5;

        var recentEvents = events.Where(e => e.CreatedAt >= DateTime.Now.AddDays(-7)).ToList();
        var earlierEvents = events.Where(e => e.CreatedAt < DateTime.Now.AddDays(-7)).ToList();

        if (earlierEvents.Count == 0) return 0.7;

        var recentSuccess = (double)recentEvents.Count(e => e.Success) / Math.Max(1, recentEvents.Count);
        var earlierSuccess = (double)earlierEvents.Count(e => e.Success) / Math.Max(1, earlierEvents.Count);

        var improvement = recentSuccess - earlierSuccess;
        return Math.Clamp(0.5 + improvement, 0.1, 0.95);
    }

    private static List<SuggestionEntity> GenerateToolSuggestions(
        string userId,
        UserProfileSummary profile,
        List<UsagePatternEntity> patterns)
    {
        var suggestions = new List<SuggestionEntity>();

        if (profile.UsageStyle == "探索型" || profile.TotalActions < 10)
        {
            suggestions.Add(new SuggestionEntity
            {
                UserId = userId,
                Type = SuggestionType.ToolRecommendation,
                Title = "发现更多工具",
                Description = "探索 OpenForgeSelf 内置的强大工具集，提升工作效率",
                Content = "OpenForgeSelf 提供了丰富的工具函数，包括文件处理、文本处理、开发工具等。\n\n尝试在对话中描述你的需求，AI 会自动选择合适的工具帮助你完成任务。",
                ActionUrl = "/plugins",
                Priority = SuggestionPriority.High,
                RelevanceScore = 0.8,
                ExpiresAt = DateTime.Now.AddDays(7)
            });
        }

        if (profile.TopTools.Count > 0 && profile.TopTools.Count < 3)
        {
            suggestions.Add(new SuggestionEntity
            {
                UserId = userId,
                Type = SuggestionType.OptimizationTip,
                Title = "尝试组合使用工具",
                Description = "将多个工具组合使用可以完成更复杂的任务",
                Content = "你已经开始使用一些工具了。试试让 AI 帮你组合使用多个工具，完成更复杂的工作流。",
                Priority = SuggestionPriority.Medium,
                RelevanceScore = 0.6,
                ExpiresAt = DateTime.Now.AddDays(7)
            });
        }

        return suggestions;
    }

    private static List<SuggestionEntity> GenerateWorkflowSuggestions(
        string userId,
        UserProfileSummary profile,
        List<UsagePatternEntity> patterns)
    {
        var suggestions = new List<SuggestionEntity>();

        if (profile.UsageStyle == "工具达人" || profile.TopTools.Count >= 3)
        {
            suggestions.Add(new SuggestionEntity
            {
                UserId = userId,
                Type = SuggestionType.WorkflowRecommendation,
                Title = "试试工作流自动化",
                Description = "将重复任务保存为工作流，一键执行更高效",
                Content = "你经常使用多个工具，可以将常用的任务组合保存为工作流。\n\n工作流支持：\n- 可视化编排步骤\n- 变量和参数传递\n- 条件分支和循环\n- 定时执行和触发器",
                ActionUrl = "/workflows",
                Priority = SuggestionPriority.High,
                RelevanceScore = 0.75,
                ExpiresAt = DateTime.Now.AddDays(14)
            });
        }

        return suggestions;
    }

    private static List<SuggestionEntity> GenerateMemorySuggestions(
        string userId,
        UserProfileSummary profile,
        List<UsagePatternEntity> patterns)
    {
        var suggestions = new List<SuggestionEntity>();

        var memoryEvents = patterns.Count(p => p.PatternType == PatternType.Frequency && p.OccurrenceCount > 5);
        if (memoryEvents > 0 || profile.TotalActions > 20)
        {
            suggestions.Add(new SuggestionEntity
            {
                UserId = userId,
                Type = SuggestionType.MemoryReminder,
                Title = "让 AI 记住你的偏好",
                Description = "将重要信息保存为记忆，AI 会在对话中主动使用它们",
                Content = "记忆系统可以帮助 AI 更好地理解你：\n\n- 事实记忆：保存重要信息\n- 偏好记忆：记住你的习惯和喜好\n- 项目记忆：保存项目相关的上下文\n- 技能记忆：记录你掌握的技能\n\n在对话中随时告诉 AI \"记住这个...\" 即可。",
                ActionUrl = "/memory",
                Priority = SuggestionPriority.Medium,
                RelevanceScore = 0.65,
                ExpiresAt = DateTime.Now.AddDays(14)
            });
        }

        return suggestions;
    }

    private static List<SuggestionEntity> GenerateAgentSuggestions(
        string userId,
        UserProfileSummary profile,
        List<UsagePatternEntity> patterns)
    {
        var suggestions = new List<SuggestionEntity>();

        if (profile.TotalActions > 30)
        {
            suggestions.Add(new SuggestionEntity
            {
                UserId = userId,
                Type = SuggestionType.AgentRecommendation,
                Title = "体验多 Agent 协作",
                Description = "复杂任务可以让多个专业 Agent 协同完成",
                Content = "OpenForgeSelf 的多 Agent 协作系统：\n\n- 协调者：任务分解和分配\n- 研究员：信息收集和分析\n- 写作者：内容创作和润色\n- 程序员：代码开发和调试\n- 分析师：数据处理和洞察\n- 评论家：质量评估和建议\n\n让多个 AI 专家为你工作，效率倍增！",
                ActionUrl = "/agents",
                Priority = SuggestionPriority.High,
                RelevanceScore = 0.7,
                ExpiresAt = DateTime.Now.AddDays(14)
            });
        }

        return suggestions;
    }

    private static List<SuggestionEntity> GenerateOptimizationTips(
        string userId,
        UserProfileSummary profile,
        List<UsagePatternEntity> patterns)
    {
        var suggestions = new List<SuggestionEntity>();

        var temporalPattern = patterns.FirstOrDefault(p => p.PatternType == PatternType.Temporal);
        if (temporalPattern != null && temporalPattern.PatternName.Contains("晚间"))
        {
            suggestions.Add(new SuggestionEntity
            {
                UserId = userId,
                Type = SuggestionType.OptimizationTip,
                Title = "晚间高效利用建议",
                Description = "晚上是学习和探索的好时机",
                Content = "你主要在晚间使用系统。建议：\n\n1. 利用安静的时间进行深度思考\n2. 尝试学习新功能和工具\n3. 规划第二天的任务\n4. 整理和回顾当天的成果",
                Priority = SuggestionPriority.Low,
                RelevanceScore = 0.5,
                ExpiresAt = DateTime.Now.AddDays(7)
            });
        }

        if (profile.EfficiencyScore < 0.5 && profile.TotalActions > 10)
        {
            suggestions.Add(new SuggestionEntity
            {
                UserId = userId,
                Type = SuggestionType.OptimizationTip,
                Title = "提升使用效率的小技巧",
                Description = "几个小技巧可以显著提升你的使用效率",
                Content = "效率提升建议：\n\n1. 尽量明确描述你的需求\n2. 利用工作流自动化重复任务\n3. 使用记忆系统保存常用信息\n4. 尝试多 Agent 协作处理复杂任务",
                Priority = SuggestionPriority.Medium,
                RelevanceScore = 0.55,
                ExpiresAt = DateTime.Now.AddDays(7)
            });
        }

        return suggestions;
    }

    private void UpdateSkillsFromEvent(UsageEventEntity evt)
    {
        if (evt.EventType != UsageEventType.ToolCall &&
            evt.EventType != UsageEventType.WorkflowRun) return;

        var skillName = evt.EventName;
        var skill = _skills.FirstOrDefault(s => s.UserId == evt.UserId && s.SkillName == skillName);

        if (skill == null)
        {
            skill = new UserSkillEntity
            {
                Id = DateTime.Now.Ticks + new Random().Next(1000, 9999),
                UserId = evt.UserId,
                SkillName = skillName,
                SkillCategory = evt.Category,
                ProficiencyLevel = 0.1,
                UsageCount = 1,
                SuccessRate = evt.Success ? 1.0 : 0.0,
                AverageDurationMs = evt.DurationMs,
                FirstUsedAt = DateTime.Now,
                LastUsedAt = DateTime.Now
            };
            _skills.Add(skill);
        }
        else
        {
            skill.UsageCount++;
            skill.LastUsedAt = DateTime.Now;
            skill.SuccessRate = (skill.SuccessRate * (skill.UsageCount - 1) + (evt.Success ? 1.0 : 0.0)) / skill.UsageCount;
            skill.AverageDurationMs = (skill.AverageDurationMs * (skill.UsageCount - 1) + evt.DurationMs) / skill.UsageCount;

            if (skill.UsageCount < 5) skill.ProficiencyLevel = 0.1 + skill.UsageCount * 0.05;
            else if (skill.UsageCount < 20) skill.ProficiencyLevel = 0.3 + skill.UsageCount * 0.02;
            else if (skill.UsageCount < 50) skill.ProficiencyLevel = 0.6 + skill.UsageCount * 0.01;
            else skill.ProficiencyLevel = Math.Min(0.95, 0.85 + skill.SuccessRate * 0.1);
        }
    }

    private void UpdatePreferencesFromEvent(UsageEventEntity evt)
    {
        if (string.IsNullOrEmpty(evt.Category)) return;

        var key = $"偏好_{evt.EventType}类别";
        var pref = _preferences.FirstOrDefault(p => p.UserId == evt.UserId && p.PreferenceKey == key);

        if (pref == null)
        {
            pref = new UserPreferenceEntity
            {
                Id = DateTime.Now.Ticks + new Random().Next(1000, 9999),
                UserId = evt.UserId,
                PreferenceKey = key,
                PreferenceValue = evt.Category,
                Category = "行为偏好",
                Confidence = 0.5,
                UpdateCount = 1,
                Source = "使用分析"
            };
            _preferences.Add(pref);
        }
        else
        {
            pref.UpdateCount++;
            pref.Confidence = Math.Min(0.95, 0.5 + pref.UpdateCount * 0.05);
            pref.UpdatedAt = DateTime.Now;
        }
    }
}
