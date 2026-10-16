---
task_id: 2026-10-15-aiagent-composer-upgrade
feature: 041-ai-agent-composer-context-bar   # 同域功能档案（上下文栏批次接续本批）
risk: { level: L1, triggers: ["Plugins/AIAgent/web/src/**（纯前端，零后端）"], raised_by_agent: false }
gate1: { mode: user, at: 2026-10-15, ref: "日记 2026-10-15 输入4「批准」（P1=A 行内触发 / P2=A 两批次 / P3=A 挂 chip）" }
gate2: { mode: agent, at: 2026-10-16, decision: APPROVED }
gate3: { mode: agent, at: 2026-10-16 }
expected_files:
  - Plugins/AIAgent/web/src/agent-core/components/ComposeTrigger.vue
  - Plugins/AIAgent/web/src/agent-core/components/ContextChips.vue
  - Plugins/AIAgent/web/src/agent-core/utils/trigger.js
  - Plugins/AIAgent/web/src/agent-core/components/ChatWidget.vue
  - Plugins/AIAgent/web/src/agent-core/config.js
  - Plugins/AIAgent/web/src/agent-core/styles/widget.css
  - Plugins/AIAgent/web/src/composer.ts
  - Plugins/AIAgent/web/src/AiAgentView.vue
  - Plugins/AIAgent/web/src/composer.test.ts
rollback: "git revert <commit>（未提交前：工作树回退到 HEAD）"
writeback: { feature_doc: done, reason: "041 功能档案随后续上下文栏批次入库" }
---

# 04 Task — 新组件 composer 升级（闸门1 已批 2026-10-15；头部块 2026-10-16 补记）

> ⛔ **闸门1 未批准前，禁止改动任何业务文件。** 本文为开工前最后一件工件。

## 批次 1（纯前端，零后端）

| ID | 任务 | 落点 | 验收 |
| --- | --- | --- | --- |
| T1 | `ComposeTrigger.vue`：触发面板骨架。入参 `trigger`（字符）、`groups`（`() => Promise<TriggerGroup[]>`）、`onPick`；支持过滤、↑↓/Enter/Esc/Tab、`role=listbox` + `aria-activedescendant`、Esc 归还焦点 | `agent-core/components/` | AC-2 / AC-3 / NFR-2 |
| T2 | `ContextChips.vue`：chips 行渲染，`config.context.chips` 驱动，点 × 触发宿主回调，点项触发宿主动作 | `agent-core/components/` | AC-1 |
| T3 | `ChatWidget.vue`：footer 内挂载 T1/T2；`onKeydown` 增加 `/` `@` 触发判定（行首或空白后 + `isComposing` 保护）；`config.js` 加 `triggers` / `context` 两个默认 `null` 的键 | `agent-core/` | AC-5（不传时零变化） |
| T4 | `widget.css`：面板与 chips 样式（只用 `--el-*` 与既有 `--aiw-*`） | `agent-core/styles/` | NFR-1 |
| T5 | 宿主组装命令分组：`buildCommandGroups()` = 内置命令（`/clear`、`/model`、`/agent`、`/skill`…）+ 工具（`/api/ai-agent/chat/tools`）+ 技能（`/api/project/skills`｜`/api/skills`）+ 脚本（`/api/scripts`），四组分组 + 搜索 | `AiAgentView.vue` / `agentBridge.ts` | AC-2 |
| T6 | 宿主 `@` 文件解析器：基于 `GET /api/project/files?path=` 逐级遍历（限深 4、排除 `node_modules/.git/dist/bin/obj`），内存模糊匹配 | `AiAgentView.vue` | AC-4 |
| T7 | 宿主上下文 chips 数据 + 项目选择面板（批次 1 走 `browse-directories`，复用既有 `POST /api/project/directory` 链路） | `AiAgentView.vue` | AC-1 |
| T8 | 单测：组件侧（触发判定/键盘/无障碍/chips/默认关闭回归）+ 宿主侧（命令分组组装、文件过滤、排除规则） | `agent-core/**/*.test.ts` | AC-1~AC-5 |
| T9 | 门禁 + 现场：插件 build、宿主 check、design:check、全量 vitest、插件冒烟 e2e、:7399 现场验收 + 截图 | — | AC-7 |

## 批次 2（含后端）

| ID | 任务 | 落点 |
| --- | --- | --- |
| T10 | `GET /api/project/list` 暴露 `IProjectRegistryService.GetAll()`（按最近活动倒序）+ xUnit | `ProjectController.cs` |
| T11 | 前端项目面板接入已登记项目列表（已登记优先，下方保留「浏览其它目录…」） | `AiAgentView.vue` |
| T12 | （视体验）文件搜索端点替代前端逐级遍历 | 后端 + 前端 |
| T13 | 全量门禁 + 现场验收 + 走查 | — |

## 门禁清单（每批次收尾）

- 插件前端 `pnpm run build`
- 宿主 `pnpm run check`（0 error）、`pnpm run design:check`
- 全量 `vitest run`（不得新增红）
- 插件冒烟 e2e 1 条（主链路）
- `dotnet build` + 相关 xUnit（仅批次 2）
- dev-stack（:7399）现场验收 + 截图

## 闸门1 —— 请拍板三项

### P1 入口形态

| 选项 | 说明 | 代价 |
| --- | --- | --- |
| **A（推荐）行内触发为主** | `/` 面板 + `@` 提及 + chips 行 + 一个「上下文」入口按钮 | 老用户要重新找位置 |
| B 原样搬回 5 个 popover | 与旧 ChatPanel 一模一样 | 中栏拥挤；组件偏离源形态，以后同步升级成本翻倍 |
| C 两套都上 | 操作栏 + 行内触发并存 | 两条路径维护两套状态 |

### P2 批次划分

| 选项 | 说明 |
| --- | --- |
| **A（推荐）两批** | 批次 1 纯前端（能力全可用，项目走「浏览目录」），批次 2 补 `GET /api/project/list` 升级为「选项目」 |
| B 一次做全 | 含后端一起交付，周期更长、单次改动面更大 |

### P3 选中「工具 / 技能」命令的语义

| 选项 | 说明 |
| --- | --- |
| **A（推荐）挂 chip** | 不写进输入文本，与后端 `selectedToolNames` / `selectedSkillIds` 语义一致 |
| B 插入 `/工具名` 文本 | 模型会把工具名当自然语言处理，与后端契约脱节 |

## 范围边界（Allowed / Forbidden，2026-10-16 补记；与实作一致）

- **Allowed（批次 1）**：`Plugins/AIAgent/web/src/**`（agent-core 组件侧 + composer.ts 组装层 + AiAgentView 接线 + 单测）；`config.js` 新增默认 null 键；widget.css 只用 `--el-*`/既有 `--aiw-*`。
- **Forbidden**：后端任何文件（批次 2 才动）；`agent-core` 内出现 ai-agent/forgeself 业务字样与端点；新自定义颜色 token；恢复 5 个常驻 popover（闸门1 P1=A 已否）。
- 实作结果核对：未碰后端（07 交付表「未改动宿主」）；门禁四件套 + 冒烟 e2e 全绿。
