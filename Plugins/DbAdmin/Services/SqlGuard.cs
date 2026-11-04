using System.Text;
using System.Text.RegularExpressions;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>SQL 类别（docs/04 §3）</summary>
public enum SqlCategory
{
    /// <summary>只读：SELECT / WITH…SELECT / 只读 PRAGMA / EXPLAIN / SHOW / DESCRIBE</summary>
    Read,
    /// <summary>普通写：INSERT / 带 WHERE 的 UPDATE·DELETE / CREATE / ALTER ADD 及无法确定类别（fail-closed）</summary>
    Write,
    /// <summary>破坏性：DROP TABLE/VIEW/INDEX / TRUNCATE / ALTER … DROP</summary>
    Destructive,
    /// <summary>硬拒：任何模式均拒绝</summary>
    Rejected,
}

/// <summary>分类结果</summary>
public record SqlClassification(SqlCategory Category, String Reason)
{
    public Boolean IsRead => Category == SqlCategory.Read;
    public Boolean IsRejected => Category == SqlCategory.Rejected;

    /// <summary>审计/历史用 kind 文本</summary>
    public String Kind => Category switch
    {
        SqlCategory.Read => "read",
        SqlCategory.Write => "write",
        SqlCategory.Destructive => "destructive",
        _ => "rejected",
    };
}

/// <summary>
/// SQL 分类与硬拒（纯函数、无状态、线程安全）。
/// 先剥注释与字符串/引号标识符，再判定；无法确定的一律按「普通写」（fail-closed）。
/// 「系统只读连接上的非只读语句」由 <see cref="DbQueryService"/> 结合连接属性另行硬拒。
/// </summary>
public static class SqlGuard
{
    private static readonly RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    /// <summary>越界到 shell / 文件 / 外部数据源 / 扩展加载的模式（剥离后的文本上匹配）</summary>
    private static readonly (Regex Pattern, String Reason)[] HardRejectPatterns =
    [
        (new(@"\bXP_\w+", Opt), "禁止调用 xp_* 扩展存储过程（如 xp_cmdshell）"),
        (new(@"\bSP_OA\w*", Opt), "禁止 OLE Automation 存储过程（sp_OA*）"),
        (new(@"\bSP_CONFIGURE\b", Opt), "禁止修改服务器配置（sp_configure）"),
        (new(@"\bSP_EXECUTE_EXTERNAL_SCRIPT\b", Opt), "禁止外部脚本执行"),
        (new(@"\bOPENROWSET\b|\bOPENDATASOURCE\b|\bOPENQUERY\b", Opt), "禁止访问外部数据源（OPENROWSET/OPENDATASOURCE/OPENQUERY）"),
        (new(@"\bBULK\s+INSERT\b", Opt), "禁止 BULK INSERT 读取文件"),
        (new(@"\bLOAD_FILE\s*\(", Opt), "禁止 LOAD_FILE 读取文件"),
        (new(@"\bLOAD\s+DATA\b", Opt), "禁止 LOAD DATA 读取文件"),
        (new(@"\bINTO\s+(OUTFILE|DUMPFILE)\b", Opt), "禁止 INTO OUTFILE/DUMPFILE 写文件"),
        (new(@"\bCOPY\b[\s\S]*\bPROGRAM\b", Opt), "禁止 COPY … PROGRAM 执行外部命令"),
        (new(@"\bCOPY\b[\s\S]*\b(FROM|TO)\s+(''|STDIN|STDOUT)", Opt), "禁止 COPY 读写服务器文件"),
        (new(@"\bPG_(READ_FILE|READ_BINARY_FILE|WRITE_FILE|LS_DIR|STAT_FILE|EXECUTE_SERVER_PROGRAM)\b", Opt), "禁止 PostgreSQL 服务器文件函数"),
        (new(@"\bLO_(IMPORT|EXPORT)\b", Opt), "禁止大对象文件导入导出（lo_import/lo_export）"),
        (new(@"\bLOAD_EXTENSION\b", Opt), "禁止加载 SQLite 扩展"),
        (new(@"\bVACUUM\b[\s\S]*\bINTO\b", Opt), "禁止 VACUUM INTO 写出文件"),
        (new(@"\bATTACH\b", Opt), "禁止 ATTACH 挂载其他数据库"),
        (new(@"\bDETACH\b", Opt), "禁止 DETACH 数据库"),
        (new(@"\bDROP\s+(DATABASE|SCHEMA)\b", Opt), "禁止 DROP DATABASE / DROP SCHEMA"),
        (new(@"\bSHUTDOWN\b", Opt), "禁止 SHUTDOWN"),
        (new(@"\bCREATE\s+(OR\s+REPLACE\s+)?(FUNCTION|PROCEDURE)\b[\s\S]*\bLANGUAGE\s+(C|PLPYTHONU?|PLPERLU|PLSH)\b", Opt), "禁止创建不受信语言函数"),
    ];

