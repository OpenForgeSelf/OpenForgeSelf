using System.Text.RegularExpressions;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>
/// 命令/输出/URL 脱敏（审计与 UI 共用）。密码、SSHPASS、私钥、Bearer 等不落原文。
/// </summary>
public static partial class SecretRedactor
{
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var result = text;
        result = BearerRegex().Replace(result, "Bearer ***REDACTED***");
        result = PasswordAssignRegex().Replace(result, "$1=***REDACTED***");
        result = TokenAssignRegex().Replace(result, "$1=***REDACTED***");
        result = ApiKeyAssignRegex().Replace(result, "$1=***REDACTED***");
        result = AuthorizationHeaderRegex().Replace(result, "$1***REDACTED***");
        result = PrivateKeyBlockRegex().Replace(result, "***REDACTED-PRIVATE-KEY***");
        result = SshPassEnvRegex().Replace(result, "SSHPASS=***REDACTED***");
        result = SshPassFlagRegex().Replace(result, "$1***REDACTED***");
        result = UrlPasswordQueryRegex().Replace(result, "$1=***REDACTED***");
        result = BasicAuthInUrlRegex().Replace(result, "$1***REDACTED***@$3");
        return result;
    }

    /// <summary>从查询串中移除 password/pwd 等敏感键（返回新 query，不含明文）。</summary>
    public static string ScrubQueryString(string? query)
    {
        if (string.IsNullOrEmpty(query)) return string.Empty;
        var q = query.TrimStart('?');
        var parts = q.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>();
        foreach (var part in parts)
        {
            var eq = part.IndexOf('=');
            var key = eq >= 0 ? part[..eq] : part;
            if (IsSensitiveQueryKey(key))
            {
                kept.Add($"{key}=***REDACTED***");
                continue;
            }
            kept.Add(part);
        }
        return string.Join('&', kept);
    }

    public static bool IsSensitiveQueryKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        var k = Uri.UnescapeDataString(key).Trim().ToLowerInvariant();
        return k is "password" or "pwd" or "passwd" or "st_password" or "st_pwd"
            or "ssh_password" or "pass";
    }

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.Compiled)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(?i)\b(password|passwd|pwd)\s*[=:]\s*[^\s""']+", RegexOptions.Compiled)]
    private static partial Regex PasswordAssignRegex();

    [GeneratedRegex(@"(?i)\b(token|access_token|refresh_token|auth_token)\s*[=:]\s*[^\s""']+", RegexOptions.Compiled)]
    private static partial Regex TokenAssignRegex();

    [GeneratedRegex(@"(?i)\b(api[_-]?key|secret[_-]?key|client[_-]?secret)\s*[=:]\s*[^\s""']+", RegexOptions.Compiled)]
    private static partial Regex ApiKeyAssignRegex();

    [GeneratedRegex(@"(?i)(Authorization\s*:\s*)\S+", RegexOptions.Compiled)]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----", RegexOptions.Compiled)]
    private static partial Regex PrivateKeyBlockRegex();

    [GeneratedRegex(@"(?i)\bSSHPASS\s*=\s*\S+", RegexOptions.Compiled)]
    private static partial Regex SshPassEnvRegex();

    [GeneratedRegex(@"(?i)(sshpass\s+-(?:p|f)\s*)(\S+)", RegexOptions.Compiled)]
    private static partial Regex SshPassFlagRegex();

    [GeneratedRegex(@"(?i)([?&](?:password|pwd|passwd|st_password|st_pwd|pass))=([^&\s#]+)", RegexOptions.Compiled)]
    private static partial Regex UrlPasswordQueryRegex();

    [GeneratedRegex(@"(?i)(://[^:/\s]+:)([^@/\s]+)(@)", RegexOptions.Compiled)]
    private static partial Regex BasicAuthInUrlRegex();
}
