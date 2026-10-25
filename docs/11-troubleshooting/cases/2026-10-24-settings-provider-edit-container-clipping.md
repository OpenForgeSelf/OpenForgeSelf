---
id: 2026-10-24-settings-provider-edit-container-clipping
title: AI 提供方编辑表单受设置容器约束而看不全
pc: PC-012
severity: P1
evidence: Verified
---
# Case：2026-10-24 AI 提供方编辑表单受设置容器约束

## 1 现象

用户反馈：点击设置页 AI 提供方的编辑按钮后，编辑内容在设置容器内显示而非弹窗，表单内容看不全，并指定改用 Element Plus 弹窗且注意透明度。用户截图可见编辑区域与设置页同层呈现。

## 2 复现红灯

- 用户截图及反馈：编辑入口显示为容器内的编辑面板，字段区域受限。
- 源码确认：旧实现为 `AiProvidersPanel.vue` 中 `el-drawer`，没有 provider 编辑弹窗回归用例。
- **测试红灯未能取得 Verified**：沙箱内 pnpm 在目录规范化时报 Windows access denied；首次 Playwright 启动暴露测试变量重名并已修复。随后重跑被自动审核拦截，因为首次启动清理了残留宿主 PID。不得以未跑通冒充红灯。

## 3 根因（5 Whys + 第 6 问）

1. 为什么编辑内容看不全？→ 内容受设置容器布局和可视区域约束。
2. 为什么内容在容器内？→ provider 编辑入口使用抽屉组件承载表单。
3. 为什么没有独立弹窗隔离？→ 实现沿用了旧抽屉形态，未使用 body 级 `el-dialog`。
4. 为什么没有被发现？→ 现有组件测试只覆盖模型标记和连接探活，没有覆盖 provider 编辑弹层位置与完整展示。
5. 为什么 UI 设计没有约束弹层层级？→ 既有验收未把「编辑弹层不受设置容器限制」列为可测标准。
- **第 6 问：为什么流程没让我一开始就写对？**→ 旧组件测试与 e2e 选择器把抽屉形态当作既定实现，没有将弹层与视口关系作为不变量；本次补 G1 测试与 PC lens。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置 |
|---|---|---|
| `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue` 提供方 CRUD 弹层 | 是 | 新增/编辑统一改为 body 级 `el-dialog`，弹窗视口宽度约束及内容滚动 |
| 同文件「编辑模型」弹层 | 否（独立模型行编辑流程） | 本次只处理用户指出的提供方配置编辑；若该入口也裁切，按 PC-012 lens 单独确认 |
| `ForgeSelf.Web/src/components/settings/ApiKeysPanel.vue` API Key 弹层 | 否 | 已使用 `el-dialog append-to-body`，未命中 |
| `ForgeSelf.Web/e2e/ai-providers.spec.ts` provider 编辑/新增/校验路径 | 是（旧选择器依赖 drawer） | 选择器更新为 `.el-dialog`；T021 针对本次 UI 真实复验被环境拦截 |

## 5 修复与被否方案

按用户指定将提供方编辑改用 Element Plus `el-dialog` 并 `append-to-body`；不改 API、密钥语义或 CRUD 流程。未采用放宽/扩展设置容器尺寸的方案，因为这不能保证弹层脱离父容器。

## 6 重构提案与结论

R0：无需重构。单个设置表单换用项目已有 Dialog 组件即可，扩展 UI 弹层基础设施的成本高于收益。

## 7 守卫落地

G1：`AiProvidersPanel.test.ts` 新增 provider 编辑弹窗测试，检查弹窗位于 `.app-layout` 外、字段回填、取消不调用 update、保存 API Key 留空。Vitest 10/10 通过。实际浏览器截图和透明度断言尚未通过验证。

## 8 逃逸分析

N/A 首次建档。

## 9 基建改进

建立 PC-012 并为后续 provider 编辑 Spec 增加视口级弹窗与背景图透明度 lens。

## 10 复用赋能

以后改 AI 提供方设置编辑入口时引用 PC-012，并回答：弹窗是否脱离设置容器？背景图模式透明度和文字可读性是否经过实测？

## 11 证据

| 结论 | 等级 | 出处 |
|---|---|---|
| 用户确认为编辑表单在容器内且内容看不全 | Verified | 当前会话用户文字反馈及截图 |
| 旧 UI 使用 `el-drawer`，新 UI 使用 `el-dialog append-to-body` | Verified | `AiProvidersPanel.vue` |
| 背景图模式存在半透明 dialog 规则 | Verified | `ForgeSelf.Web/src/styles/themes/bg-image-mode.css` `body.app-bg-image-mode .el-dialog` |
| 组件测试通过 | Verified | `pnpm --dir ForgeSelf.Web run test -- AiProvidersPanel`：10/10 |
| 浏览器截图/实机透明度 | Unknown | e2e 重跑被自动审核拦截；dev-stack 已有 TodoTracker 实例，不能重启 |
