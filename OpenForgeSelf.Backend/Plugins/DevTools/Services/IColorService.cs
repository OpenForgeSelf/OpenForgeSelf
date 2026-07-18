using OpenForgeSelf.Backend.Plugins.DevTools.Models;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public interface IColorService
{
    Task<ColorConvertResult> ConvertColorAsync(ColorConvertRequest request);
    Task<ColorPaletteResult> GeneratePaletteAsync(string baseColor, int count = 5, string scheme = "analogous");
    Task<ColorContrastResult> CheckContrastAsync(string foreground, string background);
}
