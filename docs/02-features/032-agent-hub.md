---
feature_key: F032
feature_no: 032
status: implemented
last_updated: 2026-11-19
aliases: ["032-agent-hub"]
---

# 032 · Agent 中枢（AgentHub）—— 外部 Agent 探测与委派总线

> 版本：2026-09-24（v1.0.9；插件包现 `plugin.json` = 1.0.11）· 状态：**已落地（CLI 委派）**；**OAR Runtime Provider = 定案；P0–P4 ✅（当前计划终段）**
> 来源：输入4 调研（CLI-agent 探测/委派生态）→ 输入5（探测支持附加扫描目录）→ 2026-11-19 OAR 定案（ADR-007）
> OAR 架构真源：[`docs/01-architecture/agenthub-oar-runtime.md`](../01-architecture/agenthub-oar-runtime.md)

## 1. 结论先行

| 能力 | 实现 | 状态 |
|---|---|---|
| 外部 agent 登记注册表（vendor 唯一，按 profile 预填交互口） | AgentRegistry + `api/agent-hub/agents` | ✅ |
| 本机探测（不自动登记） | AgentProbeService + `GET api/agent-hub/agents/discover` | ✅ |
| **附加扫描目录（探测时除 PATH 外额外查找）** | `AgentHubSettingsStore` + `GET\|PUT api/agent-hub/settings`（config.json 持久化） | ✅ **v1.0.9 新增** |
| 委派执行（CLI one-shot + 事件流 + 审批台） | DelegationRuntime / CliTransport / `api/agent-hub/tasks` | ✅ |
| 管理面鉴权 | 控制器类级 `[Authorize("ApiKeyPolicy")]`（铁律 17） | ✅ settings/runtimes/sessions/agents/tasks 均已带（remaining-close） |

**一句话**：探测不再是「只沿 PATH」——用户可配置附加扫描目录（每行一个，落盘 `config.json`），装在不进 PATH 的 CLI（pnpm/bun/scoop/自定义安装）也能被发现。

## 2. 探测机制（代码事实，AgentProbeService）

`ResolveExecutable(name)` 的查找顺序：

1. **绝对路径**：`Path.IsPathRooted` → `File.Exists` 直接校验；
2. **PATH**：按 `PATH` 环境变量逐目录 × 候选名（原名 + Windows 扩展名 `.exe/.cmd/.bat/.ps1` 补全）；非法 PATH 项 try/catch 跳过；
3. **附加扫描目录**（v1.0.9）：`AgentHubSettingsStore.Current.SearchDirectories` 逐目录查找，与 PATH 已覆盖目录去重；同样做扩展名补全。

`DiscoverAsync()` 扫描面 = 内置 profile（`Data/Profiles/{claude,codex,opencode,qodercli}.json` 的 Executable）逐个探测 + `KnownLocations` 硬编码兜底（cursor/cursor-agent、aider、gemini）。探测结果区分：未安装（Missing）/ 降级（Degraded，profile 断言不通过不静默）/ 可用（Ok，`--version` 探针）。

## 3. 设置接口契约（v1.0.9 新增）

路由前缀 `api/agent-hub/settings`，**类级 `[Authorize("ApiKeyPolicy")]`**（铁律 17：无 token 直接 401）。

| 方法 | 路径 | 入参 | 返回 |
|---|---|---|---|
| GET | `api/agent-hub/settings` | — | `{ searchDirectories: string[] }` |
| PUT | `api/agent-hub/settings` | `{ searchDirectories: string[] }` | 规范化后结果（去空白/去重/去空项），点即保存落盘 |

持久化位置：**插件数据目录** `{数据根}/plugins/agent-hub/config.json`（生产即 `~/.forgeself/plugins/agent-hub/config.json`），随数据走、发布覆盖不影响；文件损坏时退回默认并告警，不阻断插件。

## 4. 前端（AgentHubView.vue 扫描区）

Agent 目录 tab 工具栏下方新增「附加扫描目录」配置块：textarea（每行一个目录）+ 保存按钮；保存后回填后端规范化结果并提示「重新扫描即生效」；读取失败红色提示（操作成败可见，§3.4 交互要求）。

## 5. 验证与已知事项

- 单测 `AgentHubProbeSettingsTests`（8 个）：附加目录命中 / Windows 扩展名补全 / 绝对路径回归 / 空目录不抛 / 设置存储默认值·落盘重载·规范化·损坏文件容错。
- e2e `ForgeSelf.Web/e2e/plugins/agent-hub/agent-hub.spec.ts`：新增「设置写往返（GET→PUT→GET 持久化）」+「扫描区配置输入 UI 交互」两用例；并修复既有标题断言（版本徽标 → 前缀匹配）。
- **发布坑（v1.0.9 实证）**：版本化更新（`POST /api/plugin/update`）只把新前端产物放进 `versions/<v>/web/dist`，但前端中间件解析**插件根目录 `web/dist`** → 页面仍跑旧 bundle。发布后必须补拷：`versions/<v>/web/dist/*` → 根 `web/dist/`，并 grep 新增关键词核验（见 `plugin-publish-verify` troubleshooting）。
- **已闭合（2026-11-19 remaining-close）**：`AgentHubAgentsController` / `AgentHubTasksController` 类级 `[Authorize("ApiKeyPolicy")]` + `AgentHubAuthTests` 程序集守卫。

