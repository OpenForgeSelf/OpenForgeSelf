using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services;

/// <summary>
/// <see cref="SessionEvent"/> 的多态 JSON 转换器（B2/040）。
/// </summary>
/// <remarks>
/// <see cref="SessionEvent"/> 是抽象 record，<c>System.Text.Json</c> 无内建多态支持，
/// 反序列化时无法自行决定具体 record 类型。本转换器落盘时按<b>实际派生类型</b>序列化，
/// 回读时按 JSON 里的 <c>Type</c> 字段经 <see cref="SessionEventMap.Resolve"/> 选目标类型：
/// 库里有、map 里没有的类型直接抛 <see cref="UnknownSessionEventException"/>（严禁静默丢历史）。
/// </remarks>
public sealed class SessionEventJsonConverter : JsonConverter<SessionEvent>
{
    /// <summary>带本转换器的序列化选项（持久化层唯一入口）。</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        Converters = { new SessionEventJsonConverter() },
        // 落盘/回读同一套选项，枚举以名称还是数字并不影响一致性；保持默认（数字）以最小化载荷
        WriteIndented = false,
    };

    /// <summary>只对抽象基类生效，派生类型走默认反射转换，避免自递归。</summary>
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(SessionEvent);

    /// <summary>按 <c>Type</c> 字段还原到具体 record；未知类型抛异常。</summary>
    public override SessionEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("会话事件载荷必须是 JSON 对象");
        }

        if (!root.TryGetProperty(nameof(SessionEvent.Type), out var typeProperty)
            || typeProperty.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("会话事件载荷缺少 Type 字段，无法确定具体 record 类型");
        }

        var typeName = typeProperty.GetString() ?? string.Empty;
        // 未注册 → 抛 UnknownSessionEventException（不静默跳过，防丢历史）
        var targetType = SessionEventMap.Resolve(typeName);

        var evt = (SessionEvent?)root.Deserialize(targetType, options);
        if (evt is null)
        {
            throw new JsonException($"会话事件 {typeName} 反序列化结果为空");
        }

        return evt;
    }

    /// <summary>按实际派生类型写入，保证派生字段不丢。</summary>
    public override void Write(Utf8JsonWriter writer, SessionEvent value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }
}
