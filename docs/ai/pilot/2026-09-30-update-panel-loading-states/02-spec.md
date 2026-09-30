# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> Task ID：PILOT-051

## Functional Requirements

1. 「检查更新」按钮：`:loading` 仅绑定 `checking`（自身动作）；`:disabled` 绑定 `busy()`（防并发）。
2. 「下载更新」按钮：`:loading` 仅绑定 `downloading`（自身动作）；`:disabled` 绑定 `busy() || isReady()`。
3. 「重启并更新」按钮：保持 `:loading="applying"` 不变（该按钮转圈是正确语义，用户确认后确实在重启）。
4. `busy()` 函数本身不变（仍返回 stage 是否处于 checking/downloading/verifying/extracting/applying），继续用于禁用与配置保存守卫。
5. 新增 UpdatePanel 回归测试：stage=applying 时，仅「重启并更新」loading=true，「检查更新」「下载更新」loading=false 且 disabled=true。

## Input

- `UpdatePanel.vue` 模板中三个 el-button 的 `:loading` / `:disabled` 绑定。
- 运行时输入：`status`（UpdateStatus）、`stage`（UpdateStageInfo，含 status/progress）、`checking/downloading/applying` 三个本地 ref。

## Output

- 修改后的 `UpdatePanel.vue`（模板绑定）。
- 新增 `ForgeSelf.Web/src/__tests__/UpdatePanel.test.ts`。

## Business Rules

- loading 只表达「该按钮自身动作进行中」；disabled 表达「当前不允许点击」（含自身进行中、全局 busy、已下载就绪等）。
- 任何 stage ∈ {checking, downloading, verifying, extracting, applying} 期间，检查/下载按钮应不可点击（防并发冲突），但不转圈。

## Boundary Conditions

- stage='ready'（isReady() 为真）：「下载更新」按钮 disabled（已下载），不转圈。
- stage='applying'：「检查更新」「下载更新」disabled（busy 为真）不转圈；「重启并更新」loading=true 转圈。
- 下载中（stage='downloading'）：「检查更新」disabled 不转圈；「下载更新」按钮自身的 downloading 在 POST 返回后即 false，后续下载过程由进度条呈现，按钮 disabled 不转圈。
- 失败（stage='failed'）：busy() 为 false，按钮恢复可点。

## Error Handling

- 不涉及新增错误路径；既有 onCheck/onDownload/onApply 的 catch 与 finally 逻辑不变。

## Compatibility

- 仅前端 `UpdatePanel.vue` 与新增测试文件；不触碰 updateApi.ts / 后端 / 其他组件。
- Element Plus el-button 的 loading 与 disabled 语义不变。

## Non-functional Requirements

- 类型安全（vue-tsc 通过）；无新增依赖；不引入 `any`。

## Acceptance Criteria

1. `UpdatePanel.vue` 中「检查更新」「下载更新」的 `:loading` 不再包含 `busy()`。
2. 回归测试断言：stage=applying 时，「重启并更新」loading=true；「检查更新」「下载更新」loading=false 且 disabled=true。
3. `pnpm run check` 通过（0 error）。
4. `pnpm run test` 通过（新增用例绿 + 存量用例不回归）。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 无 | — | — |
