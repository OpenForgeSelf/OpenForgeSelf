using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.DevTools.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public class RegexService : IRegexService
{
    private static readonly List<RegexPatternItem> _commonPatterns = new()
    {
        new RegexPatternItem { Name = "邮箱地址", Pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", Description = "匹配标准邮箱地址格式", Category = "常用验证", Example = "user@example.com" },
        new RegexPatternItem { Name = "手机号码", Pattern = @"^1[3-9]\d{9}$", Description = "匹配中国大陆手机号码", Category = "常用验证", Example = "13812345678" },
        new RegexPatternItem { Name = "身份证号", Pattern = @"^[1-9]\d{5}(19|20)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])\d{3}[\dXx]$", Description = "匹配18位身份证号码", Category = "常用验证", Example = "110101199003077890" },
        new RegexPatternItem { Name = "URL地址", Pattern = @"^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$", Description = "匹配HTTP/HTTPS URL", Category = "常用验证", Example = "https://www.example.com/path?query=1" },
        new RegexPatternItem { Name = "IPv4地址", Pattern = @"^((25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(25[0-5]|2[0-4]\d|[01]?\d\d?)$", Description = "匹配IPv4地址格式", Category = "网络", Example = "192.168.1.1" },
        new RegexPatternItem { Name = "IPv6地址", Pattern = @"^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$", Description = "匹配标准IPv6地址", Category = "网络", Example = "2001:0db8:85a3:0000:0000:8a2e:0370:7334" },
        new RegexPatternItem { Name = "日期 YYYY-MM-DD", Pattern = @"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$", Description = "匹配ISO日期格式", Category = "日期时间", Example = "2024-01-15" },
        new RegexPatternItem { Name = "日期 YYYY/MM/DD", Pattern = @"^\d{4}\/(0[1-9]|1[0-2])\/(0[1-9]|[12]\d|3[01])$", Description = "匹配斜杠日期格式", Category = "日期时间", Example = "2024/01/15" },
        new RegexPatternItem { Name = "时间 HH:mm:ss", Pattern = @"^([01]\d|2[0-3]):([0-5]\d):([0-5]\d)$", Description = "匹配24小时制时间", Category = "日期时间", Example = "14:30:00" },
        new RegexPatternItem { Name = "中文字符", Pattern = @"^[\u4e00-\u9fa5]+$", Description = "匹配纯中文字符", Category = "文本", Example = "你好世界" },
        new RegexPatternItem { Name = "整数", Pattern = @"^-?\d+$", Description = "匹配正负整数", Category = "数字", Example = "-123" },
        new RegexPatternItem { Name = "正整数", Pattern = @"^[1-9]\d*$", Description = "匹配正整数（不含0）", Category = "数字", Example = "123" },
        new RegexPatternItem { Name = "小数", Pattern = @"^-?\d+\.\d+$", Description = "匹配小数", Category = "数字", Example = "3.14" },
        new RegexPatternItem { Name = "正数", Pattern = @"^\d+(\.\d+)?$", Description = "匹配正数（整数或小数）", Category = "数字", Example = "123.45" },
        new RegexPatternItem { Name = "用户名", Pattern = @"^[a-zA-Z][a-zA-Z0-9_]{4,15}$", Description = "字母开头，5-16位字母数字下划线", Category = "账户", Example = "user_123" },
        new RegexPatternItem { Name = "强密码", Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", Description = "至少8位，含大小写字母、数字、特殊字符", Category = "账户", Example = "Password@123" },
        new RegexPatternItem { Name = "邮政编码", Pattern = @"^[1-9]\d{5}$", Description = "匹配中国邮政编码", Category = "常用验证", Example = "100000" },
        new RegexPatternItem { Name = "HTML标签", Pattern = @"<[^>]+>", Description = "匹配HTML标签", Category = "文本", Example = "<div class=\"test\">" },
        new RegexPatternItem { Name = "HEX颜色值", Pattern = @"^#?([a-fA-F0-9]{6}|[a-fA-F0-9]{3})$", Description = "匹配HEX颜色值", Category = "颜色", Example = "#FF5733" },
        new RegexPatternItem { Name = "RGB颜色值", Pattern = @"^rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$", Description = "匹配RGB颜色格式", Category = "颜色", Example = "rgb(255, 87, 51)" },
    };

    public Task<RegexMatchResult> TestMatchAsync(string pattern, string input, bool ignoreCase = false, bool multiline = false, bool singleline = false, bool ignorePatternWhitespace = false, bool rightToLeft = false)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 正则表达式测试匹配");

            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("正则表达式不能为空", nameof(pattern));

            var options = BuildRegexOptions(ignoreCase, multiline, singleline, ignorePatternWhitespace, rightToLeft);
            var regex = new Regex(pattern, options);
            var matches = regex.Matches(input ?? string.Empty);

            var result = new RegexMatchResult
            {
                Success = true,
                MatchCount = matches.Count,
                CaptureGroupCount = regex.GetGroupNumbers().Length - 1,
                Matches = matches.Select(m => new RegexMatchItem
                {
                    Index = m.Index,
                    Length = m.Length,
                    Value = m.Value,
                    Groups = m.Groups.Cast<Group>().Skip(1).Select((g, i) => new RegexGroupItem
                    {
                        Name = regex.GroupNameFromNumber(i + 1),
                        Value = g.Value,
                        Index = g.Index,
                        Length = g.Length,
                        Success = g.Success
                    }).ToList()
                }).ToList()
            };

            return Task.FromResult(result);
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 正则表达式测试失败: {0}", ex.Message);
            return Task.FromResult(new RegexMatchResult { Success = false, Error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 正则表达式测试异常: {0}", ex.Message);
            return Task.FromResult(new RegexMatchResult { Success = false, Error = ex.Message });
        }
    }

    public Task<RegexMatchResult> GetMatchGroupsAsync(string pattern, string input, bool ignoreCase = false, bool multiline = false, bool singleline = false, bool ignorePatternWhitespace = false, bool rightToLeft = false)
    {
        return TestMatchAsync(pattern, input, ignoreCase, multiline, singleline, ignorePatternWhitespace, rightToLeft);
    }

    public Task<RegexReplaceResult> ReplaceAsync(string pattern, string input, string replacement, bool ignoreCase = false, bool multiline = false, bool singleline = false, bool ignorePatternWhitespace = false, bool rightToLeft = false)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 正则表达式替换");

            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("正则表达式不能为空", nameof(pattern));

            var options = BuildRegexOptions(ignoreCase, multiline, singleline, ignorePatternWhitespace, rightToLeft);
            var regex = new Regex(pattern, options);
            var matches = regex.Matches(input ?? string.Empty);
            var result = regex.Replace(input ?? string.Empty, replacement ?? string.Empty);

            return Task.FromResult(new RegexReplaceResult
            {
                Result = result,
                ReplacementCount = matches.Count
            });
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 正则表达式替换失败: {0}", ex.Message);
            return Task.FromResult(new RegexReplaceResult { Result = input, Error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 正则表达式替换异常: {0}", ex.Message);
            return Task.FromResult(new RegexReplaceResult { Result = input, Error = ex.Message });
        }
    }

    public Task<RegexSplitResult> SplitAsync(string pattern, string input, bool ignoreCase = false, bool multiline = false, bool singleline = false, bool ignorePatternWhitespace = false, bool rightToLeft = false)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 正则表达式分割");

            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("正则表达式不能为空", nameof(pattern));

            var options = BuildRegexOptions(ignoreCase, multiline, singleline, ignorePatternWhitespace, rightToLeft);
            var regex = new Regex(pattern, options);
            var parts = regex.Split(input ?? string.Empty).ToList();

            return Task.FromResult(new RegexSplitResult { Parts = parts });
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("[DevTools] 正则表达式分割失败: {0}", ex.Message);
            return Task.FromResult(new RegexSplitResult { Parts = new List<string> { input ?? string.Empty }, Error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 正则表达式分割异常: {0}", ex.Message);
            return Task.FromResult(new RegexSplitResult { Parts = new List<string> { input ?? string.Empty }, Error = ex.Message });
        }
    }

    public Task<string> GenerateRegexAsync(string description, string language = "zh")
    {
        try
        {
            XTrace.Log.Debug("[DevTools] AI生成正则表达式: {0}", description);

            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("描述不能为空", nameof(description));

            var pattern = GenerateSimpleRegex(description, language);

            return Task.FromResult(pattern);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 生成正则表达式失败: {0}", ex.Message);
            throw new ArgumentException("生成正则表达式失败: " + ex.Message, nameof(description), ex);
        }
    }

    public Task<List<RegexPatternItem>> GetCommonPatternsAsync(string? category = null)
    {
        XTrace.Log.Debug("[DevTools] 获取常用正则模板，分类: {0}", category ?? "全部");

        var patterns = string.IsNullOrEmpty(category)
            ? _commonPatterns.ToList()
            : _commonPatterns.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult(patterns);
    }

    private static RegexOptions BuildRegexOptions(bool ignoreCase, bool multiline, bool singleline, bool ignorePatternWhitespace, bool rightToLeft)
    {
        var options = RegexOptions.None;
        if (ignoreCase) options |= RegexOptions.IgnoreCase;
        if (multiline) options |= RegexOptions.Multiline;
        if (singleline) options |= RegexOptions.Singleline;
        if (ignorePatternWhitespace) options |= RegexOptions.IgnorePatternWhitespace;
        if (rightToLeft) options |= RegexOptions.RightToLeft;
        return options;
    }

    private static string GenerateSimpleRegex(string description, string language)
    {
        var desc = description.Trim().ToLowerInvariant();

        var keywordMap = new Dictionary<string, string>
        {
            { "邮箱", @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$" },
            { "email", @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$" },
            { "手机", @"^1[3-9]\d{9}$" },
            { "phone", @"^1[3-9]\d{9}$" },
            { "身份证", @"^[1-9]\d{5}(19|20)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])\d{3}[\dXx]$" },
            { "url", @"^https?:\/\/[\w\-]+(\.[\w\-]+)+([\w\-\.,@?^=%&:/~\+#]*[\w\-\@?^=%&/~\+#])?$" },
            { "网址", @"^https?:\/\/[\w\-]+(\.[\w\-]+)+([\w\-\.,@?^=%&:/~\+#]*[\w\-\@?^=%&/~\+#])?$" },
            { "ip", @"^((25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(25[0-5]|2[0-4]\d|[01]?\d\d?)$" },
            { "日期", @"^\d{4}-\d{2}-\d{2}$" },
            { "date", @"^\d{4}-\d{2}-\d{2}$" },
            { "中文", @"^[\u4e00-\u9fa5]+$" },
            { "数字", @"^\d+$" },
            { "number", @"^\d+$" },
            { "整数", @"^-?\d+$" },
            { "小数", @"^-?\d+\.\d+$" },
            { "用户名", @"^[a-zA-Z][a-zA-Z0-9_]{4,15}$" },
            { "密码", @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d@$!%*?&]{8,}$" },
            { "password", @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d@$!%*?&]{8,}$" },
            { "邮编", @"^[1-9]\d{5}$" },
            { "颜色", @"^#?([a-fA-F0-9]{6}|[a-fA-F0-9]{3})$" },
            { "color", @"^#?([a-fA-F0-9]{6}|[a-fA-F0-9]{3})$" },
        };

        foreach (var (keyword, pattern) in keywordMap)
        {
            if (desc.Contains(keyword))
                return pattern;
        }

        return language == "zh"
            ? "/* 未能根据描述生成正则，请使用更具体的关键词，如：邮箱、手机、URL、IP、日期、中文、数字等 */"
            : "/* Could not generate regex from description. Try more specific keywords like: email, phone, url, ip, date, number, etc. */";
    }
}
