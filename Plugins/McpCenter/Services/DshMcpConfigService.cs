using System.Text;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// dsh（DeepSeek Harness）MCP 配置管理（v2.3.0）。
/// 读写 dsh 补丁层 cordis.patch.yml 里全部 MCP 客户端条目（name = @deepseek-ai/dsh-mcp-client），
/// 支持：列出当前配置 / 新增 / 编辑 / 启停 / 删除 / 重复条目检测与一键去重。
/// 启停用补丁层原生 `disabled: true|false` 行（与 ui-activity-timeline 同机制），不臆造 dsh 客户端字段。
/// 全程文本级最小改写（保留注释与既有格式），写前留 .bak，先 .tmp 再原子替换；从不删除数据目录（铁律 10）。
/// </summary>
public sealed class DshMcpConfigService
{
    public const string DefaultProfile = "desktop";
    public const string ClientPlugin = "@deepseek-ai/dsh-mcp-client";
    public const string DefaultTransport = "streamable-http";

    private static readonly string[] SupportedTransports = { "streamable-http", "http-sse", "stdio" };

    private readonly McpGatewayConfig _gateway;
    private readonly object _lock = new();

    public DshMcpConfigService(McpGatewayConfig gateway)
    {
        _gateway = gateway;
    }

    /// <summary>默认写入地址 = 当前网关监听地址 + /mcp。</summary>
    public string DefaultUrl => _gateway.ListenUrl.TrimEnd('/') + "/mcp";

    // ───────────────────────── 路径 ─────────────────────────

