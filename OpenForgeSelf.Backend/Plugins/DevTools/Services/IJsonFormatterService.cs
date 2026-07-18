using OpenForgeSelf.Backend.Plugins.DevTools.Models;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public interface IJsonFormatterService
{
    Task<string> FormatJsonAsync(string input, int indentSize = 2);
    Task<string> MinifyJsonAsync(string input);
    Task<ValidateResult> ValidateJsonAsync(string input);
    Task<string> JsonPathQueryAsync(string input, string expression);
    Task<string> ConvertJsonToYamlAsync(string input);
}
