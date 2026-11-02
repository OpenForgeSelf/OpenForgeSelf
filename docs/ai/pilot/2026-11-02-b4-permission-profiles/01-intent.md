# Intent — B4 权限可配置化

> Task ID：`2026-11-02-b4-permission-profiles`｜来源：lead seq5 开工令（2026-11-02 22:37）+ 战役工作流 §B4

## 为什么做

现状权限语义是**代码硬编码**（HighRiskToolPolicy 硬名单 + BrowserUse 插件本地 config.json 域名白名单）：
无法按 Agent 区分、无法按工具配置 allow/ask/deny、白名单编辑要改文件/重启语义分散、审批行为不可审计回溯（谁在何时批了什么只能翻 ToolApproval 表）。

## 做什么（lead seq5 四点）

1. **新实体 PermissionProfile**：按 Agent/工具的 allow/ask/deny + 三类白名单（路径/域名/命令模式），XCode 落库；默认策略种子 = 现状 HighRiskToolPolicy 等价语义，**零配置行为不变（兼容性红线）**。
2. **ToolRegistry pre-execute 闸门消费策略**：查配置→决策，查不到回落默认；Deny/Ask 走既有闸门 1 语义（`ToolOutcome.Denied` / fail-closed 审批）。
3. **BrowserUse 域名白名单上收 ToolRegistry**（铁律 2 禁平行 PermissionBroker）：registry 层按策略域名白名单决策；插件本地 BrowserGuard 保留作纵深防御。
4. **审计查询端点**：谁在何时批了什么 = 策略决策日志（新 PermissionDecisionLog）+ 既有 ToolApproval 记录的联合查询。

**契约先行**：Abstractions DTO/接口 + Controller 端点骨架先落并知会 taskcenter-dev（它基于契约写设置页「权限策略」面板与审计视图，见其 TODO seq6）。

## 到什么程度（V1 边界）

- 决策三态 + 白名单三类 + 按 Agent 匹配（全局 AgentId=空 兜底）+ 审计查询（过滤/分页）。
- **不做**（本批明确排除）：IM 审批推送（战役已注 B4.2）、路径/命令通配的完整 glob 语义（V1 = 前缀匹配 + 大小写不敏感，glob 后续）、审批人身份字段（ToolApproval 无 Approver 列，V1 审计「谁」= Note/会话维度，加列属审批契约变更另批）。
- UI 面板 = taskcenter-dev 协同批（b4-permission-ui），本批只供契约。
