using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>结构性错误（不是"某一条不要"，而是这份文件没法用）：控制器映射成 400。</summary>
public sealed class ImportException(String message) : Exception(message);

/// <summary>被拒的条目：路径 + 人话原因。导入不许静默丢条目，所以每一条都要有名字。</summary>
public sealed record ImportRejected(String Path, String Reason);

/// <summary>解析统计。`Entries` 是文件里的叶子总数（含被拒的），其余按落点分类。</summary>
public sealed record ImportCounts(Int32 Entries, Int32 Aliases, Int32 Literals, Int32 Composites,
                                  Int32 Rejected, Int32 Conflicts, Int32 Duplicates);

/// <summary>一次解析的结果：待写清单 + 被拒清单 + 冲突路径 + 统计。preview 与 import **共用同一份**，不各算一遍。</summary>
public sealed record ImportPlan(IReadOnlyList<TokenPatch> Patches,
                                IReadOnlyList<ImportRejected> Rejected,
                                IReadOnlyList<String> ConflictPaths,
                                ImportCounts Counts,
                                String? DocumentProject);

/// <summary>库里某一现有路径的事实：写回哪个主题、现在是什么层级（解析器据此"不搬家、不改层级"）。</summary>
public sealed record ImportRowInfo(Int64 ThemeId, String Tier);

