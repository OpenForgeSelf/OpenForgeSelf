using OpenForgeSelf.Backend.Plugins.DevTools.Models;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public interface IXmlFormatterService
{
    Task<string> FormatXmlAsync(string input, int indentSize = 2);
    Task<string> MinifyXmlAsync(string input);
    Task<ValidateResult> ValidateXmlAsync(string input);
}
