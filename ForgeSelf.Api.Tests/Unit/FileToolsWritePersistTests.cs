using System;
using System.IO;
using System.Text.Json;
using ForgeSelf.Api.Plugins.FileTools;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// write_file 落盘持久化守卫（PC-017 复测二次发现，2026-10-30）。
///
/// 缺陷史：坏合并修复批（2026-10-29 最小语法修复）把 <c>File.WriteAllText</c> 调用弄丢，
/// write_file 拼好内容、建好父目录后直接返回 success——报成功但零落盘（静默数据丢失）。
/// 由于此前 schema 网关层错误让一切调用必挂（PC-017 主缺陷），该缺陷从未暴露；
/// schema 修复后首次 fs 复测（写→读回读）才现形。
///
/// 不变量：write_file 返回 success ⇒ 磁盘上该路径必须已存在且内容与行区间语义一致。
/// 本守卫把「响应成功」与「磁盘事实」焊死：三个用例全部以磁盘回读为准，不信响应体。
/// </summary>
public class FileToolsWritePersistTests : IDisposable
{
    private readonly string _dir;
    private readonly WriteFileToolFunction _tool;

    public FileToolsWritePersistTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "FileToolsWrite_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _tool = new WriteFileToolFunction(Mock.Of<IContext>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private JsonElement Call(string path, string content, int startLine, int endLine)
    {
        var parameters = JsonSerializer.Serialize(new
        {
            cwd = _dir,
            path = path,
            content = content,
            start_line = startLine,
            end_line = endLine
        });
        return JsonDocument.Parse(_tool.ExecuteAsync(parameters).GetAwaiter().GetResult()).RootElement;
    }

    private static bool Success(JsonElement r) =>
        r.TryGetProperty("success", out var s) && s.GetBoolean();

    [Fact]
    public void 新建文件_响应成功_则磁盘必须有内容()
    {
        var path = Path.Combine(_dir, "create-probe.md");
        var content = "# write_file 复活探针（PC-017 修复后）";

        var response = Call(path, content, 1, 1);

        Assert.True(Success(response), "新建文件应成功：" + response.GetRawText());
        Assert.True(File.Exists(path), "响应 success 但磁盘无文件 = 静默数据丢失回归（PC-017 二次发现）");
        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void 行区间替换_磁盘内容必须等于拼接结果()
    {
        var path = Path.Combine(_dir, "replace-probe.txt");
        Assert.True(Success(Call(path, "a\nb\nc", 1, 1)));

        var response = Call(path, "B2", 2, 2);

        Assert.True(Success(response), response.GetRawText());
        Assert.Equal("a\nB2\nc", File.ReadAllText(path));
    }

    [Fact]
    public void 从文末下一行写_等价追加()
    {
        var path = Path.Combine(_dir, "append-probe.txt");
        Assert.True(Success(Call(path, "first", 1, 1)));

        var response = Call(path, "second", 2, 2);

        Assert.True(Success(response), response.GetRawText());
        Assert.Equal("first\nsecond", File.ReadAllText(path));
    }

    [Fact]
    public void 不存在文件从中间行写_必须报错而非静默成功()
    {
        var path = Path.Combine(_dir, "missing", "no-such-file.txt");

        var response = Call(path, "x", 2, 2);

        Assert.False(Success(response), "不存在文件 start_line=2 必须失败，不允许静默成功");
        Assert.False(File.Exists(path));
    }
}