## 6. 演进：OAR Runtime Provider（2026-11-19 定案；P0 Worker 已落地）

目标：在**不推翻**本节已落地能力的前提下，把 AgentHub 升级为外部 Coding Agent 的统一 Runtime Provider。

| 项 | 定案 |
| --- | --- |
| 适配层 | AgentHub 内 Node≥24 Worker + `@botiverse/oar`（精确锁版本） |
| Transport | 新增 `Oar`，保留 `Cli`；按 AccessPoint 选路 |
| 跨插件契约 | 新增 `IAgentRuntime`；**保留**现有 `IAgentDelegation` |
| Session | 独立持久化 + 对外 `SessionKey` |
| 与本机 MAF | 正交；不违反 ADR-005 |

阶段：P0 Worker 验证 → P1 契约/通信 → P2 Session/Transport → P3 Provider/UI → P4 集成与测试。  
**P0（2026-11-19）**：`Plugins/AgentHub/RuntimeWorker` 已锁 `@botiverse/oar@0.109.0`，stdio JSON-RPC + `pnpm run smoke` 12/12（scripted）；pilot `docs/ai/pilot/2026-11-19-oar-runtime-worker-p0/`。  
**P1（2026-11-19）**：`ForgeSelf.Abstractions/AgentRuntimeContracts.cs`（`IAgentRuntime` + DTO）+ `Plugins/AgentHub/Services/OarWorkerClient.cs`（stdio JSON-RPC / 生命周期）；过滤测试 6/6；pilot `docs/ai/pilot/2026-11-19-oar-runtime-p1/`。  

**P2（2026-11-19）**：`IAgentTransportRegistry` + `OarTransport`；AccessPoint.`RuntimeId`；XCode `AgentHubSession` + `AgentSessionManager`（SessionKey）；`DelegationRuntime` 按 Transport 选 Cli|Oar，不破 `IAgentDelegation`；过滤测试 29/29；pilot `docs/ai/pilot/2026-11-19-oar-runtime-p2/`。  

**P3（2026-11-19）**：`AgentHubRuntimeProvider` 实现 `IAgentRuntime`（同实例 MS DI + `ctx.Register`；Effect 清理 Worker）；共享 `OarWorkerClient`；Tools/API/UI SessionKey 面（`api/agent-hub/runtimes|sessions`，`hub_ask_agent`）；过滤测试 34/34；pilot `docs/ai/pilot/2026-11-19-oar-runtime-p3/`。  

**P4（2026-11-19）**：消费者试点（`ctx.Get<IAgentRuntime>()` 每次取 + 缺席降级 + Worker 不可用时 Cli Submit）；SessionKey/OarSessionId 对外泄漏守卫；Node≥24 运维说明（架构真源 §13 + `RuntimeWorker/README`）；pilot `docs/ai/pilot/2026-11-19-oar-runtime-p4/`。当前 concurrency-plan **P0–P4 全部 ✅**，不另开 P5。  

**gap-close（2026-11-19）**：补齐第一波未完项——`OarSequence` + `AgentRuntimeRecord` 最小耐久双层事件；§9 `probe`/`models`/`abort`/`records`/`stream`；`runtimes`/`sessions` 类级 ApiKeyPolicy；Session 页签插件 e2e 冒烟；pilot `docs/ai/pilot/2026-11-19-oar-runtime-gap-close/`。  
并发计划：`docs/ai/pilot/2026-11-19-oar-agenthub-runtime/concurrency-plan.md`。  
细节与安全约束只维护在架构真源，本节不复制。

### 6.1 产品面诚实性与 UX（2026-11-19 · ux-real）

| 规则 | 说明 |
| --- | --- |
| scripted 不进产品 | Worker 仍注册 `scripted` 供 smoke/协议测；`ListAgentsAsync` / UI Runtime 列表**剔除** |
| 安装态诚实 | Worker 补充项须读 `runtime.installation`；仅 `kind=available && via=executable` → `Available=true`；`not_found` / `bundled` 原样可解释，禁止假绿 |
| 品牌图标 | 插件前端内联 `@botiverse/oar` brands SVG；缺图标用字母 glyph，禁止统一占位 |
| 页签分区 | 「Runtime / Session」= Runtime 库存（发现/安装真相）与 Session 操作（Steer/Abort）两节，一节一事 |

真源审计样例（Cloud Agent 机）：仅 Grok 为 executable+登录；Claude/Codex/OpenCode=`not_found`。pilot：`docs/ai/pilot/2026-11-19-oar-agenthub-ux-real/`。

## 7. 来源

- 输入4 调研报告：`docs/06-research/002-cli-agent-detection-delegation.md`（MCO 探测三件套 / Forkn 路径可配 / 五元组适配器契约等落地建议）
- OAR 调研与落点核对：`docs/06-research/007-oar-agenthub-runtime.md`
- 裁决：`docs/07-decisions/007-oar-agenthub-runtime.md`
- 技能：`plugin-development` §四（发布闭环）· §3.2（出树构建）· `architecture-design`
