using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

public interface IScriptTemplateService
{
    Task<List<ScriptTemplate>> GetTemplatesAsync(string? category = null, string? keyword = null, ScriptLanguage? language = null);
    Task<ScriptTemplate?> GetTemplateByIdAsync(string templateId);
    Task<List<ScriptTemplateCategory>> GetCategoriesAsync();
}
