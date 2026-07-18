using OpenForgeSelf.Backend.Plugins.DevTools.Models;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public interface IYamlFormatterService
{
    Task<string> FormatYamlAsync(string input);
    Task<ValidateResult> ValidateYamlAsync(string input);
    Task<string> ConvertYamlToJsonAsync(string input);
}
