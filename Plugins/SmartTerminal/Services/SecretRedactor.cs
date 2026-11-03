using System.Text.RegularExpressions;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>
/// 命令/输出脱敏（审计与 UI 共用）。对标常见 CLI 审计实践：token/password/key 不落原文。
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
        return result;
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
}
