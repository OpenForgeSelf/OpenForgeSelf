# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 是。用户拍板定位（重点组件：一个组件代表一个 agent、props/config 交互、零插件依赖；内嵌/悬浮双形态；showHeader 隐藏标题栏；缺失能力记 TODO 迭代补上）全部落地：agent-core 19 文件独立于插件（无宿主 import，全部相对路径/自有样式 token）；AiAgentView 以 `:config` + `:control` 引用组件展示聊天信息；双形态由 `inline:true + showHeader:false` 支持（组件侧 launcher/header 的 v-if 保留，外部可配置）。组件暂无的目录/agent/工具/技能/模型能力已登记 TODO 六条（P1-P3）。
2. **实现是否符合 Spec？** 是。02-spec 九节要点逐条对照：agent-core 迁入（JS 保真 + types.d.ts 类型边界 + test.ts 单测）✅；transport plan/free 分流契约 ✅；AiAgentView 中栏工具条保留 + 组件替换 ✅；ChatPanel 保留不引用 ✅；e2e 断言面更新（FR-4）✅；版本 1.8.0 ✅。Interaction Design 节验收项（工具条 44px / 版本徽标 / 就绪 dot / 折叠按钮 / 组件无 header/launcher / placeholder / 气泡 / 错误态）dev 走查逐项 ✅。
3. **是否超出了 Scope？** 否。全部改动限于迁移批清单（agent-core / types.d.ts / agentBridge.ts / AiAgentView.vue / 版本 / e2e 断言面 / design 基线）。B9-3 修复（:514 一行）属 dev 验收暴露的既有字段错位缺陷，修复最小化且记入日记与偏差表，不扩展范围。
4. **是否修改了不应该修改的文件？** 否。零后端文件变更；`ChatPanel.vue` 保留未删；宿主源码仅 e2e 断言面与 design 基线（组件资产合法引入）；`scripts/design-compliance-baseline.json` 变更为「合理新增」决策（组件 --aiw-* 主题 API，b/c/d 零增量）。
5. **测试是否覆盖 Acceptance Criteria？** 是。agent-core.test.ts 7/7（config 形状 / free 分流 / plan 分流 / error / transport 集成 / 无占位 / hydrate）锁组件适配层；e2e ai-agent 7/7（含主链路冒烟）+ session-management 1/1（B9-3 修复回归）；dev 走查覆盖交互清单全项。
6. **是否存在明显回归风险？** 低。版本徽标/e2e 断言面按既有契约更新；`filterBaselineServerErrors` 只豁免已知基线 5xx 簇（memories/history/sessions），真实断言仍断言响应结构；宿主 test 798/800 的 2 红为既有过期断言（P3 已记）。真实 LLM 链路未验证（环境无模型），但组件错误态路径已验证。
7. **是否存在架构不一致？** 否。agent-core 零插件依赖（无宿主 import、无 external 依赖新增，vite build 内联）；组件以 transport 回调反向调 http.ts（单向依赖）；AiAgentView 保留 plan 执行面板数据源（runCard/buildRunHandlers），组件只做聊天展示与 hydrate 灌入——符合「AIAgent 既有功能逐步迁入组件避免臃肿」的用户拍板方向。
8. **Evidence 是否足以证明任务完成？** 是（发布/提交动作除外）。05-evidence 全量实测记录（Build / 单测 / 宿主门禁 / e2e 8/8 / dev 走查 / B9-3 证据链 / 截图存档 / 动线成本）。**发布④/真实走查⑤由用户明确暂缓**，属未决授权项而非证据缺口。

## Requirement Check

PASS — 用户拍板六条全部落地（重点组件 / 双形态 / showHeader 隐藏 / 缺口记 TODO / 本批=迁移+页面引用 / 闸门1 已批）；发布暂缓按用户指令执行。

## Scope Check

PASS — 改动面与 03-plan 一致；B9-3 修复为验收暴露缺陷的最小修复（已登记偏差）。

## Test Check

PASS — 插件单测 7/7、e2e 8/8、宿主快档 check 0 err / test 798/800（2 红既有对照）/ design:check PASS、插件 build 924ms、dev 走查全链路。

## Architecture Check

PASS — 组件零插件依赖、单向 transport 回调、页面保留 plan 数据源、ChatPanel 保留不引用；无宿主模块 import 违规。

## Risk

**L1** — 已知风险：① 真实 LLM 回复链路未验证（环境无模型，错误态已验证）；② 组件缺失能力按 TODO 迭代；③ 宿主 2 例过期断言与 e2e SQL logic error 基建问题均为既有（P3/P2 已记）；④ 发布与提交待用户授权（非代码风险）。

## Findings

### Critical

无。

### Major

无。

### Minor

1. `agentBridge.ts` 的 transport 对 plan/free 均忽略 tool_call/tool_result 事件（工具轨迹组件不展示）——已按用户拍板记 TODO P1（工具卡片渲染）。
2. 组件 `MessageItem.vue` 渲染 AI 消息依赖 markdown 工具，测试仅覆盖传输层，未覆盖渲染层——渲染层由 dev 走查覆盖（错误态气泡可见），完整消息渲染待真实 LLM 链路。
3. dev 走查中发现会话 id 每次 reload 变化（`ensureSessionId` 行为），属既有会话 id 生成逻辑，不在本批范围。

## UI 审查轮增补（2026-10-14 晚）

用户 /ui-ux-design 审查发现「组件 CSS 完全没加载」这一 Critical 级视觉缺陷（首轮四套门禁全绿但漏检）。修复后复审八问：

1. **需求满足**：样式/交互/空态/主题跟随均已现场 Verified，用户验收页 7399 可见正确新组件。
2. **根因是否治本**：是——CSS 入口缺失是根因，主题适配（class 驱动暗色/变量桥接/外观订阅）是系统性修法而非打补丁。
3. **门禁为何漏检**：视觉规则存在性无自动化覆盖（check/test/design:check 只扫源码文本，e2e 不断言计算样式）；已记 TODO P2 补「关键组件计算样式断言」。
4. **新增改动的范围纪律**：仅 ChatWidget.vue / widget.css / blocks.css / agentBridge.ts / AiAgentView.vue / useChat.js 六文件 + 守卫白名单一行，无顺手重构。
5. **设计门禁**：白名单加 --aiw- 经用户逐项批准（方案 A），复跑 PASS。
6. **回归**：宿主 check 0 error、test 798/800（2 红为既有基线）；后端/e2e 结果见 05 增补。
7. **遗留**：usage 统计、浅色品牌蓝、窄屏三项均非组件缺陷，已分级记 TODO。
8. **CRAP 复核**：对比修复后截图，对比（层级/主色/禁用态）、重复（气泡/圆角/间距统一）、对齐（气泡左缘+头像对齐）、亲密性（欢迎语+chip 分组）均成立。

## Final Decision

**APPROVED**（代码与验证闭环；含 UI 审查轮 8 项修复）——闸门2 证据齐备、验收通过。

⚠️ 附注（不改变 APPROVED 的授权语义）：
- 发布④ / 真实走查⑤ / git 提交（闸门3）均属发布类动作，**按用户「发布等我看完效果再说」与 AGENTS.md §8.5 须先获用户授权**；用户看完 dev 效果后本批方可进入发布链。
- 提交前 pre-commit hook 将校验 00-07 工件齐备（本批 00-06 已齐；07-final-report 可随闸门3 提交时补全——若提交要求 07 先行，需在提交前补写）。