    /// <summary>解析目标 patch 文件路径；profile 非法（含路径分隔符/上跳）时抛 ArgumentException。</summary>
    public string ResolvePath(string? profile)
    {
        var p = string.IsNullOrWhiteSpace(profile) ? DefaultProfile : profile.Trim();
        if (p.Length == 0 || p.Contains("..") ||
            p.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
        {
            throw new ArgumentException($"dsh profile 名不合法: {profile}（仅允许字母/数字/点/下划线/短横线）");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            throw new InvalidOperationException("无法解析当前用户主目录，不能定位 dsh 配置");
        }

        return Path.Combine(home, ".dsh", "profiles", p, "cordis.patch.yml");
    }

    // ───────────────────────── 读 ─────────────────────────

    /// <summary>列出当前 dsh MCP 配置（失败原因写 LastError，不抛）。</summary>
    public DshMcpConfigDto GetStatus(string? profile)
    {
        var dto = new DshMcpConfigDto
        {
            Profile = string.IsNullOrWhiteSpace(profile) ? DefaultProfile : profile.Trim(),
            DefaultUrl = DefaultUrl
        };

        try
        {
            dto.ConfigPath = ResolvePath(dto.Profile);
            dto.ConfigExists = File.Exists(dto.ConfigPath);
            if (!dto.ConfigExists)
            {
                return dto;
            }

            var lines = ReadLines(dto.ConfigPath, out _);
            var entries = ParseEntries(lines);
            foreach (var e in entries)
            {
                dto.Servers.Add(ToDto(e));
            }

            dto.DuplicateIds = entries
                .GroupBy(e => e.Id, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
        }
        catch (Exception ex)
        {
            dto.LastError = ex.Message;
            XTrace.Log.Warn("[McpCenter] 读取 dsh 配置失败: {0}", ex.Message);
        }

        return dto;
    }

    // ───────────────────────── 写 ─────────────────────────

    /// <summary>新增一条 MCP 客户端条目（追加一个新的 insert 块）。</summary>
    public DshMcpConfigDto Add(string? profile, DshMcpServerUpsertDto dto)
    {
        if (dto == null) throw new ArgumentException("请求体不能为空");
        var serverName = Require(dto.ServerName, "serverName");
        var transport = NormalizeTransport(dto.Transport);
        var id = string.IsNullOrWhiteSpace(dto.Id) ? DeriveId(serverName) : dto.Id!.Trim();
        ValidateId(id);

        lock (_lock)
        {
            var path = ResolvePath(profile);
            var exists = File.Exists(path);
            string newline;
            List<string> lines;
            if (exists)
            {
                lines = ReadLines(path, out newline);
            }
            else
            {
                newline = Environment.NewLine;
                lines = new List<string>();
            }

            if (ParseEntries(lines).Any(e => string.Equals(e.Id, id, StringComparison.Ordinal)))
            {
                throw new ArgumentException($"条目 id '{id}' 已存在（如需修改请用编辑，或改一个 id）");
            }

            AppendEntry(lines, id, serverName, transport, dto);
            WriteAtomic(path, string.Join(newline, lines));
            XTrace.Log.Info("[McpCenter] dsh 新增 MCP 条目: {0}（{1}）", id, transport);
        }

        return GetStatus(profile);
    }

    /// <summary>编辑一条 MCP 条目（id 不可改；只改 serverName/transport/url/command/args/headers/enabled）。</summary>
    public DshMcpConfigDto Update(string? profile, string id, DshMcpServerUpsertDto dto)
    {
        if (dto == null) throw new ArgumentException("请求体不能为空");
        ValidateId(id);

        lock (_lock)
        {
            var path = ResolvePath(profile);
            if (!File.Exists(path)) throw new ArgumentException("dsh 配置文件不存在，请先新增");

            var lines = ReadLines(path, out var newline);
            var entry = ParseEntries(lines).FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));
            if (entry == null) throw new ArgumentException($"条目 id '{id}' 不存在");

            var serverName = string.IsNullOrWhiteSpace(dto.ServerName) ? entry.ServerName : dto.ServerName!.Trim();
            var transport = string.IsNullOrWhiteSpace(dto.Transport) ? entry.Transport : NormalizeTransport(dto.Transport);
            ValidateServer(transport, dto.Url ?? entry.Url, dto.Command ?? entry.Command);

            SetScalar(lines, entry, "serverName", serverName);
            SetScalar(lines, entry, "transport", transport);

            if (transport == "stdio")
            {
                RemoveField(lines, entry, "url");
                SetScalar(lines, entry, "command", (dto.Command ?? entry.Command).Trim());
                var args = ParseArgs(dto.Args) is { Count: > 0 } a ? a : (dto.Args == null ? entry.Args : new List<string>());
                SetList(lines, entry, "args", args);
            }
            else
            {
                RemoveField(lines, entry, "command");
                RemoveField(lines, entry, "args");
                SetScalar(lines, entry, "url", (dto.Url ?? entry.Url).Trim());
            }

            if (dto.Headers != null)
            {
                SetMap(lines, entry, "headers", dto.Headers);
            }

            if (dto.Enabled is { } en)
            {
                SetDisabled(lines, entry, !en);
            }

            WriteAtomic(path, string.Join(newline, lines));
            XTrace.Log.Info("[McpCenter] dsh 更新 MCP 条目: {0}", id);
        }

        return GetStatus(profile);
    }

