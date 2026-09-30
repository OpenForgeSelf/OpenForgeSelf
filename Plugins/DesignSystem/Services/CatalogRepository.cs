using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife;
using XCode;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>组件写入输入</summary>
public sealed class ComponentInput
{
    public String Code { get; set; } = "";
    public String? Name { get; set; }
    public String? Category { get; set; }
    public Boolean? Interactive { get; set; }
    public String? Description { get; set; }
    public String? DocJson { get; set; }
    public String? GuidanceJson { get; set; }
    public String? A11yNotes { get; set; }
    public String? TokenRefsJson { get; set; }
    public String? Status { get; set; }
    public Int32? SortOrder { get; set; }
}

/// <summary>变体写入输入（variant × state × theme 矩阵的一格）</summary>
public sealed class VariantInput
{
    public String Code { get; set; } = "";
    public String? VariantJson { get; set; }
    public String State { get; set; } = "default";
    public Int64 ThemeId { get; set; } = DesignSystemConstants.SharedThemeId;
    public String? Name { get; set; }
    public String? TokenRefsJson { get; set; }
    public String? CssSnippet { get; set; }
    public String? Description { get; set; }
    public Int32? SortOrder { get; set; }
}

/// <summary>图标写入输入</summary>
public sealed class IconInput
{
    public String Code { get; set; } = "";
    public String? Name { get; set; }
    public String Collection { get; set; } = DesignSystemConstants.BuiltinIconCollection;
    public String SvgBody { get; set; } = "";
    public Double? StrokeWidth { get; set; }
    public Int32? GridPx { get; set; }
    public String? ViewBox { get; set; }
    public String? Sizes { get; set; }
    public String? Tags { get; set; }
    public String? Usage { get; set; }
    public String? License { get; set; }
}

/// <summary>资产写入输入（logo/母题/图片等）。SVG 一律不含烤死色值，颜色靠 currentColor + 令牌。</summary>
public sealed record AssetRequest(String Code, String? Name, String? Kind, String? SvgBody, String? FileRef,
    String? TokenRefsJson, String? Description, String? License);

/// <summary>页面清单写入输入（这个设计系统服务哪些屏）</summary>
public sealed record ScreenRequest(String Code, String? Title, String? IconCode, String? Route,
    String? ComponentIdsJson, String? Description, String? Notes, Int64 ThemeId = 0, Int32 SortOrder = 0);

/// <summary>字体登记输入。许可证字段必须说清"从哪来、能不能分发"，不能留空当默认没问题。</summary>
public sealed record FontRequest(String Family, Int32 Weight = 400, String Style = "normal",
    String? FileName = null, String? FileRef = null, String? Display = "swap", String? Role = null,
    String? SourceUrl = null, String? License = null, String? MetricsJson = null);

/// <summary>
/// 目录类对象（组件 / 变体 / 图标 / 资产 / 页面 / 字体）的读写门面。
///
/// 两条硬纪律：
/// - 变体唯一键里的 JSON 必须先规范化（键排序）再入库，否则同语义变体会因键序不同被判为两行（design G5）；
/// - 内置图标库（ProjectId=<see cref="DesignSystemConstants.BuiltinProjectId"/>）只读，写入必须被拒绝（design G7）。
/// </summary>
public sealed class CatalogRepository
{
    #region 组件

    public IList<DesignComponent> ListComponents(Int64 projectId, String? category)
    {
        var exp = DesignComponent._.ProjectId == projectId;
        if (!category.IsNullOrEmpty()) exp &= DesignComponent._.Category == category;
        return DesignComponent.FindAll(exp).OrderBy(c => c.SortOrder).ThenBy(c => c.Code, StringComparer.Ordinal).ToList();
    }

    public DesignComponent? FindComponent(Int64 projectId, String code) =>
        DesignComponent.FindAll(DesignComponent._.ProjectId == projectId & DesignComponent._.Code == code.Trim()).FirstOrDefault();

