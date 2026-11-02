namespace ForgeSelf.Api.Plugins.BrowserUse;

/// <summary>
/// 域名白名单与 SSO 识别。未在白名单的 http(s) 一律拒绝；不代填、不绕过 SSO。
/// </summary>
internal static class BrowserGuard
{
    public static bool IsUrlAllowed(string? url, IEnumerable<string> allowedDomains)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return true;
        if (url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)) return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;
        return IsHostAllowed(uri, allowedDomains);
    }

    public static bool IsHostAllowed(Uri uri, IEnumerable<string> allowedDomains)
    {
        var host = uri.IdnHost.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0) return false;
        foreach (var raw in allowedDomains)
        {
            var entry = NormalizeDomain(raw);
            if (entry.Length == 0) continue;
            if (host.Equals(entry, StringComparison.OrdinalIgnoreCase)) return true;
            if (host.EndsWith("." + entry, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>身份提供商与常见 SSO 路径。命中后调用方必须停在人工接管，不得继续填表或点击。</summary>
    public static bool IsSso(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;

        var host = uri.IdnHost.ToLowerInvariant();
        if (host is "login.microsoftonline.com"
            or "login.microsoft.com"
            or "login.live.com"
            or "accounts.google.com"
            or "accounts.youtube.com")
            return true;
        if (host.EndsWith(".okta.com", StringComparison.Ordinal) || host.EndsWith(".auth0.com", StringComparison.Ordinal))
            return true;

        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("/saml/", StringComparison.Ordinal)
            || path.Contains("/adfs/", StringComparison.Ordinal)
            || path.Contains("/oauth/", StringComparison.Ordinal)
            || path.Contains("/sso/", StringComparison.Ordinal)
            || path.Contains("/openid-connect", StringComparison.Ordinal)
            || path.Contains("/realms/", StringComparison.Ordinal)
            || path.EndsWith("/saml", StringComparison.Ordinal)
            || path.EndsWith("/sso", StringComparison.Ordinal);
    }

    public static string NormalizeDomain(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Trim();
        if (s.StartsWith("*.", StringComparison.Ordinal)) s = s[2..];
        if (Uri.TryCreate(s, UriKind.Absolute, out var abs) && !string.IsNullOrEmpty(abs.IdnHost))
            s = abs.IdnHost;
        else
        {
            var slash = s.IndexOf('/');
            if (slash >= 0) s = s[..slash];
            var colon = s.LastIndexOf(':');
            if (colon > 0 && !s.Contains(']')) s = s[..colon];
        }
        return s.Trim().Trim('.').ToLowerInvariant();
    }
}
