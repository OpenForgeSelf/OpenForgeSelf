using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers.UnifiedAI;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 出站 <c>POST /v1/responses</c> 流式事件必须能过 Open Responses / AI SDK。
/// 不变量：每条 <c>response.output_text.delta</c> 都带非空 <c>item_id</c>；
/// 第一条真实增量之前必须已发 <c>response.output_item.added</c>（item.type=message，item.id 与 item_id 相同），
/// 结束时发同一 id 的 <c>response.output_item.done</c>。不发 <c>content_part.*</c>。
/// </summary>
public class OpenAIResponsesOutboundSchemaTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
            dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("未找到仓库根（锚点 ForgeSelf.slnx）");
        return dir.FullName;
    }

    [Fact]
    public void Production_OutputTextDelta_Sites_DeclareItemId()
    {
        var apiRoot = Path.Combine(RepoRoot(), "ForgeSelf.Api");
        var sites = new List<(string File, int Line, string Window)>();
        foreach (var file in Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///"))
                    continue;
                if (!lines[i].Contains("\"response.output_text.delta\"", StringComparison.Ordinal))
                    continue;

                var window = string.Join('\n', lines.Skip(i).Take(14));
                sites.Add((file, i + 1, window));
            }
        }

        sites.Should().NotBeEmpty("生产代码必须有出站 response.output_text.delta；扫不到说明守卫失效");
        var missing = sites.Where(s => !s.Window.Contains("item_id", StringComparison.Ordinal)).ToList();
        missing.Should().BeEmpty(
            "每条出站 response.output_text.delta 的初始化块都必须声明 item_id。缺的位置: "
            + string.Join("; ", missing.Select(s => $"{s.File}:{s.Line}")));

        var files = sites.Select(s => s.File).Distinct();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            text.Should().Contain("\"response.output_item.added\"", $"{file} 发 delta 之前必须能发出登记 text part 的 output_item.added");
            text.Should().Contain("\"response.output_item.done\"");
            text.Should().NotContain("\"response.content_part.added\"", "该客户端 zod 不接受 content_part.added");
        }
    }

    [Fact]
    public void TextDelta_OmitsEmptyPlaceholder_AndSerializesItemId()
    {
        var acc = new OpenAIResponsesStreamEvents.Accumulator();

        OpenAIResponsesStreamEvents.TextDeltaEvents(acc, "").Should().BeEmpty();
        OpenAIResponsesStreamEvents.TextDeltaEvents(acc, null).Should().BeEmpty();

        var first = OpenAIResponsesStreamEvents.TextDeltaEvents(acc, "你");
        var second = OpenAIResponsesStreamEvents.TextDeltaEvents(acc, "好");
        first.Should().HaveCount(2);
        second.Should().HaveCount(1);

        using var added = JsonDocument.Parse(JsonSerializer.Serialize(first[0], OpenAIResponsesStreamEvents.SerializerOptions));
        using var doc1 = JsonDocument.Parse(JsonSerializer.Serialize(first[1], OpenAIResponsesStreamEvents.SerializerOptions));
        using var doc2 = JsonDocument.Parse(JsonSerializer.Serialize(second[0], OpenAIResponsesStreamEvents.SerializerOptions));

        added.RootElement.GetProperty("type").GetString().Should().Be("response.output_item.added");
        added.RootElement.GetProperty("sequence_number").GetInt32().Should().Be(1);
        added.RootElement.GetProperty("output_index").GetInt32().Should().Be(0);
        var item = added.RootElement.GetProperty("item");
        item.GetProperty("type").GetString().Should().Be("message");
        item.GetProperty("role").GetString().Should().Be("assistant");
        item.GetProperty("status").GetString().Should().Be("in_progress");
        item.GetProperty("id").GetString().Should().Be(acc.MessageItemId);
        item.GetProperty("content").GetArrayLength().Should().Be(0);

        AssertDelta(doc1.RootElement, acc.MessageItemId, "你", 2);
        AssertDelta(doc2.RootElement, acc.MessageItemId, "好", 3);
        doc1.RootElement.GetProperty("item_id").GetString().Should().Be(item.GetProperty("id").GetString());
    }

    [Fact]
    public void Completed_ReusesItemId_TextAndUsage()
    {
        var acc = new OpenAIResponsesStreamEvents.Accumulator();
        OpenAIResponsesStreamEvents.TextDeltaEvents(acc, "你");
        OpenAIResponsesStreamEvents.TextDeltaEvents(acc, "好");
        acc.Observe(new UnifiedStreamChunk
        {
            Usage = new UnifiedUsage { PromptTokens = 18, CompletionTokens = 3, TotalTokens = 21 }
        });

        var finish = OpenAIResponsesStreamEvents.FinishEvents(acc, "resp_test", 100, "deepseek-v4-flash");
        finish.Should().HaveCount(2);

        using var doneDoc = JsonDocument.Parse(JsonSerializer.Serialize(finish[0], OpenAIResponsesStreamEvents.SerializerOptions));
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(finish[1], OpenAIResponsesStreamEvents.SerializerOptions));
        var done = doneDoc.RootElement;
        done.GetProperty("type").GetString().Should().Be("response.output_item.done");
        done.GetProperty("sequence_number").GetInt32().Should().Be(4);
        var doneItem = done.GetProperty("item");
        doneItem.GetProperty("id").GetString().Should().Be(acc.MessageItemId);
        doneItem.GetProperty("type").GetString().Should().Be("message");
        doneItem.GetProperty("content")[0].GetProperty("text").GetString().Should().Be("你好");

        var root = doc.RootElement;
        root.GetProperty("type").GetString().Should().Be("response.completed");
        root.GetProperty("sequence_number").GetInt32().Should().Be(5);
        var response = root.GetProperty("response");
        response.GetProperty("id").GetString().Should().Be("resp_test");
        response.GetProperty("status").GetString().Should().Be("completed");
        var usage = response.GetProperty("usage");
        usage.GetProperty("input_tokens").GetInt32().Should().Be(18);
        usage.GetProperty("output_tokens").GetInt32().Should().Be(3);
        usage.GetProperty("total_tokens").GetInt32().Should().Be(21);

        var item = response.GetProperty("output")[0];
        item.GetProperty("id").GetString().Should().Be(acc.MessageItemId);
        item.GetProperty("content")[0].GetProperty("text").GetString().Should().Be("你好");
        item.GetProperty("content")[0].GetProperty("annotations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void Finish_WithoutText_DoesNotOpenMessageItem()
    {
        var acc = new OpenAIResponsesStreamEvents.Accumulator();
        var finish = OpenAIResponsesStreamEvents.FinishEvents(acc, "resp_empty", 100, "m");
        finish.Should().HaveCount(1);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(finish[0], OpenAIResponsesStreamEvents.SerializerOptions));
        doc.RootElement.GetProperty("type").GetString().Should().Be("response.completed");
        doc.RootElement.GetProperty("sequence_number").GetInt32().Should().Be(1);
        var types = doc.RootElement.GetProperty("type").GetString();
        types.Should().NotContain("content_part");
    }

    private static void AssertDelta(JsonElement root, string itemId, string delta, int sequence)
    {
        root.GetProperty("type").GetString().Should().Be("response.output_text.delta");
        root.GetProperty("item_id").GetString().Should().Be(itemId);
        root.GetProperty("item_id").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("output_index").GetInt32().Should().Be(0);
        root.GetProperty("content_index").GetInt32().Should().Be(0);
        root.GetProperty("delta").GetString().Should().Be(delta);
        root.GetProperty("sequence_number").GetInt32().Should().Be(sequence);
    }
}
