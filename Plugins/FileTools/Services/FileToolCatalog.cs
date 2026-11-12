using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.FileTools;

/// <summary>在 cwd 内枚举文件，不进入符号链接/目录联接；到上限即停并标记 truncated。</summary>
internal static class FileToolTree
{
    public const int MaxDepth = 8;
    public const int MaxVisited = 5000;

    public static bool Walk(
        string root,
        Func<FileSystemInfo, bool> visitor,
        out int skippedReparse,
        out int skippedUnreadable,
        out bool truncated,
        out string? error)
    {
        skippedReparse = 0;
        skippedUnreadable = 0;
        truncated = false;
        error = null;
        var visited = 0;
        var stack = new Stack<(string Dir, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();
            DirectoryInfo di;
            try
            {
                di = new DirectoryInfo(dir);
            }
            catch (Exception ex)
            {
                error = "无法读取目录：" + dir + "（" + ex.Message + "）";
                return false;
            }

            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = di.EnumerateFileSystemInfos().ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skippedUnreadable++;
                continue;
            }

            foreach (var entry in entries)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    skippedReparse++;
                    continue;
                }

                visited++;
                if (visited > MaxVisited)
                {
                    truncated = true;
                    return true;
                }

                if (!visitor(entry))
                {
                    truncated = true;
                    return true;
                }

                if (entry is DirectoryInfo && depth < MaxDepth)
                {
                    stack.Push((entry.FullName, depth + 1));
                }
                else if (entry is DirectoryInfo)
                {
                    truncated = true;
                }
            }
        }

        return true;
    }
}

internal sealed record FileNameMatch(string name, string path, bool isDirectory, long size);

internal sealed record ContentMatch(string path, int line, string snippet);

internal static class FileToolArgs
{
    public static bool TryOptionalInt(JsonElement root, string name, int defaultValue, int min, int max, out int value, out string? error)
    {
        value = defaultValue;
        error = null;
        if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        var parsed = 0;
        var ok = el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt32(out parsed),
            JsonValueKind.String => int.TryParse(el.GetString(), out parsed),
            _ => false,
        };
        if (!ok)
        {
            parsed = defaultValue;
        }

        value = parsed;
        if (!ok || value < min || value > max)
        {
            error = name + " 必须为 " + min + ".." + max + " 的整数，收到：" + el.GetRawText() + "（不静默钳制）";
            value = defaultValue;
            return false;
        }

        return true;
    }

    public static string? StringProp(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return el.GetString();
    }
}

public class SearchFilesToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "file-tools.search_files";
    public string Name => "search_files";
    public string PluginId => "file-tools";
    public string Description => "在 cwd 内按文件名通配符查找文件与目录（不跟随符号链接/目录联接，不越出 cwd）。参数：cwd=工作目录绝对路径（必填）；path=起始目录绝对路径（可选，缺省=cwd）；pattern=文件名通配符（必填，如 *.cs，不能含路径）；max_results=1..200（可选，默认 100，超出范围直接拒绝）。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""cwd"": { ""type"": ""string"", ""description"": ""工作目录绝对路径（必填）"" },
        ""path"": { ""type"": ""string"", ""description"": ""起始目录绝对路径（可选，缺省=cwd，必须落在 cwd 内）"" },
        ""pattern"": { ""type"": ""string"", ""description"": ""文件名通配符，如 *.cs 或 README*；不能包含路径"" },
        ""max_results"": { ""type"": ""integer"", ""description"": ""最多返回条数，1..200，默认 100；超出范围拒绝而不是钳制"" }
    },
    ""required"": [""cwd"", ""pattern""]
}";

    public SearchFilesToolFunction(IContext ctx) => _ctx = ctx;

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var root = doc.RootElement;
            if (!FileToolPathOps.EnsureCwd(FileToolArgs.StringProp(root, "cwd"), out var cwd, out var cwdError))
            {
                return Fail(cwdError!);
            }

            if (!FileToolPathOps.TryBind(cwd!, FileToolArgs.StringProp(root, "path"), required: false, out var start, out var bindError))
            {
                return Fail(bindError!);
            }

            var pattern = FileToolArgs.StringProp(root, "pattern");
            if (string.IsNullOrWhiteSpace(pattern) || pattern.IndexOfAny(['\\', '/', ':']) >= 0)
            {
                return Fail("pattern 必须是文件名通配符（如 *.cs），不能为空或包含路径");
            }

            if (!FileToolArgs.TryOptionalInt(root, "max_results", 100, 1, 200, out var maxResults, out var maxError))
            {
                return Fail(maxError!);
            }

            if (!Directory.Exists(start))
            {
                return Fail("目录不存在：" + start);
            }

            var matches = new List<FileNameMatch>();
            var walked = FileToolTree.Walk(start!, entry =>
            {
                if (!System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, entry.Name, ignoreCase: true))
                {
                    return true;
                }

                matches.Add(new FileNameMatch(
                    entry.Name,
                    entry.FullName,
                    entry is DirectoryInfo,
                    entry is FileInfo fi ? fi.Length : 0L));
                return matches.Count < maxResults;
            }, out var skippedReparse, out var skippedUnreadable, out var truncated, out var walkError);

            if (!walked)
            {
                return Fail(walkError!);
            }

            XTrace.Log.Info("[FileTools] search_files 成功：cwd={0}, 命中={1}", cwd, matches.Count);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                cwd,
                path = start,
                pattern,
                count = matches.Count,
                truncated,
                skippedReparse,
                skippedUnreadable,
                matches
            }, ForgeJson.Default));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] search_files 执行失败: {0}", ex.Message);
            return Fail(ex.Message);
        }
    }

    private Task<string> Fail(string message) =>
        Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, message), ForgeJson.Default));
}

