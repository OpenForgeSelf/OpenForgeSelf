# Agent Task

> 阶段：Stage 4｜把任务变成 Agent 可以直接执行的工作单元，零自我决策空间。
> 前序工件：00/01/02/03 齐备；闸门1 由用户输入51 的明确指令（「应该分开多个状态，不要共用」）批准。

## Task ID

PILOT-051（目录 `docs/ai/pilot/2026-09-30-update-panel-loading-states/`）

## Objective

设置页「版本更新」面板三个按钮的 loading 解耦：各自按钮的 loading 只反映自身动作；`busy()` 仅用于禁用防并发；补回归测试并跑通前端门禁。

## Scope

### Allowed

- 修改 `ForgeSelf.Web/src/components/settings/UpdatePanel.vue` 模板两处 el-button 绑定（03-plan 所列）。
- 新增 `ForgeSelf.Web/src/__tests__/UpdatePanel.test.ts`。
- 产出 `docs/ai/pilot/2026-09-30-update-panel-loading-states/` 00-07 工件、更新 `.forgeself/memory/2026-09-30.md` 与 `TODO.md`。

### Forbidden

- 修改后端 / API 契约 / updateApi.ts / 其他组件。
- 重构 busy()、轮询、重启等待等既有更新流程逻辑。
- 新增依赖、引入 `any`、顺手改无关代码。
- 未经用户明确指示执行 git commit / push。

## Acceptance Criteria

- [ ] UpdatePanel.vue 中「检查更新」「下载更新」`:loading` 不含 `busy()`
- [ ] 新增测试断言 stage=applying 时仅「重启并更新」loading=true，检查/下载按钮 loading=false 且 disabled=true
- [ ] `pnpm run check` 通过（0 error）
- [ ] `pnpm run test` 通过（新增用例绿，存量不回归）

## Expected Files

- ForgeSelf.Web/src/components/settings/UpdatePanel.vue（修改）
- ForgeSelf.Web/src/__tests__/UpdatePanel.test.ts（新增）
- docs/ai/pilot/2026-09-30-update-panel-loading-states/（00-07）

## Verification Commands

```bash
cd ForgeSelf.Web
pnpm run check
pnpm run test
```
