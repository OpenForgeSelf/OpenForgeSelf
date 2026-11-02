using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 权限策略服务（B4）：ToolRegistry pre-execute 闸门的策略消费源。
/// 决策匹配序 agentId profile → 全局 profile → 内置默认（HighRiskToolPolicy 等价语义 =
/// registry 层沉默 Allow，高风险审批由 MAF 包装承担——零配置红线）。
/// </summary>
public sealed class PermissionProfileService : IPermissionProfileService
{
    private static readonly JsonSerializerOptions RuleJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>文件类工具（路径白名单）。</summary>
    private static readonly HashSet<string> FileTools = new(StringComparer.OrdinalIgnoreCase)
    { "write_file", "move_file", "rename_file", "delete_file" };

    /// <summary>浏览器类工具（域名白名单；上收自 BrowserUse 插件 config）。</summary>
    private static readonly HashSet<string> BrowserTools = new(StringComparer.OrdinalIgnoreCase)
    { "browser_open", "browser_navigate", "browser_download" };

    /// <summary>终端类工具（命令前缀白名单）。</summary>
    private static readonly HashSet<string> TerminalTools = new(StringComparer.OrdinalIgnoreCase)
    { "run_terminal_command" };

    private readonly ConcurrentDictionary<string, string> _sessionAgents = new(StringComparer.Ordinal);
    private int _initialized;
    private readonly object _initializeGate = new();

    /// <summary>双表幂等初始化（照 PersistentApprovalService 重试模式）。</summary>
    private void EnsureTable()
    {
        if (Volatile.Read(ref _initialized) == 1) return;
        lock (_initializeGate)
        {
            if (Volatile.Read(ref _initialized) == 1) return;
            for (var attempt = 0; attempt < 6; attempt++)
            {
                if (PermissionProfile.EnsureCreated() && PermissionDecisionLog.EnsureCreated())
                {
                    Volatile.Write(ref _initialized, 1);
                    return;
                }
                if (attempt < 5) Thread.Sleep(100 * (attempt + 1));
            }
            throw new InvalidOperationException("PermissionProfile / PermissionDecisionLog 表初始化失败");
        }
    }

    /// <inheritdoc />
    public void RememberSessionAgent(string sessionId, string? agentId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        _sessionAgents[sessionId] = agentId ?? string.Empty;
    }

    /// <inheritdoc />
    public Task<PermissionDecision> DecideAsync(string? sessionId, string toolName, string argsJson, CancellationToken ct = default)
    {
        PermissionDecision decision;
        try
        {
            EnsureTable();
            var tool = (toolName ?? string.Empty).Trim();
            var agentId = ResolveAgentId(sessionId);
            var rule = MatchRule(agentId, tool, out var source);
            decision = rule == null
                ? new PermissionDecision { Effect = PermissionEffect.Allow, Reason = "无匹配策略规则，走内置默认（高风险审批由 MAF 包装承担）", Source = "default-policy" }
                : DecideByRule(rule, tool, argsJson, source);

            LogDecision(sessionId, agentId, tool, argsJson, decision);
        }
        catch (Exception ex)
        {
            // 决策失败 fail-open（不阻断工具管线），只记日志
            XTrace.Log.Warn("[PermissionProfileService] 决策异常 fail-open: tool={0} err={1}", toolName, ex.Message);
            decision = new PermissionDecision { Effect = PermissionEffect.Allow, Reason = $"决策异常 fail-open: {ex.Message}", Source = "builtin" };
        }
        return Task.FromResult(decision);
    }

