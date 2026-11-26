---
id: PC-051
title: 跟随系统被建模成独立主题值与第三主题选项
tags: [UI, 主题, 跟随系统, 浅色, 深色]
invariant: 跟随系统是策略，不是实际主题值；实际主题枚举只能是 light/dark，跟随策略不得暴露为第三选项
status: guarded
first_seen: 2026-11-25
recurrences: []
cases: [2026-11-25-theme-system-third-mode]
signature:
  - {id: S1, kind: grep, glob: "ForgeSelf.Web/src/components/**/*.vue", regex: "el-radio-button[^\\n]*value=\"system\"|跟随系统</el-radio-button>", expect: no-match}
  - {id: S2, kind: grep, glob: "ForgeSelf.Web/src/stores/theme.ts", regex: "ThemeMode.*system|setAttribute.*ROOT_THEME_MODE_ATTRIBUTE.*mode", expect: no-match}
  - {id: S3, kind: test, path: "ForgeSelf.Web/src/stores/__tests__/theme.test.ts"}
  - {id: S4, kind: test, path: "ForgeSelf.Web/src/__tests__/SettingsView.test.ts"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Web/src/stores/__tests__/theme.test.ts", status: active}
  - {level: G1, path: "ForgeSelf.Web/src/__tests__/SettingsView.test.ts", status: active}
  - {level: G1, path: "ForgeSelf.Web/src/__tests__/appearance-store.test.ts", status: active}
lens:
  - 跟随系统是否只作为策略开关，而不是 ThemeMode 第三值？
  - 根节点 data-theme/data-theme-mode 是否始终只等于 light/dark？
  - 系统变化时浅/深选中态与纯色背景是否同步？
---
# PC-051 跟随系统被建模成独立主题值与第三主题选项

## 1 不变量与诱因

实际主题只有浅色和深色。跟随系统是解析策略：读取系统 `prefers-color-scheme` 后得到实际主题。把 `system` 持久化为与 `light/dark` 并列的第三值，会让 UI、根属性和持久化状态同时承载策略与结果。

诱因：UI radio、Pinia store 和系统监听共享了同一个 `ThemeMode` 枚举，没有把“策略开关”和“实际主题”分层。

## 2 特征签名与扫描方法

- UI 中出现第三个主题 radio value=system 或等价文案。
- ThemeMode 再次恢复为包含 system 的实际主题枚举。
- 根节点 `data-theme-mode` 写入 system。
- 系统变化后实际主题变化，但 radio/纯色背景不同步。

## 3 Canonical fix

- 实际主题只保留 `light/dark`。
- 独立保存 `forgeself.theme.followSystem`。
- 系统监听启用时将解析结果写入实际 theme 和存储。
- UI 使用 resolved actual theme 做 radio 高亮；跟随能力放在独立 switch。
- legacy `system` 存储迁移为 follow=true，并把实际存储改写为 light/dark。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|---|---|---|---|
| G1 | theme.test.ts | 快档 vitest | 系统变化、手动覆盖、legacy迁移 |
| G1 | SettingsView.test.ts | 快档 vitest | 只渲染两主题选择、跟随开关 |
| G1 | appearance-store.test.ts | 快档 vitest | 跟随主题时纯色背景同步且图片模式不覆盖 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|---|---|---|---|
| [2026-11-25-theme-system-third-mode](../cases/2026-11-25-theme-system-third-mode.md) | 2026-11-25 | 设置页把跟随系统做成第三主题按钮，且根状态持久化了 system | 首次 |
