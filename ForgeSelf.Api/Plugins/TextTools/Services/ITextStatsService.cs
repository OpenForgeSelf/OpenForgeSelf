using ForgeSelf.Api.Plugins.TextTools.Models;

namespace ForgeSelf.Api.Plugins.TextTools.Services;

public interface ITextStatsService
{
    Task<TextStatsResult> GetStatsAsync(string text);
}
