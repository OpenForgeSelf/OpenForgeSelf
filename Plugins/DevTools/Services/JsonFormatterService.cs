using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.DevTools.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public class JsonFormatterService : IJsonFormatterService
{
    public Task<string> FormatJsonAsync(string input, int indentSize = 2)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 格式化JSON，缩进大小: {0}", indentSize);

            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(string.Empty);

            var jsonDoc = JsonDocument.Parse(input);
            var options = new JsonWriterOptions
            {
                Indented = true,
                IndentCharacter = ' ',
                IndentSize = indentSize
            };

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, options))
            {
                jsonDoc.WriteTo(writer);
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            return Task.FromResult(result);
        }
        catch (JsonException ex)
        {
            XTrace.Log.Error("[DevTools] JSON格式化失败: {0}", ex.Message);
            throw new ArgumentException("无效的JSON格式: " + ex.Message, nameof(input), ex);
        }
    }

    public Task<string> MinifyJsonAsync(string input)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 压缩JSON");

            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(string.Empty);

            var jsonDoc = JsonDocument.Parse(input);
            var options = new JsonWriterOptions
            {
                Indented = false
            };

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, options))
            {
                jsonDoc.WriteTo(writer);
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            return Task.FromResult(result);
        }
        catch (JsonException ex)
        {
            XTrace.Log.Error("[DevTools] JSON压缩失败: {0}", ex.Message);
            throw new ArgumentException("无效的JSON格式: " + ex.Message, nameof(input), ex);
        }
    }

    public Task<ValidateResult> ValidateJsonAsync(string input)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 校验JSON");

            if (string.IsNullOrWhiteSpace(input))
            {
                return Task.FromResult(new ValidateResult
                {
                    IsValid = false,
                    ErrorMessage = "输入不能为空",
                    LineNumber = 0,
                    Position = 0
                });
            }

            JsonDocument.Parse(input);

            return Task.FromResult(new ValidateResult
            {
                IsValid = true,
                ErrorMessage = null,
                LineNumber = 0,
                Position = 0
            });
        }
        catch (JsonException ex)
        {
            XTrace.Log.Warn("[DevTools] JSON校验失败: {0}", ex.Message);
            return Task.FromResult(new ValidateResult
            {
                IsValid = false,
                ErrorMessage = ex.Message,
                LineNumber = (int)(ex.LineNumber ?? 0),
                Position = (int)(ex.BytePositionInLine ?? 0)
            });
        }
    }

    public Task<string> JsonPathQueryAsync(string input, string expression)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] JSONPath查询: {0}", expression);

            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(string.Empty);

            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException("JSONPath表达式不能为空", nameof(expression));

            var jsonDoc = JsonDocument.Parse(input);
            var results = ExecuteJsonPath(jsonDoc.RootElement, expression);

            if (results.Count == 0)
                return Task.FromResult(string.Empty);

            if (results.Count == 1)
            {
                return Task.FromResult(results[0].GetRawText());
            }

            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < results.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(results[i].GetRawText());
            }
            sb.Append(']');

            return Task.FromResult(FormatJson(sb.ToString()));
        }
        catch (JsonException ex)
        {
            XTrace.Log.Error("[DevTools] JSONPath查询失败: {0}", ex.Message);
            throw new ArgumentException("无效的JSON格式: " + ex.Message, nameof(input), ex);
        }
    }

    private static List<JsonElement> ExecuteJsonPath(JsonElement root, string expression)
    {
        var results = new List<JsonElement>();
        var paths = expression.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (expression.StartsWith("$."))
        {
            paths = expression.Substring(2).Split('.', StringSplitOptions.RemoveEmptyEntries);
        }
        else if (expression.StartsWith("$"))
        {
            if (expression.Length == 1)
            {
                results.Add(root);
                return results;
            }
            paths = expression.Substring(1).TrimStart('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
        }

        var currentElements = new List<JsonElement> { root };

        foreach (var path in paths)
        {
            var nextElements = new List<JsonElement>();
            var pathClean = path.Trim();

            if (pathClean == "*")
            {
                foreach (var elem in currentElements)
                {
                    if (elem.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in elem.EnumerateObject())
                        {
                            nextElements.Add(prop.Value);
                        }
                    }
                    else if (elem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in elem.EnumerateArray())
                        {
                            nextElements.Add(item);
                        }
                    }
                }
            }
            else if (pathClean.StartsWith('[') && pathClean.EndsWith(']'))
            {
                var indexStr = pathClean.Substring(1, pathClean.Length - 2);
                foreach (var elem in currentElements)
                {
                    if (elem.ValueKind == JsonValueKind.Array)
                    {
                        if (indexStr == "*")
                        {
                            foreach (var item in elem.EnumerateArray())
                            {
                                nextElements.Add(item);
                            }
                        }
                        else if (int.TryParse(indexStr, out var index))
                        {
                            var arr = elem.EnumerateArray().ToList();
                            if (index >= 0 && index < arr.Count)
                            {
                                nextElements.Add(arr[index]);
                            }
                        }
                    }
                }
            }
            else
            {
                var propName = pathClean;
                foreach (var elem in currentElements)
                {
                    if (elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty(propName, out var value))
                    {
                        nextElements.Add(value);
                    }
                }
            }

            currentElements = nextElements;
            if (currentElements.Count == 0)
                break;
        }

        results.AddRange(currentElements);
        return results;
    }

    public Task<string> ConvertJsonToYamlAsync(string input)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] JSON转YAML");

            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(string.Empty);

            var jsonDoc = JsonDocument.Parse(input);
            var yaml = ConvertToYaml(jsonDoc.RootElement, 0);
            return Task.FromResult(yaml.TrimEnd());
        }
        catch (JsonException ex)
        {
            XTrace.Log.Error("[DevTools] JSON转YAML失败: {0}", ex.Message);
            throw new ArgumentException("无效的JSON格式: " + ex.Message, nameof(input), ex);
        }
    }

    private static string ConvertToYaml(JsonElement element, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 2);
        var sb = new StringBuilder();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var props = element.EnumerateObject().ToList();
                if (props.Count == 0)
                {
                    sb.Append("{}");
                    return sb.ToString();
                }
                foreach (var prop in props)
                {
                    var value = prop.Value;
                    if (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.Array)
                    {
                        sb.AppendLine($"{indent}{prop.Name}:");
                        sb.Append(ConvertToYaml(value, indentLevel + 1));
                    }
                    else
                    {
                        var valStr = GetYamlScalarValue(value);
                        sb.AppendLine($"{indent}{prop.Name}: {valStr}");
                    }
                }
                break;

            case JsonValueKind.Array:
                var items = element.EnumerateArray().ToList();
                if (items.Count == 0)
                {
                    sb.Append("[]");
                    return sb.ToString();
                }
                foreach (var item in items)
                {
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        var childIndent = indent + "  ";
                        sb.AppendLine($"{indent}-");
                        var childYaml = ConvertToYaml(item, indentLevel + 1);
                        var lines = childYaml.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            sb.AppendLine(childIndent + line.TrimStart());
                        }
                    }
                    else
                    {
                        var valStr = GetYamlScalarValue(item);
                        sb.AppendLine($"{indent}- {valStr}");
                    }
                }
                break;

            default:
                sb.Append(GetYamlScalarValue(element));
                break;
        }

        return sb.ToString();
    }

    private static string GetYamlScalarValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var str = element.GetString() ?? string.Empty;
                if (string.IsNullOrEmpty(str) || str.Contains(':') || str.Contains('#') || 
                    str.StartsWith(" ") || str.EndsWith(" ") || str.Contains('\n') ||
                    str == "true" || str == "false" || str == "null" ||
                    Regex.IsMatch(str, @"^-?\d+(\.\d+)?$"))
                {
                    return "\"" + str.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                }
                return str;
            case JsonValueKind.Number:
                return element.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
                return "null";
            default:
                return element.GetRawText();
        }
    }

    private static string FormatJson(string json)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                jsonDoc.WriteTo(writer);
            }
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return json;
        }
    }
}
