using System.Text;
using System.Text.RegularExpressions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

/// <summary>文本统计结果（2026-10-28 自 text-tools 并入，用户拍板 text-tools 全部合并到 dev-tools）。</summary>
public class TextStatsResult
{
    public int CharCount { get; set; }
    public int CharCountNoSpaces { get; set; }
    public int WordCount { get; set; }
    public int LineCount { get; set; }
    public int ByteCount { get; set; }
}

public interface ITextStatsService
{
    Task<TextStatsResult> GetStatsAsync(string text);
}

/// <summary>
/// 文本统计服务（2026-10-28 自 text-tools 原样迁入，行为不变；pilot 2026-10-28-plugin-design-boundary D-V1）。
/// </summary>
public class TextStatsService : ITextStatsService
{
    public Task<TextStatsResult> GetStatsAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 计算文本统计信息");

            if (string.IsNullOrEmpty(text))
            {
                return Task.FromResult(new TextStatsResult
                {
                    CharCount = 0,
                    CharCountNoSpaces = 0,
                    WordCount = 0,
                    LineCount = 0,
                    ByteCount = 0
                });
            }

            var charCount = text.Length;
            var charCountNoSpaces = text.Count(c => !char.IsWhiteSpace(c));
            var lineCount = text.Count(c => c == '\n') + 1;
            var byteCount = Encoding.UTF8.GetByteCount(text);
            var wordCount = CountWords(text);

            var result = new TextStatsResult
            {
                CharCount = charCount,
                CharCountNoSpaces = charCountNoSpaces,
                WordCount = wordCount,
                LineCount = lineCount,
                ByteCount = byteCount
            };

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 文本统计失败: {0}", ex.Message);
            throw new ArgumentException("文本统计失败: " + ex.Message, nameof(text), ex);
        }
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var englishWords = Regex.Matches(text, @"[a-zA-Z]+").Count;
        var chineseChars = Regex.Matches(text, @"[\u4e00-\u9fa5]").Count;
        var numbers = Regex.Matches(text, @"\d+").Count;

        return englishWords + chineseChars + numbers;
    }
}
