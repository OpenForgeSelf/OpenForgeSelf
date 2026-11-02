---
task_id: 2026-11-02-maf-first-class
feature: 021-ai-agent
risk: { level: L2, triggers: ["docs/**", "Plugins/AIAgent/plugin.json", "Plugins/AIAgent/web/src/*.vue", "ForgeSelf.Web/src/data/features.ts"], raised_by_agent: false }
gate1: { mode: user, at: null, decision: null }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - docs/00-vision/01-vision.md
  - docs/00-vision/02-goals.md
  - docs/00-vision/03-principles.md
  - docs/00-vision/04-direction.md
  - docs/01-architecture/maf-centric-architecture.md
  - docs/07-decisions/006-maf-first-class-greenfield.md
  - docs/ai/pilot/2026-11-02-maf-first-class/00-repository-understanding.md
  - docs/ai/pilot/2026-11-02-maf-first-class/01-intent.md
  - docs/ai/pilot/2026-11-02-maf-first-class/02-spec.md
  - docs/ai/pilot/2026-11-02-maf-first-class/03-plan.md
  - docs/ai/pilot/2026-11-02-maf-first-class/04-task.md
rollback: "git checkout -- <touched docs/ui files>"
writeback: { feature_doc: pending, reason: "021 品牌节随本文档批回写" }
---

# Agent Task

> 阶段：Stage 4｜前序：00–03 齐备；**大规模业务实现须闸门1 用户确认后另开 P0 pilot**。

## Task ID

`2026-11-02-maf-first-class`

## Objective

产品/架构真源以 MAF 为中心落盘；对外命名骨架切换；P0–P4 进路线图；本回合不重写 Runtime。

## Scope

### Allowed

- `docs/00-vision/*`、`docs/01-architecture/maf-centric-architecture.md`、`docs/07-decisions/005|006|not-taken*`、`docs/README.md`、根 `README.md`
- `docs/02-features/021-ai-agent.md`（品牌/命名/快照联 ADR-006）
- `docs/15-roadmap/index.md`（指向 goals）
- `docs/ai/pilot/2026-11-02-maf-first-class/00–04`
- 展示文案：`Plugins/AIAgent/plugin.json`、`ForgeSelf.Web/src/data/features.ts`、`AiAgentView.vue`、`agentBridge.ts`、`AgentEditDialog.vue`（及必要的空态字符串）
- `.forgeself/memory/2026-11-02.md`、`TODO.md`

### Forbidden

- 重写 `MafTaskLoopAgent` / 审批接线业务逻辑 / 新建 Browser 插件
- 改 Scheduler 触发 AgentRun 的实现
- DB 迁移、API 路径重命名、`executionMode` 存储值变更
- 删除 `ReactLoopAgent` 源码
- 新建 `specs/`
- 修改 `c:\Users\12504\.cursor\plans\maf_复用不重做_9943f639.plan.md`
- 停/启/杀用户运行宿主

## Acceptance Criteria

- [ ] Intent Success Criteria 全部满足
- [ ] Spec AC1–AC7 满足
- [ ] Forbidden 无一触犯
- [ ] 用户收到：路径 + 摘要 + ≤2 待拍板（带默认推荐）

## Expected Files

见 front-matter `expected_files` + 03-plan「本回合」表。

## Verification Commands

```bash
# 文档自检：确认关键文件存在
Test-Path docs/01-architecture/maf-centric-architecture.md
Test-Path docs/07-decisions/006-maf-first-class-greenfield.md
Test-Path docs/ai/pilot/2026-11-02-maf-first-class/04-task.md

# 可选（改了 features.ts 时）
cd ForgeSelf.Web && pnpm run check:features

# 可选（改了插件前端文案时）
cd Plugins/AIAgent/web && pnpm run build
```
