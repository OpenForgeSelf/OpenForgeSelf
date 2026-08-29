using ForgeSelf.Api.Plugins.DevTools.Models;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public interface IColorService
{
    Task<ColorConvertResult> ConvertColorAsync(ColorConvertRequest request);
    Task<ColorPaletteResult> GeneratePaletteAsync(string baseColor, int count = 5, string scheme = "analogous");
    Task<ColorContrastResult> CheckContrastAsync(string foreground, string background);
}
