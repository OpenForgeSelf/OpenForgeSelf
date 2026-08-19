namespace OpenForgeSelf.Abstractions;

public interface IScriptTemplateService
{
    Task<List<ScriptTemplate>> GetTemplatesAsync(string? category = null, string? keyword = null, ScriptLanguage? language = null);
    Task<ScriptTemplate?> GetTemplateByIdAsync(string templateId);
    Task<List<ScriptTemplateCategory>> GetCategoriesAsync();
}