public class SearchContentToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "file-tools.search_content";
    public string Name => "search_content";
    public string PluginId => "file-tools";
    public string Description => "在 cwd 内搜索文本内容（UTF-8，不跟随符号链接/目录联接）。超过 1 MiB 的文件跳过并计入 skippedOversize，不截断读取。参数：cwd（必填）；path=起始目录绝对路径（可选）；query=要查找的文本（必填）；max_matches=1..100（可选，默认 50）。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""cwd"": { ""type"": ""string"", ""description"": ""工作目录绝对路径（必填）"" },
        ""path"": { ""type"": ""string"", ""description"": ""起始目录绝对路径（可选，缺省=cwd，必须落在 cwd 内）"" },
        ""query"": { ""type"": ""string"", ""description"": ""要查找的文本（大小写不敏感，最长 500 字符）"" },
        ""max_matches"": { ""type"": ""integer"", ""description"": ""最多返回命中，1..100，默认 50；超出范围拒绝而不是钳制"" }
    },
    ""required"": [""cwd"", ""query""]
}";

    public SearchContentToolFunction(IContext ctx) => _ctx = ctx;

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var root = doc.RootElement;
            if (!FileToolPathOps.EnsureCwd(FileToolArgs.StringProp(root, "cwd"), out var cwd, out var cwdError))
            {
                return Fail(cwdError!);
            }

            if (!FileToolPathOps.TryBind(cwd!, FileToolArgs.StringProp(root, "path"), required: false, out var start, out var bindError))
            {
                return Fail(bindError!);
            }

            var query = FileToolArgs.StringProp(root, "query");
            if (string.IsNullOrEmpty(query) || query.Length > 500)
            {
                return Fail("query 必须为 1..500 字符的查找文本");
            }

            if (!FileToolArgs.TryOptionalInt(root, "max_matches", 50, 1, 100, out var maxMatches, out var maxError))
            {
                return Fail(maxError!);
            }

            if (!Directory.Exists(start))
            {
                return Fail("目录不存在：" + start);
            }

            var matches = new List<ContentMatch>();
            var skippedOversize = 0;
            var skippedBinary = 0;
            var walked = FileToolTree.Walk(start!, entry =>
            {
                if (entry is not FileInfo file)
                {
                    return true;
                }

                if (file.Length > FilePathGuard.MaxFileBytes)
                {
                    skippedOversize++;
                    return true;
                }

                if (LooksBinary(file.FullName))
                {
                    skippedBinary++;
                    return true;
                }

                string text;
                try
                {
                    text = File.ReadAllText(file.FullName, Encoding.UTF8);
                }
                catch (Exception)
                {
                    skippedBinary++;
                    return true;
                }

                var lines = FileToolLineOps.SplitLines(text);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var snippet = lines[i].Trim();
                    if (snippet.Length > 160)
                    {
                        snippet = snippet[..160];
                    }

                    matches.Add(new ContentMatch(file.FullName, i + 1, snippet));
                    if (matches.Count >= maxMatches)
                    {
                        return false;
                    }
                }

                return true;
            }, out var skippedReparse, out var skippedUnreadable, out var truncated, out var walkError);

            if (!walked)
            {
                return Fail(walkError!);
            }

            XTrace.Log.Info("[FileTools] search_content 成功：cwd={0}, 命中={1}", cwd, matches.Count);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                cwd,
                path = start,
                query,
                count = matches.Count,
                truncated,
                skippedReparse,
                skippedUnreadable,
                skippedOversize,
                skippedBinary,
                matches
            }, ForgeJson.Default));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] search_content 执行失败: {0}", ex.Message);
            return Fail(ex.Message);
        }
    }

    private static bool LooksBinary(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(8192, stream.Length)];
            var read = stream.Read(buffer, 0, buffer.Length);
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private Task<string> Fail(string message) =>
        Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, message), ForgeJson.Default));
}

