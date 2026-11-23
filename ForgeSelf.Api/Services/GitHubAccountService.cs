using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ForgeSelf.Api.Services;

/// <summary>GitHub 设备流绑定服务（RFC 8628）：申请设备码 → 轮询令牌 → 拉取账号 → 落绑定。</summary>
/// <remarks>
/// <para>GitHub 无官方 .NET SDK，本服务按 RFC 8628 手工对接（2026-11-22 seqT5 结论）。</para>
/// <para><b>协议语义不抛错</b>：轮询阶段的 400 + authorization_pending / slow_down 是协议内的
/// 「继续等」信号，绝不按状态码抛异常；只有网络故障与不可恢复错误（expired_token / access_denied）
/// 才以异常/终态上报。申请阶段失败原因经 <see cref="DescribeStartFailure"/> 透传上游
/// error / error_description（截断 ≤200，不再吞成一句「申请失败」——2026-11-22 输入20 seqT4 修复）。</para>
/// <para><b>预填</b>：GitHub 设备流实测不返回 verification_uri_complete（探针结论 2026-11-22 输入21），
/// 由 <see cref="BuildVerificationUri"/> 本地构造 user_code 查询参数。</para>
/// </remarks>
public sealed class GitHubAccountService
{
    private readonly GitHubOAuthConfig _config;
    private readonly GitHubBindingStore _store;
    private readonly HttpClient _http;

    public GitHubAccountService(GitHubOAuthConfig config, GitHubBindingStore store, HttpClient? http = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _http = http ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.Contains("Accept"))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    }

    #region 申请设备码

    /// <summary>申请设备码（RFC 8628 §3.1）。返回给前端的 verificationUri 已带预填。</summary>
    public async Task<DeviceCodeStartResult> StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.IsConfigured)
            throw new InvalidOperationException("GitHub OAuth 未配置：缺少 GitHubOAuth:ClientId");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _config.ClientId,
            ["scope"] = _config.Scopes ?? "",
        });
        using var resp = await _http.PostAsync(_config.DeviceCodeUrl, content, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(DescribeStartFailure(body));

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var deviceCode = GetString(root, "device_code") ?? throw new InvalidOperationException("GitHub 响应缺少 device_code");
        var userCode = GetString(root, "user_code") ?? throw new InvalidOperationException("GitHub 响应缺少 user_code");
        var verificationUri = GetString(root, "verification_uri") ?? "https://github.com/login/device";
        var expiresIn = GetInt(root, "expires_in") ?? 900;
        var interval = GetInt(root, "interval") ?? 5;

        return new DeviceCodeStartResult
        {
            DeviceCode = deviceCode,
            UserCode = userCode,
            VerificationUri = verificationUri,
            VerificationUriPrefilled = BuildVerificationUri(verificationUri, userCode),
            ExpiresIn = expiresIn,
            Interval = interval,
        };
    }

    /// <summary>
    /// 归并上游失败原因：优先取 JSON 的 error_description / error；非 JSON 则透传响应体；
    /// 合并后截断 ≤200 字符，全部为空回退固定文案。绝不抛出、绝不返回 null。
    /// </summary>
    public static string DescribeStartFailure(string? responseBody)
    {
        var parts = new List<string>();
        var trimmed = responseBody?.Trim() ?? "";
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                var desc = GetString(root, "error_description");
                var err = GetString(root, "error");
                var message = GetString(root, "message");
                if (!string.IsNullOrWhiteSpace(desc)) parts.Add(desc);
                if (!string.IsNullOrWhiteSpace(err)) parts.Add(err);
                if (parts.Count == 0 && !string.IsNullOrWhiteSpace(message)) parts.Add(message);
            }
            catch (JsonException)
            {
                // 非 JSON 落到下方透传
            }
        }
        if (parts.Count == 0 && trimmed.Length > 0) parts.Add(trimmed);
        if (parts.Count == 0) parts.Add("GitHub 未返回失败原因");

        var merged = string.Join(" · ", parts);
        return merged.Length <= 200 ? merged : merged[..200];
    }

    /// <summary>设备流预填：GitHub 不返回 verification_uri_complete，本地拼 user_code 查询参数。</summary>
    public static string BuildVerificationUri(string verificationUri, string userCode)
    {
        var baseUri = verificationUri.Trim();
        if (string.IsNullOrWhiteSpace(userCode)) return baseUri;
        var separator = baseUri.Contains('?') ? "&" : "?";
        return $"{baseUri}{separator}user_code={Uri.EscapeDataString(userCode)}";
    }

    #endregion

    #region 轮询令牌

    /// <summary>轮询一次令牌（RFC 8628 §3.4）。协议内等待态返回 Pending/SlowDown，不抛异常。</summary>
    public async Task<PollOutcome> PollAsync(DeviceCodeStartResult start, CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _config.ClientId,
            ["device_code"] = start.DeviceCode,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
        });
        using var resp = await _http.PostAsync(_config.TokenUrl, content, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var error = GetString(root, "error");

        var accessToken = GetString(root, "access_token");
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            var login = await FetchAccountLoginAsync(accessToken, cancellationToken);
            _store.SaveToken(accessToken, login, GetString(root, "scope") ?? _config.Scopes ?? "");
            return new PollOutcome { Status = PollStatus.Bound, AccountLogin = login };
        }

        switch (error)
        {
            case "authorization_pending":
                return new PollOutcome { Status = PollStatus.Pending };
            case "slow_down":
                return new PollOutcome { Status = PollStatus.SlowDown };
            case "expired_token":
                return new PollOutcome { Status = PollStatus.Expired, Error = "设备码已过期，请重新发起绑定" };
            case "access_denied":
                return new PollOutcome { Status = PollStatus.Denied, Error = "用户在 GitHub 端拒绝了授权" };
            default:
                // 非协议等待态的失败（bad_verification_code 等）：透传原因，终态
                return new PollOutcome { Status = PollStatus.Failed, Error = DescribeStartFailure(body) };
        }
    }

    /// <summary>用访问令牌拉取账号 login；失败回退 "github-user"（绑定仍生效，仅展示名降级）。</summary>
    private async Task<string> FetchAccountLoginAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{_config.ApiBase.TrimEnd('/')}/user");
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
            req.Headers.TryAddWithoutValidation("User-Agent", "OpenForgeSelf");
            using var resp = await _http.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            return GetString(doc.RootElement, "login") ?? "github-user";
        }
        catch
        {
            return "github-user";
        }
    }

    #endregion

    #region 绑定状态

    /// <summary>绑定状态（不含令牌）。</summary>
    public GitHubBindingDto GetStatus() => _store.Get();

    /// <summary>解绑。</summary>
    public void Unbind() => _store.Unbind();

    /// <summary>绑定态开关（插件 GitHub 拉取是否自动附凭据）。</summary>
    public void SetAutoAttach(bool enabled) => _store.SetAutoAttach(enabled);

    /// <summary>出站调用取令牌明文：仅「已绑定 + 自动附凭据开」时返回；禁止落日志/回响应。</summary>
    public string? GetBearerToken() => ShouldAttachToken() ? _store.GetTokenAsync() : null;

    /// <summary>是否应自动附凭据（已绑定 + 开关打开）。</summary>
    public bool ShouldAttachToken() => _store.Get() is { Bound: true, AutoAttachToken: true };

    #endregion

    #region JSON 工具

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.TryGetInt32(out var i)
            ? i
            : null;

    #endregion
}

