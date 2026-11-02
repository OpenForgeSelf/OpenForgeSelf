# Spec — B4 权限可配置化

> Task ID：`2026-11-02-b4-permission-profiles`｜AC 编号 **无连字符**（EnforceAcCoverage 机器查，05 映射表逐字含同 token）

## 1 背景与目标

权限语义从代码硬编码升级为**数据可配 + 闸门统一消费 + 可审计**。单一句话验收（战役 §B4）：策略可配且生效（改配置→同工具行为变化实测）；审计可查。

## 2 范围（In / Out）

**In**：`ForgeSelf.Abstractions/PermissionProfiles.cs`（新契约）、`ForgeSelf.Api/Entities/PermissionProfile(.Biz).cs + PermissionDecisionLog(.Biz).cs`（新实体）、`ForgeSelf.Api/Services/PermissionProfileService.cs`（决策+CRUD+审计+会话→Agent 记忆）、`ForgeSelf.Api/Controllers/PermissionsController.cs`（CRUD+审计端点）、`ForgeSelf.Api/Services/ToolRegistry.cs` + `AppBuilder.cs`（监听器接线）、`ForgeSelf.Abstractions/HighRiskToolPolicy.cs`（补默认策略等价语义注释/常量）、`Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs`（包装让位判断，防双问）、`Plugins/AIAgent/Services/AIAgentService.cs` + `RunOrchestratorService.cs`（RememberSessionAgent 两处最小接线）、`Plugins/BrowserUse/`（零逻辑改动，仅文档注明纵深防御关系）。

**Out**：IM 推送（B4.2）、glob 通配、Approver 字段列、taskcenter UI 面板（协同批）、`:51888` 实例任何操作。

## 3 契约（Abstractions/PermissionProfiles.cs）

```csharp
public enum PermissionEffect { Allow = 0, Ask = 1, Deny = 2 }
public sealed class ToolPermissionRule {
    string ToolName;          // 精确工具名（OrdinalIgnoreCase）；"*" = 兜底规则
    PermissionEffect Effect;
    string[] PathAllowlist;   // 文件类：允许路径前缀（大小写不敏感；空=不启用该白名单）
    string[] DomainAllowlist; // 浏览器类：允许域名（含子域，语义同 BrowserGuard）
    string[] CommandAllowlist;// 终端类：允许命令前缀
}
public sealed class PermissionProfileDto {
    long Id; string AgentId;  // 空 = 全局默认 profile
    bool Enabled; IReadOnlyList<ToolPermissionRule> Rules; string? Remark; DateTimeOffset UpdatedAt;
}
public sealed class PermissionDecision { PermissionEffect Effect; string Reason; string Source; }
// Source ∈ "profile:<id>" | "default-policy" | "builtin"
public interface IPermissionProfileService {
    Task<PermissionDecision> DecideAsync(string? sessionId, string toolName, string argsJson, CancellationToken ct = default);
    void RememberSessionAgent(string sessionId, string? agentId);   // sessionId→agentId 记忆（决策匹配用）
    Task<bool> HasRuleForToolAsync(string? agentId, string toolName, CancellationToken ct = default); // MAF 包装让位判断
    Task<IReadOnlyList<PermissionProfileDto>> ListAsync(CancellationToken ct = default);
    Task<PermissionProfileDto> SaveAsync(PermissionProfileDto profile, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<PermissionAuditDto>> QueryAuditAsync(PermissionAuditQuery query, CancellationToken ct = default);
}
public sealed class PermissionAuditQuery { string? SessionId; string? ToolName; string? Kind; DateTimeOffset? From; DateTimeOffset? To; int Page = 1; int PageSize = 50; }
public sealed class PermissionAuditDto { long Id; string Kind; /* "policy-decision" | "tool-approval" */ DateTimeOffset Time; string SessionId; string AgentId; string ToolName; string ArgsHash; string Status; string Source; string Reason; long? ApprovalId; }
```

HTTP（宿主 `ForgeSelf.Api/Controllers/PermissionsController.cs`，camelCase JSON）：

| 端点 | 语义 |
|------|------|
| `GET /api/permissions/profiles` | 列出 profiles（含禁用），按 AgentId 升序 |
| `PUT /api/permissions/profiles/{id}` | 保存（id=0 新建；Rules 全量替换；AgentId 唯一性：同 AgentId 仅一条，冲突 409） |
| `DELETE /api/permissions/profiles/{id}` | 删除 |
| `GET /api/permissions/audit?toolName=&sessionId=&kind=&from=&to=&page=&pageSize=` | 策略决策日志 + ToolApproval 联合，Time 倒序分页 |

## 4 决策语义（PermissionProfileService.DecideAsync）

匹配序：**agentId 精确 profile（Enabled）→ 全局 profile（AgentId=空，Enabled）→ 内置默认策略**。profile 内规则匹配：先精确 ToolName，无则 `"*"` 兜底规则；规则内白名单判定按工具类（文件类=write/move/rename/delete_file 取 path 类参数；浏览器类=browser_open/navigate/download 取 url；终端类=run_terminal_command 取 command）：

1. 规则 Effect=Deny → `Deny(「权限策略拒绝：<工具名>」)`。
2. 规则 Effect=Ask → `Ask(「权限策略要求审批：<工具名>」)`。
3. 规则 Effect=Allow：
   - 该类白名单未配置（空数组）→ `Allow`。
   - 白名单已配置且参数可提取且**全部命中** → `Allow`（**免审批快路径**，本批核心价值）。
   - 已配置但参数未命中：浏览器类 → `Deny(「域名不在白名单…可在权限策略加入」)`（**与现插件 domain_not_allowed 语义等价**，避免「批了仍被插件本地拒」）；文件/终端类 → `Ask(「白名单未命中，转人工审批」)`（回退即现行为）。
   - 已配置但参数不可提取（解析失败/无路径字段）→ `Ask`（fail-safe）。