    public DesignComponent SaveComponent(Int64 projectId, ComponentInput input)
    {
        var code = input.Code.Trim();
        if (code.IsNullOrEmpty()) throw new ArgumentException("组件 Code 不能为空", nameof(input));

        var e = FindComponent(projectId, code) ?? new DesignComponent { ProjectId = projectId, Code = code, CreatedAt = DateTime.Now };
        e.Name = input.Name.IsNullOrEmpty() ? code : input.Name!;
        if (!input.Category.IsNullOrEmpty()) e.Category = input.Category!;
        else if (e.Category.IsNullOrEmpty()) e.Category = "primitive";
        if (input.Interactive != null) e.Interactive = input.Interactive.Value;
        if (input.Description != null) e.Description = input.Description;
        if (input.DocJson != null) e.DocJson = CanonicalJson(input.DocJson);
        if (input.GuidanceJson != null) e.GuidanceJson = CanonicalJson(input.GuidanceJson);
        if (input.A11yNotes != null) e.A11yNotes = input.A11yNotes;
        if (input.TokenRefsJson != null) e.TokenRefsJson = CanonicalJson(input.TokenRefsJson);
        if (!input.Status.IsNullOrEmpty()) e.Status = input.Status!;
        else if (e.Status.IsNullOrEmpty()) e.Status = "demo";
        if (input.SortOrder != null) e.SortOrder = input.SortOrder.Value;
        e.UpdatedAt = DateTime.Now;
        e.Save();
        return e;
    }

