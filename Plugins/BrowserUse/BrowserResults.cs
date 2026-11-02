using System.Text.Encodings.Web;
using System.Text.Json;

namespace ForgeSelf.Api.Plugins.BrowserUse;

internal static class BrowserResults
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Ok(object payload) => JsonSerializer.Serialize(payload, Opts);

    public static string Fail(string code, string error, string? url = null)
        => JsonSerializer.Serialize(new { success = false, code, error, url }, Opts);

    /// <summary>未在白名单：不跳转，并标记需审批（不写入审批泵）。</summary>
    public static string DomainDenied(string? url)
        => JsonSerializer.Serialize(new
        {
            success = false,
            code = "domain_not_allowed",
            error = "域名不在白名单，已拒绝跳转（未执行导航）。请把域名写入插件数据目录 config.json 的 allowedDomains，或经人工确认后再加入白名单。",
            url,
            needsApproval = true
        }, Opts);

    public static string Takeover(string kind, string? url)
        => JsonSerializer.Serialize(new
        {
            success = false,
            code = "human_takeover_required",
            kind,
            error = kind == "sso"
                ? "遇到 SSO，需要人工接管。插件不会代填，也不会绕过。"
                : "遇到 CAPTCHA，需要人工接管。插件不会识别，也不会绕过。",
            url
        }, Opts);
}