/// <summary>设备码申请结果（RFC 8628 §3.2）。</summary>
public sealed class DeviceCodeStartResult
{
    /// <summary>设备码（仅用于轮询，不展示给用户）。</summary>
    public string DeviceCode { get; set; } = "";

    /// <summary>用户码（展示并预填）。</summary>
    public string UserCode { get; set; } = "";

    /// <summary>官方验证页地址（未预填）。</summary>
    public string VerificationUri { get; set; } = "";

    /// <summary>预填后的验证页地址（user_code 已带）。</summary>
    public string VerificationUriPrefilled { get; set; } = "";

    /// <summary>设备码有效期（秒，GitHub 默认 900）。</summary>
    public int ExpiresIn { get; set; }

    /// <summary>建议轮询间隔（秒，GitHub 默认 5）。</summary>
    public int Interval { get; set; }
}

/// <summary>轮询结果状态。</summary>
public enum PollStatus
{
    /// <summary>用户尚未在 GitHub 端完成授权（协议内等待）。</summary>
    Pending,

    /// <summary>GitHub 要求放慢轮询（协议内等待，间隔 +5s）。</summary>
    SlowDown,

    /// <summary>绑定成功。</summary>
    Bound,

    /// <summary>设备码过期（终态，需重新申请）。</summary>
    Expired,

    /// <summary>用户拒绝授权（终态）。</summary>
    Denied,

    /// <summary>其他失败（终态，Error 带上游原因）。</summary>
    Failed,
}

/// <summary>轮询结果（终态时 Error 带原因；Bound 时带账号）。</summary>
public sealed class PollOutcome
{
    /// <summary>结果状态。</summary>
    public PollStatus Status { get; set; }

    /// <summary>绑定成功的账号 login。</summary>
    public string? AccountLogin { get; set; }

    /// <summary>终态失败原因（已透传上游，≤200 字符）。</summary>
    public string? Error { get; set; }
}
