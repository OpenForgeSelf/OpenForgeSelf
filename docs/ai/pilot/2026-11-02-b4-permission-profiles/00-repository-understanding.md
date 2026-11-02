# Repository Understanding — B4 权限可配置化

> Task ID：`2026-11-02-b4-permission-profiles`｜执行者：verify-dev｜战役：`docs/ai/campaigns/2026-11-02-maf-runtime-hardening/00-campaign-workflow.md` §B4

## 1 相关架构事实（实读代码）

| 事实 | 依据（文件:行） |
|------|----------------|
| 六闸门管线真源 | `ForgeSelf.Api/Services/ToolRegistry.cs:277-389`：闸门 1 = `tools/pre-execute` SerialAsync 三态（`ForgeSelf.Abstractions/ToolPipeline.cs:21-31`，无监听器 = null = Allow） |
| 生产端尚无 pre-execute 监听器 | grep `OnSerial<ToolExecution` 仅测试命中——策略监听器将是第一个 |
| 审批契约 | `ForgeSelf.Abstractions/IApprovalService.cs`：AskAsync（fail-closed 红线 4）+ CreatePending/Decide/ConsumeContinuations；MAF 泵经 `ToolApprovalRequestContent` 挂起回合 |
| 审批持久化 | `ForgeSelf.Api/Services/PersistentApprovalService.cs`（XCode ToolApproval 表 + EnsureCreated 幂等模式 + 内存 waiter）；记录**无审批人字段**（Note 除外） |
| 审批 HTTP 面 | `Plugins/AIAgent/Controllers/ApprovalsController.cs`：`/api/ai-agent/approvals`（插件控制器经 IContext 取宿主服务） |
| 高风险硬名单 | `ForgeSelf.Abstractions/HighRiskToolPolicy.cs:9-19`：run_terminal_command/write_file/move_file/rename_file/delete_file/browser_click/browser_download |
| MAF 侧包装点 | `Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs:210`：`HighRiskToolPolicy.RequiresApproval(definition.Name)` → `ApprovalRequiredAIFunction`（按工具定义静态包装，无参数上下文） |
| 宿主 DI 接线 | `ForgeSelf.Api/AppBuilder.cs:181-187`：IEventBus 单例 + IToolGuardRegistry + IApprovalService + IToolRegistry |
| 宿主实体形态 | `ForgeSelf.Api/Entities/ToolApproval.cs + ToolApproval.Biz.cs`：手写生成态属性类 + Biz partial（EnsureCreated/校验/查询）；宿主无 Model.xml（仅 `Plugins/AIAgent/Data/Model.xml` 走 xcode 生成） |
| ToolExecution 无 AgentId | `ForgeSelf.Abstractions/ToolPipeline.cs:53-89`：CallId/ToolName/ArgsJson/SessionId——策略按 Agent 匹配需要 sessionId→agentId 解析接缝 |
| IAgentRegistry 探测式取 Agent | `ForgeSelf.Abstractions/Agents.cs:186-196`：TryGetAsync(sessionId)→IAgent；但 AgentOptions（Agents.cs:26-78）**无 AgentId 字段**——agentId 只在 AIAgentService 请求入口（`Plugins/AIAgent/Services/AIAgentService.cs:179/219`）可见 |
| BrowserUse 白名单现状 | `Plugins/BrowserUse/BrowserGuard.cs`（IsUrlAllowed/IsHostAllowed/NormalizeDomain + SSO 识别）；`BrowserSession.cs:132/143/238/352/390` 五处调用；白名单外 = 拒绝结果 `domain_not_allowed`（`BrowserResults.cs:19-24`，不进审批泵）；配置源 = 插件数据目录 config.json `allowedDomains`（默认仅本机）+ `FORGESELF_BROWSER_ALLOWED_DOMAINS` |
| 宿主控制器目录 | `ForgeSelf.Api/Controllers/`（Settings/ApiKeys/UsageStats 等，权限管理页属设置域） |

## 2 约束与红线

- **零配置行为不变**（lead seq5 兼容性红线）：无任何 profile 时，决策必须等价于现状——高风险名单工具经 MAF 包装走审批，registry 层沉默。
- **架构铁律 2**（maf-centric-architecture.md §5）：内核/MAF 禁止平行 PermissionBroker；BrowserUse 域名白名单上收 ToolRegistry，插件本地校验保留作纵深防御。
- **Ask fail-closed**（IApprovalService 红线 4）与 finalize 恰好一次（ToolRegistry 红线 5）不可破坏。
- dev-stack 运行实例锁 DLL（B2 实证 MSB3027）——代码与测试先行，build 报 lead 开窗。

## 3 复用与模式

- 实体：完全照 `ToolApproval(.Biz)` 模式手写（生成态属性类 + Biz partial + EnsureCreated 重试初始化）。
- 服务：照 `PersistentApprovalService` 模式（XCode + EnsureTable + 幂等 + XTrace 日志）。
- 控制器：宿主 `ForgeSelf.Api/Controllers/` 直注 DI（非插件 IContext 模式）。
