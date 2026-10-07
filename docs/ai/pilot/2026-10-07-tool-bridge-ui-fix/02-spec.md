# 02 Spec

## Functional Requirements
- FR-1 粘贴框：仅当「这一次鼠标左键点击把焦点带进来」时全选；焦点已在框内时的点击只放置光标；blur 后标记复位；Tab 聚焦与窗口切回不触发全选（用户 2026-10-07 明确：点击才全选）。
- FR-2 两个复制按钮文案恒为「复制初始指令」「复制结果」，不再随状态变化。
- FR-3 每个按钮旁一个独立提示，成功：「已复制（第 N 次）· HH:mm:ss · M 字符」；回退：「自动复制不可用，已选中文本，请按 Ctrl+C（第 N 次）」。N 为该按钮自上次复位起的点击序号。
- FR-4 结果区提示与计数在 doParse / doTurn / doExecuteParsed / reloadTurn 时清零；初始指令提示不受这些动作影响。
- FR-5 copy() 不再写 copyHint；copiedKey 整体移除。
- FR-6 plugin.json Version 升为 1.0.3。

## Input
用户鼠标左键点击粘贴框；点击两个复制按钮；既有的解析 / 执行 / 载入回合动作。

## Output
粘贴框选区；按钮旁提示文本（带 data-testid：tb-copy-prompt-note / tb-copy-result-note）；剪贴板内容 = 当前展示的文本。

## Business Rules
- 提示必须每次点击都有可见差异（序号递增），不得依赖时间或字数变化。
- 失败不得假装成功（沿用 clipboard.ts 既有回退语义）。

## Boundary Conditions
- 非左键点击不处理；框内容为空时 select() 无副作用。
- 首次点击拦截 mousedown 默认行为，因此首次点击不能拖选，也不放置光标（符合「点击即全选」）。
- 复制空文本沿用既有回退文案（已知文案不准，记待办，本任务不修）。

## Error Handling
剪贴板写入失败 ⇒ 沿用回退：选中文本 + 提示按 Ctrl+C；序号仍递增。

## Compatibility
既有 6 条 e2e 与 3 处 tb-hint 断言保持绿；不改后端契约。

## Non-functional Requirements
不新增依赖；类型检查在 vue-tsc -b（发布链参数）下通过。

## Acceptance Criteria
- AC1 连点「复制结果」两次：提示依次含「第 1 次」「第 2 次」，且剪贴板内容等于结果文本。
- AC2 按钮文案全程不含「已复制」。
- AC3 复制后再点「解析并执行」：结果提示清空，下次复制为「第 1 次」。
- AC4 「复制初始指令」同样递增，且不被解析 / 执行清空。
- AC5 失焦后点击粘贴框：selectionStart=0 且 selectionEnd=文本长度。
- AC6 已有焦点时再点击：选区折叠（start=end）。
- AC7 键盘 / 程序化 focus（无鼠标点击）不全选。
- AC8 既有 6 条用例仍绿；pnpm run build 与 vue-tsc -b 通过。
- AC9 版本徽标显示 v1.0.3。

## Unknown
- U-1 窗口切回后再点击是否全选：依赖 Chromium 的 blur 行为，无法由 e2e 真实模拟，标 Inferred，用户手测。
- U-2 e2e 浏览器内核与剪贴板权限可用性：待实施时读 playwright 配置确认。
