using System.Text.RegularExpressions;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>
/// 脱敏（docs/04 §5）：SQL 中疑似密钥的字符串字面量整段替换为 <c>'****'</c>；
/// 自由文本（错误、详情、连接串）中 password=/pwd=/Bearer/token 等替换为 <c>****</c>。
/// 参数值一律不落库（审计只记参数名）。
/// </summary>
public static class SqlRedactor
{
    public const String Mask = "****";

    private static readonly RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    /// <summary>键值对形式的密钥：password=xxx; pwd: xxx; api_key=xxx …（值到 ; 空白 引号 & 结束）</summary>
    private static readonly Regex KeyValueRx = new(
        @"\b(password|passwd|pwd|secret|client_secret|token|access_token|refresh_token|api[_-]?key|apikey|auth)\s*([=:])\s*(""[^""]*""|'[^']*'|[^;\s'""&,)]+)", Opt);

    private static readonly Regex BearerRx = new(@"\b(Bearer|Basic)\s+[A-Za-z0-9\-._~+/=]+", Opt);

    /// <summary>MySQL/PG：IDENTIFIED BY 'x' / PASSWORD 'x' / WITH PASSWORD = 'x'</summary>
    private static readonly Regex IdentifiedByRx = new(@"\b(IDENTIFIED\s+(WITH\s+\w+\s+)?BY|PASSWORD)\s*(=\s*)?'(?:[^']|'')*'", Opt);

    /// <summary>URI 中的口令：scheme://user:pass@host</summary>
    private static readonly Regex UriUserInfoRx = new(@"(\w+://[^:/\s'""]+):([^@\s'""]+)@", Opt);

    /// <summary>字符串字面量</summary>
    private static readonly Regex LiteralRx = new(@"'(?:[^']|'')*'", Opt);

    /// <summary>疑似密钥的特征（字面量内命中即整段掩码）</summary>
    private static readonly Regex SecretHintRx = new(
        @"(password|passwd|pwd|secret|token|api[_-]?key|bearer\s|data source=|server=|user id=|uid=|://[^\s]*:[^\s]*@|^sk-[A-Za-z0-9]{8,}|^eyJ[A-Za-z0-9_-]{10,})", Opt);

    /// <summary>脱敏 SQL 文本</summary>
    public static String RedactSql(String? sql)
    {
        if (String.IsNullOrEmpty(sql)) return sql ?? "";
        var s = IdentifiedByRx.Replace(sql, m => m.Groups[1].Value + " " + (m.Groups[3].Success ? "= " : "") + "'" + Mask + "'");
        s = LiteralRx.Replace(s, m =>
        {
            var inner = m.Value[1..^1];
            return SecretHintRx.IsMatch(inner) ? "'" + Mask + "'" : m.Value;
        });
        return RedactText(s);
    }

    /// <summary>脱敏自由文本（错误信息、连接串、详情）</summary>
    public static String RedactText(String? text)
    {
        if (String.IsNullOrEmpty(text)) return text ?? "";
        var s = KeyValueRx.Replace(text, m => m.Groups[1].Value + m.Groups[2].Value + Mask);
        s = BearerRx.Replace(s, m => m.Groups[1].Value + " " + Mask);
        s = UriUserInfoRx.Replace(s, m => m.Groups[1].Value + ":" + Mask + "@");
        return s;
    }

    /// <summary>参数摘要：只保留参数名，值一律掩码</summary>
    public static String DescribeParams(IDictionary<String, Object?>? ps)
    {
        if (ps == null || ps.Count == 0) return "";
        return "params: " + String.Join(", ", ps.Keys.Select(k => k + "=" + Mask));
    }
}