    /// <inheritdoc />
    public Task<bool> HasRuleForToolAsync(string? sessionId, string toolName, CancellationToken ct = default)
    {
        try
        {
            EnsureTable();
            var tool = (toolName ?? string.Empty).Trim();
            if (tool.Length == 0) return Task.FromResult(false);

            // 会话→Agent 解析（未记忆 = 仅全局兜底）
            var agentId = ResolveAgentId(sessionId);

            // 匹序：agentId 精确 → 全局（agentId 为空只查一次）
            foreach (var agent in string.IsNullOrWhiteSpace(agentId) ? new[] { string.Empty } : new[] { agentId, string.Empty })
            {
                var profile = PermissionProfile.FindByAgentId(agent);
                if (profile == null || !profile.Enabled) continue;
                foreach (var rule in DeserializeRules(profile.RulesJson))
                {
                    if (string.Equals(rule.ToolName?.Trim(), tool, StringComparison.OrdinalIgnoreCase)) return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[PermissionProfileService] HasRuleForTool 异常按未配置处理: {0}", ex.Message);
            return Task.FromResult(false);
        }
    }

    /// <summary>按 02-spec §4 分支对单条规则决策。</summary>
    private static PermissionDecision DecideByRule(ToolPermissionRule rule, string tool, string argsJson, string source)
    {
        switch (rule.Effect)
        {
            case PermissionEffect.Deny:
                return new PermissionDecision { Effect = PermissionEffect.Deny, Reason = $"权限策略拒绝：{tool}", Source = source };
            case PermissionEffect.Ask:
                return new PermissionDecision { Effect = PermissionEffect.Ask, Reason = $"权限策略要求审批：{tool}", Source = source };
        }

        // Effect=Allow：按工具类做白名单校验
        if (FileTools.Contains(tool)) return CheckList(rule.PathAllowlist, ExtractArg(argsJson, "path"), "路径", "路径不在白名单，转人工审批", source, Matches);
        if (BrowserTools.Contains(tool))
        {
            var url = ExtractArg(argsJson, "url");
            // 浏览器内部地址（about:/blob:）与现插件语义一致直接放行（不进域名白名单判定）
            if (!string.IsNullOrWhiteSpace(url)
                && (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)))
                return new PermissionDecision { Effect = PermissionEffect.Allow, Reason = "浏览器内部地址（about/blob），策略放行", Source = source };
            var host = ExtractHost(url);
            if (host == null && !string.IsNullOrWhiteSpace(url))
            {
                // 非 http(s) 或非法绝对地址：与现插件语义一致拒绝跳转
                return new PermissionDecision { Effect = PermissionEffect.Deny, Reason = $"url 非法或非 http(s)，拒绝跳转：{url}", Source = source };
            }
            return CheckList(rule.DomainAllowlist, host, "域名", $"域名不在白名单（{host ?? "无法解析 url"}）：可在权限策略加入后重试", source, DomainMatches);
        }
        if (TerminalTools.Contains(tool)) return CheckList(rule.CommandAllowlist, ExtractArg(argsJson, "command"), "命令", "命令不在白名单，转人工审批", source, Matches);
        return new PermissionDecision { Effect = PermissionEffect.Allow, Reason = $"权限策略放行：{tool}", Source = source };
    }

    /// <summary>白名单判定：未配置 = 放行；配置且命中 = 放行；配置未命中/不可提取 = fallback（浏览器 Deny，其余 Ask）。</summary>
    private static PermissionDecision CheckList(string[] allowlist, string? value, string label, string missReason, string source, Func<string, string, bool> matcher)
    {
        if (allowlist == null || allowlist.Length == 0)
            return new PermissionDecision { Effect = PermissionEffect.Allow, Reason = "白名单未启用，策略放行", Source = source };
        if (string.IsNullOrWhiteSpace(value))
            return new PermissionDecision { Effect = PermissionEffect.Ask, Reason = $"{label}白名单已配置但参数不可提取，fail-safe 转审批", Source = source };

        var hit = allowlist.Any(a => matcher(value!, a));
        return hit
            ? new PermissionDecision { Effect = PermissionEffect.Allow, Reason = $"白名单命中，免审批放行", Source = source }
            : new PermissionDecision
            {
                Effect = label == "域名" ? PermissionEffect.Deny : PermissionEffect.Ask,
                Reason = missReason,
                Source = source,
            };
    }

    /// <summary>匹配语义：路径/命令 = 前缀（大小写不敏感，斜杠归一）；域名 = 相等或子域。</summary>
    private static bool Matches(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        var v = value.Trim().Replace('/', '\\');
        var p = pattern.Trim().Replace('/', '\\');
        return v.StartsWith(p, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>从参数 JSON 提取首个存在的指定键（顶层；忽略大小写）。</summary>
    private static string? ExtractArg(string? argsJson, string key)
    {
        if (string.IsNullOrWhiteSpace(argsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase)) continue;
                return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>从 url 提取主机名（语义对齐 BrowserGuard.NormalizeDomain；非法/非 http(s) 返回 null 走 fail-safe）。</summary>
    private static string? ExtractHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)) return url;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;
        var host = uri.IdnHost.Trim().TrimEnd('.').ToLowerInvariant();
        return host.Length == 0 ? null : host;
    }

    /// <summary>域名命中：相等或以其为后缀（子域），大小写不敏感。</summary>
    private static bool DomainMatches(string host, string entry)
    {
        var e = entry.Trim().TrimStart('*', '.').Trim().Trim('.').ToLowerInvariant();
        if (e.Length == 0) return false;
        return host.Equals(e, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + e, StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveAgentId(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        return _sessionAgents.TryGetValue(sessionId, out var agentId) && agentId.Length > 0 ? agentId : null;
    }

    /// <summary>匹配序：agentId 精确 profile → 全局 profile；返回首个命中的精确/兜底规则。</summary>
    private ToolPermissionRule? MatchRule(string? agentId, string tool, out string source)
    {
        foreach (var agent in string.IsNullOrWhiteSpace(agentId) ? new[] { string.Empty } : new[] { agentId.Trim(), string.Empty })
        {
            var profile = PermissionProfile.FindByAgentId(agent);
            if (profile == null || !profile.Enabled) continue;
            var rules = DeserializeRules(profile.RulesJson);
            var rule = rules.FirstOrDefault(r => string.Equals(r.ToolName?.Trim(), tool, StringComparison.OrdinalIgnoreCase))
                ?? rules.FirstOrDefault(r => string.Equals(r.ToolName?.Trim(), "*", StringComparison.Ordinal));
            if (rule != null)
            {
                source = $"profile:{profile.Id}";
                return rule;
            }
        }
        source = "default-policy";
        return null;
    }

    private static IReadOnlyList<ToolPermissionRule> DeserializeRules(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<ToolPermissionRule>>(rulesJson, RuleJsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[PermissionProfileService] 规则 JSON 解析失败按空规则: {0}", ex.Message);
            return [];
        }
    }

    /// <summary>决策落审计日志（只增；失败不阻断，异常内部吞掉）。</summary>
    private void LogDecision(string? sessionId, string? agentId, string tool, string argsJson, PermissionDecision decision)
    {
        try
        {
            new PermissionDecisionLog
            {
                SessionId = sessionId ?? string.Empty,
                AgentId = agentId ?? string.Empty,
                ToolName = tool,
                ArgsHash = HashArgs(argsJson),
                Effect = decision.Effect.ToString(),
                Source = decision.Source,
                Reason = decision.Reason?.Length > 500 ? decision.Reason[..500] : decision.Reason ?? string.Empty,
                ApprovalId = 0,
            }.Insert();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[PermissionProfileService] 决策日志写入失败（不阻断）: {0}", ex.Message);
        }
    }

    private static string HashArgs(string? argsJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(argsJson ?? "{}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PermissionProfileDto>> ListAsync(CancellationToken ct = default)
    {
        EnsureTable();
        var rows = PermissionProfile.FindAllOrdered();
        IReadOnlyList<PermissionProfileDto> list = rows.Select(ToDto).ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<PermissionProfileDto?> SaveAsync(PermissionProfileDto profile, CancellationToken ct = default)
    {
        EnsureTable();
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        var agentId = (profile.AgentId ?? string.Empty).Trim();

        var conflict = PermissionProfile.FindByAgentId(agentId);
        PermissionProfile row;
        if (profile.Id > 0)
        {
            row = PermissionProfile.FindById(profile.Id);
            if (row == null)
            {
                row = new PermissionProfile { Id = 0, AgentId = agentId };
            }
            else if (conflict != null && conflict.Id != row.Id)
            {
                return Task.FromResult<PermissionProfileDto?>(null);
            }
        }
        else
        {
            if (conflict != null) return Task.FromResult<PermissionProfileDto?>(null);
            row = new PermissionProfile { AgentId = agentId };
        }

        row.AgentId = agentId;
        row.Enabled = profile.Enabled;
        row.RulesJson = SerializeRules(profile.Rules);
        row.Remark = profile.Remark ?? string.Empty;
        row.Save();

        XTrace.Log.Info("[PermissionProfileService] 保存 profile id={0} agent={1} rules={2}", row.Id, row.AgentId, profile.Rules?.Count ?? 0);
        return Task.FromResult<PermissionProfileDto?>(ToDto(row));
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        EnsureTable();
        var row = PermissionProfile.FindById(id);
        if (row == null) return Task.FromResult(false);
        row.Delete();
        XTrace.Log.Info("[PermissionProfileService] 删除 profile id={0} agent={1}", id, row.AgentId);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<PermissionAuditDto> Items, int Total)> QueryAuditAsync(PermissionAuditQuery query, CancellationToken ct = default)
    {
        EnsureTable();
        var q = query ?? new PermissionAuditQuery();
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize <= 0 ? 50 : q.PageSize, 1, 200);
        var from = q.From?.UtcDateTime;
        var to = q.To?.UtcDateTime;

        var decisions = PermissionDecisionLog.SearchOrdered(q.ToolName, q.SessionId, from, to, 10000, 0)
            .Select(r => new PermissionAuditDto
            {
                Id = r.Id,
                Kind = "policy-decision",
                Time = new DateTimeOffset(DateTime.SpecifyKind(r.Time, DateTimeKind.Local)),
                SessionId = r.SessionId,
                AgentId = r.AgentId,
                ToolName = r.ToolName,
                ArgsHash = r.ArgsHash,
                Status = r.Effect,
                Source = r.Source,
                Reason = r.Reason ?? string.Empty,
                ApprovalId = r.ApprovalId > 0 ? r.ApprovalId : null,
            });

        // 审批记录：有 sessionId 按会话查，否则全量
        var approvals = string.IsNullOrWhiteSpace(q.SessionId)
            ? ToolApproval.FindAll(ToolApproval._.Id > 0)
            : ToolApproval.FindAllBySessionIdOrdered(q.SessionId);

        var approvalDtos = approvals
            .Where(r => q.ToolName == null || string.Equals(r.ToolName, q.ToolName, StringComparison.OrdinalIgnoreCase))
            .Where(r => from == null || r.CreatedAt >= from)
            .Where(r => to == null || r.CreatedAt <= to)
            .Select(r => new PermissionAuditDto
            {
                Id = r.Id,
                Kind = "tool-approval",
                Time = new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)),
                SessionId = r.SessionId,
                AgentId = string.Empty,
                ToolName = r.ToolName,
                ArgsHash = r.ArgumentsHash,
                Status = r.Status,
                Source = r.RiskLevel,
                Reason = string.IsNullOrWhiteSpace(r.Note) ? r.Description : $"{r.Description}（备注：{r.Note}）",
                ApprovalId = r.Id,
            });

        IEnumerable<PermissionAuditDto> merged = decisions.Cast<PermissionAuditDto>().Concat(approvalDtos);
        if (string.Equals(q.Kind, "policy-decision", StringComparison.OrdinalIgnoreCase))
            merged = merged.Where(i => i.Kind == "policy-decision");
        else if (string.Equals(q.Kind, "tool-approval", StringComparison.OrdinalIgnoreCase))
            merged = merged.Where(i => i.Kind == "tool-approval");

        var ordered = merged.OrderByDescending(i => i.Time).ThenByDescending(i => i.Id).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<PermissionAuditDto>, int)>((items, total));
    }

    private static PermissionProfileDto ToDto(PermissionProfile row) => new()
    {
        Id = row.Id,
        AgentId = row.AgentId,
        Enabled = row.Enabled,
        Rules = DeserializeRulesStatic(row.RulesJson),
        Remark = row.Remark,
        UpdatedAt = new DateTimeOffset(row.UpdatedAt),
    };

    private static IReadOnlyList<ToolPermissionRule> DeserializeRulesStatic(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<ToolPermissionRule>>(rulesJson, RuleJsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string SerializeRules(IReadOnlyList<ToolPermissionRule>? rules)
        => JsonSerializer.Serialize(rules ?? [], RuleJsonOptions);
}
