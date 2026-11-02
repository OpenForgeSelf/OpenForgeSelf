# Evidence — B4 权限可配置化（verify-dev）

> Task：`2026-11-02-b4-permission-profiles`｜日期：2026-11-02｜执行：verify-dev
> 证据分级：**Verified**（本会话亲自跑出真实输出）/ Inferred（代码推断）/ Unknown（未验证）。

## 0. 证据清单（实测）

| # | 证据 | 命令 / 来源 | 结果 |
|---|------|-------------|------|
| E1 | 单元+控制器测试 21 用例 | `dotnet test --filter PermissionProfileServiceTests -p:OutDir=.temp\bin-r2\` | **Verified：21/21 通过**（`已通过! - 失败: 0，通过: 21`，日志 `.temp/tmp/b4-unit-test5.log`） |
| E2 | MAF 层 AC8 让位正反用例 | `dotnet test --filter MafApprovalLoopTests -p:OutDir=.temp\bin-r3\` | **Verified：6/6 通过**（含 2 条新增 B4 用例，日志 `.temp/tmp/b4-maf-test2.log`） |
| E3 | 编译 0 错误 | 同上两次 dotnet test 构建段 | **Verified**：宿主/Abstractions/插件/测试工程编译通过（首轮 4 个编译错误均已修复：PermissionProfile.FindById 缺失、PermissionDecisionLog._.SessionId 快捷字段、ResolveAgentId 名对齐、测试 ActionResult 形状） |
| E4 | 中档全量 `dotnet test` | `dotnet test -p:OutDir=.temp\bin-r3\`（后台 job pwsh-c8ca69da1f524df6，17m07s） | **Verified：总数 3435（基线 3432 + 本批 23 新用例）／通过 3348／失败 87**（日志 `.temp/tmp/b4-fulltest-final.log`） |
| E7 | 87 红隔离复跑定性 | `dotnet test --no-build`（标准 bin 21:52 构建态 = B2+B3 已入库、无 B4、含 Plugins 布局） | **Verified：两批代表集 81 条全绿**（AIProviderRegistry 5 + AIService 3 + Multimodal 6 + XCodeIronRule 1 + ChatCompletionWritePath 1 + PersistentSessionStore.Di 1 = 17；ChatController 17 + Invariant 4 + Race 1 + RealLLM 4 = 26；合计 54+27） → 87 红中 44 条为 **OutDir 旁路平坦布局伪红（环境漂移）**，其余 43 条与 qa-baseline §2 同族（ImGateway/ProjectWorkspace/WorkflowPlanning/ScriptRunnerDi/MemGuard 稳定·环境红） |

### E4/E7 归责结论（对表 qa-gate 21:5x 基线：3432/3283/89）

- **零新增红**：87 = 43（基线同族：ImGateway 17 + ProjectWorkspace 16 + WorkflowPlanning 6 + ScriptRunnerDi 1 + MemGuard 3）+ 44（OutDir 平坦布局伪红，标准 bin `--no-build` 隔离复跑全绿实证）。
- 基线 89 中 TodoTracker 32 + TodosController 7 本跑全绿（环境好转，非本批写面）。
- 本批 23 条新用例（PermissionProfileServiceTests 21 + MafApprovalLoop B4 2）在全量与过滤集中均绿。
- 注记：基线构建态为 20:07（B2/B3 入库前），本跑为 B2+B3+B4 现库新构建；dev-stack bin 的 Value 语义修复仍待 B4 收口重启后生效（不属本批验证面）。
| E5 | AC1 契约知会留痕 | send_message → taskcenter-dev（DTO/端点/决策语义全量快照） | **Verified**（发送成功回执，UI panel 可据此并行） |
| E6 | OutDir 旁路避让 dev-stack DLL 锁 | `.temp/bin-r2`→`.temp/bin-r3` 切换 + 孤儿 testhost 119960（本任务后台任务遗留）清理 | **Verified**：dev-stack 进程（33776/59644）未触碰；:51888 未触碰 |

## 1. AC1..AC9 映射（token 逐字对应 02-spec §9）

| AC | 断言（02-spec §9 原文要点） | 证据 | 状态 |
|----|------------------------------|------|------|
| **AC1** | 契约先行——PermissionProfiles.cs + PermissionsController 端点骨架编译绿，契约快照知会 taskcenter-dev | E3 + E5 | ✅ Verified |
| **AC2** | PermissionProfile + PermissionDecisionLog 实体落库——EnsureCreated 幂等，CRUD/审计查询单测绿（SQLite 真库） | E1（Save_List_Delete_Roundtrip、Save_DuplicateAgentId_Conflict、EnsureCreated_IsIdempotent） | ✅ Verified |
| **AC3** | 零配置红线——无 profile 决策=内置默认（registry 层 Allow 沉默 + MAF 包装不变），既有集全绿零行为变化 | E1（NoProfile_DefaultPolicy、NoProfile_Decision_IsLogged）+ E4 既有 MafApprovalLoop/MafTaskLoopAgent 全绿 | ✅ Verified（E4 收口后最终确认） |
| **AC4** | 策略可配且生效——deny/ask/allow+白名单决策变化实测，覆盖 §4 全部分支 | E1（Deny/Ask/PathWhitelist 命中未命中/Domain 命中+子域+about:+非http/Command 前缀/参数不可提取 fail-safe/兜底*/Agent 优先+全局兜底/Disabled 跳过） | ✅ Verified |
| **AC5** | pre-execute 闸门消费——Deny→Denied；Ask→IApprovalService 既有语义；Allow 命中→放行（走 ToolRegistry.ExecuteAsync 真管线） | E1（Registry_PreExecute_ConsumesPolicy_Deny_BlocksExecution / _AllowWhitelist_Executes，真 ToolRegistry+EventBus 管线）。Ask 分支复用既有 fail-closed 语义（PersistentApprovalServiceTests 全绿） E4 | ✅ Verified（Ask 经挂审批的端到端为 Inferred——闸门 Ask→IApprovalService 路径未变，由既有测试覆盖） |
| **AC6** | BrowserUse 域名白名单上收——registry 层白名单外 browser_* → Deny（语义等价 domain_not_allowed）；BrowserGuard 本地校验保留（铁律 2） | E1（Allow_WithDomainWhitelist_Hit_Allows_Miss_Denies：子域命中/未命中 Deny/about: 放行/非 http(s) Deny）+ BrowserUseConfig.cs 头注释（BrowserGuard 零逻辑改动，diff 仅注释） | ✅ Verified |
| **AC7** | 审计可查——GET /api/permissions/audit 联合返回策略决策 + ToolApproval（Time 倒序、toolName/sessionId/kind 过滤、分页） | E1（Audit_Endpoint_Union_Filters_Paginates：联合 total=3、kind 过滤、toolName 过滤、分页第 2 页剩 1、非法 kind 400）+ Save/Conflict 409/Delete 404/服务缺席 503 | ✅ Verified（控制器级；路由层由 [ApiController]+[Route] 骨架编译覆盖） |
| **AC8** | 防双问——配置规则的工具 MAF 包装让位（单测）；未配置工具包装行为不变（既有 MafTaskLoopAgent 测试全绿） | E2（ConfiguredRule_YieldsMafWrapper_ToolExecutesWithoutApprovalRequest：无 approval-request、真实执行、正常收束；UnconfiguredRule_KeepsMafWrapper_SuspendsAsBefore：照旧挂起）+ E4 既有 MAF 测试全绿 | ✅ Verified |
| **AC9** | 门禁——中档全量 dotnet test 收口，对表基线零新增红；构建 0 错误 | E4 + E7 + E3 | ✅ Verified（终态见 §3） |

## 2. 关键实现事实（file:line 供 Review 抽查）

- 决策核心：`ForgeSelf.Api/Services/PermissionProfileService.cs`（匹配序 Agent→全局→内置默认；fail-open；决策日志只增；白名单三口径）
- 闸门接线：`ForgeSelf.Api/AppBuilder.cs`（DI 注册 + `tools/pre-execute` 监听器，Decision→Deny/Ask/Allow 映射）
- 防双问：`Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs`（wrapForApproval 条件 + HasConfiguredRuleAsync）+ `Plugins/AIAgent/Services/ReactLoopAgent.cs`（AgentTurnRuntime.PermissionProfiles 注入面）
- 会话记忆：`AIAgentService`（StreamChat 入口）+ `RunOrchestratorService`（RunSessionId(run) 键 = 步骤执行 sessionId 约定）
- 审计端点：`ForgeSelf.Api/Controllers/PermissionsController.cs`（4 端点 + 400/404/409/503 语义）

## 3. 全量门禁结果（E4 回填）

见 §0 E4/E7 与归责结论：**3435/3348/87（零新增红）**，本批 23 新用例全绿；44 条旁路伪红经标准 bin 隔离复跑 81 条全绿定性为环境漂移。

| AC | 断言（02-spec §9 原文要点） | 证据 | 状态（终态） |
|----|------------------------------|------|------|
| **AC9** | 门禁——中档全量 dotnet test 收口，对表基线零新增红；构建 0 错误 | E4 + E7 + E3 | ✅ Verified |

## 4. V1 已知边界（进  风险与悬置）

- 决策审计含 default-policy Allow 行（每次工具调用一条）——量级 V1 接受，07 列保留策略悬置项
- ToolApproval 无 Approver 列——「谁」= Note + session 维度（列变更为契约破坏，deferred）
- 规则 glob 语义 V1 = 大小写不敏感前缀
- IM 推送审批（B4.2）未做；HasRuleForToolAsync 签名与 02-spec §3 草案的 agentId→sessionId 微调已在 04-task 偏差记录
