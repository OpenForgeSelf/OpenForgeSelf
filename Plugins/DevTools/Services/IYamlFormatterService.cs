using ForgeSelf.Api.Plugins.DevTools.Models;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public interface IYamlFormatterService
{
    Task<string> FormatYamlAsync(string input);
    Task<ValidateResult> ValidateYamlAsync(string input);
    Task<string> ConvertYamlToJsonAsync(string input);
}
