using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 「改了哪些文件」的规范化：落库统一成紧凑 JSON 数组 <c>[{"path":"…","change":"M"}]</c>。
///
/// 为什么要抽一层：三面写入口（界面表单、REST、AI 工具函数）拿到的形状天然不一致 ——
/// 一处给数组、一处给一行一个的文本、一处给 <c>A path</c> 这种 git 风格。
/// 与其让读取方各自兼容（等于把解析负担永久外包给每个消费者），不如写入时一次归一。
/// 自由文本回报解析<b>不在本批范围</b>（02-spec U-1：没有真实回报样例，不做猜测式解析）；
/// 这里只处理"人明确给了路径列表"这一种输入形状。
/// </summary>
public static class FileChangeList
{
    /// <summary>git 风格前缀：<c>M path</c> / <c>M: path</c> / <c>A path</c>（字母后允许冒号与空白混用）。</summary>
    private static readonly Regex Prefixed = new(@"^([AMDRCU?])[:\s]+(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 落库用 camelCase：出参 <see cref="ChangedFileDto"/> 也是 camelCase，
    /// 存成 PascalCase 会让 <see cref="FromStorage"/> 读回空串（实测踩过：三面写进去的记录都"看不出改了哪些文件"）。
    /// </summary>
    private static readonly JsonSerializerOptions StorageOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>结构化数组优先；数组为空时用文本（一行一个路径，可带 A/M/D 前缀）。</summary>
    public static string ToStorage(IReadOnlyList<ChangedFileDto>? items, string? text)
    {
        var list = new List<ChangedFileDto>();

        if (items is { Count: > 0 })
        {
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Path)) continue;
                list.Add(new ChangedFileDto { Path = item.Path.Trim(), Change = (item.Change ?? string.Empty).Trim().ToUpperInvariant() });
            }
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var line in text.Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                var parsed = ParseLine(line);
                if (parsed != null) list.Add(parsed);
            }
        }

        // 去重（同一路径多次出现只留最后一次的变更类型）
        var deduped = list.GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();

        return deduped.Count == 0 ? string.Empty : JsonSerializer.Serialize(deduped, StorageOpts);
    }

    /// <summary>解析存量 JSON。非法或非数组时返回空集合，并由 <see cref="ToRawText"/> 保住原文。</summary>
    public static IReadOnlyList<ChangedFileDto> FromStorage(string? storage)
    {
        if (string.IsNullOrWhiteSpace(storage)) return [];
        try
        {
            using var doc = JsonDocument.Parse(storage);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            var list = new List<ChangedFileDto>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                list.Add(new ChangedFileDto
                {
                    Path = GetString(el, "path"),
                    Change = GetString(el, "change")
                });
            }
            return list;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>给界面的兜底原文：JSON 解析不出条目时（例如历史脏数据）把原文原样交出去，不丢信息。</summary>
    public static string ToRawText(string? storage)
    {
        if (string.IsNullOrWhiteSpace(storage)) return string.Empty;
        return FromStorage(storage).Count > 0 ? string.Empty : storage.Trim();
    }

    private static ChangedFileDto? ParseLine(string line)
    {
        var text = line.Trim().Trim('`', '"', '\'');
        if (text.Length == 0) return null;

        var match = Prefixed.Match(text);
        if (match.Success)
            return new ChangedFileDto { Change = match.Groups[1].Value.ToUpperInvariant(), Path = match.Groups[2].Value.Trim() };

        return new ChangedFileDto { Path = text, Change = string.Empty };
    }

    private static string GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? string.Empty).Trim() : string.Empty;
}
