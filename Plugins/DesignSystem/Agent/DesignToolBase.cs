using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Abstractions;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Agent;

/// <summary>design_* 工具的公共行为：{success,data|error} 封套（JSON 字符串进出，宿主 ToolRegistry 约定）、异常兜底、结果体积上限、写动作与写开关拦截、项目解析。</summary>
public abstract class DesignToolBase : IToolFunctionExtension
{
    public const Int32 MaxResultChars = 120_000;

    protected DesignToolBase(DesignToolKit kit)
    {
        Kit = kit;
    }

    public DesignToolKit Kit { get; }

    public abstract String Id { get; }
    public abstract String Name { get; }
    public abstract String PluginId { get; }
    public abstract String Description { get; }
    public abstract String ParametersJsonSchema { get; }

    /// <summary>工具的"写"判定（§A）：create apply=true、audit run=true、edit set_token/publish/regenerate+apply=true。</summary>
    public abstract Boolean IsWrite(JsonElement args);

    /// <summary>执行入口（宿主约定）：参数 JSON 字符串 → 结果 JSON 字符串。任何异常转 {success:false,error}，不抛给转发层。
    /// 序列化统一 camelCase（03-plan §B 出参键名即契约，全部小写）。</summary>
    public async Task<string> ExecuteAsync(string parameters)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        try
        {
            JsonElement args;
            using (var doc = parameters.IsNullOrWhiteSpace() ? JsonDocument.Parse("{}") : JsonDocument.Parse(parameters))
                args = doc.RootElement.Clone();

            if (IsWrite(args))
            {
                var (allow, source, corrupt, _) = Kit.AgentAccess.Get();
                if (!allow)
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "外部写入已被关闭（设计系统 › 接入 › 写入开关，或 PUT api/design-system/agent-access）。只读能力不受影响。",
                        hint = "可先用 apply=false 或只读动作预演。",
                        source,
                        corrupt,
                    }, options);
            }

            var data = await Handle(args);
            var json = JsonSerializer.Serialize(data, options);
            if (json.Length > MaxResultChars)
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"结果过大（{json.Length} 字符，上限 {MaxResultChars}），请缩小范围（sections/limit/maxChars/prefix）",
                }, options);
            return JsonSerializer.Serialize(new { success = true, data }, options);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"参数 JSON 非法：{ex.Message}" }, options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"{ex.GetType().Name}: {ex.Message}" }, options);
        }
    }

    protected abstract Task<Object?> Handle(JsonElement args);

    // ---- 参数助手 ----

    protected static String? GetStr(JsonElement args, String key)
    {
        if (!args.TryGetProperty(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => v.GetBoolean().ToString(),
            _ => null,
        };
    }

    protected static String GetStr(JsonElement args, String key, String fallback)
    {
        var v = GetStr(args, key);
        return v.IsNullOrEmpty() ? fallback : v!;
    }

    protected static Boolean GetBool(JsonElement args, String key, Boolean fallback = false)
    {
        if (!args.TryGetProperty(key, out var v)) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => Boolean.TryParse(v.GetString(), out var b) && b,
            _ => fallback,
        };
    }

    protected static Int32 GetInt(JsonElement args, String key, Int32 fallback, Int32 min, Int32 max)
    {
        if (!args.TryGetProperty(key, out var v)) return fallback;
        var ok = v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetInt32(out _),
            JsonValueKind.String => Int32.TryParse(v.GetString(), out _),
            _ => false,
        };
        if (!ok) return fallback;
        var n = v.ValueKind == JsonValueKind.Number ? v.GetInt32() : Int32.Parse(v.GetString()!);
        return Math.Clamp(n, min, max);
    }

    protected static IReadOnlyList<String> GetStrArray(JsonElement args, String key)
    {
        if (!args.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Array) return [];
        return v.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList();
    }

    protected static IReadOnlyList<JsonElement> GetObjArray(JsonElement args, String key)
    {
        if (!args.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Array) return [];
        return v.EnumerateArray().ToList();
    }

    // ---- 项目解析（§A）----

    /// <summary>project 参数解析：code 精确匹配（Trim+忽略大小写）→ 纯数字按 id → 缺省唯一非归档项目；0/多 → 错误列 code。</summary>
    protected DesignProject ResolveProject(String? project, Boolean requireWrite)
    {
        var code = (project ?? "").Trim();
        DesignProject? p = null;
        if (!code.IsNullOrEmpty())
        {
            p = Kit.Projects.List(null, null).FirstOrDefault(x => String.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            if (p == null && code.All(Char.IsDigit)) p = Kit.Projects.Find(Int64.Parse(code));
            if (p == null) throw new ArgumentException($"项目 {code} 不存在（可选：{ListCodes()}）");
        }
        else
        {
            var active = Kit.Projects.List(ProjectStatus.Draft, null)
                .Concat(Kit.Projects.List(ProjectStatus.Published, null)).ToList();
            if (active.Count == 1) p = active[0];
            else if (active.Count == 0)
                throw new ArgumentException("请指定 project（当前没有非归档项目；可先用 design_presets action=recommend + design_create 创建一个）");
            else
                throw new ArgumentException($"请指定 project（可选：{String.Join(", ", active.Take(20).Select(x => x.Code))}）");
        }

        if (requireWrite && p.Status == ProjectStatus.Archived)
            throw new ArgumentException($"项目 {p.Code} 已归档（软删），不能写入；先在界面把状态改回 draft");
        return p;
    }

    String ListCodes() => String.Join(", ", Kit.Projects.List(null, null).Take(20).Select(x => x.Code));

    /// <summary>主题解析：缺省项目默认主题；请求主题不存在则回落默认并带说明。</summary>
    protected (String Theme, String? Note) ResolveTheme(DesignProject project, String? theme)
    {
        if (!theme.IsNullOrEmpty() && Kit.Projects.FindTheme(project.Id, theme!) != null) return (theme!, null);
        var def = Kit.Projects.ListThemes(project.Id).FirstOrDefault(t => t.IsDefault);
        var fallback = def?.Code ?? "light";
        return (fallback, theme.IsNullOrEmpty() ? null : $"主题 {theme} 不存在，已回落默认主题 {fallback}");
    }
}
