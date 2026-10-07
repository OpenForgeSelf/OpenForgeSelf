using System.Text;

namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>
/// 文件类工具的真实执行（PILOT-053 02-spec FR-3.1~3.3、BC-4/5）。
/// 所有路径先过 <see cref="SandboxRoot.TryResolve"/>，越界即拒且**不碰文件系统**。
/// </summary>
public static class FileExecutor
{
    private static readonly Encoding NoBomUtf8 = new UTF8Encoding(false);

    public static ToolResult Read(SandboxRoot sandbox, string? relative)
    {
        var start = Environment.TickCount64;
        if (!sandbox.TryResolve(relative, out var full, out var pathError))
        {
            return Rejected(sandbox, relative, "read_file", "outside_workspace", pathError, start);
        }

        if (Directory.Exists(full))
        {
            return Rejected(sandbox, relative, "read_file", "is_a_directory", "这是一个目录，请改用 list_dir", start);
        }

        if (!File.Exists(full))
        {
            // BC-5：绝不返回空内容冒充"文件是空的"——AI 会误读。
            return Rejected(sandbox, relative, "read_file", "not_found", $"文件不存在：{sandbox.ToRelative(full)}", start);
        }

        try
        {
            var info = new FileInfo(full);
            var limit = ToolSpec.MaxFileBytes;
            using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[limit + 1];
            var read = stream.Read(buffer, 0, buffer.Length);
            var truncated = read > limit;
            var byteLength = Math.Min(read, limit);
            var bytes = new byte[byteLength];
            Array.Copy(buffer, bytes, byteLength);

            var text = Decode(bytes, out var bomStripped);
            var binarySuspect = LooksBinary(text);

            var payload = new JsonObject
            {
                ["path"] = sandbox.ToRelative(full),
                ["content"] = text,
                ["bytes"] = info.Length,
                ["encoding"] = bomStripped ? "utf-8-bom" : "utf-8"
            };
            if (binarySuspect) payload["binarySuspect"] = true;
            if (truncated) payload["truncatedNote"] = $"已截断，原长 {info.Length} 字节（上限 {limit}）";

            return new ToolResult
            {
                Tool = ToolSpec.ReadFile,
                Ok = true,
                Args = new Dictionary<string, JsonNode> { ["path"] = JsonValue.Create(relative ?? string.Empty)! },
                Result = payload,
                Truncated = truncated,
                OriginalBytes = info.Length,
                DurationMs = Environment.TickCount64 - start
            };
        }
        catch (Exception ex)
        {
            return Rejected(sandbox, relative, "read_file", "read_failed", ex.Message, start);
        }
    }

