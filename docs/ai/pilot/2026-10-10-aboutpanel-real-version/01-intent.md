# Intent

> Task ID: PILOT-2026-1010-ABOUTVER；日期：2026-10-10

## Problem
设置页「关于」标签硬编码 `v0.1.0`。项目已进入自动更新常态（运行实例为 2.4.x 线），关于页展示的是过期假版本，误导用户对当前版本的判断。

## Why
版本号是用户排查/升级决策的锚点，「关于」页必须展示运行实例的真实版本并随升级自动变化。

## Expected Outcome
AboutPanel 挂载后经 `updateApi.getStatus()` 取 `currentVersion` 展示（`v` 前缀，与 UpdatePanel 一致）；加载中有占位；失败优雅降级。

## Constraints
- 只改 `For geSelf.Web/src/components/settings/AboutPanel.vue`（+可新增单测文件）——用户硬性要求
- 保持现有布局与样式不变
- 禁止 git 历史/启停进程/改 publish 与 .trash
- 闸门1 视为已通过（用户明确委托全程执行）

## Success Criteria
- 模板不再含 v0.1.0；版本文案格式 `v{{ version }}`
- 加载态显示 `v…`；成功显示真实版本；失败显示 `v未知` 且无控制台报错
- `pnpm run check` 与相关 vitest 全绿
- TODO/日记/工件齐全
