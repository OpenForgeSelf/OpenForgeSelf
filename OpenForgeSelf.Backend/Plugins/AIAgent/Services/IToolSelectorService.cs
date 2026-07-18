using OpenForgeSelf.Backend.Plugins.Abstractions;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Services;

public interface IToolSelectorService
{
    List<IToolFunctionExtension> SelectTools(string taskDescription);
    ToolMatchResult CalculateMatchScore(IToolFunctionExtension tool, string taskDescription);
    List<ToolMatchResult> RankTools(string taskDescription);
}

public class ToolMatchResult
{
    public IToolFunctionExtension Tool { get; set; } = null!;
    public double MatchScore { get; set; }
    public List<string> MatchedKeywords { get; set; } = new();
}
