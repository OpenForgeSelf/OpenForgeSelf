using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.DevTools.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public class YamlFormatterService : IYamlFormatterService
{
    public Task<string> FormatYamlAsync(string input)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 格式化YAML");

            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(string.Empty);

            var json = ConvertYamlToJson(input);
            var yaml = ReformatYamlFromJson(json);
            return Task.FromResult(yaml);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] YAML格式化失败: {0}", ex.Message);
            throw new ArgumentException("YAML格式化失败: " + ex.Message, nameof(input), ex);
        }
    }

    public Task<ValidateResult> ValidateYamlAsync(string input)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 校验YAML");

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

            var json = ConvertYamlToJson(input);
            JsonDocument.Parse(json);

            return Task.FromResult(new ValidateResult
            {
                IsValid = true,
                ErrorMessage = null,
                LineNumber = 0,
                Position = 0
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[DevTools] YAML校验失败: {0}", ex.Message);
            return Task.FromResult(new ValidateResult
            {
                IsValid = false,
                ErrorMessage = ex.Message,
                LineNumber = 0,
                Position = 0
            });
        }
    }

    public Task<string> ConvertYamlToJsonAsync(string input)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] YAML转JSON");

            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(string.Empty);

            var json = ConvertYamlToJson(input);
            var jsonDoc = JsonDocument.Parse(json);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                jsonDoc.WriteTo(writer);
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return Task.FromResult(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] YAML转JSON失败: {0}", ex.Message);
            throw new ArgumentException("YAML转JSON失败: " + ex.Message, nameof(input), ex);
        }
    }

    private static string ConvertYamlToJson(string yaml)
    {
        var lines = yaml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var result = ParseYamlLines(lines, 0, 0);
        return JsonSerializer.Serialize(result.Value);
    }

    private static (object? Value, int NextLine) ParseYamlLines(string[] lines, int startLine, int baseIndent)
    {
        if (startLine >= lines.Length)
            return (null, startLine);

        var firstLine = lines[startLine];
        var firstIndent = GetIndentLevel(firstLine);

        if (firstIndent < baseIndent)
            return (null, startLine);

        var trimmed = firstLine.TrimStart();

        if (trimmed.StartsWith("- "))
        {
            var list = new List<object?>();
            var currentLine = startLine;

            while (currentLine < lines.Length)
            {
                var line = lines[currentLine];
                var indent = GetIndentLevel(line);
                var lineTrimmed = line.TrimStart();

                if (indent < baseIndent || !lineTrimmed.StartsWith("- "))
                    break;

                var valueStr = lineTrimmed.Substring(2);

                if (string.IsNullOrEmpty(valueStr) || valueStr.Trim() == "|")
                {
                    var nestedIndent = indent + 2;
                    var (nestedValue, nextLine) = ParseYamlLines(lines, currentLine + 1, nestedIndent);
                    list.Add(nestedValue);
                    currentLine = nextLine;
                }
                else if (IsKeyValuePair(valueStr))
                {
                    var (key, value) = ParseKeyValue(valueStr);
                    var dict = new Dictionary<string, object?>
                    {
                        [key] = ParseScalarValue(value)
                    };

                    var nestedIndent = indent + 2;
                    currentLine++;
                    while (currentLine < lines.Length)
                    {
                        var nextLine = lines[currentLine];
                        var nextIndent = GetIndentLevel(nextLine);
                        if (nextIndent < nestedIndent)
                            break;
                        if (nextIndent == nestedIndent && nextLine.TrimStart().StartsWith("- "))
                            break;

                        var (nestedDict, nextLineIdx) = ParseDictLines(lines, currentLine, nestedIndent);
                        foreach (var kvp in nestedDict)
                        {
                            dict[kvp.Key] = kvp.Value;
                        }
                        currentLine = nextLineIdx;
                    }

                    list.Add(dict);
                }
                else
                {
                    list.Add(ParseScalarValue(valueStr));
                    currentLine++;
                }
            }

            return (list, currentLine);
        }
        else if (IsKeyValuePair(trimmed))
        {
            var (dict, nextLine) = ParseDictLines(lines, startLine, baseIndent);
            return (dict, nextLine);
        }
        else
        {
            return (ParseScalarValue(trimmed), startLine + 1);
        }
    }

    private static (Dictionary<string, object?> Dict, int NextLine) ParseDictLines(string[] lines, int startLine, int baseIndent)
    {
        var dict = new Dictionary<string, object?>();
        var currentLine = startLine;

        while (currentLine < lines.Length)
        {
            var line = lines[currentLine];
            var indent = GetIndentLevel(line);
            var trimmed = line.TrimStart();

            if (indent < baseIndent)
                break;

            if (indent > baseIndent)
            {
                currentLine++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
            {
                currentLine++;
                continue;
            }

            if (trimmed.StartsWith("- "))
                break;

            if (IsKeyValuePair(trimmed))
            {
                var (key, value) = ParseKeyValue(trimmed);

                if (string.IsNullOrEmpty(value) || value.Trim() == "|" || value.Trim() == ">")
                {
                    var nestedIndent = baseIndent + 2;
                    var (nestedValue, nextLine) = ParseYamlLines(lines, currentLine + 1, nestedIndent);
                    dict[key] = nestedValue;
                    currentLine = nextLine;
                }
                else
                {
                    dict[key] = ParseScalarValue(value);
                    currentLine++;
                }
            }
            else
            {
                currentLine++;
            }
        }

        return (dict, currentLine);
    }

    private static int GetIndentLevel(string line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c == ' ')
                count++;
            else if (c == '\t')
                count += 2;
            else
                break;
        }
        return count;
    }

    private static bool IsKeyValuePair(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex <= 0)
            return false;

        var beforeColon = line.Substring(0, colonIndex).Trim();
        if (string.IsNullOrEmpty(beforeColon))
            return false;

        if (beforeColon.StartsWith("\"") && beforeColon.EndsWith("\""))
            return true;
        if (beforeColon.StartsWith("'") && beforeColon.EndsWith("'"))
            return true;

        return !beforeColon.Contains(' ');
    }

    private static (string Key, string Value) ParseKeyValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        var key = line.Substring(0, colonIndex).Trim();
        var value = line.Substring(colonIndex + 1).Trim();

        if ((key.StartsWith("\"") && key.EndsWith("\"")) || (key.StartsWith("'") && key.EndsWith("'")))
        {
            key = key.Substring(1, key.Length - 2);
        }

        return (key, value);
    }

    private static object? ParseScalarValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (value == "null" || value == "~" || value == "Null" || value == "NULL")
            return null;

        if (value == "true" || value == "True" || value == "TRUE")
            return true;

        if (value == "false" || value == "False" || value == "FALSE")
            return false;

        if ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'")))
        {
            return value.Substring(1, value.Length - 2);
        }

        if (int.TryParse(value, out var intVal))
            return intVal;

        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
        {
            if (doubleVal.ToString(System.Globalization.CultureInfo.InvariantCulture) == value ||
                value.Contains('.') || value.Contains('e') || value.Contains('E'))
                return doubleVal;
        }

        return value;
    }

    private static string ReformatYamlFromJson(string json)
    {
        var jsonDoc = JsonDocument.Parse(json);
        return ConvertToYaml(jsonDoc.RootElement, 0).TrimEnd();
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
                        var childYaml = ConvertToYaml(value, indentLevel + 1);
                        if (value.ValueKind == JsonValueKind.Array)
                        {
                            sb.AppendLine($"{indent}{prop.Name}:");
                            sb.Append(childYaml);
                        }
                        else
                        {
                            sb.AppendLine($"{indent}{prop.Name}:");
                            sb.Append(childYaml);
                        }
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
                        sb.AppendLine($"{indent}-");
                        var childYaml = ConvertToYaml(item, indentLevel + 1);
                        var lines = childYaml.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        var childIndent = new string(' ', (indentLevel + 1) * 2);
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
}
