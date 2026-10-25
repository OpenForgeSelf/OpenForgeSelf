---
id: PC-012
title: 设置容器内的提供方编辑未使用视口级弹窗
tags: [settings-ui, ai-provider, element-plus-dialog, container-clipping]
invariant: AI 提供方编辑表单必须脱离设置容器，以视口级 Element Plus 弹窗承载并保持可见可操作。
status: guarded
first_seen: 2026-10-24
recurrences: []
cases: [2026-10-24-settings-provider-edit-container-clipping]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts", status: active}
lens:
  - AI 提供方编辑是否通过视口级弹窗脱离设置容器布局和尺寸约束？
  - 背景图模式下弹窗透明度是否沿用主题规则且文字、输入控件仍清楚可读？
---
# PC-012 设置容器内的提供方编辑未使用视口级弹窗

## 1 不变量与诱因

AI 提供方编辑表单必须由 Element Plus `el-dialog` 传送到 body，不能留在设置内容容器中受其布局/尺寸约束。背景图模式弹窗遵守现有主题透明度规则，不能为修布局而写死不透明底色。

诱因：提供方配置表单沿用 `el-drawer`，在设置容器内展示时受宿主布局约束；现有 UI 测试未覆盖编辑弹层脱离父容器这一不变量。

## 2 特征签名与扫描方法

入口签名：`AiProvidersPanel.vue` 的 provider 编辑操作必须打开 body 级 `.el-dialog`，不得渲染为容器内抽屉。G1 测试验证弹窗存在且不属于 `.app-layout`。

扫描范围：设置页 `AiProvidersPanel.vue` 中的提供方 CRUD 弹层、同域设置面板弹层、`ForgeSelf.Web/e2e/ai-providers.spec.ts` 中相关 provider 操作选择器。当前模型行的「编辑模型」是不同编辑流程，不属于 provider 编辑入口；若用户报告同样裁切，按本 PC lens 扩展。

## 3 Canonical fix

提供方新增/编辑表单统一使用 `el-dialog` 与 `append-to-body`，弹窗宽度受视口限制，内容区限高并可滚动；背景图透明度复用 `bg-image-mode.css` 的既有 `.el-dialog` 规则，不引入固定颜色。

参考实现：`ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue` 的「新增/编辑提供方弹窗」。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|---|---|---|---|
| G1 | `ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts` | 每次组件测试 | 不验证真实浏览器截图；由 provider 设置页 e2e/UI 走查补充 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|---|---|---|---|
| [2026-10-24-settings-provider-edit-container-clipping](../cases/2026-10-24-settings-provider-edit-container-clipping.md) | 2026-10-24 | 编辑提供方表单受设置容器约束且内容看不全 | 否，首次建档 |
