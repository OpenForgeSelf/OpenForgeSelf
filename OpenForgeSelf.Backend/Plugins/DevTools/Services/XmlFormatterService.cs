using System.Text;
using System.Xml;
using OpenForgeSelf.Backend.Plugins.DevTools.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public class XmlFormatterService : IXmlFormatterService
{
    public Task<string> FormatXmlAsync(string input, int indentSize = 2)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 格式化XML，缩进大小: {0}", indentSize);

            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(string.Empty);

            var doc = new XmlDocument();
            doc.LoadXml(input);

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
            XTrace.Log.Error("[DevTools] XML格式化失败: {0}", ex.Message);
            throw new ArgumentException("无效的XML格式: " + ex.Message, nameof(input), ex);
        }
    }

    public Task<string> MinifyXmlAsync(string input)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 压缩XML");

            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(string.Empty);

            var doc = new XmlDocument();
            doc.LoadXml(input);

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
            XTrace.Log.Error("[DevTools] XML压缩失败: {0}", ex.Message);
            throw new ArgumentException("无效的XML格式: " + ex.Message, nameof(input), ex);
        }
    }

    public Task<ValidateResult> ValidateXmlAsync(string input)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 校验XML");

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

            var doc = new XmlDocument();
            doc.LoadXml(input);

            return Task.FromResult(new ValidateResult
            {
                IsValid = true,
                ErrorMessage = null,
                LineNumber = 0,
                Position = 0
            });
        }
        catch (XmlException ex)
        {
            XTrace.Log.Warn("[DevTools] XML校验失败: {0}", ex.Message);
            return Task.FromResult(new ValidateResult
            {
                IsValid = false,
                ErrorMessage = ex.Message,
                LineNumber = ex.LineNumber,
                Position = ex.LinePosition
            });
        }
    }
}
