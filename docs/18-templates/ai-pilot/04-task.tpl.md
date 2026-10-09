---
task_id: 2026-10-06-example
feature: 034-mcp-center          # 对应 docs/02-features 编号；无则写 none 并说明原因
risk: { level: L1, triggers: ["ForgeSelf.Web/src/views/Foo.vue"], raised_by_agent: false }
gate1: { mode: auto, at: 2026-10-06T10:02, ref: "risk-policy#L1" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Web/src/views/Foo.vue
rollback: "git revert <commit>"
writeback: { feature_doc: pending, reason: "" }
---

# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。

## Task ID

PILOT-001

## Objective

<!-- 一句话：做完后仓库达到的可验证状态 -->

## Scope

### Allowed

<!-- 允许改哪些文件、加哪些测试，引用 03-plan.md 的 Files To Change -->

### Forbidden

<!-- 明确禁止：生产环境 / DB 结构 / 鉴权权限支付 / 新依赖 / 无关重构 / 超出 Plan 的文件 -->

## Acceptance Criteria

- [ ]
- [ ]
- [ ]

## Expected Files

-
-

## Verification Commands

```bash
# 逐条列出可真实运行的验证命令（build / 单测 / e2e / lint），与 03-plan.md Verification 一致
```
