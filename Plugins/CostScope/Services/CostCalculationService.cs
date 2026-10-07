namespace ForgeSelf.Api.Plugins.CostScope.Services;

/// <summary>单次调用的计价输入：token 用量 + 该模型的输入/输出单价（元 / 100 万 tokens）。</summary>
/// <param name="PromptTokens">提示词 token 数。</param>
/// <param name="CompletionTokens">补全 token 数。</param>
/// <param name="InputPricePer1M">输入单价；<see langword="null"/> 表示「未配单价」，该段成本按未知处理（绝不计 0）。</param>
/// <param name="OutputPricePer1M">输出单价，同上。</param>
public readonly record struct CostInput(
    int PromptTokens,
    int CompletionTokens,
    decimal? InputPricePer1M,
    decimal? OutputPricePer1M);

/// <summary>
/// 成本金额（输入/输出两段各自可判定「未知」）。
/// 任一段未知时，<see cref="Total"/> 只是<b>下界</b>（未知段按 0 计入），
/// 聚合方必须把该模型列入 <c>unpricedModels</c>，不得当作「已核算的成本」（02-spec BR-2）。
/// </summary>
/// <param name="InputCost">输入段成本；<see langword="null"/> = 未知。</param>
/// <param name="OutputCost">输出段成本；<see langword="null"/> = 未知。</param>
public readonly record struct CostAmount(decimal? InputCost, decimal? OutputCost)
{
    /// <summary>两段皆未知（未配单价）。</summary>
    public static readonly CostAmount Unknown = new(null, null);

    /// <summary>两段皆未知。</summary>
    public bool IsFullyUnknown => InputCost is null && OutputCost is null;

    /// <summary>任一段未知 —— 此时 <see cref="Total"/> 是下界。</summary>
    public bool HasUnknownPart => InputCost is null || OutputCost is null;

    /// <summary>至少有一段算得出来（聚合时可进成本，但须带「不完整」标注）。</summary>
    public bool HasKnownPart => InputCost is not null || OutputCost is not null;

    /// <summary>总额；未知段按 0 计入，故 <see cref="HasUnknownPart"/> 时为下界。</summary>
    public decimal Total => (InputCost ?? 0m) + (OutputCost ?? 0m);
}

/// <summary>
/// 成本纯函数引擎（PILOT-033 原子任务 A3；02-spec FR-3.1~3.4、BR-1~BR-2）。
/// 零 IO：单价由调用方注入，本类不读库、不缓存、不取时间，故可被单测完全锁死单位换算。
/// 落位于 CostScope 插件（2026-10-05 输入N+4「以插件为主，宿主侧也迁到插件」），宿主侧不留第二份真相。
/// </summary>
public static class CostCalculationService
{
    /// <summary>计价单位 = 每 100 万 tokens（FR-3.2）。</summary>
    public const decimal TokensPerPriceUnit = 1_000_000m;

    /// <summary>金额小数位：<c>decimal(18,8)</c>（FR-3.2）。</summary>
    public const int CostDecimals = 8;

    /// <summary>单价上界（100 万元 / 100 万 tokens），越界视为非法输入而非真实报价。</summary>
    public const decimal MaxPricePer1M = 1_000_000m;

    /// <summary>
    /// 单价是否在 [0, 1e6] 内。<see langword="null"/>（未配单价）视为可接受，
    /// 由 <see cref="Calculate(CostInput)"/> 归为「未知」——「没配价」不是「配了个错价」。
    /// </summary>
    public static bool IsPriceValid(decimal? pricePer1M) =>
        pricePer1M is null || (pricePer1M >= 0m && pricePer1M <= MaxPricePer1M);

    /// <summary>单次调用成本（FR-3.1 的签名口径：token + 双向单价齐备）。</summary>
    /// <exception cref="ArgumentOutOfRangeException">token 为负数或单价越界。</exception>
    public static CostAmount Calculate(
        int promptTokens,
        int completionTokens,
        decimal inputPricePer1M,
        decimal outputPricePer1M) =>
        Calculate(new CostInput(promptTokens, completionTokens, inputPricePer1M, outputPricePer1M));

    /// <summary>单次调用成本，允许单价缺失以表达「未配单价」。</summary>
    /// <exception cref="ArgumentOutOfRangeException">token 为负数或单价越界。</exception>
    public static CostAmount Calculate(CostInput input)
    {
        if (input.PromptTokens < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input), input.PromptTokens, "提示词 token 数不可为负数（负用量说明上游数据已失真，必须拒绝而非折算）。");
        }

        if (input.CompletionTokens < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input), input.CompletionTokens, "补全 token 数不可为负数（负用量说明上游数据已失真，必须拒绝而非折算）。");
        }

        if (!IsPriceValid(input.InputPricePer1M))
        {
            throw new ArgumentOutOfRangeException(
                nameof(input), input.InputPricePer1M,
                $"输入单价必须落在 [0, {MaxPricePer1M}]（元 / 100 万 tokens），拒绝静默按 0 元计算。");
        }

        if (!IsPriceValid(input.OutputPricePer1M))
        {
            throw new ArgumentOutOfRangeException(
                nameof(input), input.OutputPricePer1M,
                $"输出单价必须落在 [0, {MaxPricePer1M}]（元 / 100 万 tokens），拒绝静默按 0 元计算。");
        }

        // BR-2：未配的哪一段就是未知，绝不折算成 0 元
        var inputCost = input.InputPricePer1M is null ? (decimal?)null : SegmentCost(input.PromptTokens, input.InputPricePer1M.Value);
        var outputCost = input.OutputPricePer1M is null ? (decimal?)null : SegmentCost(input.CompletionTokens, input.OutputPricePer1M.Value);

        return new CostAmount(inputCost, outputCost);
    }

    /// <summary>一段用量的成本 = tokens ÷ 1e6 × 单价，按 (18,8) 舍入。</summary>
    private static decimal SegmentCost(int tokens, decimal pricePer1M) =>
        Math.Round(tokens / TokensPerPriceUnit * pricePer1M, CostDecimals, MidpointRounding.AwayFromZero);
}
