---
task_id: 2026-10-07-tool-bridge-ui-fix
feature: none          # ToolBridge 前端缺陷修复（复制计数/粘贴框选区），无独立 feature id
risk: { level: L1, triggers: ["Plugins/ToolBridge/web/src/ToolBridgeView.vue"], raised_by_agent: true }
gate1: { mode: user, at: 2026-10-07, ref: "远程同步补登 YAML（原任务已实施并入库；用户裁剪 e2e 手测）" }
gate2: { mode: reviewer, at: 2026-10-07, decision: "APPROVED" }
gate3: { mode: agent, at: 2026-10-07 }
expected_files:
  - Plugins/ToolBridge/web/src/ToolBridgeView.vue
  - Plugins/ToolBridge/plugin.json
rollback: "git revert <commit>"
writeback: { feature_doc: none, reason: "补登头部，无新增文档需求" }
---# 04 Task

## Task ID
TB-UI-FIX-20261007

## Objective
粘贴框点击全选（仅带入焦点那一下）+ 复制按钮恒定文案、按钮旁独立提示每次点击可见变化；插件升 1.0.3。

## Scope
### Allowed
- Plugins/ToolBridge/web/src/ToolBridgeView.vue
- Plugins/ToolBridge/web/src/clipboard.ts（及条件项 clipboard.test.ts）
- ForgeSelf.Web/e2e/plugins/tool-bridge/tool-bridge.spec.ts
- Plugins/ToolBridge/plugin.json（仅 Version）
- 本目录 05~07 工件

### Forbidden
- 命令守卫、CommandExecutor、任何后端代码；pnpm 解析、超时丢输出、复制空文本文案（均已记日记，另立任务）。
- 宿主源码、已安装目录的清单、已发布的 1.0.2 产物；停启宿主进程。
- 一次性 temp 脚本充当验证；git 提交（未获闸门2/3 授权）。

## Acceptance Criteria
- [ ] AC1 连点复制结果两次，序号 1→2，剪贴板读回一致
- [ ] AC2 按钮文案全程不含已复制
- [ ] AC3 解析并执行后结果提示复位，再复制为第 1 次
- [ ] AC4 复制初始指令同样递增且不被清空
- [ ] AC5 失焦后点击粘贴框，选区 = 全文
- [ ] AC6 已有焦点再点击，选区折叠
- [ ] AC7 程序化 focus 不全选
- [ ] AC8 既有 6 条 e2e 仍绿；build 与 vue-tsc -b 通过
- [ ] AC9 版本徽标 v1.0.3

## Expected Files
ToolBridgeView.vue、clipboard.ts、tool-bridge.spec.ts、plugin.json（+ 条件项 clipboard.test.ts）

## Verification Commands
见 03-plan Verification；汇报必须写明档位与每条读数来源（Verified / Inferred / Unknown）。

## 裁剪记录
未获用户明文授权免闸门，亦未选轻量裁剪；按八件出，05~07 在验证后补。闸门1 待用户批准。
