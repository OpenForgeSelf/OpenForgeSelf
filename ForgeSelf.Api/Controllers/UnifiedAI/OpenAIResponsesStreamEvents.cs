using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Controllers.UnifiedAI;

/// <summary>
/// 把统一流分片收成 Open Responses SSE 事件。
/// AI SDK 只在 <c>response.output_item.added</c> 且 <c>item.type=message</c> 时 enqueue <c>text-start</c>（id=<c>item.id</c>）。
/// 随后 <c>response.output_text.delta</c> 用同一 id 变成 <c>text-delta</c>；没有先登记就会报 <c>text part {id} not found</c>。
/// 不发 <c>content_part.*</c> / <c>output_text.done</c>：该客户端的 zod union 没有这些 type，未知 type 会回到 invalid_union。
/// </summary>
internal static class OpenAIResponsesStreamEvents
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal sealed class Accumulator
    {
        public string MessageItemId { get; } = $"msg_{Guid.NewGuid():N}";
        public StringBuilder Text { get; } = new();
        public int Sequence { get; private set; }
        public int InputTokens { get; private set; }
        public int OutputTokens { get; private set; }
        public int TotalTokens { get; private set; }
        public bool MessageOpened { get; private set; }

        public int NextSequence() => ++Sequence;

        public void AddText(string delta) => Text.Append(delta);

        public void MarkMessageOpened() => MessageOpened = true;

        public void Observe(UnifiedStreamChunk chunk)
        {
            if (chunk.Usage == null)
                return;

            InputTokens = chunk.Usage.PromptTokens;
            OutputTokens = chunk.Usage.CompletionTokens;
            TotalTokens = chunk.Usage.TotalTokens;
        }
    }

    /// <summary>
    /// 空增量不发事件。第一条非空增量之前先发 <c>response.output_item.added</c>，item.id 与后续 delta 的 item_id 相同。
    /// </summary>
    internal static IReadOnlyList<object> TextDeltaEvents(Accumulator acc, string? delta)
    {
        if (string.IsNullOrEmpty(delta))
            return Array.Empty<object>();

        var events = new List<object>(2);
        if (!acc.MessageOpened)
        {
            acc.MarkMessageOpened();
            events.Add(new
            {
                type = "response.output_item.added",
                sequence_number = acc.NextSequence(),
                output_index = 0,
                item = new
                {
                    id = acc.MessageItemId,
                    type = "message",
                    status = "in_progress",
                    role = "assistant",
                    content = Array.Empty<object>()
                }
            });
        }

        acc.AddText(delta);
        events.Add(new
        {
            type = "response.output_text.delta",
            sequence_number = acc.NextSequence(),
            item_id = acc.MessageItemId,
            output_index = 0,
            content_index = 0,
            delta
        });
        return events;
    }

    /// <summary>有过文本增量时先发 <c>response.output_item.done</c>（AI SDK 据此 text-end），再发 completed。</summary>
    internal static IReadOnlyList<object> FinishEvents(Accumulator acc, string responseId, long created, string model)
    {
        var events = new List<object>(2);
        if (acc.MessageOpened)
        {
            events.Add(new
            {
                type = "response.output_item.done",
                sequence_number = acc.NextSequence(),
                output_index = 0,
                item = new
                {
                    id = acc.MessageItemId,
                    type = "message",
                    status = "completed",
                    role = "assistant",
                    content = new object[]
                    {
                        new
                        {
                            type = "output_text",
                            text = acc.Text.ToString(),
                            annotations = Array.Empty<object>()
                        }
                    }
                }
            });
        }

        events.Add(Completed(acc, responseId, created, model));
        return events;
    }

    internal static object Completed(Accumulator acc, string responseId, long created, string model)
    {
        return new
        {
            type = "response.completed",
            sequence_number = acc.NextSequence(),
            response = new
            {
                id = responseId,
                @object = "response",
                created_at = created,
                model,
                status = "completed",
                output = new object[]
                {
                    new
                    {
                        type = "message",
                        id = acc.MessageItemId,
                        status = "completed",
                        role = "assistant",
                        content = new object[]
                        {
                            new
                            {
                                type = "output_text",
                                text = acc.Text.ToString(),
                                annotations = Array.Empty<object>()
                            }
                        }
                    }
                },
                usage = new
                {
                    input_tokens = acc.InputTokens,
                    output_tokens = acc.OutputTokens,
                    total_tokens = acc.TotalTokens
                }
            }
        };
    }
}