public class MoveFileToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "file-tools.move_file";
    public string Name => "move_file";
    public string PluginId => "file-tools";
    public string Description => "把 cwd 内的文件移动到另一处（仍须在 cwd 内）。不跟随、不移动符号链接/目录联接，不覆盖已存在目标，不移动目录。高风险，须审批。参数：cwd、source、destination，均为绝对路径。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""cwd"": { ""type"": ""string"", ""description"": ""工作目录绝对路径（必填）"" },
        ""source"": { ""type"": ""string"", ""description"": ""源文件绝对路径（必填，必须落在 cwd 内）"" },
        ""destination"": { ""type"": ""string"", ""description"": ""目标文件绝对路径（必填，必须落在 cwd 内，且尚不存在）"" }
    },
    ""required"": [""cwd"", ""source"", ""destination""]
}";

    public MoveFileToolFunction(IContext ctx) => _ctx = ctx;

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var root = doc.RootElement;
            if (!FileToolPathOps.EnsureCwd(FileToolArgs.StringProp(root, "cwd"), out var cwd, out var cwdError))
            {
                return Fail(cwdError!);
            }

            if (!FileToolPathOps.TryBind(cwd!, FileToolArgs.StringProp(root, "source"), required: true, out var source, out var sourceError))
            {
                return Fail(sourceError!);
            }

            if (!FileToolPathOps.TryBind(cwd!, FileToolArgs.StringProp(root, "destination"), required: true, out var destination, out var destError))
            {
                return Fail(destError!);
            }

            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("source 与 destination 相同，未移动");
            }

            if (!File.Exists(source))
            {
                return Fail(Directory.Exists(source) ? "move_file 只移动文件，不移动目录：" + source : "文件不存在：" + source);
            }

            if (File.Exists(destination) || Directory.Exists(destination))
            {
                return Fail("目标已存在，拒绝覆盖：" + destination);
            }

            var parent = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            {
                return Fail("目标父目录不存在，未创建目录、未移动：" + parent);
            }

            File.Move(source!, destination!);
            XTrace.Log.Info("[FileTools] move_file 成功：{0} -> {1}", source, destination);
            return Task.FromResult(JsonSerializer.Serialize(new { success = true, cwd, source, destination }, ForgeJson.Default));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] move_file 执行失败: {0}", ex.Message);
            return Fail(ex.Message);
        }
    }

    private Task<string> Fail(string message) =>
        Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, message), ForgeJson.Default));
}

