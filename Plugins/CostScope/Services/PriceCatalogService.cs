using ForgeSelf.Api.Plugins.CostScope.Entities;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Plugins.CostScope.Services;

/// <summary>
/// 模型单价目录 CRUD（FR-3.5）+ 供聚合侧按模型取价（FR-3.3 的价格来源）。
/// </summary>
/// <remarks>
/// <para><b>数据落点</b>：插件自有库 <c>CostScope.db</c>（连接名 <c>CostScope</c>，由宿主
/// <c>XCodeConfig.PluginDbs</c> 统一登记，插件<b>不自行</b> AddConnStr——铁律 12 / F15）。
/// <b>绝不写宿主库</b>（F1 / BR-5）。</para>
/// <para><b>删除语义</b>（FR-3.5）：删除单价后该模型历史成本自动转 <c>Unknown</c>（纯函数在聚合时现算，
/// 成本不入明细层 ⇒ FR-3.4 天然成立），<b>不删除任何用量行</b>；重新补回单价即自动恢复核算（可撤销）。</para>
/// <para><b>单价是用户本地配置</b>（BR-8）：不上传、不进 git。</para>
/// </remarks>
public sealed class PriceCatalogService
{
    /// <summary>单价上界（每 1M token），超出即判非法（02-spec Error Handling「非法价格 → 400」）。</summary>
    public const decimal MaxPricePer1M = 1_000_000m;

    /// <summary>默认币种（U-8：CNY 无税）。</summary>
    public const string DefaultCurrency = "CNY";

