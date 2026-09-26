using ForgeSelf.Api.Plugins.DevTools.Models;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public interface IRegexService
{
    Task<RegexMatchResult> TestMatchAsync(string pattern, string input, bool ignoreCase = false, bool multiline = false, bool singleline = false, bool ignorePatternWhitespace = false, bool rightToLeft = false);
    Task<RegexMatchResult> GetMatchGroupsAsync(string pattern, string input, bool ignoreCase = false, bool multiline = false, bool singleline = false, bool ignorePatternWhitespace = false, bool rightToLeft = false);
    Task<RegexReplaceResult> ReplaceAsync(string pattern, string input, string replacement, bool ignoreCase = false, bool multiline = false, bool singleline = false, bool ignorePatternWhitespace = false, bool rightToLeft = false);
    Task<RegexSplitResult> SplitAsync(string pattern, string input, bool ignoreCase = false, bool multiline = false, bool singleline = false, bool ignorePatternWhitespace = false, bool rightToLeft = false);
    Task<string> GenerateRegexAsync(string description, string language = "zh");
    Task<List<RegexPatternItem>> GetCommonPatternsAsync(string? category = null);
}
