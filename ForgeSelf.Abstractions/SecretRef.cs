using System.Text.Json;

namespace ForgeSelf.Abstractions;

/// <summary>
/// 凭据保险库引用（SecretRef）：配置只保存引用，不保存明文/密文。
/// 方案真源：<c>docs/08-security/credential-vault-design.md</c> §4。
/// 引用只定位，<b>不携带授权</b>——知道 SecretRef 不代表能使用凭据。
/// </summary>
public sealed record SecretRef
{
    /// <summary>引用来源固定为宿主保险库</summary>
    public const string VaultSource = "vault";

    /// <summary>引用 Id 前缀（sec_ + 32 位小写 hex）</summary>
    public const string IdPrefix = "sec_";

    /// <summary>「跟随最新轮值」版本标记</summary>
    public const string CurrentVersion = "current";

    public string Source { get; init; } = VaultSource;

    /// <summary>凭据引用 Id（sec_ 前缀）</summary>
    public string Id { get; init; } = "";

    /// <summary>current（跟随最新）或固定轮值版本号字符串</summary>
    public string Version { get; init; } = CurrentVersion;

    /// <summary>生成新引用 Id</summary>
    public static SecretRef New() => new() { Id = IdPrefix + Guid.NewGuid().ToString("N") };

    /// <summary>序列化为紧凑 JSON（配置落盘形态）</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    /// <summary>尝试解析并校验引用；任何形态不合法返回 false。</summary>
    public static bool TryParse(string? json, out SecretRef? secretRef)
    {
        secretRef = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        SecretRef? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SecretRef>(json, JsonOpts);
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed == null) return false;
        if (!IsValid(parsed)) return false;

        secretRef = parsed;
        return true;
    }

    /// <summary>解析失败返回 null（调用方按「非法引用」处理，Fail Closed）。</summary>
    public static SecretRef? ParseOrNull(string? json) => TryParse(json, out var r) ? r : null;

    private static bool IsValid(SecretRef r)
    {
        if (!string.Equals(r.Source, VaultSource, StringComparison.Ordinal)) return false;
        if (string.IsNullOrEmpty(r.Id) || r.Id.Length != IdPrefix.Length + 32) return false;
        if (!r.Id.StartsWith(IdPrefix, StringComparison.Ordinal)) return false;
        foreach (var ch in r.Id.AsSpan(IdPrefix.Length))
        {
            // 只接受小写 hex（与 Guid.ToString("N") 生成形态一致；大写一律拒绝，避免双形态引用）
            var isLowerHex = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f');
            if (!isLowerHex) return false;
        }

        // Version：空 → current；否则只允许 current 或正整数（固定轮值版本）
        var version = string.IsNullOrEmpty(r.Version) ? CurrentVersion : r.Version;
        if (!string.Equals(version, CurrentVersion, StringComparison.Ordinal))
        {
            if (!ulong.TryParse(version, out var v) || v == 0) return false;
        }

        return true;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
}
