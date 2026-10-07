using System.Text.RegularExpressions;

namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>
/// 工具调用文本解析（PILOT-053 02-spec FR-2）：**纯函数、零副作用**——不启动进程、不读写文件（BR-2、AC3）。
/// 四档格式按「逐段扫描、每段独立定档」处理（BC-12），认不出的必须进 unparsed 并带具体原因，
/// 绝不合成调用、绝不做前缀/包含式模糊匹配（BR-1、AC5）。
/// </summary>
public static class CallParser
{
    private const int MaxUnparsedChars = 500;
    private const int MaxFragmentChars = 200;

    private static readonly Regex TagBlock = new(
        @"<(?<tag>tool|invoke|function_call|tool_call|function)\b(?<attrs>[^>]*)>(?<body>[\s\S]*?)</\k<tag>>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ParamTag = new(
        @"<(?:parameter|param|arg)\b\s+name\s*=\s*(?<q>[""'])(?<n>[^""']+)\k<q>[^>]*>(?<v>[\s\S]*?)</(?:parameter|param|arg)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NameAttr = new(
        @"(?:name|tool)\s*=\s*(?<q>[""'])(?<n>[^""']+)\k<q>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnclosedTag = new(
        @"</?(?:tool|invoke|function_call|tool_call|function|parameter|param|arg)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] NameKeys = ["tool", "name", "function", "action"];
    private static readonly string[] ArgsKeys = ["args", "arguments", "parameters", "input"];
    private static readonly string[] CallsKeys = ["calls", "actions", "tool_calls"];

    private static readonly JsonSerializerOptions LooseJson = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static ParseResult Parse(string? text)
    {
        var result = new ParseResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            result.Unparsed.Add(new UnparsedFragment { Fragment = string.Empty, Reason = "输入为空：请把 AI 的回复整段粘贴进来" });
            result.Stats = Stats(result);
            return result;
        }

        var normalized = Normalize(text);
        // 代码围栏按 ``` 切段：偶数段是正文、奇数段是围栏内容（BC-12 的分段基础）。
        var parts = normalized.Split("```", StringSplitOptions.None);
        var offset = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var chunk = parts[i];
            if (i % 2 == 1)
            {
                ParseFence(chunk, offset, result);
            }
            else
            {
                ParseTextRegion(chunk, offset, result);
            }
            offset += chunk.Length + 3;
        }