4. **内置默认策略（无匹配规则/profile）= registry 层 Allow（沉默）**——高风险工具的审批由 MAF 侧 `ApprovalRequiredAIFunction` 包装承担（现状），**零配置红线**；默认策略决策同样写审计日志（Source="default-policy"，Effect=Allow）。

**防双问（AC8）**：`MafTaskLoopAgent.cs:210` 包装条件改为 `HighRiskToolPolicy.RequiresApproval(name) && !await profiles.HasRuleForToolAsync(agentId, name)`——配置了规则的工具让位给 registry 层单点决策；未配置工具行为完全不变。

**会话→Agent 解析**：`RememberSessionAgent(sessionId, agentId)` 由 `AIAgentService.StreamChat/RunAdHocCoreAsync`（agentId 入参处）与 `RunOrchestratorService`（run.AgentId）调用；未记忆/未命中 → 仅全局 profile 参与匹配。

## 5 Interaction Design（API 交互规格 · 本批为无界面后端批）

宿主 HTTP 面（UI 面板属协同批 `2026-11-02-b4-permission-ui`，其 02-spec 另出）：

- `GET /api/permissions/profiles`：200 返回 `{items:[{id,agentId,enabled,rules:[{toolName,effect,pathAllowlist,domainAllowlist,commandAllowlist}],remark,updatedAt}]}`；无 profile 返回空数组（200，不报错）。
- `PUT /api/permissions/profiles/0`：合法 body → 201 + 新 profile（id>0）；AgentId 重复 → 409 `{error}`；Rules 含未知工具名不校验（工具可后装，策略先行）。
- `DELETE /api/permissions/profiles/{id}`：存在 → 200 `{deleted:true}`；不存在 → 404。
- `GET /api/permissions/audit`：200 `{items:[...],total,page,pageSize}`；kind 只接受 `policy-decision|tool-approval`（其余 400）；分页越界返回空 items（200）。
- 错误体统一 `{error: string}`；鉴权同宿主既有 Bearer。

## 6 数据模型（XCode）

- `PermissionProfile`：Id / AgentId(100, 空=全局, 索引) / Enabled(bool) / RulesJson(-1, camelCase) / Remark(500) / CreatedAt / UpdatedAt。
- `PermissionDecisionLog`：Id / Time(DateTime, 索引) / SessionId(100) / AgentId(100) / ToolName(100, 索引) / ArgsHash(64) / Effect(16) / Source(32) / Reason(500) / ApprovalId(long?)。
- 建表：`EnsureCreated` 幂等模式（照 ToolApproval.Biz 重试初始化）；审计日志**只增不改**。

## 7 非功能

- 决策路径**零同步 IO 阻塞循环**：profile 读取走 XCode 缓存（Meta.Cache，Count<1000 同 ToolApproval 模式）；审计写入失败**不得**阻断工具执行（try-catch + XTrace.Warn，fail-open 仅日志面）。
- 审计保留策略：本批不实现清理（量级：每工具调用 1 行），记 03-plan 风险。

## 8 Unknown（不猜，显式登记）

- XCode 在 SQLite 下 bool 列的读取行为（Enabled）以单测实测为准。
- EventBus 监听器在 AppBuilder 接线的确切时机（provider 构建后启动段）以实现时读 `AppBuilder` 启动流为准。
- audit 联合查询在 SQLite 上的分页性能（V1 数据量小，不做优化断言）。

## 9 验收标准（AC1..ACn，05 映射表逐字含同 token）

- **AC1**：契约先行——`ForgeSelf.Abstractions/PermissionProfiles.cs` + `PermissionsController` 端点骨架编译绿，契约快照知会 taskcenter-dev（send_message 留痕）。
- **AC2**：PermissionProfile + PermissionDecisionLog 实体落库——EnsureCreated 幂等，CRUD/审计查询单测绿（SQLite 真库）。
- **AC3**：零配置红线——无任何 profile 时决策=内置默认（registry 层 Allow 沉默 + MAF 包装审批不变），既有过滤集全绿零行为变化。
- **AC4**：策略可配且生效——配置 profile（deny/ask/allow+白名单各一）后同工具同参数决策变化实测（单测覆盖 §4 全部分支）。
- **AC5**：pre-execute 闸门消费——Deny → `ToolOutcome.Denied`；Ask → 走 IApprovalService fail-closed 既有语义；Allow+白名单命中 → 放行（集成测试走 ToolRegistry.ExecuteAsync 真管线）。
- **AC6**：BrowserUse 域名白名单上收——registry 层白名单外 browser_open/navigate/download → Deny（语义等价现插件 domain_not_allowed）；插件 BrowserGuard 本地校验保留（纵深防御，铁律 2 无平行 PermissionBroker）。
- **AC7**：审计可查——GET /api/permissions/audit 联合返回策略决策 + ToolApproval（Time 倒序、toolName/sessionId/kind 过滤、分页）。
- **AC8**：防双问——配置规则的工具 MAF 包装让位（单测）；未配置工具包装行为不变（既有 MafTaskLoopAgent 测试全绿）。
- **AC9**：门禁——中档全量 `dotnet test` 收口，对表基线零新增红；契约/实体/服务/控制器构建 0 错误。
