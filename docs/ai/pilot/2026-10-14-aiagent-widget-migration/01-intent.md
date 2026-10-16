# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：2026-10-14-aiagent-widget-migration ｜ 日期：2026-10-14

## Problem

1. **聊天区与定位不匹配**：AIAgent 当前聊天区为手写的 `ChatPanel.vue`（50KB，含消息列表/composer 五入口选择器/步骤卡），是一个「页面内嵌面板」，不是可复用的独立组件；而用户已明确新的组件定位 —— **一个组件代表一个 agent，未来所有 agent 相关功能都往组件上做**，支持内嵌/悬浮多形态、通过属性控制显示隐藏。
2. **新组件尚未入库**：`ai-assistant-widget-1.13.0` 是项目外的独立组件（非 git 仓库），已通过自身验证链（gate 225/225 + mock 17/17，含修复会话导出 bug），但尚未进入 OpenForgeSelf 仓库，无法被项目内页面引用、也无法进入版本管理。
3. **功能迁移路径未建立**：AIAgent 现有能力（目录/Agent/工具/技能/模型选择、计划驱动步骤卡、工具调用卡片等）与组件能力（Markdown 富渲染/会话导出/多标签同步/权限审计/断点续传等）尚未形成「组件为主、页面为辅、逐步收敛」的落点。

## Why

- 用户明确指令：迁移组件进项目、替换 AIAgent 聊天区，组件成为重点组件。
- 长期架构收益：聊天 UI 与 agent 能力沉淀进一个可复用组件，未来不同场景（AIAgent 页面内嵌、悬浮客服、其他宿主页面）通过配置/属性复用同一核心，避免每个页面手写一套聊天实现。
- 降低维护成本：组件单测（225 条）随代码入库，替换后 AIAgent 聊天区获得既有测试覆盖。

## Expected Outcome

1. `ai-assistant-widget` 组件源码（核心聊天 UI + 数据层 + 样式）迁入仓库 `Plugins/AIAgent/web/src/agent-core/`，**零插件依赖**（纯 Vue + props/config 驱动，不 import 插件 http/types）。
2. `AIAgent` 页面中栏聊天区由 `ChatPanel.vue` 替换为 `agent-core` 组件（inline 形态、隐藏组件自带标题栏 `showHeader:false`），对话**走通 AIAgent 后端**（`/api/ai-agent/chat/stream` free 模式 / `/api/ai-agent/runs` plan 模式），会话切换/历史加载/发送/停止/错误提示闭环可用。
3. 中栏页面级工具条保留：折叠左右栏、标题 + 插件版本徽标（铁律 13）、状态点、执行记录入口 —— e2e 断言面（`.chat__version` 等）不破。
4. 组件暂无的能力（目录/Agent/工具/技能/模型选择、计划驱动步骤卡、工具调用卡片、会话历史侧栏/导出/多标签同步/权限审计/遥控等）**逐条记入 TODO.md**，本批不实现；`ChatPanel.vue` 保留文件作为后续功能迁移蓝本。
5. 维护闭环六步全部走完：门禁（构建+单测）→ 冒烟 e2e（更新为替换后的主链路）→ 起源码本地验收 → 本地目录发布（**需用户同意**）→ 真实走查 → 文档+日志。

## Constraints

- 插件铁律：`vue`/`element-plus` 等 external 不变；不 `import` 宿主模块；插件根视图版本徽标必显示；e2e 冒烟仅 1 条主链路；改插件必走维护闭环六步。
- 闸门：闸门1（本 Intent/Spec/Plan/Task）经用户批准后才 Implement；闸门2 Evidence+Review 齐备后交验收；闸门3 验收后提交归档（提交动作本身另需用户同意）。
- **不新增运行时依赖**：agent-core 仅依赖 vue（组件自带 utils/charts/markdown 均为自研）。
- **不删除/不迁移用户数据**：会话历史继续由 AIAgent 后端（ISessionStore）承载；组件内部 persist 关闭，避免双轨。
- 发布动作（本地目录发布/页面点更新）属发布类动作，须用户明确同意后执行；禁止 agent 停/启/杀宿主进程。
- 本批不改后端代码（AIAgent Controllers/Services/实体）、不改会话数据模型。
- 组件迁移保持 JS（原组件语言），以 `agent-core/types.d.ts` 提供类型边界；agent-core 渐进 TS 化记 TODO（不在本批）。

## Success Criteria

1. `Plugins/AIAgent/web/src/agent-core/` 存在且 `vite build` 产物 `web/dist/index.js` + `style.css` 正常（插件构建门禁绿）。
2. 宿主 `pnpm run test`（vitest）覆盖 agent-core 新增单测（`.test.ts`）+ 既有插件测试，全绿；`pnpm run check` 与 `pnpm run design:check` 绿。
3. dev-stack 本地前后端验收：`/ai-agent` 页面中栏为新组件聊天区（inline、无组件标题栏、无 launcher），版本徽标/折叠/执行记录在工具条可见；发送消息走通 `/api/ai-agent/chat/stream`（e2e 断言 SSE 200 + "type" 事件）；会话切换加载历史正常。
4. 冒烟 e2e（`ai-agent.spec.ts` 更新后主链路）通过：渲染三栏 + 版本徽标 + 新组件消息区/输入框可见 + 发送后无致命报错 + 无服务端 5xx。
5. TODO.md 已登记功能缺口清单（目录/Agent/工具/技能/模型选择、步骤卡、工具卡片、组件侧会话导出/多标签/权限/遥控等），并注明「功能迁入组件后删除 ChatPanel.vue / 恢复 composer e2e」。
6. 维护闭环六步证据齐全（门禁日志 / e2e 输出 / 验收截图 / 发布产物校验 / 走查记录 / 文档更新 + 工作日记）。
