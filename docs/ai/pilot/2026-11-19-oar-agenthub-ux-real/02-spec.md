# Specification

> Task：oar-agenthub-ux-real

## Functional Requirements

1. `IAgentRuntime.ListAgentsAsync` / `GET api/agent-hub/runtimes` **剔除** `runtimeId=scripted`。
2. Worker 补充 Runtime 时调用 `runtime.installation`：仅 `kind=available && via=executable` → `Available=true`；`not_found` / `bundled` 诚实标注。
3. 前端 Runtime 卡与 Agent 卡使用 `@botiverse/oar` 品牌 SVG（内联）；无图标则字母 glyph。
4. Session 页签拆为「Runtime 库存」与「Session 操作」两节；探针 JSON 默认折叠。
5. 引导文案不再把 scripted 当正常用户路径。

## Input

- OAR Worker `runtime.list` + `runtime.installation`
- 已登记 Agent 列表

## Output

- `AgentDescriptor` 增补 `InstallationKind` / `InstallationVia`
- 重设计 `AgentHubView` sessions 面 + `RuntimeBrandIcon`

## Business Rules

- scripted = 测试专用，永不进产品 Runtime 列表。
- 禁止假绿；禁止 echo session 当 vendor 成功。

## Boundary Conditions

- Worker 不可用：只显示已登记 Agent。
- bundled（如 Pi）：显示「内置捆绑」，Available=false。

## Error Handling

- installation 探测失败 → Available=false + 可解释 reason。

## Compatibility

- Worker smoke / `OarWorkerClientTests` 仍可见 scripted。
- 既有 Cli 委派路径不变。

## Non-functional Requirements

- 列表刷新可接受串行 installation 探测（本机秒级）。
- 仅 `--el-*` token；无自定义色值。

## Interaction Design（交互设计）

### 交互规格

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 开 Runtime/Session 页 | 点 Tab | 加载库存+会话；scripted 不出现 | 加载/正常/错 | toolbar「加载中」 | Runtime 空 / Session 空分文案 | — |
| Runtime 卡「检查」 | 点击 | 展开 installation/auth JSON | busy 按行 | toast 已探测 | — | 无 runtimeId 禁用 |
| Runtime 卡「模型」 | 点击 | 展开 models；失败原文 | busy | toast / 错 | — | not_found 不假装有模型 |
| Runtime 卡「安装」 | 点击 | 确认框+plan；取消无副作用 | 已装禁用安装 | toast | — | confirmed 才执行 |
| 选 Session | 点行 | 右侧详情+Steer | 可 Steer/禁用原因可见 | — | 无会话引导去委派 | caps 禁用有 title |
| Agent 卡图标 | 渲染 | 品牌 SVG 或字母 | — | — | — | scripted 不作为产品 Agent 默认 |

### 视觉规格

- Runtime 卡网格：品牌图标 36px + 名称层级 + 状态徽标（已安装=success / 未安装=danger / 捆绑=idle）。
- 两节之间边框分隔；一节一事。
- 主题：全部 `--el-*`；亮/暗各截 Runtime 库存 + Session 区。

### 交互验收标准

- AC-UI1：列表无 scripted 行。
- AC-UI2：Grok 徽标「已安装」；claude「未安装」。
- AC-UI3：Grok/Claude/Codex 图标视觉互异。
- AC-UI4：探针 JSON 默认不展开。

### 走查符合性

按 ui-ux-design 清单：CRAP 分区、空态引导、禁用原因可见、亮暗两态截图。

## Acceptance Criteria

- [ ] AC1 审计文档 Grok-only 结论
- [ ] AC2 API/UI 无 scripted
- [ ] AC3 安装态诚实（executable vs not_found vs bundled）
- [ ] AC4 品牌图标
- [ ] AC5 UX 分区 + 截图
- [ ] AC6 Worker smoke 仍绿；web build 绿

## Unknown

| 点 | 影响 | 处理 |
| --- | --- | --- |
| Pi bundled 是否应对用户隐藏 | 低 | 本批诚实标注，不隐藏（非 scripted） |
