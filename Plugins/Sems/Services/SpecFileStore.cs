using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ForgeSelf.Api.Plugins.Sems.Services;

/// <summary>规格的一个版本（落盘为 v{N}.json，写入后不再修改）。</summary>
public sealed record SpecVersion(
    string SpecId, int Version, int ProjectId, string ProjectRoot,
    string Title, string Content, string? Author, string? ChangeNote, DateTime CreatedAt,
    IReadOnlyList<SpecCriterion>? Criteria = null);

/// <summary>验收标准条目：Id 在同一版本内唯一（如 AC1）。</summary>
public sealed record SpecCriterion(string Id, string Text);

/// <summary>验收标准与用例的手工登记（追加到 cases.jsonl）。Case 约定写成「测试文件路径::用例名」，只校验非空，不检查是否存在或通过。</summary>
public sealed record SpecCase(string SpecId, int Version, string CriterionId, string Case, DateTime At);

/// <summary>规格版本与外部标识（commit / PR / Issue）的手工关联，追加到 links.jsonl。</summary>
public sealed record SpecLink(string SpecId, int Version, string Kind, string Ref, DateTime At);

/// <summary>
/// 规格文件存储：只追加、不覆盖。
/// 目录布局：{根}/{projectId}/{specId}/v{N}.json + links.jsonl。
/// 根目录由委托在<b>调用时</b>才取（构造期不解析服务，符合插件 DI 守卫）。
/// </summary>
public sealed class SpecFileStore
{
    private static readonly object Gate = new();
    private static readonly Regex IdRx = new("^[0-9a-f]{12}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    private readonly Func<string> _root;

    public SpecFileStore(Func<string> root) => _root = root;

    /// <summary>specId 只允许服务端生成的 12 位小写十六进制，天然排除路径穿越。</summary>
    public static bool IsValidId(string? id) => id != null && IdRx.IsMatch(id);

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    private string Dir(int projectId, string specId) =>
        Path.Combine(_root(), projectId.ToString(CultureInfo.InvariantCulture), specId);

    private static IEnumerable<int> Versions(string dir) =>
        !Directory.Exists(dir)
            ? Enumerable.Empty<int>()
            : Directory.EnumerateFiles(dir, "v*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f)[1..])
                .Select(s => int.TryParse(s, out var n) ? n : 0)
                .Where(n => n > 0);

    /// <summary>追加新版本：版本号在锁内计算，文件用 CreateNew 创建，已有版本绝不被覆盖。</summary>
    public SpecVersion Append(int projectId, string specId, Func<int, SpecVersion> build)
    {
        lock (Gate)
        {
            var dir = Dir(projectId, specId);
            Directory.CreateDirectory(dir);
            var next = Versions(dir).DefaultIfEmpty(0).Max() + 1;
            var rec = build(next);
            var path = Path.Combine(dir, $"v{next}.json");
            using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(fs, rec, Opts);
            return rec;
        }
    }

    public int LatestVersion(int projectId, string specId) =>
        Versions(Dir(projectId, specId)).DefaultIfEmpty(0).Max();

    public SpecVersion? Read(int projectId, string specId, int version)
    {
        var path = Path.Combine(Dir(projectId, specId), $"v{version}.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<SpecVersion>(File.ReadAllText(path))
            : null;
    }

    public IReadOnlyList<string> ListSpecIds(int projectId)
    {
        var dir = Path.Combine(_root(), projectId.ToString(CultureInfo.InvariantCulture));
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory.EnumerateDirectories(dir)
            .Select(d => Path.GetFileName(d))
            .Where(n => IsValidId(n))
            .Select(n => n!)
            .ToList();
    }

    public void AppendLink(int projectId, string specId, SpecLink link)
    {
        lock (Gate)
        {
            var dir = Dir(projectId, specId);
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "links.jsonl"), JsonSerializer.Serialize(link) + "\n");
        }
    }

    /// <summary>读取规格的全部关联记录（links.jsonl）；文件不存在返回空，损坏的行跳过不抛。</summary>
    public IReadOnlyList<SpecLink> ReadLinks(int projectId, string specId)
    {
        var path = Path.Combine(Dir(projectId, specId), "links.jsonl");
        if (!File.Exists(path)) return Array.Empty<SpecLink>();
        string[] lines;
        lock (Gate)
        {
            lines = File.ReadAllLines(path);
        }

        var list = new List<SpecLink>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var link = JsonSerializer.Deserialize<SpecLink>(line);
                if (link != null) list.Add(link);
            }
            catch (JsonException)
            {
                // 坏行跳过，不影响其余关联
            }
        }

        return list;
    }

    public void AppendCase(int projectId, string specId, SpecCase c)
    {
        lock (Gate)
        {
            var dir = Dir(projectId, specId);
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "cases.jsonl"), JsonSerializer.Serialize(c) + "\n");
        }
    }

    /// <summary>读取规格的全部用例登记（cases.jsonl）；文件不存在返回空，损坏的行跳过不抛。</summary>
    public IReadOnlyList<SpecCase> ReadCases(int projectId, string specId)
    {
        var path = Path.Combine(Dir(projectId, specId), "cases.jsonl");
        if (!File.Exists(path)) return Array.Empty<SpecCase>();
        string[] lines;
        lock (Gate)
        {
            lines = File.ReadAllLines(path);
        }

        var list = new List<SpecCase>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var c = JsonSerializer.Deserialize<SpecCase>(line);
                if (c != null) list.Add(c);
            }
            catch (JsonException)
            {
                // 坏行跳过
            }
        }

        return list;
    }
}