    private static readonly Regex WhereRx = new(@"\bWHERE\b", Opt);
    private static readonly Regex DmlInsideRx = new(@"\b(INSERT|UPDATE|DELETE|MERGE|REPLACE|UPSERT)\b", Opt);
    private static readonly Regex SelectIntoRx = new(@"\bINTO\b", Opt);
    private static readonly Regex AlterDropRx = new(@"\bDROP\b", Opt);

    /// <summary>对一段 SQL 分类</summary>
    public static SqlClassification Classify(String? sql)
    {
        if (String.IsNullOrWhiteSpace(sql)) return new(SqlCategory.Rejected, "SQL 为空");

        String stripped;
        try
        {
            stripped = Strip(sql);
        }
        catch (FormatException ex)
        {
            return new(SqlCategory.Rejected, ex.Message);
        }

        // 多语句：按 ; 切分（字符串与注释已剥），去掉末尾空语句后 >1 条即硬拒
        var statements = SplitStatements(stripped).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (statements.Count == 0) return new(SqlCategory.Rejected, "SQL 为空（仅含注释或分号）");
        if (statements.Count > 1) return new(SqlCategory.Rejected, $"禁止一次执行多条语句（检测到 {statements.Count} 条）");

        var text = Regex.Replace(statements[0], @"\s+", " ").Trim();

        foreach (var (pattern, reason) in HardRejectPatterns)
        {
            if (pattern.IsMatch(text)) return new(SqlCategory.Rejected, reason);
        }

        return ClassifyStatement(text);
    }

    private static SqlClassification ClassifyStatement(String text)
    {
        var first = FirstWord(text);
        switch (first)
        {
            case "SELECT":
                // SELECT … INTO 新表 = 写
                if (SelectIntoRx.IsMatch(text)) return new(SqlCategory.Write, "SELECT INTO 会创建/写入表");
                return new(SqlCategory.Read, "SELECT");
            case "VALUES":
            case "SHOW":
            case "DESCRIBE":
            case "DESC":
                return new(SqlCategory.Read, first);
            case "WITH":
                if (DmlInsideRx.IsMatch(text))
                {
                    // 主语句及每个 CTE / 子查询里的 UPDATE/DELETE 都要在「本层」有 WHERE；子层 WHERE 不算
                    if (HasUnboundedDml(text))
                        return new(SqlCategory.Rejected, "禁止无 WHERE 的 UPDATE / DELETE（只认本层 WHERE，子查询/CTE 内的不算）");
                    return new(SqlCategory.Write, "WITH 内含写操作");
                }
                if (SelectIntoRx.IsMatch(text)) return new(SqlCategory.Write, "WITH … SELECT INTO 会写入表");
                return new(SqlCategory.Read, "WITH … SELECT");
            case "PRAGMA":
                // PRAGMA x = y / PRAGMA x(y) 中带赋值视为写；table_info(...) 等元数据为读
                if (text.Contains('=')) return new(SqlCategory.Write, "PRAGMA 赋值会修改数据库设置");
                if (Regex.IsMatch(text, @"^PRAGMA\s+(\w+\.)?(TABLE_INFO|TABLE_XINFO|TABLE_LIST|INDEX_LIST|INDEX_INFO|INDEX_XINFO|FOREIGN_KEY_LIST|DATABASE_LIST|COLLATION_LIST|FUNCTION_LIST|PRAGMA_LIST|COMPILE_OPTIONS|USER_VERSION|SCHEMA_VERSION|PAGE_SIZE|PAGE_COUNT|ENCODING|JOURNAL_MODE|FOREIGN_KEYS|INTEGRITY_CHECK|QUICK_CHECK)\b", RegexOptions.IgnoreCase))
                    return new(SqlCategory.Read, "PRAGMA 元数据");
                return new(SqlCategory.Write, "未知 PRAGMA 按写处理（fail-closed）");
            case "EXPLAIN":
                {
                    // EXPLAIN [ANALYZE] [QUERY PLAN] <stmt>：按内层语句分类（EXPLAIN ANALYZE 会真实执行）
                    var inner = Regex.Replace(text, @"^EXPLAIN(\s+(ANALYZE|ANALYSE|VERBOSE|QUERY\s+PLAN|FORMAT\s*=?\s*\w+|EXTENDED))*\s*", "", RegexOptions.IgnoreCase);
                    if (inner.Length == 0) return new(SqlCategory.Write, "无法识别的 EXPLAIN（fail-closed）");
                    var c = ClassifyStatement(inner);
                    return c with { Reason = "EXPLAIN " + c.Reason };
                }
            case "INSERT":
            case "REPLACE":
            case "MERGE":
            case "UPSERT":
                return new(SqlCategory.Write, first);
            case "UPDATE":
            case "DELETE":
                if (!WhereRx.IsMatch(OwnLevel(text))) return new(SqlCategory.Rejected, $"禁止无 WHERE 的 {first}（只认顶层 WHERE，子查询内的不算）");
                if (HasUnboundedDml(text)) return new(SqlCategory.Rejected, "禁止无 WHERE 的 UPDATE / DELETE");
                return new(SqlCategory.Write, first + " … WHERE");
            case "CREATE":
                return new(SqlCategory.Write, "CREATE");
            case "ALTER":
                if (AlterDropRx.IsMatch(text)) return new(SqlCategory.Destructive, "ALTER … DROP");
                return new(SqlCategory.Write, "ALTER");
            case "DROP":
                return new(SqlCategory.Destructive, "DROP");
            case "TRUNCATE":
                return new(SqlCategory.Destructive, "TRUNCATE");
            default:
                return new(SqlCategory.Write, $"无法确定类别（{(first.Length == 0 ? "?" : first)}），按写操作询问（fail-closed）");
        }
    }

