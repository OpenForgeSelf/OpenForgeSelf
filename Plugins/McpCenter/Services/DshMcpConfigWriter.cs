using System.Text;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// dsh（DeepSeek Harness）MCP 客户端配置写入器（v2.3.0 新增）。
/// 目标 = %USERPROFILE%\.dsh\profiles\&lt;profile&gt;\cordis.patch.yml（dsh 的补丁层，顶层为 YAML 数组）。
/// 语义 = 按条目 id 幂等 upsert：已存在则就地改写其 config.url；不存在则追加一段 insert 块
/// （name = @deepseek-ai/dsh-mcp-client）。
/// 只做**文本级最小改写**（不整篇 YAML 往返），保留用户注释与既有格式；
/// 写入前留 .bak 备份，先写 .tmp 再原子替换；从不删除任何数据目录/文件（铁律 10）。
/// </summary>
public sealed class DshMcpConfigWriter
{
    public const string DefaultProfile = "desktop";
    public const string DefaultServerId = "mcp-forgeself";
    public const string DefaultServerName = "ForgeSelf";
    public const string DefaultTransport = "streamable-http";
    public const string DshClientPlugin = "@deepseek-ai/dsh-mcp-client";

    private readonly McpGatewayConfig _gateway;

    public DshMcpConfigWriter(McpGatewayConfig gateway)
    {
        _gateway = gateway;
    }

    /// <summary>默认写入地址 = 当前网关监听地址 + /mcp（请求可覆盖，例如指向本地聚合网关）。</summary>
    public string DefaultUrl => _gateway.ListenUrl.TrimEnd('/') + "/mcp";

    /// <summary>解析目标 patch 文件路径；profile 非法（含路径分隔符/上跳）时抛 ArgumentException。</summary>
    public string ResolvePath(string? profile)
    {
        var p = string.IsNullOrWhiteSpace(profile) ? DefaultProfile : profile.Trim();
        if (p.Length == 0 || p.Contains("..") || p.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
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

    /// <summary>查看写入状态：目标文件是否存在、条目是否存在、当前 url（失败原因写 LastError，不抛）。</summary>
    public DshMcpConfigDto GetStatus(string? profile, string? serverId, string? serverName)
    {
        var dto = new DshMcpConfigDto
        {
            Profile = string.IsNullOrWhiteSpace(profile) ? DefaultProfile : profile.Trim(),
            EntryId = string.IsNullOrWhiteSpace(serverId) ? DefaultServerId : serverId.Trim(),
            ServerName = string.IsNullOrWhiteSpace(serverName) ? DefaultServerName : serverName.Trim(),
            Transport = DefaultTransport,
            Url = DefaultUrl
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
            var (found, url) = LocateEntry(lines, dto.EntryId);
            dto.EntryExists = found;
            if (found && !string.IsNullOrWhiteSpace(url))
            {
                dto.Url = url;
            }
        }
        catch (Exception ex)
        {
            dto.LastError = ex.Message;
            XTrace.Log.Warn("[McpCenter] 读取 dsh 配置失败: {0}", ex.Message);
        }

        return dto;
    }

    /// <summary>
    /// 幂等写入：同 id 条目已存在则就地更新 url，否则追加 insert 块。
    /// 非法参数抛 ArgumentException（→ API 400），IO 失败抛其它异常（→ API 500）。
    /// </summary>
    public DshMcpConfigDto Upsert(DshMcpConfigWriteDto dto)
    {
        if (dto == null)
        {
            throw new ArgumentException("请求体不能为空");
        }

        var id = string.IsNullOrWhiteSpace(dto.ServerId) ? DefaultServerId : dto.ServerId.Trim();
        var name = string.IsNullOrWhiteSpace(dto.ServerName) ? DefaultServerName : dto.ServerName.Trim();
        var url = string.IsNullOrWhiteSpace(dto.Url) ? DefaultUrl : dto.Url.Trim();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException($"MCP 地址仅支持 http/https 绝对地址: {url}");
        }

        var path = ResolvePath(dto.Profile);
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

        if (!UpsertEntry(lines, id, url))
        {
            AppendEntry(lines, id, name, url);
        }

        WriteAtomic(path, string.Join(newline, lines));
        XTrace.Log.Info("[McpCenter] 已写入 dsh MCP 配置: {0} -> {1}", path, url);

        var status = GetStatus(dto.Profile, id, name);
        status.ConfigExists = true;
        status.EntryExists = true;
        status.Url = url;
        status.LastError = null;
        return status;
    }

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

    /// <summary>在既有条目内就地改写 url；返回 false 表示该 id 条目不存在（需追加）。</summary>
    private static bool UpsertEntry(List<string> lines, string id, string url)
    {
        var (idIndex, indent) = FindIdLine(lines, id);
        if (idIndex < 0)
        {
            return false;
        }

        var end = BlockEnd(lines, idIndex, indent);

        for (var j = idIndex + 1; j < end; j++)
        {
            if (lines[j].TrimStart().StartsWith("url:", StringComparison.Ordinal))
            {
                lines[j] = new string(' ', IndentOf(lines[j])) + "url: " + url;
                return true;
            }
        }

        // 块内没有 url 行：在该块最深键缩进处补一行
        var insertIndent = indent + 2;
        for (var j = idIndex + 1; j < end; j++)
        {
            var t = lines[j].Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }
            var ind = IndentOf(lines[j]);
            if (ind > insertIndent && t.Contains(':'))
            {
                insertIndent = ind;
            }
        }

        lines.Insert(end, new string(' ', insertIndent) + "url: " + url);
        return true;
    }

    /// <summary>追加一段 insert 块（与 dsh 现有 mcp 客户端条目同形）。</summary>
    private static void AppendEntry(List<string> lines, string id, string name, string url)
    {
        if (lines.Count > 0 && lines[^1].Trim().Length != 0)
        {
            lines.Add("");
        }

        lines.Add("- insert:");
        lines.Add($"    - id: {id}");
        lines.Add($"      name: '{DshClientPlugin}'");
        lines.Add("      config:");
        lines.Add($"        serverName: {Scalar(name)}");
        lines.Add($"        transport: {DefaultTransport}");
        lines.Add($"        url: {url}");
    }

    private static (bool Found, string? Url) LocateEntry(List<string> lines, string id)
    {
        var (idIndex, indent) = FindIdLine(lines, id);
        if (idIndex < 0)
        {
            return (false, null);
        }

        var end = BlockEnd(lines, idIndex, indent);
        for (var j = idIndex + 1; j < end; j++)
        {
            var t = lines[j].Trim();
            if (t.StartsWith("url:", StringComparison.Ordinal))
            {
                return (true, t[4..].Trim().Trim('\'', '"'));
            }
        }

        return (true, null);
    }

    private static (int Index, int Indent) FindIdLine(List<string> lines, string id)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (!t.StartsWith("id:", StringComparison.Ordinal))
            {
                continue;
            }

            var value = t[3..].Trim().Trim('\'', '"');
            if (string.Equals(value, id, StringComparison.Ordinal))
            {
                return (i, IndentOf(lines[i]));
            }
        }

        return (-1, 0);
    }

    /// <summary>条目块结束下标（不含）：下一个缩进 &lt;= 条目 id 缩进的非空非注释行。</summary>
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
        var needsQuote = value.Any(c => c is ':' or '#' or '\'' or '"' or '{' or '}' or '[' or ']' or ',' or '&' or '*' or '!' or '|' or '>' or '%' or '@' or '`');
        return needsQuote ? "'" + value.Replace("'", "''") + "'" : value;
    }
}