    /// <summary>
    /// 矩阵读序：**先按变体分组（按 `VariantAxes` 档位序），组内按落库 `SortOrder`** —— 生成时 `SortOrder` 就是"变体序 → 状态档位序"编的，
    /// 所以组内序即状态档位序。不再按 `State` 字母序重排（那会让界面、DESIGN.md、registry 三处各排各的）。
    /// 分组这一步是必要的：用户自己补的格子 `SortOrder` 可能是 0，只按 `SortOrder` 排会让它们整批跳到矩阵最前面。
    /// </summary>
    public IList<DesignComponentVariant> ListVariants(Int64 componentId) =>
        DesignComponentVariant.FindAll(DesignComponentVariant._.ComponentId == componentId)
            .OrderBy(v => AxisSortKey(v.VariantKey), StringComparer.Ordinal)
            .ThenBy(v => v.VariantKey, StringComparer.Ordinal)
            .ThenBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 一次取回一批组件的变体并按组件分组，行序与 <see cref="ListVariants"/> 逐位一致。
    /// 为什么要它：导出每次都"逐组件查一次"（N+1），SQLite 并发下锁窗口被拉得很大 ——
    /// v2.6.8 的 e2e 里导出页 12 个格式并行预览 ⇒ 上百次变体查询 ⇒ 实测 <c>database is locked</c> 500。
    /// 查不到行的组件也给空组，调用方不必自己补默认（少一处会写错的分支）。
    /// </summary>
    public IDictionary<Int64, IList<DesignComponentVariant>> VariantsByComponent(IReadOnlyList<Int64> componentIds)
    {
        var grouped = componentIds.Distinct().ToDictionary(id => id, _ => (IList<DesignComponentVariant>)Array.Empty<DesignComponentVariant>());
        if (componentIds.Count == 0) return grouped;

        var ids = componentIds.Distinct().ToArray();
        var ordered = DesignComponentVariant.FindAll(DesignComponentVariant._.ComponentId.In(ids))
            .OrderBy(v => AxisSortKey(v.VariantKey), StringComparer.Ordinal)
            .ThenBy(v => v.VariantKey, StringComparer.Ordinal)
            .ThenBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal).ToList();
        foreach (var group in ordered.GroupBy(v => v.ComponentId).Where(g => grouped.ContainsKey(g.Key)))
            grouped[group.Key] = group.ToList();
        return grouped;
    }

    /// <summary>轴的先验展示序：尺寸 → 角色 → 状态，其余轴按字母序跟在后面</summary>
    static readonly String[] AxisPriority = [VariantAxes.Size, VariantAxes.Role, VariantAxes.State];

    /// <summary>
    /// 变体分组的排序键：把 canonical JSON 里的轴值按 <see cref="VariantAxes"/> 的档位序编码成定宽串。
    /// 为什么不能直接按 <c>VariantKey</c> 排：那是 JSON 文本的字典序，<c>{"role":"danger"}</c> 会排在
    /// <c>{"role":"primary"}</c> 前面 —— 矩阵行序正是设计师核对档位的那条线，字母序凑它等于没有序
    /// （v2.6.4 修的是状态维，这是同族里漏掉的变体维）。
    /// 轴值不在词表里 → 排到该轴末尾并按原值；JSON 解析不了 → 整组排最后并按原文（可重复，不猜）。
    /// </summary>
    static String AxisSortKey(String? variantKey)
    {
        if (variantKey.IsNullOrWhiteSpace()) return "";

        Dictionary<String, String> axes;
        try
        {
            using var doc = JsonDocument.Parse(variantKey);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return "~" + variantKey;
            axes = new Dictionary<String, String>(StringComparer.Ordinal);
            foreach (var p in doc.RootElement.EnumerateObject())
                axes[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.GetRawText();
        }
        catch (JsonException) { return "~" + variantKey; }

        if (axes.Count == 0) return "";   // base 变体（无轴）排最前

        var sb = new StringBuilder();
        var axesInOrder = AxisPriority.Concat(axes.Keys.Where(k => !AxisPriority.Contains(k)).OrderBy(k => k, StringComparer.Ordinal));
        var axisIdx = 0;
        foreach (var axis in axesInOrder)
        {
            if (!axes.TryGetValue(axis, out var value)) { axisIdx++; continue; }
            // 轴的位置在前、档位在内：反过来写会让 `size=sm` 与 `role=primary` 交错（都是 0001），
            // 分组就散了 —— 这条顺序是"先按轴分块，块内按档位"，不是全局按档位。
            sb.Append(axisIdx.ToString("D2")).Append(':')
              .Append(Math.Min(VariantAxes.Rank(axis, value), 9999).ToString("D4"))
              .Append(':').Append(axis).Append('=').Append(value).Append('|');
            axisIdx++;
        }
        return sb.ToString();
    }

    public DesignComponentVariant SaveVariant(Int64 projectId, DesignComponent component, VariantInput input)
    {
        var state = input.State.IsNullOrEmpty() ? "default" : input.State.Trim().ToLowerInvariant();
        var key = CanonicalJson(input.VariantJson ?? "{}");

        var e = DesignComponentVariant.FindAll(DesignComponentVariant._.ComponentId == component.Id
                    & DesignComponentVariant._.VariantKey == key
                    & DesignComponentVariant._.State == state
                    & DesignComponentVariant._.ThemeId == input.ThemeId).FirstOrDefault()
                ?? new DesignComponentVariant
                {
                    ProjectId = projectId,
                    ComponentId = component.Id,
                    ComponentCode = component.Code,
                    VariantKey = key,
                    State = state,
                    ThemeId = input.ThemeId,
                    CreatedAt = DateTime.Now,
                };

        e.Code = input.Code.IsNullOrEmpty() ? $"{component.Code}-{state}" : input.Code.Trim();
        e.Name = input.Name.IsNullOrEmpty() ? e.Code : input.Name!;
        e.VariantJson = key;
        if (input.TokenRefsJson != null) e.TokenRefsJson = CanonicalJson(input.TokenRefsJson);
        if (input.CssSnippet != null) e.CssSnippet = input.CssSnippet;
        if (input.Description != null) e.Description = input.Description;
        if (input.SortOrder != null) e.SortOrder = input.SortOrder.Value;
        e.UpdatedAt = DateTime.Now;
        e.Save();
        return e;
    }

    #endregion

    #region 图标

    public IList<DesignIcon> ListIcons(Int64 projectId, String? collection, String? keyword)
    {
        // 项目视图默认连带内置库一起返回：0=内置库，非 0 时把内置项一并列出
        var ids = projectId == DesignSystemConstants.BuiltinProjectId
            ? new[] { DesignSystemConstants.BuiltinProjectId }
            : new[] { DesignSystemConstants.BuiltinProjectId, projectId };

        var exp = DesignIcon._.ProjectId == ids[0];
        for (var i = 1; i < ids.Length; i++) exp |= DesignIcon._.ProjectId == ids[i];
        if (!collection.IsNullOrEmpty()) exp &= DesignIcon._.Collection == collection;
        if (!keyword.IsNullOrEmpty())
            exp &= DesignIcon._.Code.Contains(keyword!) | DesignIcon._.Name.Contains(keyword!) | DesignIcon._.Tags.Contains(keyword!);

        return DesignIcon.FindAll(exp).OrderBy(i => i.Collection, StringComparer.Ordinal)
            .ThenBy(i => i.Code, StringComparer.Ordinal).ToList();
    }

    public DesignIcon? FindIcon(Int64 projectId, String code) =>
        DesignIcon.FindAll(DesignIcon._.ProjectId == projectId & DesignIcon._.Code == code.Trim()).FirstOrDefault();

    /// <summary>写入项目图标；内置库只读，拒绝写入</summary>
    public DesignIcon SaveIcon(Int64 projectId, IconInput input)
    {
        if (projectId == DesignSystemConstants.BuiltinProjectId)
            throw new DesignConflictException("内置图标库为只读，请在项目内复制或导入后再修改");

        var code = input.Code.Trim();
        if (code.IsNullOrEmpty()) throw new ArgumentException("图标 Code 不能为空", nameof(input));
        if (input.SvgBody.IsNullOrWhiteSpace() && input.ViewBox.IsNullOrWhiteSpace())
            throw new ArgumentException("图标必须有 SvgBody 或 ViewBox", nameof(input));

        var e = FindIcon(projectId, code) ?? new DesignIcon { ProjectId = projectId, Code = code, CreatedAt = DateTime.Now };
        e.Name = input.Name.IsNullOrEmpty() ? code : input.Name!;
        e.Collection = input.Collection.IsNullOrEmpty() ? "custom" : input.Collection.Trim();
        if (input.SvgBody != null) e.SvgBody = input.SvgBody;
        if (input.StrokeWidth != null) e.StrokeWidth = input.StrokeWidth.Value;
        if (input.GridPx != null) e.GridPx = input.GridPx.Value;
        if (input.ViewBox != null) e.ViewBox = input.ViewBox;
        if (input.Sizes != null) e.Sizes = input.Sizes;
        if (input.Tags != null) e.Tags = input.Tags;
        if (input.Usage != null) e.Usage = input.Usage;
        if (input.License != null) e.License = input.License;
        e.UpdatedAt = DateTime.Now;
        e.Save();
        return e;
    }

    /// <summary>首植内置图标库用（仅插件启动时调用，允许写 ProjectId=0）</summary>
    public DesignIcon SeedBuiltinIcon(IconInput input)
    {
        var code = input.Code.Trim();
        var e = FindIcon(DesignSystemConstants.BuiltinProjectId, code) ?? new DesignIcon
        {
            ProjectId = DesignSystemConstants.BuiltinProjectId,
            Code = code,
            CreatedAt = DateTime.Now,
        };
        e.Name = input.Name.IsNullOrEmpty() ? code : input.Name!;
        e.Collection = DesignSystemConstants.BuiltinIconCollection;
        e.SvgBody = input.SvgBody;
        e.StrokeWidth = input.StrokeWidth ?? 1.5;
        e.GridPx = input.GridPx ?? 24;
        e.ViewBox = input.ViewBox ?? "0 0 24 24";
        e.Sizes = input.Sizes ?? "16,20,24,32";
        e.Tags = input.Tags ?? "";
        e.Usage = input.Usage ?? "";
        e.License = input.License ?? "Owned";
        e.UpdatedAt = DateTime.Now;
        e.Save();
        return e;
    }

    #endregion

    #region 资产 / 页面 / 字体

    public IList<DesignAsset> ListAssets(Int64 projectId, String? kind)
    {
        var exp = DesignAsset._.ProjectId == projectId;
        if (!kind.IsNullOrEmpty()) exp &= DesignAsset._.Kind == kind;
        return DesignAsset.FindAll(exp).OrderBy(a => a.SortOrder).ThenBy(a => a.Code, StringComparer.Ordinal).ToList();
    }

    public DesignAsset SaveAsset(Int64 projectId, String code, String? name, String? kind, String? svgBody, String? fileRef, String? tokenRefsJson, String? description, String? license)
    {
        code = code.Trim();
        if (code.IsNullOrEmpty()) throw new ArgumentException("资产 Code 不能为空", nameof(code));
        var e = DesignAsset.FindAll(DesignAsset._.ProjectId == projectId & DesignAsset._.Code == code).FirstOrDefault()
                ?? new DesignAsset { ProjectId = projectId, Code = code, CreatedAt = DateTime.Now };
        e.Name = name.IsNullOrEmpty() ? code : name!;
        if (!kind.IsNullOrEmpty()) e.Kind = kind!;
        else if (e.Kind.IsNullOrEmpty()) e.Kind = "logo";
        if (svgBody != null) e.SvgBody = svgBody;
        if (fileRef != null) e.FileRef = fileRef;
        if (tokenRefsJson != null) e.TokenRefsJson = CanonicalJson(tokenRefsJson);
        if (description != null) e.Description = description;
        if (license != null) e.License = license;
        e.UpdatedAt = DateTime.Now;
        e.Save();
        return e;
    }

    public IList<DesignScreen> ListScreens(Int64 projectId) =>
        DesignScreen.FindAll(DesignScreen._.ProjectId == projectId).OrderBy(s => s.SortOrder).ThenBy(s => s.Code, StringComparer.Ordinal).ToList();

    public DesignScreen SaveScreen(Int64 projectId, String code, String? title, String? iconCode, String? route, String? componentIdsJson, String? description, String? notes, Int64 themeId, Int32 sortOrder)
    {
        code = code.Trim();
        if (code.IsNullOrEmpty()) throw new ArgumentException("页面 Code 不能为空", nameof(code));
        var e = DesignScreen.FindAll(DesignScreen._.ProjectId == projectId & DesignScreen._.Code == code).FirstOrDefault()
                ?? new DesignScreen { ProjectId = projectId, Code = code, CreatedAt = DateTime.Now };
        e.Title = title.IsNullOrEmpty() ? code : title!;
        if (iconCode != null) e.IconCode = iconCode;
        if (route != null) e.Route = route;
        if (componentIdsJson != null) e.ComponentIdsJson = CanonicalJson(componentIdsJson);
        if (description != null) e.Description = description;
        if (notes != null) e.Notes = notes;
        e.ThemeId = themeId;
        e.SortOrder = sortOrder;
        e.UpdatedAt = DateTime.Now;
        e.Save();
        return e;
    }

    public IList<DesignFontFace> ListFonts(Int64 projectId) =>
        DesignFontFace.FindAll(DesignFontFace._.ProjectId == projectId | DesignFontFace._.ProjectId == DesignSystemConstants.BuiltinProjectId)
            .OrderBy(f => f.Family, StringComparer.Ordinal).ThenBy(f => f.Weight).ToList();

    public DesignFontFace SaveFont(Int64 projectId, String family, Int32 weight, String style, String? fileName, String? fileRef, String? display, String? role, String? sourceUrl, String? license, String? metricsJson)
    {
        if (family.IsNullOrWhiteSpace()) throw new ArgumentException("字族名不能为空", nameof(family));
        style = style.IsNullOrEmpty() ? "normal" : style!.Trim().ToLowerInvariant();

        var e = DesignFontFace.FindAll(DesignFontFace._.ProjectId == projectId
            & DesignFontFace._.Family == family.Trim()
            & DesignFontFace._.Weight == weight
            & DesignFontFace._.Style == style).FirstOrDefault()
            ?? new DesignFontFace { ProjectId = projectId, Family = family.Trim(), Weight = weight, Style = style, CreatedAt = DateTime.Now };

        if (fileName != null) e.FileName = fileName;
        if (fileRef != null) e.FileRef = fileRef;
        if (display != null) e.Display = display;
        else if (e.Display.IsNullOrEmpty()) e.Display = "swap";
        if (role != null) e.Role = role;
        if (sourceUrl != null) e.SourceUrl = sourceUrl;
        if (license != null) e.License = license;
        if (metricsJson != null) e.MetricsJson = CanonicalJson(metricsJson);
        e.UpdatedAt = DateTime.Now;
        e.Save();
        return e;
    }

    #endregion

    #region JSON 规范化

    /// <summary>
    /// 递归按键序规范化 JSON（对象键升序、紧凑输出、非 JSON 输入原样返回）。
    /// 变体唯一索引包含该串，键序不稳就会产生重复行、破坏幂等 upsert。
    /// </summary>
    public static String CanonicalJson(String? json)
    {
        if (json.IsNullOrWhiteSpace()) return "{}";
        try
        {
            var node = JsonNode.Parse(json);
            if (node == null) return "{}";
            var sb = new StringBuilder();
            Write(node, sb);
            return sb.ToString();
        }
        catch (JsonException)
        {
            return json.Trim();
        }

        static void Write(JsonNode? n, StringBuilder w)
        {
            switch (n)
            {
                case JsonObject o:
                    w.Append('{');
                    var first = true;
                    foreach (var kv in o.OrderBy(k => k.Key, StringComparer.Ordinal))
                    {
                        if (!first) w.Append(',');
                        first = false;
                        w.Append(JsonSerializer.Serialize(kv.Key));
                        w.Append(':');
                        Write(kv.Value, w);
                    }
                    w.Append('}');
                    break;
                case JsonArray a:
                    w.Append('[');
                    for (var i = 0; i < a.Count; i++)
                    {
                        if (i > 0) w.Append(',');
                        Write(a[i], w);
                    }
                    w.Append(']');
                    break;
                default:
                    w.Append(n?.ToJsonString(new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "null");
                    break;
            }
        }
    }

    #endregion
}
