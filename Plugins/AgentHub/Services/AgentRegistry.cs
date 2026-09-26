using System.Text.Json;
using ForgeSelf.Api.Plugins.AgentHub.Entities;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using NewLife;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// Agent 注册表实现：XCode 实体读写 + profile 预填 + DTO 映射。
/// </summary>
public class AgentRegistry : IAgentRegistry
{
    private readonly ProfileLoader _profiles;

    public AgentRegistry(ProfileLoader profiles)
    {
        _profiles = profiles;
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentDto> List(Boolean enabledOnly = false)
    {
        var list = enabledOnly
            ? AgentDefinition.FindAllEnabled()
            : AgentDefinition.FindAll(null, AgentDefinition._.Priority.Desc() + "," + AgentDefinition._.Id.Asc(), null, 0, 0);

        return list.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public AgentDto? Get(Int32 id)
    {
        if (id <= 0) return null;

        var entity = AgentDefinition.FindById(id);
        return entity == null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public AgentDto? GetByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        var entity = AgentDefinition.Find(AgentDefinition._.Name == name);
        return entity == null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public AgentDto Create(AgentSaveRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Name.IsNullOrEmpty()) throw new ArgumentException("显示名不能为空", nameof(request));
        if (request.Vendor.IsNullOrEmpty()) throw new ArgumentException("厂商标识不能为空", nameof(request));

        // 唯一性校验一律直查数据库（FindList / Find 走 Meta.Cache，可能返回过期快照甚至
        // 其它物理库的实体；正确性关键路径不能容忍这种偏差，理由见 FindAllByVendor 的注释）。
        if (AgentDefinition.FindCount(AgentDefinition._.Name == request.Name) > 0)
            throw new ArgumentException($"已存在同名 Agent「{request.Name}」", nameof(request));

        // 同 vendor 只允许登记一次（同一份 CLI 装一个实例，多份会职责不清）
        var sameVendor = AgentDefinition.FindAllByVendor(request.Vendor);
        if (sameVendor.Count > 0)
            throw new ArgumentException($"厂商标识「{request.Vendor}」已被 Agent「{sameVendor[0].Name}」占用", nameof(request));

        var entity = new AgentDefinition
        {
            Name = request.Name.Trim(),
            Vendor = request.Vendor.Trim(),
            Kind = request.Kind ?? "Coding",
            Tags = request.Tags == null ? null : String.Join(",", request.Tags),
            Enabled = request.Enabled ?? true,
            Priority = request.Priority ?? 0,
            DefaultCwd = request.DefaultCwd,
            Notes = request.Notes
        };

        var profile = _profiles.Get(entity.Vendor);
        entity.DisplayName = profile?.DisplayName;

        // 能力矩阵：显式传入优先，否则按 profile 填充
        var matrix = request.Capabilities
                     ?? (profile?.Capabilities == null ? null : AgentCapabilityMatrix.FromFlags(profile.Capabilities));
        entity.CapabilitiesJson = (matrix ?? new AgentCapabilityMatrix()).ToJson();

        // 策略：显式传入优先；危险模式一律拒绝写入
        var policy = request.Policy ?? new AgentPolicy();
        if (AgentPolicy.IsForbiddenMode(policy.PermissionMode))
            throw new ArgumentException($"权限模式「{policy.PermissionMode}」禁止使用（不得设为默认）", nameof(request));
        entity.PolicyJson = policy.ToJson();

        entity.Insert();

        // 交互口：不传 → 按 profile 预填；传空数组 → 显式不建（用户稍后手填）
        if (request.AccessPoints == null)
        {
            CreateAccessPointsFromProfile(entity.Id, profile);
        }
        else if (request.AccessPoints.Count > 0)
        {
            SaveAccessPoints(entity.Id, request.AccessPoints);
        }

        XTrace.Log.Info("[AgentHub] 已登记 Agent {0}（{1}，Id={2}）", entity.Name, entity.Vendor, entity.Id);

        return Get(entity.Id)!;
    }

    /// <inheritdoc />
    public AgentDto? Update(Int32 id, AgentSaveRequest request)
    {
        if (id <= 0 || request == null) return null;

        var entity = AgentDefinition.FindById(id);
        if (entity == null) return null;

        if (request.Name != null)
        {
            var trimmed = request.Name.Trim();
            if (trimmed.IsNullOrEmpty()) throw new ArgumentException("显示名不能为空", nameof(request));

            var dup = GetByName(trimmed);
            if (dup != null && dup.Id != id)
                throw new ArgumentException($"已存在同名 Agent「{trimmed}」", nameof(request));

            entity.Name = trimmed;
        }

        if (request.Kind != null) entity.Kind = request.Kind;
        if (request.Tags != null) entity.Tags = String.Join(",", request.Tags);
        if (request.DefaultCwd != null) entity.DefaultCwd = request.DefaultCwd;
        if (request.Enabled.HasValue) entity.Enabled = request.Enabled.Value;
        if (request.Priority.HasValue) entity.Priority = request.Priority.Value;
        if (request.Notes != null) entity.Notes = request.Notes;

        if (request.Capabilities != null) entity.CapabilitiesJson = request.Capabilities.ToJson();

        if (request.Policy != null)
        {
            if (AgentPolicy.IsForbiddenMode(request.Policy.PermissionMode))
                throw new ArgumentException($"权限模式「{request.Policy.PermissionMode}」禁止使用（不得设为默认）", nameof(request));

            entity.PolicyJson = request.Policy.ToJson();
        }

        entity.Update();

        // 交互口：null = 保持原样；非 null = 全量替换
        if (request.AccessPoints != null)
        {
            // 先清旧的（全量替换语义，避免残留脏交互口）
            foreach (var ap in AgentAccessPoint.FindAllByAgentId(id))
            {
                ap.Delete();
            }
            SaveAccessPoints(id, request.AccessPoints);
        }

        XTrace.Log.Info("[AgentHub] 已更新 Agent {0}（Id={1}）", entity.Name, entity.Id);

        return Get(entity.Id);
    }

    /// <inheritdoc />
    public Boolean Delete(Int32 id)
    {
        if (id <= 0) return false;

        var entity = AgentDefinition.FindById(id);
        if (entity == null) return false;

        // 级联删交互口（任务与事件保留，用于复盘）
        foreach (var ap in AgentAccessPoint.FindAllByAgentId(id))
        {
            ap.Delete();
        }

        entity.Delete();

        XTrace.Log.Info("[AgentHub] 已删除 Agent {0}（Id={1}）", entity.Name, id);
        return true;
    }

    /// <inheritdoc />
    public AgentDto? SetTrust(Int32 id, Boolean trusted, IReadOnlyList<String>? scopes)
    {
        if (id <= 0) return null;

        var entity = AgentDefinition.FindById(id);
        if (entity == null) return null;

        // 授信必须指明范围：只授信空范围等于没授信，容易被误解为「全权」
        if (trusted && (scopes == null || scopes.Count == 0))
            throw new ArgumentException("授信必须指定范围（如 write_file,exec_command），不支持无范围授信", nameof(scopes));

        entity.Trusted = trusted;
        entity.TrustedScopes = trusted ? String.Join(",", scopes!) : null;
        entity.Update();

        XTrace.Log.Info("[AgentHub] Agent {0} 授信状态变更为 {1}（范围: {2}）",
            entity.Name, trusted, entity.TrustedScopes ?? "无");

        return Get(id);
    }

    /// <inheritdoc />
    public AgentAccessPointDto? GetDefaultAccessPoint(Int32 agentId)
    {
        if (agentId <= 0) return null;

        var list = AgentAccessPoint.FindAllByAgentId(agentId);
        if (list.Count == 0) return null;

        var ap = list.FirstOrDefault(e => e.IsDefault) ?? list[0];
        return ToAccessPointDto(ap);
    }

    /// <inheritdoc />
    public AgentAccessPointDto? GetAccessPoint(Int32 accessPointId)
    {
        if (accessPointId <= 0) return null;

        var ap = AgentAccessPoint.FindById(accessPointId);
        return ap == null ? null : ToAccessPointDto(ap);
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentDto> SelectCandidates(String? facet, String? tag)
    {
        var candidates = new List<AgentDto>();

        foreach (var entity in AgentDefinition.FindAllEnabled())
        {
            // 必须有可用交互口才谈得上派活
            var aps = AgentAccessPoint.FindAllByAgentId(entity.Id);
            if (aps.Count == 0) continue;

            if (!facet.IsNullOrEmpty())
            {
                var matrix = AgentCapabilityMatrix.FromJson(entity.CapabilitiesJson);
                if (!matrix.Supports(facet)) continue;
            }

            if (!tag.IsNullOrEmpty())
            {
                var tags = SplitTags(entity.Tags);
                if (!tags.Any(t => t.Contains(tag, StringComparison.OrdinalIgnoreCase))) continue;
            }

            candidates.Add(ToDto(entity, aps));
        }

        return candidates;
    }

    /// <inheritdoc />
    public void UpdateHealth(Int32 accessPointId, String health, String? version, String? error)
    {
        if (accessPointId <= 0) return;

        var ap = AgentAccessPoint.FindById(accessPointId);
        if (ap == null) return;

        ap.Health = health;
        ap.LastVersion = version;
        ap.LastError = error;
        ap.LastProbeTime = DateTime.Now;
        ap.Update();
    }

    #region 内部方法
    /// <summary>按 profile 预填交互口（内置 profile 的开箱即用路径）</summary>
    private void CreateAccessPointsFromProfile(Int32 agentId, AgentProfile? profile)
    {
        if (profile == null) return;

        var ap = new AgentAccessPoint
        {
            AgentId = agentId,
            Mode = "OneShot",
            Transport = "Cli",
            Executable = profile.Executable,
            ArgsTemplate = BuildArgsTemplate(profile),
            PromptInjection = profile.PromptInjection,
            OutputFormat = profile.OutputFormat,
            OutputMappingJson = profile.OutputMapping == null
                ? null
                : JsonSerializer.Serialize(profile.OutputMapping),
            SessionFlagTemplate = profile.SessionFlagTemplate,
            ProbeArgs = profile.ProbeArgs,
            CancelSupported = false, // one-shot 只有杀进程，不算真正的取消能力
            Health = "Unknown",
            IsDefault = true
        };
        ap.Insert();
    }

    /// <summary>把 profile 的权限模板拼进参数模板（占位符 {permission} 由执行期替换）</summary>
    private static String BuildArgsTemplate(AgentProfile profile)
    {
        var args = profile.ArgsTemplate;

        // profile 未在 argsTemplate 里写 {permission}，但有独立权限模板时补上
        if (!profile.PermissionFlagTemplate.IsNullOrEmpty() && !args.Contains("{permission}"))
        {
            args = $"{args} {profile.PermissionFlagTemplate}";
        }

        return args;
    }

    /// <summary>批量保存交互口（全量替换语义）</summary>
    private void SaveAccessPoints(Int32 agentId, IReadOnlyList<AgentAccessPointSaveRequest> requests)
    {
        var isFirst = true;
        foreach (var req in requests)
        {
            var ap = new AgentAccessPoint
            {
                AgentId = agentId,
                Mode = req.Mode ?? "OneShot",
                Transport = req.Transport ?? "Cli",
                Executable = req.Executable,
                ArgsTemplate = req.ArgsTemplate,
                EnvJson = req.EnvVars == null ? null : JsonSerializer.Serialize(req.EnvVars),
                PromptInjection = req.PromptInjection ?? "Arg",
                OutputFormat = req.OutputFormat ?? "Text",
                OutputMappingJson = req.OutputMappingJson,
                SessionFlagTemplate = req.SessionFlagTemplate,
                CancelSupported = req.CancelSupported ?? false,
                ProbeArgs = req.ProbeArgs,
                Health = "Unknown",
                // 用户没显式指定时，第一条默认作为默认交互口
                IsDefault = req.IsDefault ?? isFirst
            };
            ap.Insert();
            isFirst = false;
        }
    }

    /// <summary>实体 → DTO</summary>
    private AgentDto ToDto(AgentDefinition entity) => ToDto(entity, AgentAccessPoint.FindAllByAgentId(entity.Id));

    /// <summary>实体 → DTO（复用已查出的交互口，避免 N+1 查询）</summary>
    private static AgentDto ToDto(AgentDefinition entity, IList<AgentAccessPoint> accessPoints)
    {
        var dto = new AgentDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Vendor = entity.Vendor,
            DisplayName = entity.DisplayName,
            Kind = entity.Kind,
            Tags = SplitTags(entity.Tags).ToList(),
            Capabilities = AgentCapabilityMatrix.FromJson(entity.CapabilitiesJson),
            DefaultCwd = entity.DefaultCwd,
            Policy = AgentPolicy.FromJson(entity.PolicyJson),
            Enabled = entity.Enabled,
            Priority = entity.Priority,
            Trusted = entity.Trusted,
            TrustedScopes = SplitTags(entity.TrustedScopes).ToList(),
            Notes = entity.Notes,
            AccessPoints = accessPoints.Select(ToAccessPointDto).ToList(),
            CreateTime = entity.CreateTime,
            UpdateTime = entity.UpdateTime
        };

        dto.Health = ComputeHealth(dto.AccessPoints);

        return dto;
    }

    /// <summary>交互口实体 → DTO</summary>
    private static AgentAccessPointDto ToAccessPointDto(AgentAccessPoint ap)
    {
        var dto = ToAccessPointDtoCore(ap);

        // 补齐 vendor：transport 要按 vendor 取 profile 做输出映射
        if (ap.AgentId > 0)
        {
            var agent = AgentDefinition.FindById(ap.AgentId);
            dto.Vendor = agent?.Vendor;
        }

        return dto;
    }

    /// <summary>交互口实体 → DTO（不含 vendor 补齐）</summary>
    private static AgentAccessPointDto ToAccessPointDtoCore(AgentAccessPoint ap)
    {
        var envVars = new Dictionary<String, String>();
        if (!ap.EnvJson.IsNullOrEmpty())
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<String, String>>(ap.EnvJson);
                if (parsed != null) envVars = parsed;
            }
            catch
            {
                // envJson 是用户手填的，坏格式不炸流程，仅忽略（UI 会显示为空）
            }
        }

        return new AgentAccessPointDto
        {
            Id = ap.Id,
            AgentId = ap.AgentId,
            Mode = ap.Mode,
            Transport = ap.Transport,
            Executable = ap.Executable,
            ArgsTemplate = ap.ArgsTemplate,
            EnvVars = envVars,
            PromptInjection = ap.PromptInjection,
            OutputFormat = ap.OutputFormat,
            SessionFlagTemplate = ap.SessionFlagTemplate,
            CancelSupported = ap.CancelSupported,
            ProbeArgs = ap.ProbeArgs,
            Health = ap.Health,
            LastProbeTime = ap.LastProbeTime,
            LastVersion = ap.LastVersion,
            LastError = ap.LastError,
            IsDefault = ap.IsDefault
        };
    }

    /// <summary>整体健康状态 = 各交互口最差者（Missing > Degraded > Unknown > Ok）</summary>
    /// <param name="accessPoints">交互口 DTO 列表</param>
    /// <returns>健康状态</returns>
    private static String ComputeHealth(IReadOnlyList<AgentAccessPointDto> accessPoints)
    {
        if (accessPoints.Count == 0) return "Missing";

        var worst = "Ok";
        var worstRank = 0;
        foreach (var ap in accessPoints)
        {
            var rank = ap.Health switch
            {
                "Ok" => 0,
                "Unknown" => 1,
                "Degraded" => 2,
                "Missing" => 3,
                _ => 1
            };
            if (rank > worstRank)
            {
                worstRank = rank;
                worst = ap.Health;
            }
        }
        return worst;
    }

    /// <summary>拆分逗号分隔的标签/范围（去空去重）</summary>
    private static IEnumerable<String> SplitTags(String? raw)
    {
        if (raw.IsNullOrEmpty()) yield break;

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }
    #endregion
}