/// <summary>
/// DTCG（W3C Design Tokens 2025.10）→ `TokenPatch` 的**纯解析器**：不读库、不落库、不 catch 后静默丢条目。
///
/// 三条纪律：
/// 1. **层级/类型只从证据推**：类型取自 `$type`（沿树继承，与我们导出侧 `ApplyTypeInheritance` 互逆），
///    推不出来就拒这一条并回原因 —— 猜成 `color` 会让一条 dimension 变成会算错的颜色。
///    DTCG 文件里**没有 tier**：库里已有的路径一律保留库里的层级（只有新路径才按首段推），
///    否则一次导入会把 `chart.series-1` 这类语义层降级成 primitive，它的别名就"逆向指向上层"，整批被图校验拒（实测踩过）。
/// 2. **不搬家**：文件里的路径若在库里已存在（共享层或目标主题），就写回它**原本所在**的主题，
///    只有新路径才落到 `targetThemeId`。否则"导出→导入→再导出"会把共享 primitive 复制进主题层，
///    round-trip 判据当场不成立，而且库里长出一批重复行。
/// 3. **来源只写进列、不写进 `$extensions`**：`Generator=imported` + `GeneratorSeed=sha256(文件字节)`。
///    把 provenance 塞进逐令牌 `Extensions` 会被导出带进 DTCG，第一次 round-trip 就不逐字一致了。
///
/// 字段留空 = 不改既有列（`TokenRepository.Apply` 的语义），所以导入不会顺手抹掉库里已有的
/// `Name`/`Group`/`SortOrder`/`Lifecycle` 等文件里根本没有的信息。
/// </summary>
public static class DtcgImporter
{
    /// <summary>别名引用形状：DTCG 用 `"{path.to.token}"`，与我们导出侧的 `RenderAlias` 一致。</summary>
    static readonly System.Text.RegularExpressions.Regex AliasPattern =
        new(@"^\{\s*([^{}\s]+)\s*\}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>库里受保护行的键格式：`{themeId}:{path}`（两侧共用，避免各拼一份）</summary>
    public static String Key(Int64 themeId, String path) => $"{themeId}:{path}";

    /// <summary>层级只看路径首段（FR-I3）：`semantic.` / `component.` 之外一律 primitive。</summary>
    public static String TierOf(String path) => path switch
    {
        _ when path.StartsWith("semantic.", StringComparison.Ordinal) => TokenTiers.Semantic,
        _ when path.StartsWith("component.", StringComparison.Ordinal) => TokenTiers.Component,
        _ => TokenTiers.Primitive,
    };

    /// <param name="doc">上传的 DTCG 根对象</param>
    /// <param name="targetThemeId">新路径的归属主题（0 = 共享层）</param>
    /// <param name="existingByPath">库里 path → 该写入的主题与既有层级（同路径两主题都有时由调用方给目标主题优先）</param>
    /// <param name="protectedKeys">库里受手改/导入保护的键集合（<see cref="Key"/>）</param>
    /// <param name="seed">文件字节的 sha256（小写 hex），写进每条的 GeneratorSeed</param>
    /// <param name="overwrite">true 时不产生"冲突"（会覆盖受保护行）</param>
    public static ImportPlan Parse(JsonElement doc, Int64 targetThemeId,
                                   IReadOnlyDictionary<String, ImportRowInfo> existingByPath,
                                   IReadOnlySet<String> protectedKeys,
                                   String seed, Boolean overwrite)
    {
        if (doc.ValueKind != JsonValueKind.Object)
            throw new ImportException("DTCG 根必须是对象（一组或多组令牌）");

        var patches = new List<TokenPatch>();
        var rejected = new List<ImportRejected>();
        var seen = new HashSet<(Int64, String)>();
        Int32 entries = 0, aliases = 0, literals = 0, composites = 0, duplicates = 0;

        Walk(doc, "", null);
        var conflictPaths = new List<String>();
        if (!overwrite)
            foreach (var p in patches)
                if (protectedKeys.Contains(Key(p.ThemeId, p.Path))) conflictPaths.Add(p.Path);
        // 上限在解析器里判：控制器与单测走同一条链，避免"两处各写一个阈值"（U5 落定值见 ImportLimits）
        if (entries > ImportLimits.MaxEntries)
            throw new ImportException($"条目数 {entries} 超过单次上限 {ImportLimits.MaxEntries}");
        return new ImportPlan(patches, rejected, conflictPaths,
            new ImportCounts(entries, aliases, literals, composites, rejected.Count, conflictPaths.Count, duplicates),
            ProjectOf(doc));

        static String? ProjectOf(JsonElement root) =>
            root.TryGetProperty("$extensions", out var ext) && ext.ValueKind == JsonValueKind.Object &&
            ext.TryGetProperty("forgeself", out var fs) && fs.ValueKind == JsonValueKind.Object &&
            fs.TryGetProperty("project", out var pj) && pj.ValueKind == JsonValueKind.String
                ? pj.GetString() : null;

        void Walk(JsonElement node, String prefix, String? inheritedType)
        {
            // 组上的 $type 沿树向下携带（导出侧同规则）
            var groupType = inheritedType;
            if (node.TryGetProperty("$type", out var gt) && gt.ValueKind == JsonValueKind.String)
                groupType = gt.GetString();

            foreach (var prop in node.EnumerateObject())
            {
                if (prop.Name.StartsWith('$')) continue;      // $schema/$extensions/$type/$description 不是组名
                var path = prefix.Length == 0 ? prop.Name : prefix + "." + prop.Name;

                if (prop.Value.ValueKind != JsonValueKind.Object)
                {
                    rejected.Add(new ImportRejected(path, "令牌的值必须是对象（含 $value）"));
                    continue;
                }

                if (!prop.Value.TryGetProperty("$value", out var value))
                {
                    Walk(prop.Value, path, groupType);        // 没有 $value = 这是一个组
                    continue;
                }

                entries++;
                var (patch, reason) = Leaf(path, prop.Value, value, groupType);
                if (reason != null)
                {
                    rejected.Add(new ImportRejected(path, reason));
                    continue;
                }

                if (!seen.Add((patch!.ThemeId, patch.Path)))
                {
                    duplicates++;
                    rejected.Add(new ImportRejected(path, $"同一文件内路径重复：{path}（主题 {patch.ThemeId}）"));
                    continue;
                }

                if (!patch.AliasPath.IsNullOrEmpty()) aliases++;
                else if (!patch.ValueJson.IsNullOrEmpty()) composites++;
                else literals++;
                patches.Add(patch);
            }
        }

        (TokenPatch?, String?) Leaf(String path, JsonElement leaf, JsonElement value, String? inheritedType)
        {
            var type = inheritedType;
            if (leaf.TryGetProperty("$type", out var t) && t.ValueKind == JsonValueKind.String) type = t.GetString();
            if (type.IsNullOrEmpty()) return (null, "缺 $type（自身与父组都没有），不猜类型");
            if (!TokenTypes.IsValid(type)) return (null, $"未知 $type：{type}（不在 TokenTypes.All 内）");

            String? alias = null, text = null, json = null;
            if (value.ValueKind == JsonValueKind.String)
            {
                var s = value.GetString() ?? "";
                var m = AliasPattern.Match(s);
                alias = m.Success ? m.Groups[1].Value : null;
                text = m.Success ? null : s;
            }
            else if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                text = value.GetRawText();
            else if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                if (!TokenTypes.IsComposite(type)) return (null, $"{type} 的 $value 不该是对象/数组形状");
                json = value.GetRawText();
            }
            else return (null, "$value 形状无法识别");

            existingByPath.TryGetValue(path, out var row);
            return (new TokenPatch
            {
                Path = path,
                // 库里已有这条路径就写回它原来的层（纪律 2），只有新路径才落到选中的主题
                ThemeId = row?.ThemeId ?? targetThemeId,
                // 库里已有 = 层级以库为准（文件里没有 tier，猜不得）；新路径才按首段推
                Tier = row?.Tier ?? TierOf(path),
                Type = type,
                // 别名/复合：Value 留空 = 不动既有列；字面值：清掉别名与可能残留的复合 JSON
                Value = text,
                ValueJson = json,
                AliasPath = alias ?? "",
                Description = leaf.TryGetProperty("$description", out var d) && d.ValueKind == JsonValueKind.String
                    ? d.GetString() : null,
                Extensions = ExtensionsOf(leaf),
                Generator = TokenGenerators.Imported,
                GeneratorSeed = seed,
            }, null);
        }
    }

    /// <summary>
    /// 逐令牌的 `$extensions` 原样保留（round-trip 要它回得来）。
    /// 非对象形状一律丢弃并回 null —— 宁可不带元数据，也不存一个没人能解析的袋子。
    /// </summary>
    static String? ExtensionsOf(JsonElement leaf)
    {
        if (!leaf.TryGetProperty("$extensions", out var ext) || ext.ValueKind != JsonValueKind.Object) return null;
        try
        {
            JsonNode.Parse(ext.GetRawText());
            return ext.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
