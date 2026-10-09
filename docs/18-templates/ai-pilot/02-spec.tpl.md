# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：PILOT-___

## Functional Requirements

## Input

## Output

## Business Rules

## Boundary Conditions

## Error Handling

## Compatibility

## Non-functional Requirements

## Interaction Design（交互设计 · 必写节 · 2026-10-08 用户立）

> **凡是涉及用户可见 UI/交互的功能，本节为必写节**（涉及 UI 改动 = Spec 必须有本节，缺则闸门1 不通过）。
> 编写与自查用 `.agents/skills/ui-ux-design` 技能（CRAP 四原则 + 配色 + 字体 + 交互规格模板）。

### 交互规格（点什么出现什么）

对每个用户可见功能点一行一表，写清「触发 → 结果」：

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| （例：委派按钮） | 点击 | 确认弹窗 → 确认后入队 toast | 可委派/不可委派（分态） | toast + loading | agents 空态引导去登记 | 四栏不齐禁用且可见原因 |

### 交互验收标准（并入 Acceptance Criteria，逐条可测）

- 每个交互点至少一条 AC：断言「触发后页面出现什么」（toast 文案/弹窗/徽标/跳转），断言锚在真实服务入口返回，不只看界面回显。
- 空态分级：每种空态文案 + 下一步引导可测。
- 禁用/不可用状态必须可见原因（不无声禁用）。
- 破坏性/状态变更操作二次确认（`ElMessageBox.confirm` + 可单测编排函数）。

### 走查符合性（DoD 绑定）

- 走查（§四 第 4 步）按 `ui-ux-design` 的「走查 UI 符合性清单」逐项核对：交互规格「触发 → 结果」与实现一致、CRAP 层级/重复/对齐/亲密性成立、点即保存落盘、轮询无闪动、筛选/分页边界、长文本/窄屏不破版。
- e2e 覆盖交互路径（确认取消两条路、空态、边界）；纯视觉用截图读图对照设计基准（预期字号/间距/颜色 token）。

## Acceptance Criteria

<!-- 逐条可测；闸门1 用户确认的就是这里的清单 -->

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
|  |  |  |
