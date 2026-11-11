---
task_id: 2026-11-06-plugin-ui-audit
feature: none                  # 跨插件 UI 规范核对，不对应单一功能编号
risk: { level: L1, triggers: [], raised_by_agent: false }
gate1: { mode: user, at: null, ref: "AGENTS §11 闸门1（本批只出工件，执行待批）" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files: []             # 本批不产代码；B1–B3 的落点见 03-plan
rollback: "无代码变更，删除本目录即可"
writeback: { feature_doc: pending, reason: "执行后按插件回写 ui-design-brief" }
---

# Agent Task

## Task ID

PILOT-plugin-ui-audit（目录 `docs/ai/pilot/2026-11-06-plugin-ui-audit/`）

## Objective

为 14 个带前端插件产出统一的 UI 规范核对表与分批修复计划（C1–C8），**只出工件，不执行修复**；未实测项一律标 `Unknown(未核)`。

## Scope

### Allowed

- 新增/修改 `docs/ai/pilot/2026-11-06-plugin-ui-audit/` 下的工件
- 读取任意源码/样式/测试用于核对（只读）

### Forbidden

- 修改任何插件源码、宿主源码、主题 token、测试与脚本
- 修改基线 JSON / 放宽门禁
- 以推测填写 C2–C5/C8（未实测必须标 Unknown）
- 顺手重构与本次核对无关的代码

## Acceptance Criteria

- [ ] 14 个带前端插件全部入表（AgentHub/AIAgent/CostScope/DbAdmin/DesignSystem/FileTools/Home/ImGateway/McpCenter/QuickLinks/Sems/SmartTerminal/TodoTracker/ToolBridge）
- [ ] C1（裸色值）、C6（CT）、C7（e2e）列有实测数据
- [ ] C2–C5/C8 未核项明确 `Unknown(未核)`
- [ ] 修复清单每条含「文件 / 验证命令 / 判定标准」
- [ ] 批次含 P1（CostScope e2e + 可读性）与 P2（状态/基线）
- [ ] `git status --short` 显示本批仅新增本目录

## Expected Files

- `docs/ai/pilot/2026-11-06-plugin-ui-audit/00-repository-understanding.md`
- `docs/ai/pilot/2026-11-06-plugin-ui-audit/01-intent.md`
- `docs/ai/pilot/2026-11-06-plugin-ui-audit/02-spec.md`
- `docs/ai/pilot/2026-11-06-plugin-ui-audit/03-plan.md`
- `docs/ai/pilot/2026-11-06-plugin-ui-audit/04-task.md`

## Verification Commands

```bash
git status --short                       # 只应有本目录新增
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-plugin-ui-audit -EnforceHeader -EnforceAcCoverage -EnforceInteraction
```
