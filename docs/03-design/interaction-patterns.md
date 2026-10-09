# 交互模式库（跨页面一致性单一来源）

> 归属：`docs/03-design/`（L2）。**新增通用交互模式必须先在此登记，再在页面使用。**
> 目的：消除「同一类控件各页写法不同」这类结构性问题——它没有单一来源，只靠"请保持一致"无法约束。
> 依据：AGENTS.md §8；本项目技术栈实测为 Element Plus + Tailwind（样式只用 `--el-*` 与 Tailwind 布局原语）。
> 说明：下文为**模式契约**（每种必须固定一份写法）。每条的「参考实现」待各页落地后补**真实文件路径**，未补前不得虚构。

---

## 1 模式契约总表

| # | 模式 | 固定写法（契约） | 状态 |
|---|------|------------------|------|
| P1 | 列表页 | `<el-table>` + `<el-pagination>`；空态走 `#empty` 槽 | 已登记 |
| P2 | 表单 | `<el-form>` + `FormRules`；提交按钮 loading 绑定 | 已登记 |
| P3 | 空态 | `<el-empty>`，文案 + 可选主操作 | 已登记 |
| P4 | 加载 | 区块用 `v-loading`（`ElLoading` 指令）；按钮用 `:loading` | 已登记 |
| P5 | 错误 | API 失败 → `ElMessage.error`；表单字段错误 → 字段级 `error` | 已登记 |
| P6 | 确认弹窗 | 危险操作统一 `ElMessageBox.confirm`（类型 + 确认文案） | 已登记 |
| P7 | 无权限 | 401/403 统一处理，不渲染空页当成功 | 已登记 |

> 组件导入约束：**禁止显式 `import { ElXxx } from 'element-plus'`**，统一模板 `<ElXxx>` 由 unplugin 自动解析；
> 例外为 API 调用型组件 `ElMessage` / `ElMessageBox` / `ElNotification` / `ElLoading`（由 `eslint.config.mjs` 白名单放行）。

---

## 2 逐条契约

### P1 列表页
- 容器：`<el-table :data>` + 列 `<el-table-column>`；分页 `<el-pagination>`。
- 空态：`<template #empty>` 内放 P3 空态，**不得**用"表格无行"冒充空态。
- 加载：整表 `v-loading`（P4）。
- 样式：表格外观走 `--el-table-*` 令牌；不得给行内元素写死背景/边框色。
- 参考实现：（待补真实文件路径）

### P2 表单
- 容器：`<el-form :model :rules ref>`；规则用 `FormRules` 类型；提交前 `await formRef.validate()`。
- 提交态：提交按钮 `:loading="submitting"`，成功后 `ElMessage.success`。
- 校验失败：字段级 `error` 展示，禁止只弹全局 toast。
- 参考实现：（待补）

### P3 空态
- 统一 `<el-empty :description>`，可有 1 个主操作按钮；文案走 i18n/常量，不散落硬编码。
- 与"加载中"区分：数据未返回前显示 P4，不显示空态。
- 参考实现：（待补）

### P4 加载
- 区块级：`v-loading="loading"`（`ElLoading` 指令），禁用期间阻断交互。
- 按钮级：`:loading`，防重复提交。
- 禁止用自定义遮罩/转圈另起一套。
- 参考实现：（待补）

### P5 错误
- 请求失败：统一 `ElMessage.error(<人类可读信息>)`，不裸露堆栈/原始错误码。
- 表单错误：字段级；网络错误：区块级 + 重试入口。
- 参考实现：（待补）

### P6 确认弹窗
- 危险/不可逆操作：`ElMessageBox.confirm(内容, 标题, { type: 'warning', confirmButtonText, cancelButtonText })`。
- 删除类：确认文案必须含对象名；不提供"不再提示"。
- 参考实现：（待补）

### P7 无权限
- 鉴权失败（401）：跳登录/提示重新授权；**不得把空页当成功**（对照 `plugin-publish-verify`「401 立即上报」）。
- 无权限（403）：显示无权限态（P3 变体 + 说明），不是空列表。
- 参考实现：（待补）

---

## 3 使用与 Review

- **UI 任务 Spec 必须引用三样**：设计稿页面（`forgeself-design/pages/`）、交互模式名（本文 §1 的 P1–P7）、使用的 token。
- **新增模式**：先在本文 §1 登记（L2），再在页面使用；未登记即使用视为偏离。
- **Review 检查项**：对照设计稿读截图；同类控件/空态/加载/错误/确认弹窗/无权限，与**同域最近的既有页面**一致。
- 样式硬约束（禁止硬编码色值）由 `scripts/check-style-tokens.mjs` 棘轮守住：允许历史存量，**禁止新增**。