    public static ToolResult Write(SandboxRoot sandbox, string? relative, string content, bool append)
    {
        var start = Environment.TickCount64;
        if (!sandbox.TryResolve(relative, out var full, out var pathError))
        {
            return Rejected(sandbox, relative, "write_file", "outside_workspace", pathError, start);
        }

        if (Directory.Exists(full))
        {
            return Rejected(sandbox, relative, "write_file", "is_a_directory", "目标是一个目录，不能作为文件写入", start);
        }

        var bytes = Encoding.UTF8.GetByteCount(content);
        if (bytes > ToolSpec.MaxFileBytes)
        {
            return Rejected(sandbox, relative, "write_file", "content_too_large",
                $"content {bytes} 字节，超过上限 {ToolSpec.MaxFileBytes}", start);
        }

        try
        {
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (append)
            {
                using var appendStream = new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.Read);
                appendStream.Write(NoBomUtf8.GetBytes(content));
            }
            else
            {
                // 不带 BOM：AI 之后 read_file 回来的应当与它写进去的逐字节一致（AC9 plain 同口径）。
                File.WriteAllText(full, content, NoBomUtf8);
            }

            var info = new FileInfo(full);
            var payload = new JsonObject
            {
                ["path"] = sandbox.ToRelative(full),
                ["bytesWritten"] = bytes,
                ["totalBytes"] = info.Length,
                ["append"] = append,
                ["created"] = !append
            };

            return new ToolResult
            {
                Tool = ToolSpec.WriteFile,
                Ok = true,
                Args = new Dictionary<string, JsonNode>
                {
                    ["path"] = JsonValue.Create(relative ?? string.Empty)!,
                    ["append"] = JsonValue.Create(append)!
                },
                Result = payload,
                DurationMs = Environment.TickCount64 - start
            };
        }
        catch (Exception ex)
        {
            return Rejected(sandbox, relative, "write_file", "write_failed", ex.Message, start);
        }
    }

    public static ToolResult ListDir(SandboxRoot sandbox, string? relative)
    {
        var start = Environment.TickCount64;
        if (!sandbox.TryResolve(relative, out var full, out var pathError))
        {
            return Rejected(sandbox, relative, "list_dir", "outside_workspace", pathError, start);
        }

        if (!Directory.Exists(full))
        {
            return Rejected(sandbox, relative, "list_dir", "not_found", $"目录不存在：{sandbox.ToRelative(full)}", start);
        }

        try
        {
            var dirs = Directory.EnumerateDirectories(full).Select(p => new DirectoryInfo(p)).ToList();
            var files = Directory.EnumerateFiles(full).Select(p => new FileInfo(p)).ToList();

            var entries = dirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => (JsonNode)new JsonObject
                {
                    ["name"] = d.Name,
                    ["isDir"] = true,
                    ["size"] = (long?)null,
                    ["relativePath"] = Combine(sandbox.ToRelative(full), d.Name)
                })
                .Concat(files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => (JsonNode)new JsonObject
                    {
                        ["name"] = f.Name,
                        ["isDir"] = false,
                        ["size"] = f.Length,
                        ["relativePath"] = Combine(sandbox.ToRelative(full), f.Name)
                    }))
                .ToList();

            var truncated = entries.Count > ToolSpec.MaxListEntries;
            var kept = truncated ? entries.GetRange(0, ToolSpec.MaxListEntries) : entries;

            var payload = new JsonObject
            {
                ["path"] = sandbox.ToRelative(full),
                ["count"] = kept.Count,
                ["entries"] = new JsonArray(kept.ToArray())
            };
            if (truncated) payload["truncatedNote"] = $"条目超过 {ToolSpec.MaxListEntries}，已截断";

            return new ToolResult
            {
                Tool = ToolSpec.ListDir,
                Ok = true,
                Args = new Dictionary<string, JsonNode> { ["path"] = JsonValue.Create(relative ?? string.Empty)! },
                Result = payload,
                Truncated = truncated,
                DurationMs = Environment.TickCount64 - start
            };
        }
        catch (Exception ex)
        {
            return Rejected(sandbox, relative, "list_dir", "list_failed", ex.Message, start);
        }
    }

    private static string Combine(string parent, string name) =>
        string.IsNullOrEmpty(parent) || parent == "." ? name : parent.TrimEnd('/') + "/" + name;

    private static string Decode(byte[] bytes, out bool bomStripped)
    {
        bomStripped = false;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            bomStripped = true;
            return NoBomUtf8.GetString(bytes, 3, bytes.Length - 3);
        }
        return NoBomUtf8.GetString(bytes);
    }

    /// <summary>BC-4：不猜编码，检出 NUL 或大量替换符/控制字符时显式标 binarySuspect。</summary>
    private static bool LooksBinary(string text)
    {
        if (text.Length == 0) return false;
        var sample = text.Length > 4096 ? text[..4096] : text;
        var bad = 0;
        foreach (var ch in sample)
        {
            if (ch == '\0') return true;
            if (ch == '\uFFFD' || (char.IsControl(ch) && ch is not ('\n' or '\r' or '\t'))) bad++;
        }
        return bad * 20 > sample.Length; // 超过 5% 视为可疑
    }

    private static ToolResult Rejected(SandboxRoot sandbox, string? relative, string tool, string error, string? reason, long start) => new()
    {
        Tool = tool,
        Ok = false,
        Args = new Dictionary<string, JsonNode> { ["path"] = JsonValue.Create(relative ?? string.Empty)! },
        Error = error,
        Reason = reason,
        DurationMs = Environment.TickCount64 - start
    };
}