    /// <summary>列出单价条目。</summary>
    /// <param name="onlyEnabled">true = 只列启用态（聚合取价口径）。</param>
    public IReadOnlyList<CostModelPrice> List(bool onlyEnabled = true)
    {
        var rows = onlyEnabled
            ? CostModelPrice.FindAll(CostModelPrice._.IsEnabled == true)
            : CostModelPrice.FindAll();

        return rows.OrderBy(p => p.Model, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>按模型名取有效单价（聚合侧主入口）；未配置返回 <see langword="null"/> ⇒ 调用方按「未配单价」处理。</summary>
    public CostModelPrice? Find(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return null;

        return FindOne(model.Trim());
    }

    /// <summary>新增或更新一条单价（按 <see cref="CostModelPrice.Model"/> 唯一）。</summary>
    /// <remarks>
    /// 重复模型名<b>显式拒绝</b>（抛 <see cref="InvalidOperationException"/>）而<b>不静默覆盖</b>——
    /// 静默覆盖会让用户以为改生效了、实际覆盖了别的模型配置。
    /// </remarks>
    public CostModelPrice Save(CostModelPrice draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var model = (draft.Model ?? string.Empty).Trim();
        if (model.Length == 0)
            throw new ArgumentException("模型名不可为空", nameof(draft));

        // Provider 在实体层是必填（Model.xml Nullable=False，XCode 生成的 Valid() 会抛），
        // 故在服务层先显式校验并给出清晰报错，而不是让调用方撞实体层异常。
        var provider = (draft.Provider ?? string.Empty).Trim();
        if (provider.Length == 0)
            throw new ArgumentException("供应商名不可为空（单价目录按模型→供应商归属）", nameof(draft));

        if (!IsValidPrice(draft.InputPricePer1M))
            throw new ArgumentOutOfRangeException(nameof(draft), $"输入单价须在 [0, {MaxPricePer1M}] 区间");
        if (!IsValidPrice(draft.OutputPricePer1M))
            throw new ArgumentOutOfRangeException(nameof(draft), $"输出单价须在 [0, {MaxPricePer1M}] 区间");

        var now = DateTime.Now;
        var existing = FindOne(model);
        if (existing is null)
        {
            var created = new CostModelPrice
            {
                Model = model,
                Provider = provider,
                InputPricePer1M = draft.InputPricePer1M,
                OutputPricePer1M = draft.OutputPricePer1M,
                Currency = string.IsNullOrWhiteSpace(draft.Currency) ? DefaultCurrency : draft.Currency.Trim(),
                EffectiveFrom = draft.EffectiveFrom == default ? now : draft.EffectiveFrom,
                EffectiveTo = draft.EffectiveTo,
                IsEnabled = draft.IsEnabled,
                CreatedTime = now,
                UpdatedTime = now,
            };
            created.Save();
            XTrace.Log.Info("[CostScope] 新增单价 {0}/{1} in={2} out={3}", created.Provider, created.Model, created.InputPricePer1M, created.OutputPricePer1M);
            return created;
        }

        // 同名重复提交：**内容完全相同 ⇒ 幂等放行；内容不同 ⇒ 显式拒绝**（不静默覆盖既有配置）。
        // 注意：此处只比内容，不比模型（模型已相同，比模型恒为真会让拒绝分支形同虚设）。
        var sameContent = existing.InputPricePer1M == draft.InputPricePer1M
            && existing.OutputPricePer1M == draft.OutputPricePer1M
            && string.Equals(existing.Provider ?? string.Empty, provider, StringComparison.OrdinalIgnoreCase)
            && existing.IsEnabled == draft.IsEnabled;

        if (!sameContent)
            throw new InvalidOperationException($"模型 {model} 已有单价条目且内容不同；若要改价请调用 Update（新增通道不静默覆盖既有配置）");

        ApplyTo(existing, draft, provider, now);
        return existing;
    }

    /// <summary>
    /// 显式<b>改价</b>通道（FR-3.4「改单价后历史成本自动重算」的入口）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Save"/> 的分工：<c>Save</c> 是<b>新增</b>通道（重复即拒绝，防误覆盖）；
    /// <c>Update</c> 是<b>改价</b>通道（要求条目已存在，不存在则抛错——
    /// 若做成 upsert 就会掩盖「改了一个根本不存在的模型」这种误操作）。
    /// </remarks>
    public CostModelPrice Update(CostModelPrice draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var model = (draft.Model ?? string.Empty).Trim();
        if (model.Length == 0) throw new ArgumentException("模型名不可为空", nameof(draft));

        var provider = (draft.Provider ?? string.Empty).Trim();
        if (provider.Length == 0)
            throw new ArgumentException("供应商名不可为空（单价目录按模型→供应商归属）", nameof(draft));

        if (!IsValidPrice(draft.InputPricePer1M))
            throw new ArgumentOutOfRangeException(nameof(draft), $"输入单价须在 [0, {MaxPricePer1M}] 区间");
        if (!IsValidPrice(draft.OutputPricePer1M))
            throw new ArgumentOutOfRangeException(nameof(draft), $"输出单价须在 [0, {MaxPricePer1M}] 区间");

        var existing = FindOne(model);
        if (existing is null)
            throw new InvalidOperationException($"模型 {model} 尚无单价条目，请先用新增（Save）创建");

        ApplyTo(existing, draft, provider, DateTime.Now);
        return existing;
    }

    /// <summary>把草稿字段落到既有实体上（新增后的重放 / 改价共用，保证两条路径口径一致）。</summary>
    private static void ApplyTo(CostModelPrice existing, CostModelPrice draft, string provider, DateTime now)
    {
        existing.Provider = provider;
        existing.InputPricePer1M = draft.InputPricePer1M;
        existing.OutputPricePer1M = draft.OutputPricePer1M;
        if (!string.IsNullOrWhiteSpace(draft.Currency)) existing.Currency = draft.Currency.Trim();
        if (draft.EffectiveFrom != default) existing.EffectiveFrom = draft.EffectiveFrom;
        existing.EffectiveTo = draft.EffectiveTo;
        existing.IsEnabled = draft.IsEnabled;
        existing.UpdatedTime = now;
        existing.Save();

        XTrace.Log.Info("[CostScope] 更新单价 {0}/{1} in={2} out={3}", existing.Provider, existing.Model, existing.InputPricePer1M, existing.OutputPricePer1M);
    }

    /// <summary>删除一条单价。返回 true 表示确实删掉了。</summary>
    /// <remarks>
    /// 删除<b>不触碰任何用量行</b>；该模型历史成本此后按「未配单价 ⇒ Unknown」处理（BR-2，绝不静默计 0）。
    /// </remarks>
    public bool Remove(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return false;

        var existing = FindOne(model.Trim());
        if (existing is null) return false;

        var name = existing.Model;
        existing.Delete();
        XTrace.Log.Info("[CostScope] 删除单价 {0}（历史成本转 Unknown，用量行未动）", name);
        return true;
    }

    /// <summary>单价是否落在合法区间 [0, 1e6]（02-spec：单价 ∈ [0,1e6]）。</summary>
    public static bool IsValidPrice(decimal price) => price >= 0m && price <= MaxPricePer1M;

    /// <summary>
    /// 按模型名取唯一单价。
    /// </summary>
    /// <remarks>
    /// XCode 只生成了 <c>FindAllByModel</c>（列表）——说明 <c>Data/Model.xml</c> 里 Model 的索引
    /// <b>不是唯一索引</b>，DB 层不保证唯一。此处取首条，重复由 <see cref="Save"/> 的显式拒绝兜住
    /// （服务层唯一性保证；DB 级唯一索引已记为后续项）。
    /// </remarks>
    private static CostModelPrice? FindOne(string model)
    {
        var list = CostModelPrice.FindAllByModel(model);
        return list.Count == 0 ? null : list[0];
    }
}
