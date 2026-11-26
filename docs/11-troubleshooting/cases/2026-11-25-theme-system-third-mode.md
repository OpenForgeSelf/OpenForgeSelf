---
id: 2026-11-25-theme-system-third-mode
title: 设置页将跟随系统建模成独立第三主题态
pc: PC-051
severity: P2
evidence: Verified
---
# Case：2026-11-25 设置页将跟随系统建模成独立第三主题态

## 1 现象

用户反馈设置-外观把“跟随系统”显示为与“浅色”“深色”并列的第三项，且实际状态曾把 `system` 持久化为主题值。

## 2 复现红灯

Focused Vitest reproduced two failures:

- Theme Store: system change kept `data-theme-mode=system`
- SettingsView: appearance rendered 3 theme controls instead of 2

## 3 根因（5 Whys + 第6问）

1. 为什么有第三主题？→ ThemeMode 枚举包含 system。
2. 为什么 system 会进入持久化和根属性？→ store 直接持久化 mode 并用 mode 写根属性。
3. 为什么系统变化没有被当成解析策略？→ store 同时把策略和 resolved result 放进一个字段。
4. 为什么界面底色也偏差？→ theme state 变化没有和 AppearanceStore 纯色背景联动。
5. 为什么流程没拦住？→ 缺少主题枚举与 UI radio 数量守卫。
- 第6问：现有流程没有把“策略开关 vs 实际主题值”写成 Spec 不变量，故 UI/存储可并存漂移。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|---|---|---|
| AppearancePanel | 是 | 改为2 radio+独立 switch |
| Sidebar | 是 | 去掉 system button，按 resolvedTheme 高亮 |
| theme.ts | 是 | 分离 followSystem 与 resolvedTheme |
| appearance.ts/App.vue | 是 | pure solid background follows resolvedTheme |

## 5 修复与被否方案

最终修法：followSystem independent key + light/dark actual state. Legacy system migration.

被否方案：继续保留三态但把按钮文案改成“自动”；未解决状态持久化和根属性仍是 system 的根因。

## 6 重构提案与结论

R1 局部修复：现有 Theme/Appearance store already is small boundary; no public-contract or architecture change.

## 7 守卫落地

- G1 theme.test.ts
- G1 SettingsView.test.ts
- G1 appearance-store.test.ts

## 8 逃逸分析

首次发现。No recurrence/escape analysis required.

## 9 基建改进

The pilot Spec now records “follow strategy is not actual theme enum” and unit guards cover both UI and resolved-state boundaries.

## 10 复用赋能

All future appearance/theme work must keep only resolved light/dark in root/persistence, and use follow policy separately.

## 11 证据

|结论|等级|出处|
|---|---|---|
|Red reproduced|Verified|focused Vitest 2 failures before fix|
|Green|Verified|theme/settings/appearance 43 tests|
|Full frontend|Verified|123 files / 1281 tests|
|Live light/dark|Verified|7399 source stack plus dark/light screenshots|