    /// <summary>DML 关键字但不是 ON DELETE / ON UPDATE / FOR UPDATE / OF 之类修饰</summary>
    private static readonly Regex DmlVerbRx = new(@"(?<!\b(ON|FOR|OF|BEFORE|AFTER)\s)\b(UPDATE|DELETE)\b", Opt);

    /// <summary>
    /// 按括号层级检查：顶层与每个括号组的「本层文本」（子括号内容替换为 ()）里若出现 UPDATE/DELETE 动词，
    /// 则同一层必须出现 WHERE。子查询 / CTE 里的 WHERE 不会为外层兜底。
    /// </summary>
    internal static Boolean HasUnboundedDml(String text)
    {
        foreach (var seg in Levels(text))
        {
            if (DmlVerbRx.IsMatch(seg) && !WhereRx.IsMatch(seg)) return true;
        }
        return false;
    }

    /// <summary>仅保留深度 0 的字符；括号内内容折叠为 ()</summary>
    internal static String OwnLevel(String text)
    {
        var sb = new StringBuilder(text.Length);
        var depth = 0;
        foreach (var c in text)
        {
            if (c == '(') { if (depth++ == 0) sb.Append('('); continue; }
            if (c == ')') { if (depth > 0 && --depth == 0) sb.Append(')'); continue; }
            if (depth == 0) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>枚举顶层及所有括号组各自的本层文本</summary>
    private static IEnumerable<String> Levels(String text)
    {
        yield return OwnLevel(text);
        var stack = new Stack<Int32>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(') stack.Push(i);
            else if (text[i] == ')' && stack.Count > 0)
            {
                var open = stack.Pop();
                yield return OwnLevel(text.Substring(open + 1, i - open - 1));
            }
        }
    }

    private static readonly Regex WordOrSemiRx = new(@"[A-Za-z_][A-Za-z0-9_$]*|;", Opt);

    /// <summary>
    /// 按 ; 切分语句（字符串/注释已剥）。仅当语句以 CREATE … TRIGGER 开头时，把 BEGIN…END 视为块，
    /// 块内分号不切分；CASE…END 单独配对，不当块。其它语句（含 BEGIN TRANSACTION）照常按 ; 切分。
    /// </summary>
    internal static List<String> SplitStatements(String stripped)
    {
        var result = new List<String>();
        var start = 0;
        var blockDepth = 0;
        var caseDepth = 0;
        var trigger = false;
        var isCreate = false;
        var wordIndex = 0;
        foreach (Match m in WordOrSemiRx.Matches(stripped))
        {
            var tok = m.Value;
            if (tok == ";")
            {
                if (trigger && blockDepth > 0) continue;
                result.Add(stripped[start..m.Index]);
                start = m.Index + 1;
                blockDepth = caseDepth = wordIndex = 0;
                trigger = isCreate = false;
                continue;
            }
            var w = tok.ToUpperInvariant();
            // CREATE [TEMP|TEMPORARY] TRIGGER [IF NOT EXISTS] …：TRIGGER 须出现在前几个词内
            if (wordIndex == 0) isCreate = w == "CREATE";
            else if (isCreate && !trigger && wordIndex <= 2 && w == "TRIGGER") trigger = true;
            wordIndex++;
            if (!trigger) continue;
            switch (w)
            {
                case "CASE": caseDepth++; break;
                case "BEGIN": blockDepth++; break;
                case "END":
                    if (caseDepth > 0) caseDepth--;
                    else if (blockDepth > 0) blockDepth--;
                    break;
            }
        }
        result.Add(stripped[start..]);
        return result;
    }

    private static String FirstWord(String text)
    {
        var t = text.TrimStart('(', ' ');
        var m = Regex.Match(t, @"^[A-Za-z_]+");
        return m.Success ? m.Value.ToUpperInvariant() : "";
    }

    /// <summary>
    /// 剥离注释（-- / # 行尾不处理以免误伤 SQL Server 临时表；/* */ 块注释）与
    /// 字符串字面量（'…'，支持 '' 转义）、引号标识符（"…"、`…`、[…]），替换为 <c>''</c> / <c>_q_</c> 占位。
    /// 未闭合的字符串/注释抛 <see cref="FormatException"/>（按硬拒处理，避免绕过）。
    /// </summary>
    public static String Strip(String sql)
    {
        var sb = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            var n = i + 1 < sql.Length ? sql[i + 1] : '\0';
            if (c == '-' && n == '-')
            {
                while (i < sql.Length && sql[i] != '\n') i++;
                sb.Append(' ');
                continue;
            }
            if (c == '/' && n == '*')
            {
                var end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) throw new FormatException("块注释未闭合");
                i = end + 2;
                sb.Append(' ');
                continue;
            }
            if (c == '\'' || ((c == 'E' || c == 'e' || c == 'N' || c == 'n') && n == '\'' && (i == 0 || !Char.IsLetterOrDigit(sql[i - 1]))))
            {
                if (c != '\'') i++;
                i = SkipQuoted(sql, i, '\'', '\'', allowBackslash: c == 'E' || c == 'e');
                sb.Append("''");
                continue;
            }
            if (c == '$' && TryDollarTag(sql, i, out var tag))
            {
                var end = sql.IndexOf(tag, i + tag.Length, StringComparison.Ordinal);
                if (end < 0) throw new FormatException("美元引号字符串未闭合");
                i = end + tag.Length;
                sb.Append("''");
                continue;
            }
            if (c == '"' || c == '`')
            {
                i = SkipQuoted(sql, i, c, c, allowBackslash: false);
                sb.Append("_q_");
                continue;
            }
            if (c == '[')
            {
                i = SkipQuoted(sql, i, '[', ']', allowBackslash: false);
                sb.Append("_q_");
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>从 start（开引号位置）跳过引号段，返回闭引号之后的位置；双写闭引号视为转义</summary>
    private static Int32 SkipQuoted(String s, Int32 start, Char open, Char close, Boolean allowBackslash)
    {
        var i = start + 1;
        while (i < s.Length)
        {
            if (allowBackslash && s[i] == '\\') { i += 2; continue; }
            if (s[i] == close)
            {
                if (i + 1 < s.Length && s[i + 1] == close) { i += 2; continue; }
                return i + 1;
            }
            i++;
        }
        throw new FormatException($"引号 {open} 未闭合");
    }

    private static Boolean TryDollarTag(String s, Int32 i, out String tag)
    {
        tag = "";
        if (i > 0 && (Char.IsLetterOrDigit(s[i - 1]) || s[i - 1] == '_')) return false;
        var j = i + 1;
        while (j < s.Length && (Char.IsLetterOrDigit(s[j]) || s[j] == '_')) j++;
        if (j < s.Length && s[j] == '$')
        {
            // $1 之类位置参数不算
            if (j > i + 1 && Char.IsDigit(s[i + 1])) return false;
            tag = s.Substring(i, j - i + 1);
            return true;
        }
        return false;
    }
}
