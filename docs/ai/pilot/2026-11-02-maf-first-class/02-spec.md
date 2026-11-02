# Specification

> 阶段：Stage 2｜Task ID：`2026-11-02-maf-first-class`｜日期：2026-11-02

## Functional Requirements

| ID | 需求 |
|----|------|
| FR-1 | 产品愿景真源更新：铸己匣=本机个人可扩展 Agent 平台；用户可见插件名=智能体；执行中枢技术名=MAF（架构层 MAF Runtime） |
| FR-2 | 目标路线图 P0–P4（MAF-centric 命名），明确不做云端计算机/团队企业管理/平行 LocalBot |
| FR-3 | 架构文档：四层（工作台 → MAF Runtime → 执行安全内核 → 本机能力插件）+ 命名对照 |
| FR-4 | ADR-006：执行中枢 MAF 一等 + 绿场无迁移；修订 ADR-005 指向；not-taken 同步 |
| FR-5 | 对外命名：插件菜单/页面标题/功能清单用「智能体」；执行模式文案为开放任务 / 计划任务。不把 MAF 当插件名 |
| FR-6 | 文档标记 ReactLoop/ad-hoc/LocalBot 为废弃或禁止；插件 Id 与路由保持 `ai-agent` / `/ai-agent`（用户已批，不改 `/maf`） |

## Input

- 用户裁决原文（MAF 一等、绿场、插件简化、P0–P4 远景）
- 仓库事实：`MafTaskLoopAgent` 默认、`ToolRegistry`、`AgentRun`、ADR-005

## Output

- 更新/新建文档（vision、goals、principles、direction、maf-centric-architecture、ADR-006、021 节、README 索引）
- 展示层文案（`plugin.json`、`features.ts`、`AiAgentView`/`agentBridge`/`AgentEditDialog`）
- 本 pilot 00–04

## Business Rules

- Chat 只是入口之一；驱动单元叙事为 Task/Run
- 工具统一 Permission → Approval → Execute → Verify → Result
- 能力进插件；禁止平行 Runtime
- 绿场：不做旧名长期兼容层

## Boundary Conditions

- 插件 Id `ai-agent`、路由 `/ai-agent`、API `api/ai-agent/*` **保持**（2026-11-02 用户已批：不改成 `/maf`）
- `executionMode` 存储值 `free`/`plan` **保持**（仅改展示文案为开放任务 / 计划任务）
- 不删除 `ReactLoopAgent` 源码（仅文档废弃）

## Error Handling

- 文档冲突以 ADR-006 + `01-vision.md` + `maf-centric-architecture.md` 为准
- 展示文案改动不影响 API；若 check 失败仅修类型/文案相关

## Compatibility

- **无迁移承诺**（ADR-006 / not-taken 053）
- 旧文档中 FreeLoop/PlanDriven 出现处：本回合优先改愿景/架构/021 快照与 UI；历史 pilot 史料可不批量改写

## Non-functional Requirements

- 真源单一：愿景在 `00-vision/`，架构在 `01-architecture/maf-centric-architecture.md`
- 方案可审计：决策写 ADR；缓做写 not-taken

## Interaction Design（交互设计）

> 本回合仅触及**展示命名**，不改交互流程状态机。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 侧栏/插件菜单 | 打开智能体入口 | 菜单文案显示 **智能体**（原 AI Agent；上一批曾写成 MAF） | 插件已启用 | 无额外 toast | 插件未启用则无菜单 | 路由仍 `/ai-agent`，不改 `/maf` |
| 工作台标题 | 进入页面 | 标题区显示 **智能体** | — | — | — | 欢迎语提及铸己匣智能体 |
| Agent 执行模式 | 编辑 Agent | 选项文案：**开放任务** / **计划任务**（值仍 free/plan；说明可保留 MAF Runtime / MAF Run 技术名） | free\|plan | 保存后 ConfigJson 不变语义 | — | 不改 radio value |
| 执行记录面板 | 打开 | 空态文案提「计划任务」 | — | — | 「暂无执行记录」 | 不改 API |

### 视觉规格

- 本批**不改**色板/间距/组件几何；仅字符串替换。
- 主题矩阵：N/A（无样式面改动）。

### 交互验收标准

- AC-UI-1：菜单或 `plugin.json` frontend.menu 为「智能体」；`Id` 仍 `ai-agent`，`route` 仍 `/ai-agent`
- AC-UI-2：`AgentEditDialog` 可见「开放任务」「计划任务」（不以 MAF 作模式名前缀）
- AC-UI-3：`features.ts` 中 ai-agent 条目 name 为「智能体」，description 写明执行中枢 MAF

### 走查符合性

- 本回合文档批：起源码走查**非必须**（无布局变更）；闸门1 后若继续 UI 深度改版再按 ui-ux-design 全量走查。

## Acceptance Criteria

- [ ] AC1：愿景文件含「MAF 一等」与不做清单
- [ ] AC2：goals 含 P0–P4
- [ ] AC3：架构文含四层 + 命名表
- [ ] AC4：ADR-006 + not-taken 052–054
- [ ] AC5：README §一可点到愿景与 MAF 架构/ADR-006
- [ ] AC6：对外文案三处以上已切
- [ ] AC7：04-task Allowed/Forbidden 明确；无大规模业务重构

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 插件 Id / 路由是否改名为 `maf` | 契约与书签 | **用户已批准（闸门1）**：路由不改。Id 仍 `ai-agent`，路由仍 `/ai-agent`，不改成 `/maf` |
| 用户可见插件名用 MAF 还是「智能体」 | 菜单/标题/功能清单 | **用户已批准（闸门1）**：插件名「智能体」。MAF 仍为执行中枢技术名，不是插件名 |
| 「MAF」是否在 UI 展开全称 Microsoft Agent Framework | 品牌教育 | 不在插件名上展开。产品文案写「智能体（执行中枢 MAF）」；架构层仍称 MAF Runtime |
