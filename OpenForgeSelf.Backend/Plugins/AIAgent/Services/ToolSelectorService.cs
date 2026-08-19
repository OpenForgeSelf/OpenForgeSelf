using System.Text.RegularExpressions;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Services;

public class ToolSelectorService : IToolSelectorService
{
    private readonly IToolRegistry _toolRegistry;

    private static readonly Dictionary<string, List<string>> CategoryKeywords = new()
    {
        ["file"] = new() { "文件", "file", "folder", "目录", "文件夹", "重命名", "rename", "压缩", "compress", "zip", "清理", "cleanup", "统计", "stats", "信息", "info" },
        ["system"] = new() { "系统", "system", "cpu", "内存", "memory", "磁盘", "disk", "网络", "network", "进程", "process", "监控", "monitor", "性能", "performance" },
        ["text"] = new() { "文本", "text", "编码", "encode", "解码", "decode", "哈希", "hash", "加密", "encrypt", "格式化", "format", "统计", "count" },
        ["time"] = new() { "时间", "time", "日期", "date", "当前", "now", "时钟", "clock" },
        ["math"] = new() { "计算", "calculate", "数学", "math", "加减乘除", "运算", "expression" },
        ["workflow"] = new() { "工作流", "workflow", "流程", "执行", "execute", "计划", "plan", "调度", "schedule" }
    };

    public ToolSelectorService(IContext ctx)
    {
        // 宿主契约（IToolRegistry）经 Cordis 上下文在运行期获取，而非 MS DI 构造注入。
        _toolRegistry = ctx.Get<IToolRegistry>() ?? throw new InvalidOperationException("宿主未提供 IToolRegistry 契约，无法初始化工具选择器");
    }

    public List<IToolFunctionExtension> SelectTools(string taskDescription)
    {
        var ranked = RankTools(taskDescription);
        return ranked.Where(r => r.MatchScore > 0.2).Select(r => r.Tool).ToList();
    }

    public ToolMatchResult CalculateMatchScore(IToolFunctionExtension tool, string taskDescription)
    {
        var result = new ToolMatchResult
        {
            Tool = tool,
            MatchScore = 0,
            MatchedKeywords = new List<string>()
        };

        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            return result;
        }

        var taskLower = taskDescription.ToLower();
        var toolName = tool.Name.ToLower();
        var toolDesc = (tool.Description ?? string.Empty).ToLower();

        if (taskLower.Contains(toolName))
        {
            result.MatchScore += 0.5;
            result.MatchedKeywords.Add(tool.Name);
        }

        foreach (var keyword in ExtractKeywords(toolDesc))
        {
            if (taskLower.Contains(keyword))
            {
                result.MatchScore += 0.15;
                result.MatchedKeywords.Add(keyword);
            }
        }

        foreach (var category in CategoryKeywords)
        {
            var categoryMatched = false;
            foreach (var keyword in category.Value)
            {
                if (taskLower.Contains(keyword) && (toolName.Contains(keyword) || toolDesc.Contains(keyword)))
                {
                    if (!categoryMatched)
                    {
                        result.MatchScore += 0.2;
                        categoryMatched = true;
                    }
                    result.MatchedKeywords.Add(keyword);
                }
            }
        }

        result.MatchScore = Math.Min(result.MatchScore, 1.0);

        return result;
    }

    public List<ToolMatchResult> RankTools(string taskDescription)
    {
        var allTools = _toolRegistry.GetAllTools().ToList();
        var results = new List<ToolMatchResult>();

        foreach (var tool in allTools)
        {
            var matchResult = CalculateMatchScore(tool, taskDescription);
            results.Add(matchResult);
        }

        return results.OrderByDescending(r => r.MatchScore).ToList();
    }

    private static List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var words = Regex.Matches(text, @"[\w\u4e00-\u9fa5]+")
            .Cast<Match>()
            .Select(m => m.Value.ToLower())
            .Where(w => w.Length >= 2)
            .Distinct()
            .ToList();

        return words;
    }
}