    /// <summary>启用/停用一条 MCP 条目（写补丁层 disabled 行）。</summary>
    public DshMcpConfigDto Toggle(string? profile, string id, bool enabled)
    {
        ValidateId(id);
        lock (_lock)
        {
            var path = ResolvePath(profile);
            if (!File.Exists(path)) throw new ArgumentException("dsh 配置文件不存在");
            var lines = ReadLines(path, out var newline);
            var entry = ParseEntries(lines).FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));
            if (entry == null) throw new ArgumentException($"条目 id '{id}' 不存在");
            SetDisabled(lines, entry, !enabled);
            WriteAtomic(path, string.Join(newline, lines));
        }

        return GetStatus(profile);
    }

    /// <summary>删除一条 MCP 条目（删除其全部同名重复项）。</summary>
    public DshMcpConfigDto Delete(string? profile, string id)
    {
        ValidateId(id);
        lock (_lock)
        {
            var path = ResolvePath(profile);
            if (!File.Exists(path)) throw new ArgumentException("dsh 配置文件不存在");
            var lines = ReadLines(path, out var newline);
            var entries = ParseEntries(lines).Where(e => string.Equals(e.Id, id, StringComparison.Ordinal)).ToList();
            if (entries.Count == 0) throw new ArgumentException($"条目 id '{id}' 不存在");
            foreach (var e in entries.OrderByDescending(x => x.Start))
            {
                lines.RemoveRange(e.Start, e.End - e.Start);
            }

            TrimBlankRun(lines);
            WriteAtomic(path, string.Join(newline, lines));
            XTrace.Log.Info("[McpCenter] dsh 删除 MCP 条目: {0}（{1} 处）", id, entries.Count);
        }

        return GetStatus(profile);
    }

    /// <summary>一键去重：同一 id 只保留第一条，删除其余重复条目。</summary>
    public DshMcpConfigDto Dedupe(string? profile)
    {
        lock (_lock)
        {
            var path = ResolvePath(profile);
            if (!File.Exists(path)) throw new ArgumentException("dsh 配置文件不存在");
            var lines = ReadLines(path, out var newline);
            var entries = ParseEntries(lines);
            var dupes = entries
                .GroupBy(e => e.Id, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderBy(x => x.Start).Skip(1))
                .OrderByDescending(e => e.Start)
                .ToList();
            foreach (var e in dupes)
            {
                lines.RemoveRange(e.Start, e.End - e.Start);
            }

            TrimBlankRun(lines);
            WriteAtomic(path, string.Join(newline, lines));
            XTrace.Log.Info("[McpCenter] dsh 去重完成，移除 {0} 条重复条目", dupes.Count);
        }

        return GetStatus(profile);
    }

    /// <summary>快捷写入/更新 ForgeSelf 条目地址（旧版 API 兼容）。</summary>
    public DshMcpConfigDto QuickWrite(string? profile, string? serverId, string? serverName, string? url)
    {
        var id = string.IsNullOrWhiteSpace(serverId) ? "mcp-forgeself" : serverId!.Trim();
        var name = string.IsNullOrWhiteSpace(serverName) ? "ForgeSelf" : serverName!.Trim();
        var target = string.IsNullOrWhiteSpace(url) ? DefaultUrl : url!.Trim();
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException($"MCP 地址仅支持 http/https 绝对地址: {target}");
        }

        lock (_lock)
        {
            var path = ResolvePath(profile);
            var exists = File.Exists(path);
            string newline;
            List<string> lines;
            if (exists)
            {
                lines = ReadLines(path, out newline);
            }
            else
            {
                newline = Environment.NewLine;
                lines = new List<string>();
            }

            var match = ParseEntries(lines).FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));
            if (match != null)
            {
                SetScalar(lines, match, "url", target);
                SetScalar(lines, match, "serverName", name);
            }
            else
            {
                var dto = new DshMcpServerUpsertDto { Url = target, Transport = DefaultTransport };
                AppendEntry(lines, id, name, DefaultTransport, dto);
            }

            WriteAtomic(path, string.Join(newline, lines));
        }

        return GetStatus(profile);
    }

    // ───────────────────────── 解析 ─────────────────────────

    private sealed class Entry
    {
        public int Start;
        public int End; // exclusive
        public int Indent;
        public string Id = string.Empty;
        public string PluginName = string.Empty;
        public string ServerName = string.Empty;
        public string Transport = string.Empty;
        public string Url = string.Empty;
        public string Command = string.Empty;
        public List<string> Args = new();
        public Dictionary<string, string> Headers = new();
        public bool Disabled;
    }

    private static List<Entry> ParseEntries(List<string> lines)
    {
        var result = new List<Entry>();
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (!t.StartsWith("- id:", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = IndentOf(lines[i]);
            var end = BlockEnd(lines, i, indent);
            var id = t[5..].Trim().Trim('\'', '"');

            var plugin = Extract(lines, i, end, "name");
            if (!string.Equals(plugin, ClientPlugin, StringComparison.Ordinal))
            {
                continue; // 只管理 MCP 客户端条目
            }

            var entry = new Entry
            {
                Start = i,
                End = end,
                Indent = indent,
                Id = id,
                PluginName = plugin,
                ServerName = Extract(lines, i, end, "serverName"),
                Transport = Extract(lines, i, end, "transport"),
                Url = Extract(lines, i, end, "url"),
                Command = Extract(lines, i, end, "command"),
                Args = ExtractList(lines, i, end, "args"),
                Headers = ExtractMap(lines, i, end, "headers")
            };

            var disabledLine = FindKey(lines, i, end, "disabled", indent + 2);
            if (disabledLine >= 0)
            {
                entry.Disabled = lines[disabledLine].Trim()[9..].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            result.Add(entry);
            i = end - 1;
        }

        return result;
    }

    private static DshMcpServerDto ToDto(Entry e) => new()
    {
        Id = e.Id,
        ServerName = e.ServerName,
        Transport = e.Transport,
        Url = e.Url,
        Command = e.Command,
        Args = e.Args,
        Headers = e.Headers,
        Enabled = !e.Disabled,
        Line = e.Start + 1
    };

    /// <summary>取块内某 key 的第一个标量值（去掉引号）。</summary>
    private static string Extract(List<string> lines, int start, int end, string key)
    {
        for (var j = start + 1; j < end; j++)
        {
            var t = lines[j].Trim();
            if (t.StartsWith(key + ":", StringComparison.Ordinal))
            {
                return t[(key.Length + 1)..].Trim().Trim('\'', '"');
            }
        }

        return string.Empty;
    }

    /// <summary>取块内某 key 的子列表（- value 形式）。</summary>
    private static List<string> ExtractList(List<string> lines, int start, int end, string key)
    {
        var list = new List<string>();
        var k = FindKey(lines, start, end, key, -1);
        if (k < 0)
        {
            return list;
        }

        var baseIndent = IndentOf(lines[k]);
        for (var j = k + 1; j < end; j++)
        {
            var t = lines[j].Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (IndentOf(lines[j]) <= baseIndent)
            {
                break;
            }

            if (t.StartsWith("- ", StringComparison.Ordinal) || t == "-")
            {
                list.Add(t.Length > 2 ? t[2..].Trim().Trim('\'', '"') : string.Empty);
            }
        }

        return list;
    }

    /// <summary>取块内某 key 的子映射（key: value 形式）。</summary>
    private static Dictionary<string, string> ExtractMap(List<string> lines, int start, int end, string key)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var k = FindKey(lines, start, end, key, -1);
        if (k < 0)
        {
            return map;
        }

        var baseIndent = IndentOf(lines[k]);
        for (var j = k + 1; j < end; j++)
        {
            var t = lines[j].Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (IndentOf(lines[j]) <= baseIndent)
            {
                break;
            }

            var colon = t.IndexOf(':');
            if (colon > 0)
            {
                map[t[..colon].Trim()] = t[(colon + 1)..].Trim().Trim('\'', '"');
            }
        }

        return map;
    }

    /// <summary>块内查 key 行；indentFilter &lt; 0 表示不限缩进，否则要求等于该缩进。</summary>
    private static int FindKey(List<string> lines, int start, int end, string key, int indentFilter)
    {
        for (var j = start + 1; j < end; j++)
        {
            var t = lines[j].Trim();
            if (!t.StartsWith(key + ":", StringComparison.Ordinal))
            {
                continue;
            }

            if (indentFilter < 0 || IndentOf(lines[j]) == indentFilter)
            {
                return j;
            }
        }

        return -1;
    }

    private static int BlockEnd(List<string> lines, int start, int indent)
    {
        for (var j = start + 1; j < lines.Count; j++)
        {
            var t = lines[j].Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (IndentOf(lines[j]) <= indent)
            {
                return j;
            }
        }

        return lines.Count;
    }

    // ───────────────────────── 改写 ─────────────────────────

    private static void SetScalar(List<string> lines, Entry entry, string key, string value)
    {
        var end = RefreshEnd(lines, entry);
        var k = FindKey(lines, entry.Start, end, key, -1);
        if (k >= 0)
        {
            lines[k] = new string(' ', IndentOf(lines[k])) + key + ": " + Scalar(value);
            return;
        }

        lines.Insert(end, new string(' ', entry.Indent + 4) + key + ": " + Scalar(value));
    }

    private static void SetList(List<string> lines, Entry entry, string key, List<string> values)
    {
        var end = RefreshEnd(lines, entry);
        var k = FindKey(lines, entry.Start, end, key, -1);
        var indent = entry.Indent + 4;
        if (k >= 0)
        {
            indent = IndentOf(lines[k]);
            RemoveNested(lines, k, entry);
        }
        else
        {
            k = RefreshEnd(lines, entry);
        }

        if (values.Count == 0)
        {
            if (FindKey(lines, entry.Start, RefreshEnd(lines, entry), key, -1) < 0)
            {
                return; // 没内容不写空键
            }
        }

        var block = new List<string> { new string(' ', indent) + key + ":" };
        foreach (var v in values)
        {
            block.Add(new string(' ', indent + 2) + "- " + Scalar(v));
        }

        lines.InsertRange(k, block);
    }

    private static void SetMap(List<string> lines, Entry entry, string key, Dictionary<string, string> values)
    {
        var end = RefreshEnd(lines, entry);
        var k = FindKey(lines, entry.Start, end, key, -1);
        var indent = entry.Indent + 4;
        if (k >= 0)
        {
            indent = IndentOf(lines[k]);
            RemoveNested(lines, k, entry);
        }
        else
        {
            k = RefreshEnd(lines, entry);
        }

        if (values.Count == 0)
        {
            return;
        }

        var block = new List<string> { new string(' ', indent) + key + ":" };
        block.AddRange(values.Select(kv => new string(' ', indent + 2) + kv.Key + ": " + Scalar(kv.Value)));
        lines.InsertRange(k, block);
    }

    private static void RemoveField(List<string> lines, Entry entry, string key)
    {
        var end = RefreshEnd(lines, entry);
        var k = FindKey(lines, entry.Start, end, key, -1);
        if (k >= 0)
        {
            RemoveNested(lines, k, entry);
        }
    }

    /// <summary>删除 key 行及其子行（缩进更深的续行）。</summary>
    private static void RemoveNested(List<string> lines, int keyLine, Entry entry)
    {
        var baseIndent = IndentOf(lines[keyLine]);
        var j = keyLine + 1;
        while (j < lines.Count)
        {
            var t = lines[j].Trim();
            if (t.Length == 0 || !t.StartsWith("#", StringComparison.Ordinal))
            {
                if (t.Length != 0 && IndentOf(lines[j]) <= baseIndent)
                {
                    break;
                }
            }

            if (t.Length == 0)
            {
                break;
            }

            j++;
        }

        lines.RemoveRange(keyLine, j - keyLine);
        entry.End = Math.Max(entry.Start + 1, entry.End - (j - keyLine));
    }

    private static void SetDisabled(List<string> lines, Entry entry, bool disabled)
    {
        var end = RefreshEnd(lines, entry);
        var k = FindKey(lines, entry.Start, end, "disabled", entry.Indent + 2);
        if (disabled)
        {
            if (k >= 0)
            {
                lines[k] = new string(' ', entry.Indent + 2) + "disabled: true";
            }
            else
            {
                lines.Insert(entry.Start + 1, new string(' ', entry.Indent + 2) + "disabled: true");
                entry.End++;
            }
        }
        else if (k >= 0)
        {
            lines.RemoveAt(k);
            entry.End--;
        }
    }

    /// <summary>条目当前结束下标（改写过后续算，避免陈旧区间）。</summary>
    private static int RefreshEnd(List<string> lines, Entry entry)
    {
        var end = BlockEnd(lines, entry.Start, entry.Indent);
        entry.End = end;
        return end;
    }

    private static void AppendEntry(List<string> lines, string id, string serverName, string transport, DshMcpServerUpsertDto dto)
    {
        if (lines.Count > 0 && lines[^1].Trim().Length != 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add("- insert:");
        lines.Add($"    - id: {id}");
        lines.Add($"      name: '{ClientPlugin}'");
        lines.Add("      config:");
        lines.Add($"        serverName: {Scalar(serverName)}");
        lines.Add($"        transport: {transport}");

        if (transport == "stdio")
        {
            lines.Add($"        command: {Scalar((dto.Command ?? string.Empty).Trim())}");
            var args = ParseArgs(dto.Args);
            if (args.Count > 0)
            {
                lines.Add("        args:");
                lines.AddRange(args.Select(a => $"          - {Scalar(a)}"));
            }
        }
        else
        {
            lines.Add($"        url: {(dto.Url ?? string.Empty).Trim()}");
        }

        if (dto.Headers is { Count: > 0 })
        {
            lines.Add("        headers:");
            lines.AddRange(dto.Headers.Select(kv => $"          {kv.Key}: {Scalar(kv.Value)}"));
        }

        if (dto.Enabled is false)
        {
            lines.Insert(lines.Count, "      disabled: true");
        }
    }

    private static void TrimBlankRun(List<string> lines)
    {
        // 删除条目后可能留下连续空行，压缩为最多一个
        for (var i = lines.Count - 1; i > 0; i--)
        {
            if (lines[i].Trim().Length == 0 && lines[i - 1].Trim().Length == 0)
            {
                lines.RemoveAt(i);
            }
        }

        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }

    // ───────────────────────── 工具 ─────────────────────────

    private static List<string> ReadLines(string path, out string newline)
    {
        var raw = File.ReadAllText(path, Encoding.UTF8);
        newline = raw.Contains("\r\n") ? "\r\n" : "\n";
        return raw.Replace("\r\n", "\n").Split('\n').ToList();
    }

    private static void WriteAtomic(string path, string text)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(path))
        {
            try
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[McpCenter] dsh 配置备份失败（继续写入）: {0}", ex.Message);
            }
        }

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    private static int IndentOf(string line)
    {
        var n = 0;
        while (n < line.Length && line[n] == ' ')
        {
            n++;
        }

        return n;
    }

    private static string Scalar(string value)
    {
        var needsQuote = value.Length == 0 ||
            value.Any(c => c is ':' or '#' or '\'' or '"' or '{' or '}' or '[' or ']' or ',' or '&' or '*' or '!' or '|' or '>' or '%' or '@' or '`');
        return needsQuote ? "'" + value.Replace("'", "''") + "'" : value;
    }

    private static string Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} 必填");
        }

        return value.Trim();
    }

    private static string NormalizeTransport(string? transport)
    {
        var t = string.IsNullOrWhiteSpace(transport) ? DefaultTransport : transport!.Trim().ToLowerInvariant();
        if (!SupportedTransports.Contains(t, StringComparer.Ordinal))
        {
            throw new ArgumentException($"传输类型不支持: {transport}（支持 {string.Join(" / ", SupportedTransports)}）");
        }

        return t;
    }

    private static void ValidateServer(string transport, string url, string command)
    {
        if (transport == "stdio")
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException("stdio 传输必须填写 command");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException($"{transport} 传输必须填写 url");
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException($"MCP 地址仅支持 http/https 绝对地址: {url}");
        }
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64 ||
            !id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
        {
            throw new ArgumentException($"条目 id 不合法: {id}（仅允许字母/数字/-/_/.）");
        }
    }

    private static string DeriveId(string serverName)
    {
        var chars = serverName.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        return string.IsNullOrWhiteSpace(slug) ? "mcp-server" : "mcp-" + slug;
    }

    private static List<string> ParseArgs(string? args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return new List<string>();
        }

        return args
            .Replace("\r\n", "\n")
            .Split(new[] { '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().Trim('\'', '"'))
            .Where(s => s.Length > 0)
            .ToList();
    }
}
