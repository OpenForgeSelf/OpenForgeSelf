using OpenForgeSelf.Backend.Plugins.TextTools.Models;

namespace OpenForgeSelf.Backend.Plugins.TextTools.Services;

public interface ITextStatsService
{
    Task<TextStatsResult> GetStatsAsync(string text);
}
