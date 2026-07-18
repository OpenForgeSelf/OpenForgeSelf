using System.Text;
using System.Text.RegularExpressions;
using OpenForgeSelf.Backend.Plugins.TextTools.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.TextTools.Services;

public class TextStatsService : ITextStatsService
{
    public Task<TextStatsResult> GetStatsAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("计算文本统计信息");

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
            XTrace.Log.Error("文本统计失败: {0}", ex.Message);
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
