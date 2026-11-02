# 05 Evidence — 2026-11-02-maf-first-class

> 阶段：Stage 7｜Task ID：`2026-11-02-maf-first-class`｜日期：2026-11-02
> 来源等级：✅=Verified（亲自跑/读图）；本 pilot 范围=FR-1..6 文档与展示命名，配套功能块（审批/技能/记忆/本地模型/BrowserUse/Scheduler/FileTools/McpCenter）由同日并行批实现，走查批（`2026-11-02-maf-walkthrough-fixes`）独立验证。

## AC 逐条验证

| AC | 验证方式 | 结果 |
|----|---------|------|
| AC1 愿景含「MAF 一等」与不做清单 | `docs/00-vision/01-vision.md`（+38 行）、`02-goals.md`（+61）、`04-direction.md`（+25）；not-taken 052–054 见 `07-decisions/not-taken-decisions.md`（+46） | ✅ Verified |
| AC2 goals 含 P0–P4 | `docs/00-vision/02-goals.md` P0–P4 段与 `01-architecture/maf-centric-architecture.md` §6 路线图表一致 | ✅ Verified |
| AC3 架构文含四层+命名表 | `docs/01-architecture/maf-centric-architecture.md` §2 四层图 + §3 命名体系（3.1 产品 / 3.2 代码标识 / 3.3 六闸门叙事） | ✅ Verified |
| AC4 ADR-006 + not-taken 052–054 | `docs/07-decisions/006-maf-first-class-greenfield.md`（新）、`005-maf-local-agent-reuse.md`（新）、not-taken +46 行 | ✅ Verified |
| AC5 README §一可点到愿景/架构/ADR | `README.md` 与 `docs/README.md` 索引更新（各 1 文件改动） | ✅ Verified |
| AC6 对外文案三处以上已切 | ① `Plugins/AIAgent/plugin.json` menu=智能体 ② `ForgeSelf.Web/src/stores/tabs.ts` label=智能体 ③ 宿主 `features.ts` ai-agent 条目=智能体（执行中枢 MAF）④ `AgentEditDialog` 开放任务/计划任务（radio 值仍 free/plan）⑤ 页面标题智能体 v1.9.9 | ✅ Verified（走查截图 01/02/04） |
| AC7 04-task Allowed/Forbidden | `04-task.md` 含 Allowed/Forbidden 节；本批未大规模业务重构（功能代码由并行批承担） | ✅ Verified |

## 浏览器走查证据（起源码 dev-stack :7399）

- 截图目录 `ForgeSelf.Web/screenshots/dev/ai-agent-maf-20261102/`（11 张，均读图核对）：01 首页命名、02 智能体 v1.9.9 页、03 执行记录面板、04 开放/计划任务编辑、05 审批全链路、06 技能名册、07 本地模型类型、08 本地模式开关、09 兜底页、10/11 走查批修复复验。
- 走查批对宿主外残留的同类修复：Home 插件 4 处旧名（PC-021），截图 10 首页零「AI Agent」字样。

## 测试

- 走查批守卫 `MemoryTimestampGuardTests` 3 用例 + 记忆回归 23 条绿（快档，`dotnet test --filter`）。
- 配套功能块测试：`MafApprovalLoopTests` / `PersistentApprovalServiceTests` / `RunTerminalCommandTimeoutKillTests` / `PluginLoadDependencyGuardTests`（本批新增文件，随批提交；全量归 qa 基线档）。

## 未验证 / 风险

- 宿主前端 `pnpm run check/test` 未在本批单跑（走查批打包时 vite build 全过）；e2e 未跑（无本批专属 spec，冒烟以走查替代）。
- `docs/00-vision` 等历史 pilot 史料未批量改写（spec Compatibility 允许）。
