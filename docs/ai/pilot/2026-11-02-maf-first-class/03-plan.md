# Plan

> 阶段：Stage 3｜Task ID：`2026-11-02-maf-first-class`｜日期：2026-11-02

## Files To Change

### 本回合（闸门1 前可完成的文档 + 展示骨架）

| file | reason |
|------|--------|
| `docs/00-vision/01-vision.md` | 产品远景 MAF-centric |
| `docs/00-vision/02-goals.md` | P0–P4 路线图 |
| `docs/00-vision/03-principles.md` | P9/P10、N6/N7 |
| `docs/00-vision/04-direction.md` | 演进方向对齐 |
| `docs/01-architecture/maf-centric-architecture.md` | 新建：四层+命名 |
| `docs/07-decisions/006-maf-first-class-greenfield.md` | 新建 ADR |
| `docs/07-decisions/005-maf-local-agent-reuse.md` | 指向 006 |
| `docs/07-decisions/not-taken-decisions.md` | 050 加固 + 052–054 |
| `docs/README.md` | §一登记 |
| `README.md` | 简介/特性：对外「智能体」，执行中枢 MAF |
| `docs/02-features/021-ai-agent.md` | 插件名智能体；路由不改；MAF 仍为执行中枢技术名 |
| `docs/15-roadmap/index.md` | 指向 goals P0–P4 |
| `Plugins/AIAgent/plugin.json` | Name/menu「智能体」；Id `ai-agent`、route `/ai-agent` 不改 |
| `ForgeSelf.Web/src/data/features.ts` | 功能清单名称「智能体」 |
| `Plugins/AIAgent/web/src/AiAgentView.vue` | 标题「智能体」 |
| `Plugins/AIAgent/web/src/agentBridge.ts` | 欢迎语/title「智能体」 |
| `Plugins/AIAgent/web/src/components/AgentEditDialog.vue` | 执行模式：开放任务 / 计划任务 |
| `Plugins/AIAgent/web/src/components/RunRecordPanel.vue` | 空态「计划任务」（去掉 MAF 前缀） |
| `docs/ai/pilot/2026-11-02-maf-first-class/00–04` | 本工件链 |

### 后续批次（写入路线图，本回合 Forbidden）

| 批次 | 内容 | 建议 pilot |
|------|------|------------|
| P0a | MAF 工具审批接线（ApprovalRequiredAIFunction + UI 卡） | `YYYY-MM-DD-maf-tool-approval` |
| P0b | 取消/超时杀终端子进程 | `YYYY-MM-DD-maf-cancel-kill-process` |
| P0c | 删除或隔离 ReactLoop / ad-hoc 扩面 | 随 P0 或独立清理批 |
| P1 | 文件/终端策略 + Skills 一等 | 独立 pilot |
| P2 | Scheduler → MAF Run | 独立 pilot |
| P3 | Browser 插件立项+实现 | feasibility → plugin-development |
| P4 | Memory / 本地模型 | 独立 pilot |
| 命名契约批 | `executionMode` 值、实体改名（可选） | **路由不在此列**：用户已批不改 `/maf`。其余仅另批明确授权后 |

## Implementation Steps

1. 落盘愿景/目标/原则/方向与架构文、ADR-006、not-taken。
2. 更新 docs/README §一、根 README、021、roadmap 索引。
3. 改对外展示文案（plugin.json / features.ts / 三个前端字符串点）。
4. 写齐 pilot 00–04；日记勾选；TODO 改为「待闸门1」或移除本回合文档项。
5. **停**：路由不改与插件名「智能体」已由用户批准。不启动 P0 审批代码。

## Test Plan

1. 文档：人工核对 README 链接可点、命名表自洽。
2. 若改 `features.ts`：`cd ForgeSelf.Web && pnpm run check:features`（若脚本存在）。
3. 前端字符串：`cd Plugins/AIAgent/web && pnpm run build`（可选快档；纯文案可 `check`）。
4. **不跑**全量 e2e / 后端全量（无业务逻辑变更）。

## Verification

### Build

```bash
# 可选：展示层改动后
cd Plugins/AIAgent/web && pnpm run build
```

### Unit Test

```bash
# N/A 本回合无单测逻辑变更
```

### Integration Test

```bash
# N/A
```

### E2E

```bash
# N/A 本回合；P0 批再跑 e2e/plugins/ai-agent 冒烟
```

### Other Checks

- 日记拆解勾选；TODO 收口
- 确认未修改 `c:\Users\12504\.cursor\plans\maf_复用不重做_9943f639.plan.md`

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-11-02 输入7 | 展示名与路由 | 默认：展示名 MAF，Id/路由暂留，`/maf` 另批待拍板 | **用户已批准**：插件名「智能体」；路由不改（Id `ai-agent`，不改 `/maf`）。MAF 仍为执行中枢技术名。不实现 P0 |
