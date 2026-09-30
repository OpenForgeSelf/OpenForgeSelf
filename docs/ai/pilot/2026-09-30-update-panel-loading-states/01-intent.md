# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，不提前决定具体代码实现。
> Task ID：PILOT-051 ｜ 日期：2026-09-30

## Problem

设置页「版本更新」面板中，「检查更新」「下载更新」「重启并更新」三个按钮的 `:loading` 绑定了公共状态：前两个按钮为 `自身动作状态 || busy()`，`busy()` 依据后端全局 stage（含 `applying`）。用户在「重启并更新」弹窗点击确认后，后端 stage 变为 `applying`，导致「检查更新」「下载更新」两个**并未执行任何动作**的按钮也跟着转圈。

## Why

- 转圈在 Element Plus 语义中表示「该按钮自身的动作正在执行」；无关按钮转圈会给用户错误的进行中反馈，掩盖真实状态（真正在跑的是「重启并更新」）。
- 该共用模式在其他 stage（checking/downloading/verifying/extracting）下同样会让无关按钮转圈，是同一缺陷的多个触发面。

## Expected Outcome

每个按钮的 loading 只由**它自己的动作状态**驱动；`busy()` 仅用于「禁用」以防止并发冲突操作（如下载期间再点检查），不再让无关按钮出现转圈。

## Constraints

- 不改后端、不改 API 契约、不加依赖。
- 不重构 `busy()` 之外的更新流程逻辑（轮询、重启等待、进度展示均不动）。
- 遵守项目前端规范（Element Plus 组件属性用法、TS 类型）。
- 按用户偏好：本任务改动汇总后一次性交付，不自动 git commit/push。

## Success Criteria

1. 「检查更新」「下载更新」按钮的 `:loading` 不再包含 `busy()`。
2. 后端 stage 为 `applying` 时：仅「重启并更新」按钮转圈，「检查更新」「下载更新」按钮为禁用（disabled）但不转圈。
3. 存在回归测试覆盖上述行为（stage=applying 时三个按钮的 loading/disabled 断言）。
4. `pnpm run check` 与 `pnpm run test` 通过（新增测试绿）。
