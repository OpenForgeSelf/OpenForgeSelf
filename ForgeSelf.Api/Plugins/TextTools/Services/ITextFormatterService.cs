namespace ForgeSelf.Api.Plugins.TextTools.Services;

public interface ITextFormatterService
{
    Task<string> FormatJsonAsync(string text, int indentSize = 2);
    Task<string> MinifyJsonAsync(string text);
    Task<string> FormatXmlAsync(string text, int indentSize = 2);
    Task<string> MinifyXmlAsync(string text);
    Task<string> FormatHtmlAsync(string text, int indentSize = 2);
    Task<string> MinifyHtmlAsync(string text);
}
