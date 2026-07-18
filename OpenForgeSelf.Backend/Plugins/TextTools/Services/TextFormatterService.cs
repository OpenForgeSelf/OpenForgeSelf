using System.Text;
using System.Text.Json;
using System.Xml;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.TextTools.Services;

public class TextFormatterService : ITextFormatterService
{
    public Task<string> FormatJsonAsync(string text, int indentSize = 2)
    {
        try
        {
            XTrace.Log.Debug("格式化JSON，缩进大小: {0}", indentSize);

            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(string.Empty);

            var jsonDoc = JsonDocument.Parse(text);
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
            XTrace.Log.Error("JSON格式化失败: {0}", ex.Message);
            throw new ArgumentException("无效的JSON格式: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> MinifyJsonAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("压缩JSON");

            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(string.Empty);

            var jsonDoc = JsonDocument.Parse(text);
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
            XTrace.Log.Error("JSON压缩失败: {0}", ex.Message);
            throw new ArgumentException("无效的JSON格式: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> FormatXmlAsync(string text, int indentSize = 2)
    {
        try
        {
            XTrace.Log.Debug("格式化XML，缩进大小: {0}", indentSize);

            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(string.Empty);

            var doc = new XmlDocument();
            doc.LoadXml(text);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = new string(' ', indentSize),
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
                Encoding = new UTF8Encoding(false)
            };

            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, settings))
            {
                doc.WriteTo(writer);
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            return Task.FromResult(result);
        }
        catch (XmlException ex)
        {
            XTrace.Log.Error("XML格式化失败: {0}", ex.Message);
            throw new ArgumentException("无效的XML格式: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> MinifyXmlAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("压缩XML");

            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(string.Empty);

            var doc = new XmlDocument();
            doc.LoadXml(text);

            var settings = new XmlWriterSettings
            {
                Indent = false,
                NewLineChars = string.Empty,
                NewLineHandling = NewLineHandling.None,
                Encoding = new UTF8Encoding(false)
            };

            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, settings))
            {
                doc.WriteTo(writer);
            }

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            return Task.FromResult(result);
        }
        catch (XmlException ex)
        {
            XTrace.Log.Error("XML压缩失败: {0}", ex.Message);
            throw new ArgumentException("无效的XML格式: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> FormatHtmlAsync(string text, int indentSize = 2)
    {
        try
        {
            XTrace.Log.Debug("格式化HTML，缩进大小: {0}", indentSize);

            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(string.Empty);

            var result = FormatHtml(text, indentSize);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("HTML格式化失败: {0}", ex.Message);
            throw new ArgumentException("HTML格式化失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<string> MinifyHtmlAsync(string text)
    {
        try
        {
            XTrace.Log.Debug("压缩HTML");

            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(string.Empty);

            var result = MinifyHtml(text);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("HTML压缩失败: {0}", ex.Message);
            throw new ArgumentException("HTML压缩失败: " + ex.Message, nameof(text), ex);
        }
    }

    private static string FormatHtml(string html, int indentSize)
    {
        var result = new StringBuilder();
        var currentIndent = 0;
        var i = 0;
        var lastTagWasSelfClosing = false;

        var voidElements = new HashSet<string>
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input",
            "link", "meta", "param", "source", "track", "wbr"
        };

        while (i < html.Length)
        {
            if (html[i] == '<')
            {
                var tagEnd = html.IndexOf('>', i);
                if (tagEnd == -1)
                {
                    result.Append(html.Substring(i));
                    break;
                }

                var tagContent = html.Substring(i, tagEnd - i + 1);
                var isClosingTag = tagContent.StartsWith("</");
                var isSelfClosing = tagContent.EndsWith("/>") || tagContent.EndsWith("/>");
                var isComment = tagContent.StartsWith("<!--");
                var isDoctype = tagContent.StartsWith("<!", StringComparison.OrdinalIgnoreCase);

                var tagName = string.Empty;
                if (!isComment && !isDoctype)
                {
                    var nameStart = isClosingTag ? 2 : 1;
                    var nameEnd = tagContent.IndexOfAny(new[] { ' ', '>', '/' }, nameStart);
                    if (nameEnd == -1) nameEnd = tagContent.Length - 1;
                    tagName = tagContent.Substring(nameStart, nameEnd - nameStart).ToLowerInvariant();
                }

                if (isClosingTag && !lastTagWasSelfClosing)
                {
                    currentIndent = Math.Max(0, currentIndent - 1);
                }

                if (result.Length > 0 && result[^1] != '\n')
                {
                    result.AppendLine();
                }

                result.Append(' ', indentSize * currentIndent);
                result.Append(tagContent.Trim());

                if (!isClosingTag && !isSelfClosing && !isComment && !isDoctype && !voidElements.Contains(tagName))
                {
                    currentIndent++;
                }

                lastTagWasSelfClosing = isSelfClosing || voidElements.Contains(tagName) || isClosingTag || isComment || isDoctype;
                i = tagEnd + 1;
            }
            else if (char.IsWhiteSpace(html[i]))
            {
                if (result.Length > 0 && result[^1] != '\n')
                {
                    result.Append(' ');
                }
                i++;
            }
            else
            {
                result.Append(html[i]);
                i++;
            }
        }

        return result.ToString().Trim();
    }

    private static string MinifyHtml(string html)
    {
        var result = new StringBuilder();
        var inScript = false;
        var inStyle = false;
        var i = 0;

        while (i < html.Length)
        {
            if (html[i] == '<')
            {
                var tagEnd = html.IndexOf('>', i);
                if (tagEnd == -1)
                {
                    result.Append(html.Substring(i));
                    break;
                }

                var tagContent = html.Substring(i, tagEnd - i + 1).Trim();
                var tagLower = tagContent.ToLowerInvariant();

                if (tagLower.StartsWith("<script"))
                    inScript = true;
                else if (tagLower.StartsWith("</script"))
                    inScript = false;
                else if (tagLower.StartsWith("<style"))
                    inStyle = true;
                else if (tagLower.StartsWith("</style"))
                    inStyle = false;

                if (result.Length > 0 && result[^1] == ' ')
                {
                    result.Length--;
                }

                result.Append(tagContent);
                i = tagEnd + 1;
            }
            else if (char.IsWhiteSpace(html[i]))
            {
                if (inScript || inStyle)
                {
                    result.Append(html[i]);
                }
                else if (result.Length > 0 && result[^1] != ' ' && result[^1] != '>')
                {
                    result.Append(' ');
                }
                i++;
            }
            else
            {
                result.Append(html[i]);
                i++;
            }
        }

        return result.ToString().Trim();
    }
}