        result.Stats = Stats(result);
        return result;
    }

    private static ParseStats Stats(ParseResult r) => new()
    {
        Recognized = r.Calls.Count,
        Unknown = r.Unknown.Count,
        Unparsed = r.Unparsed.Count
    };

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('\uFEFF');

    // ---------- 围栏段 ----------

    private static void ParseFence(string chunk, int offset, ParseResult result)
    {
        var body = chunk.Trim('\n');
        // 语言标注行（```json 之后残留的首行单词）剥掉。
        var firstBreak = body.IndexOf('\n');
        var head = firstBreak < 0 ? body : body[..firstBreak];
        if (firstBreak > 0 && head.Length <= 16 && head.All(c => char.IsLetterOrDigit(c) || c is '+' or '-' or '_'))
        {
            body = body[(firstBreak + 1)..];
        }

        if (TryParseJson(body, out var node) && node != null)
        {
            AddFromJson(node, offset, body.Trim(), result);
            return;
        }

        ParseLines(body, offset, result);
    }

    // ---------- 裸 JSON（无围栏）----------

    /// <summary>
    /// 工具目录的特征键：<see cref="PromptBuilder.ToolsAsJson"/> 生成的每一项都带这些键。
    /// 裸 JSON 走"宁可少认"口径——带目录特征的片段不当调用，免得用户把初始指令整段粘回来时
    /// 被凭空造出四条"调用"（违背不猜原则）。
    /// </summary>
    private static readonly string[] CatalogMarkerKeys = ["description", "aliases", "parametersSchema", "schema", "inputSchema"];

    private static string ExtractBareJson(string text, int offset, ParseResult result)
    {
        var spans = new List<(int Start, int End, JsonNode Node)>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is '{' or '[')
            {
                var end = FindBalancedSpanEnd(text, i);
                if (end > i + 1 && TryParseJson(text[i..end], out var node) && node != null && LooksLikeInvocation(node))
                {
                    spans.Add((i, end, node));
                    i = end;
                    continue;
                }
            }
            i++;
        }

        // 从后往前消费，避免前面的替换让后面的下标失效。
        foreach (var (start, end, node) in spans.OrderByDescending(s => s.Start))
        {
            AddFromJson(node, offset + start, text[start..end].Trim(), result);
            text = ReplaceSpan(text, start, end - start, "\n");
        }
        return text;
    }

    /// <summary>
    /// 判定"这段 JSON 是不是调用"：对象有工具名键且不带目录特征键；或数组的每一项都满足；
    /// 或对象是 <c>tool_calls/calls/actions</c> 包装。其余一律不当调用（宁可少认）。
    /// </summary>
    private static bool LooksLikeInvocation(JsonNode node)
    {
        switch (node)
        {
            case JsonArray arr:
                return arr.Count > 0 && arr.All(item => item is JsonObject io && IsInvocationObject(io));
            case JsonObject obj:
                if (CallsKeys.Any(k => obj[k] is JsonArray)) return true;
                return IsInvocationObject(obj);
            default:
                return false;
        }
    }

    private static bool IsInvocationObject(JsonObject obj)
    {
        if (CatalogMarkerKeys.Any(k => obj[k] != null)) return false;
        return NameKeys.Any(k => obj[k] is JsonValue v && v.GetValueKind() == JsonValueKind.String)
            || obj["function"] is JsonObject;
    }

    /// <summary>
    /// 从 <paramref name="start"/>（必须是 { 或 [）起做**字符串感知**的括号配对，返回配对闭合处的下标+1；
    /// 配不上返回 -1（这段就当散文）。字符串内的括号与转义都跳过，未闭合的 JSON 不会被半口吞掉。
    /// </summary>
    private static int FindBalancedSpanEnd(string text, int start)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped) { escaped = false; }
                else if (c == '\\') { escaped = true; }
                else if (c == '"') { inString = false; }
                continue;
            }
            switch (c)
            {
                case '"': inString = true; break;
                case '{' or '[': depth++; break;
                case '}' or ']':
                    depth--;
                    if (depth == 0) return i + 1;
                    if (depth < 0) return -1;
                    break;
            }
        }
        return -1;
    }

    // ---------- 正文段（先抠标签式，再按行） ----------

    private static void ParseTextRegion(string region, int offset, ParseResult result)
    {
        var remaining = region;
        foreach (Match m in TagBlock.Matches(region))
        {
            var body = m.Groups["body"].Value;
            var attrs = m.Groups["attrs"].Value;
            var nameMatch = NameAttr.Match(attrs);
            var rawName = nameMatch.Success ? nameMatch.Groups["n"].Value.Trim() : null;
            var start = offset + m.Index;

            if (string.IsNullOrWhiteSpace(rawName))
            {
                result.Unparsed.Add(Unparsed(body, start, "标签式调用未给出工具名（name= 属性缺失）"));
                remaining = remaining.Remove(m.Index, Math.Min(m.Length, remaining.Length - m.Index))
                    .Insert(m.Index, "\n");
                continue;
            }

            var parameters = ParamTag.Matches(body);
            if (parameters.Count > 0)
            {
                var args = new Dictionary<string, JsonNode>();
                foreach (Match p in parameters)
                {
                    args[p.Groups["n"].Value.Trim()] = JsonValue.Create(Dedent(p.Groups["v"].Value)) ?? JsonValue.Create("");
                }
                AddCall(rawName, args, "tag", start, Trim(m.Value), result);
            }
            else if (TryParseJson(body, out var node) && node is JsonObject obj)
            {
                AddCall(rawName, ToArgDict(obj, rawName), "tag", start, Trim(m.Value), result);
            }
            else if (string.IsNullOrWhiteSpace(body))
            {
                AddCall(rawName, new Dictionary<string, JsonNode>(), "tag", start, Trim(m.Value), result);
            }
            else
            {
                result.Unparsed.Add(Unparsed(body, start, "标签体内的内容既不是 JSON 对象，也不是逐个参数标签"));
            }

            remaining = ReplaceSpan(remaining, m.Index, m.Length, "\n");
        }

        // 裸 JSON（**没有 ``` 围栏**）：网页聊天把代码块渲染出来后再复制，常常只带内容不带围栏。
        // 早先只有围栏内才试 JSON ⇒ 用户实测「同一段带围栏能解析、去掉围栏就报没认出」，
        // 而这正是本插件最常见输入形态（2026-10-06）。只消费"像调用"的片段，其余留给行解析。
        remaining = ExtractBareJson(remaining, offset, result);

        // 未闭合的标签片段必须可见（否则用户看不到"为什么少了一条"）。整行消费，
        // 免得行首的散文残留被再当成第二条 unparsed 记录重复计数。
        var unclosedSpans = new List<(int Start, int End, string Line)>();
        foreach (Match open in UnclosedTag.Matches(remaining))
        {
            var lineStart = remaining.LastIndexOf('\n', Math.Max(0, Math.Min(open.Index, remaining.Length - 1))) + 1;
            var lineEnd = remaining.IndexOf('\n', open.Index);
            if (lineEnd < 0) lineEnd = remaining.Length;
            var line = remaining[lineStart..lineEnd];
            if (line.Trim().Length == 0) continue;
            if (unclosedSpans.Any(s => lineStart < s.End && s.Start < lineEnd)) continue;
            unclosedSpans.Add((lineStart, lineEnd, line));
        }

        foreach (var span in unclosedSpans.OrderByDescending(s => s.Start))
        {
            result.Unparsed.Add(Unparsed(span.Line, offset + span.Start, "标签式调用未闭合或缺少 name 属性"));
            remaining = ReplaceSpan(remaining, span.Start, span.End - span.Start, "\n");
        }

        ParseLines(remaining, offset, result);
    }

    private static string ReplaceSpan(string text, int index, int length, string replacement)
    {
        if (index < 0 || index > text.Length) return text;
        var take = Math.Min(length, text.Length - index);
        return string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + take));
    }

    // ---------- 行式（P3）与箭头式（P4） ----------

    private static void ParseLines(string text, int offset, ParseResult result)
    {
        var block = new List<string>();
        var blockStart = 0;
        var lineStart = offset;

        void Flush()
        {
            if (block.Count == 0) return;
            result.Unparsed.Add(DescribeBlock(string.Join("\n", block), blockStart));
            block.Clear();
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Trim().Length == 0)
            {
                Flush();
                lineStart += rawLine.Length + 1;
                continue;
            }

            var stripped = StripBullet(line);
            var parsed = TryLine(stripped, lineStart, result);
            if (!parsed)
            {
                if (block.Count == 0) blockStart = lineStart;
                block.Add(line.Trim());
            }
            else
            {
                Flush();
            }

            lineStart += rawLine.Length + 1;
        }

        Flush();
    }

    private static string StripBullet(string line)
    {
        var t = line.TrimStart();
        if (t.StartsWith("> ")) t = t[2..].TrimStart();
        else if (t.StartsWith(">")) t = t[1..].TrimStart();
        else if (t.Length > 1 && (t[0] == '-' || t[0] == '*' || t[0] == '+') && t[1] == ' ') t = t[2..].TrimStart();
        else if (t.Length > 2 && char.IsDigit(t[0]))
        {
            var dot = t.IndexOf(". ", StringComparison.Ordinal);
            if (dot is > 0 and < 4 && t[..dot].All(char.IsDigit)) t = t[(dot + 2)..].TrimStart();
        }
        return t;
    }

    private static bool TryLine(string text, int start, ParseResult result)
    {
        var tokenEnd = text.IndexOfAny([' ', '\t', ':', '=']);
        if (tokenEnd <= 0) return false;

        var head = text[..tokenEnd];
        var canonical = ToolSpec.Normalize(head);
        if (canonical == null) return false;

        var rest = text[(tokenEnd + 1)..].TrimStart(':', '=', ' ', '\t');
        var separator = text[tokenEnd];
        var args = new Dictionary<string, JsonNode>();

        if (rest.Length == 0)
        {
            // 光杆工具名：交给 ToolDispatcher 报 missing_argument，让 AI 自己看到缺什么。
        }
        else if (rest[0] is '{' or '[' && TryParseJson(rest, out var node) && node is JsonObject obj)
        {
            args = ToArgDict(obj, canonical);
        }
        else if (rest.Contains('='))
        {
            if (!TryKeyValuePairs(rest, canonical, args)) return false;
        }
        else
        {
            // 单个裸值 → 绑到该工具第一个必填参数（FR-2.2 P4 的固定映射，非模糊推断）。
            var entry = ToolSpec.Find(canonical);
            var first = entry?.Required.FirstOrDefault();
            if (first == null) return false;
            args[first] = JsonValue.Create(rest.Trim('"', '\'')) ?? JsonValue.Create("");
        }

        result.Calls.Add(new ParsedCall
        {
            Tool = canonical,
            RawName = head,
            Args = args,
            Via = separator == ':' ? "arrow" : "kv",
            Start = start,
            Fragment = Trim(text)
        });
        return true;
    }

    /// <summary>行式 key=value（值可加双引号，引号内空白保留；引号不成对即判不可解析并交回上层）。</summary>
    private static bool TryKeyValuePairs(string text, string canonical, Dictionary<string, JsonNode> args)
    {
        var tokens = TokenizeQuoted(text);
        if (tokens == null) return false;

        var entry = ToolSpec.Find(canonical);
        foreach (var token in tokens)
        {
            var eq = token.IndexOf('=');
            if (eq <= 0) return false;
            var key = token[..eq].Trim();
            var value = token[(eq + 1)..].Trim();
            if (key.Contains('"') || !ValueQuotesAreWellFormed(value))
            {
                // 引号配对可疑（如 path="broken content="x）：宁可不认这一行，
                // 也不把两段参数粘成一段假值交给 AI（AC4/BC-1 的"不猜"口径）。
                return false;
            }
            args[MapArgName(key, entry)] = JsonValue.Create(Unquote(value)) ?? JsonValue.Create("");
        }
        return args.Count > 0;
    }

    /// <summary>值里的引号要么没有，要么恰好成对包住整个值；夹在中间的引号判为不可解析。</summary>
    private static bool ValueQuotesAreWellFormed(string value)
    {
        var quotes = value.Count(c => c == '"');
        if (quotes == 0) return true;
        return quotes == 2 && value.Length >= 2 && value[0] == '"' && value[^1] == '"';
    }

    private static List<string>? TokenizeQuoted(string text)
    {
        var list = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuote = false;
        foreach (var ch in text)
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
                sb.Append(ch);
                continue;
            }
            if (!inQuote && (ch == ' ' || ch == '\t'))
            {
                if (sb.Length > 0)
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                }
                continue;
            }
            sb.Append(ch);
        }
        if (inQuote) return null; // 引号不成对 = 无法可靠分词，交回上层进 unparsed
        if (sb.Length > 0) list.Add(sb.ToString());
        return list;
    }

    private static string MapArgName(string key, ToolSpecEntry? entry)
    {
        if (entry == null) return key;
        var squeezed = Squeeze(key);
        foreach (var name in entry.Required)
        {
            if (string.Equals(Squeeze(name), squeezed, StringComparison.Ordinal)) return name;
        }
        var schema = TryParseJson(entry.ParametersSchema, out var node) && node is JsonObject obj ? obj : null;
        if (schema?["properties"] is JsonObject props)
        {
            foreach (var kv in props)
            {
                if (string.Equals(Squeeze(kv.Key), squeezed, StringComparison.Ordinal)) return kv.Key;
            }
        }
        return key;
    }

    private static string Squeeze(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    // ---------- JSON 形状（P1） ----------

    private static void AddFromJson(JsonNode node, int offset, string fragment, ParseResult result)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item != null) AddFromJson(item, offset, Trim(item.ToJsonString()), result);
                }
                return;

            case JsonObject obj:
            {
                var callsKey = CallsKeys.FirstOrDefault(k => obj[k] is JsonArray);
                if (callsKey != null)
                {
                    foreach (var item in (JsonArray)obj[callsKey]!)
                    {
                        if (item is JsonObject itemObj) AddFromCallObject(itemObj, offset, Trim(itemObj.ToJsonString()), result);
                        else result.Unparsed.Add(Unparsed(item?.ToJsonString() ?? string.Empty, offset, "调用项不是 JSON 对象"));
                    }
                    return;
                }

                AddFromCallObject(obj, offset, fragment, result);
                return;
            }

            default:
                result.Unparsed.Add(Unparsed(fragment, offset, "JSON 根节点既不是对象也不是数组，无法判定调用"));
                return;
        }
    }

    private static void AddFromCallObject(JsonObject obj, int offset, string fragment, ParseResult result)
    {
        string? rawName = null;
        JsonObject? nested = null;
        foreach (var key in NameKeys)
        {
            var value = obj[key];
            if (value is JsonValue v && v.GetValueKind() == JsonValueKind.String)
            {
                rawName = v.GetValue<string>();
                break;
            }
            if (value is JsonObject nestedObj)
            {
                // OpenAI 风格：function 是对象，内含 name 与 arguments。
                nested = nestedObj;
                rawName = nestedObj["name"] is JsonValue nv && nv.GetValueKind() == JsonValueKind.String
                    ? nv.GetValue<string>()
                    : null;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(rawName))
        {
            result.Unparsed.Add(Unparsed(fragment, offset, "JSON 缺工具名键（需要 tool / name / function / action 之一）"));
            return;
        }

        JsonNode? argsNode = nested?["arguments"] ?? nested?["args"] ?? nested?["parameters"] ?? nested?["input"];
        if (argsNode == null)
        {
            argsNode = ArgsKeys.Select(k => obj[k]).FirstOrDefault(n => n != null);
        }

        var args = new Dictionary<string, JsonNode>();
        var canonical = ToolSpec.Normalize(rawName);
        var entry = ToolSpec.Find(canonical ?? string.Empty);

        if (argsNode == null || argsNode.GetValueKind() == JsonValueKind.Null)
        {
            // 无参数：交给分派层报缺参（AI 能看到自己漏了什么）。
        }
        else if (argsNode.GetValueKind() == JsonValueKind.String)
        {
            var inner = argsNode.GetValue<string>();
            if (TryParseJson(inner, out var innerNode) && innerNode is JsonObject innerObj)
            {
                args = ToArgDict(innerObj, canonical ?? rawName);
            }
            else
            {
                result.Unknown.Add(new UnknownCall
                {
                    RawName = rawName,
                    Fragment = Trim(inner, MaxFragmentChars),
                    Reason = "arguments 是字符串但不是合法 JSON 对象，无法取参数"
                });
                return;
            }
        }
        else if (argsNode is JsonObject argsObj)
        {
            args = ToArgDict(argsObj, canonical ?? rawName, entry);
        }
        else
        {
            result.Unparsed.Add(Unparsed(fragment, offset, "参数字段不是 JSON 对象"));
            return;
        }

        var via = nested != null || CallsKeys.Any(obj.ContainsKey) ? "openai" : "json";
        AddCall(rawName, args, via, offset, fragment, result);
    }

    private static Dictionary<string, JsonNode> ToArgDict(JsonObject obj, string? rawName, ToolSpecEntry? entry = null)
    {
        entry ??= ToolSpec.Find(ToolSpec.Normalize(rawName ?? string.Empty) ?? string.Empty);
        var dict = new Dictionary<string, JsonNode>();
        foreach (var kv in obj)
        {
            if (kv.Value == null) continue;
            dict[MapArgName(kv.Key, entry)] = kv.Value.DeepClone();
        }
        return dict;
    }

    private static void AddCall(string rawName, Dictionary<string, JsonNode> args, string via, int start, string fragment, ParseResult result)
    {
        var canonical = ToolSpec.Normalize(rawName);
        if (canonical == null)
        {
            result.Unknown.Add(new UnknownCall
            {
                RawName = rawName,
                Fragment = fragment,
                Reason = "工具名不在本插件清单内（不做模糊匹配，绝不就近执行）",
                Suggestion = ToolSpec.Suggest(rawName)
            });
            return;
        }

        result.Calls.Add(new ParsedCall
        {
            Tool = canonical,
            RawName = rawName,
            Args = args,
            Via = via,
            Start = start,
            Fragment = fragment
        });
    }

    // ---------- 未解析块 ----------

    private static UnparsedFragment DescribeBlock(string block, int start)
    {
        var tokens = block.Split([' ', '\t', ':', '=', '"', '\''], StringSplitOptions.RemoveEmptyEntries);
        var bareTool = tokens.FirstOrDefault(t =>
        {
            var c = ToolSpec.Normalize(t);
            return c != null;
        });

        var reason = bareTool != null
            ? $"疑似工具名「{bareTool}」但缺参数结构（需要 key=value 或 JSON 参数）"
            : "未找到结构化调用字段（自然语言正文，已原样保留；本插件不从散文推断意图）";

        return Unparsed(block, start, reason);
    }

    private static UnparsedFragment Unparsed(string fragment, int start, string reason) => new()
    {
        Fragment = Trim(fragment, MaxUnparsedChars),
        Reason = reason
    };

    // ---------- 小工具 ----------

    private static bool TryParseJson(string? text, out JsonNode? node)
    {
        node = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Trim(string value, int max = MaxFragmentChars)
    {
        var t = value.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }

    private static string Dedent(string value)
    {
        var lines = value.Trim('\n').Split('\n');
        var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        if (indent <= 0) return value.Trim('\n');
        return string.Join("\n", lines.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart())).Trim('\n');
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"') ? value[1..^1] : value;
}
