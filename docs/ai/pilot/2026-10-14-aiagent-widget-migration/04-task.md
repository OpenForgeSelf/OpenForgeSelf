---
task_id: 2026-10-14-aiagent-widget-migration
feature: ai-agent-widget-migration    # 对应 docs/02-features 编号；无则写 none 并说明原因：组件迁移新课题，无既有功能文档编号，完成后补插件文档
risk: { level: L2, triggers: ["Plugins/AIAgent/web/src/AiAgentView.vue", "Plugins/AIAgent/web/src/agent-core/**", "ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts"], raised_by_agent: true }
gate1: { mode: manual, at: null, ref: "docs/ai/pilot/2026-10-14-aiagent-widget-migration/01-intent.md#Success-Criteria" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AIAgent/web/src/agent-core/**
  - Plugins/AIAgent/web/src/agentBridge.ts
  - Plugins/AIAgent/web/src/AiAgentView.vue
  - ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts
  - Plugins/AIAgent/plugin.json
  - Plugins/AIAgent/web/package.json
  - TODO.md
rollback: "git revert <commit>（本批文件均为新增/局部修改，无数据迁移；ChatPanel.vue 未删，回退即恢复引用）"
writeback: { feature_doc: pending, reason: "完成后补 Plugins/AIAgent/docs 与 docs/02-features 插件文档" }
---

# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。

## Task ID

2026-10-14-aiagent-widget-migration

## Objective

把 `ai-assistant-widget-1.13.0` 聊天组件迁入 `Plugins/AIAgent/web/src/agent-core/`（零插件依赖），AIAgent 页面中栏聊天区由 `ChatPanel.vue` 替换为该组件（inline + 隐藏标题栏），对话走通 AIAgent 后端（free/plan 双通道），功能缺口记 TODO，维护闭环六步证据齐备。

## Scope

### Allowed

- 新建 `Plugins/AIAgent/web/src/agent-core/**`（复制迁移源 + 按 03-plan 微改：config.js 三键 / ChatWidget.vue 两处 v-if + hydrate exposed / useChat.js transport 分支 + hydrate / types.d.ts）。
- 新建 `Plugins/AIAgent/web/src/agentBridge.ts`（config + transport，plan/free 分流，调既有 `http.ts`）。
- 修改 `Plugins/AIAgent/web/src/AiAgentView.vue`（中栏工具条保留 + 组件替换 + hydrate/onTurnComplete 接线；删除 ChatPanel import）。
- 修改 `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts`（用例 1/3/6 断言面按 FR-4 更新）。
- 修改 `Plugins/AIAgent/plugin.json`（1.8.0）、`web/package.json`（1.3.0）。
- 新建 `agent-core.test.ts`（transport/hydrate/showHeader/inline）。
- 更新 `TODO.md`（功能缺口清单）。
- 维护闭环：门禁 → 冒烟 e2e → dev-stack 本地验收 → （用户同意后）本地目录发布 → 真实走查 → 文档+日志。

### Forbidden

- **不修改** `ChatPanel.vue` 内容（保留为迁移蓝本，仅失去引用）。
- **不修改** AIAgent 后端（Controllers/Services/Models/Data）、`http.ts`、`types.ts`、`markdown.ts`、`toolEvents.ts`、`sessionArchive.ts`、`index.ts`、`SessionPanel/ContextPanel/FileEditor/RunRecordPanel/AgentEditDialog/StepProgressCard`。
- **不修改** 宿主 `ForgeSelf.Web/src/**` 与 `ForgeSelf.Api/**`。
- **不实现** 02-spec Boundary 列出的本批不做项（composer 五选择器/步骤卡/工具卡片/组件侧历史/导出/多标签/权限/遥控/断点续传 UI/Shadow DOM/launcher）。
- **不新增** 运行时依赖；不删除/迁移任何用户数据/会话数据；不触碰用户运行实例（`:51888`/`D:\src\tools\ForgeSelf`）。
- **不提交 git / 不打 tag / 不发布**，除非用户明确同意（发布动作单独请示）。

## Acceptance Criteria

- [ ] AC-1：agent-core 存在且零插件依赖（grep 无 `@/`、无 `../http`、无 `../types` import）。
- [ ] AC-2：`vite build` 产物 `dist/index.js` + `style.css` 生成；`index.js` 仍导出 `AiAgentView`。
- [ ] AC-3：`pnpm run test` 全绿（含 agent-core.test.ts + 既有插件测试）；`pnpm run check`、`pnpm run design:check` 绿。
- [ ] AC-4：dev-stack 验收：三栏可见、中栏新组件（inline/无标题栏/无 launcher）、版本徽标 v1.8.0、发送→流式（或错误条）、会话切换 hydrate、截图存档。
- [ ] AC-5：冒烟 e2e 通过（渲染+版本+新组件可见+发送主链路无致命报错/无 5xx）。
- [ ] AC-6：TODO.md 功能缺口清单登记（含恢复条件）。
- [ ] AC-7：发布产物校验通过（zip + SHA256SUMS + L1/L2/L3 + -Sign）→ 用户更新 → 真实走查完成（只读复验）。
- [ ] AC-8：插件文档/功能文档 + 当天工作日记更新。

## Expected Files

- `Plugins/AIAgent/web/src/agent-core/**`（新增约 20 个文件）
- `Plugins/AIAgent/web/src/agentBridge.ts`
- `Plugins/AIAgent/web/src/AiAgentView.vue`（修改）
- `Plugins/AIAgent/web/src/agent-core/agent-core.test.ts`
- `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts`（修改）
- `Plugins/AIAgent/plugin.json`、`Plugins/AIAgent/web/package.json`（版本）
- `TODO.md`

## Verification Commands

```powershell
# 1. 插件前端构建
cd Plugins/AIAgent/web && pnpm run build
# 2. 宿主单测 + 类型/lint + 设计守卫
cd ForgeSelf.Web && pnpm run test
cd ForgeSelf.Web && pnpm run check
cd ForgeSelf.Web && pnpm run design:check
# 3. 本插件冒烟 e2e（更新后）
cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/ai-agent --grep "冒烟"
# 4. 本地验收（唯一合法入口）
pwsh scripts/dev-stack.ps1        # 按 AC-4 逐项；-Stop 停止
# 5. 发布（用户同意后）
pwsh scripts/release/release-local.ps1 -Version v1.8.0 -Sign -UpdateDir <目录>
```