public class RenameFileToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "file-tools.rename_file";
    public string Name => "rename_file";
    public string PluginId => "file-tools";
    public string Description => "在同一目录内重命名 cwd 中的一个文件。new_name 只能是文件名，不能含路径。不跟随符号链接/目录联接，不覆盖已存在目标。高风险，须审批。参数：cwd、path（绝对路径）、new_name。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""cwd"": { ""type"": ""string"", ""description"": ""工作目录绝对路径（必填）"" },
        ""path"": { ""type"": ""string"", ""description"": ""要重命名的文件绝对路径（必填，必须落在 cwd 内）"" },
        ""new_name"": { ""type"": ""string"", ""description"": ""新文件名（单个片段，不能含路径或 ..）"" }
    },
    ""required"": [""cwd"", ""path"", ""new_name""]
}";

    public RenameFileToolFunction(IContext ctx) => _ctx = ctx;

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var root = doc.RootElement;
            if (!FileToolPathOps.EnsureCwd(FileToolArgs.StringProp(root, "cwd"), out var cwd, out var cwdError))
            {
                return Fail(cwdError!);
            }

            if (!FileToolPathOps.TryBind(cwd!, FileToolArgs.StringProp(root, "path"), required: true, out var path, out var pathError))
            {
                return Fail(pathError!);
            }

            if (!FileToolPathOps.TrySingleSegment(FileToolArgs.StringProp(root, "new_name"), out var newName, out var nameError))
            {
                return Fail(nameError!);
            }

            if (!File.Exists(path))
            {
                return Fail(Directory.Exists(path) ? "rename_file 只重命名文件，不重命名目录：" + path : "文件不存在：" + path);
            }

            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent))
            {
                return Fail("无法解析父目录：" + path);
            }

            var destination = Path.Combine(parent, newName!);
            if (!FileToolPathOps.TryBind(cwd!, destination, required: true, out destination, out var destError))
            {
                return Fail(destError!);
            }

            if (string.Equals(path, destination, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("新名称与原名称相同，未重命名");
            }

            if (File.Exists(destination) || Directory.Exists(destination))
            {
                return Fail("目标已存在，拒绝覆盖：" + destination);
            }

            File.Move(path!, destination!);
            XTrace.Log.Info("[FileTools] rename_file 成功：{0} -> {1}", path, destination);
            return Task.FromResult(JsonSerializer.Serialize(new { success = true, cwd, path, newName, destination }, ForgeJson.Default));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] rename_file 执行失败: {0}", ex.Message);
            return Fail(ex.Message);
        }
    }

    private Task<string> Fail(string message) =>
        Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, message), ForgeJson.Default));
}

public class DeleteFileToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "file-tools.delete_file";
    public string Name => "delete_file";
    public string PluginId => "file-tools";
    public string Description => "删除 cwd 内的一个文件。不删除目录，不跟随、不删除符号链接/目录联接。高风险，须审批。参数：cwd、path（绝对路径）。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""cwd"": { ""type"": ""string"", ""description"": ""工作目录绝对路径（必填）"" },
        ""path"": { ""type"": ""string"", ""description"": ""要删除的文件绝对路径（必填，必须落在 cwd 内）"" }
    },
    ""required"": [""cwd"", ""path""]
}";

    public DeleteFileToolFunction(IContext ctx) => _ctx = ctx;

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var root = doc.RootElement;
            if (!FileToolPathOps.EnsureCwd(FileToolArgs.StringProp(root, "cwd"), out var cwd, out var cwdError))
            {
                return Fail(cwdError!);
            }

            if (!FileToolPathOps.TryBind(cwd!, FileToolArgs.StringProp(root, "path"), required: true, out var path, out var pathError))
            {
                return Fail(pathError!);
            }

            if (Directory.Exists(path) && !File.Exists(path))
            {
                return Fail("delete_file 只删除文件，不删除目录：" + path);
            }

            if (!File.Exists(path))
            {
                return Fail("文件不存在：" + path);
            }

            File.Delete(path!);
            XTrace.Log.Info("[FileTools] delete_file 成功：{0}", path);
            return Task.FromResult(JsonSerializer.Serialize(new { success = true, cwd, path, deleted = true }, ForgeJson.Default));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] delete_file 执行失败: {0}", ex.Message);
            return Fail(ex.Message);
        }
    }

    private Task<string> Fail(string message) =>
        Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, message), ForgeJson.Default));
}
