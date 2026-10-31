---
id: PC-019
title: flex/grid 子项尺寸约束双面缺陷：shrink 面压缩裁切 + min-width 面撑破挤压
tags: [frontend-css, flexbox, grid, scroll-container, ui-clipping, min-width]
invariant: flex/grid 子项尺寸约束双面——①shrink 面：flex 列滚动容器（display:flex; flex-direction:column; overflow-y:auto）的直接子项必须 flex-shrink:0（滚动交给容器，子项永不压缩）；容器内带 max-height 的块必须配 overflow。②min-width 面：内容可能超宽（nowrap 预览/长标题/长 URL）的 flex/grid 子项与其分栏容器必须显式 min-width:0（或 overflow hidden），否则 automatic minimum（min-width:auto = min-content）逐级上传撑破分栏。
status: guarded
first_seen: 2026-10-26
recurrences: [2026-10-31-imgateway-minwidth-overflow]
cases: [2026-10-31-aiagent-flex-shrink-compression, 2026-10-31-imgateway-minwidth-overflow]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts"}
  - {id: S2, kind: test, path: "ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts", status: active}
  - {level: G2, path: "ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts", status: active}
lens:
  - 这个 flex 列容器（overflow-y:auto）的直接子项都写 flex-shrink:0 了吗？
  - 容器内带 max-height/固定高的块配 overflow 了吗（溢出内容叠画到哪里）？
  - 子项自身 overflow:hidden 时，压缩后是否会把行头裁成「两条内容之间的残行」？
  - 这个 flex/grid 子项的内容超宽吗（nowrap 预览/长标题/长 URL）？它和父分栏容器都写了 min-width:0（或 overflow）吗？
  - 走查种子里的文本是长文本压力数据（60+ 字符）吗？分栏宽度用 getBoundingClientRect 实测过吗（短种子下缺陷不可见）？
---
# PC-019 flex/grid 子项尺寸约束双面缺陷（shrink 面压缩裁切 + min-width 面撑破挤压）

## 1 不变量与诱因

**shrink 面（2026-10-26 首建档）**：flex 列滚动容器的子项必须 `flex-shrink:0`：容器负责滚动（overflow-y:auto + min-height:0），子项保持自然高度。违反后果有两态：子项被压扁（内容看不全）；子项自身 `overflow:hidden` 时被裁切成残行碎片，视觉上「两条内容之间夹着别的行」——比单纯看不全更具误导性。诱因：CSS flexbox 默认 `flex-shrink:1`，「容器能滚」的直觉让人忘了子项会先被压缩。

**min-width 面（2026-10-31 复发扩面）**：内容可能超宽（nowrap 预览/长标题长 URL）的 flex/grid 子项必须显式 `min-width:0`（或 overflow hidden）。flex 子项的 automatic minimum（`min-width:auto`）= min-content：`white-space:nowrap` 的长文本（如 IM 网关 60 字符预览 ≈720px）逐级上传把固定基准分栏（`flex:0 0 300px`）撑到 ~788px、主内容 `1fr` 被挤到 ~460px；**内层省略链（overflow:hidden + ellipsis）只管自身盒绘制，消不掉外层子项的固有宽度贡献**。诱因：省略链看起来「已经处理了长文本」——但它处理的是绘制，不是布局裁决；且短文本种子下分栏恒等于基准宽，缺陷不可见（天然逃逸型，与 shrink 面同构）。

## 2 特征签名与扫描方法

签名 S1 = e2e 几何锁（真实浏览器 getComputedStyle + 像素裁切量）：断言步骤行 `flexShrink === '0'` 且 `scrollHeight - clientHeight ≤ 4px`。几何断言比样式 grep 强：样式类缺陷 grep 难以配对「容器 vs 子项」语义，而 computed style + scrollHeight 直接量测最终布局事实。

签名 S2（min-width 面）= e2e 几何锁（长文本压力数据 + getBoundingClientRect）：压力种子（60+ 字符）下断言 `.ig-chat__side` width ≤ 基准+容差（≤360px）且主栏 > 侧栏，预览省略生效（`scrollWidth > clientWidth` + nowrap/overflow 样式对）。**短种子下分栏恒等于基准宽 → 锁测不出**，压力数据是签名的一部分。

已知盲区：S1/S2 各自只锚定各自插件实例；其他插件的同类容器由 lens 问句在 Spec/Review 阶段人工覆盖（触 tags: frontend-css 的 UI 任务必答）。grep 类签名（如「overflow-y:auto 且子项无 flex-shrink:0」「nowrap 且无 min-width:0 配对」）无法用单正则表达配对关系，故不设 S-grep。

## 3 Canonical fix

**shrink 面**：容器侧 `display:flex; flex-direction:column; overflow-y:auto; min-height:0`；子项侧 `flex-shrink:0`；容器内带 `max-height` 的块必须同时配 `overflow-y:auto`（否则溢出叠画到后续区块——同族变体，PILOT-077 `.rrp__plan-body` 命中）。

**min-width 面（2026-10-31 扩）**：超宽内容子项与其分栏容器显式 `min-width:0`（保留固定基准宽如 `flex:0 0 300px`）；内层文本省略链（`min-width:0` + `overflow:hidden` + `text-overflow:ellipsis` + `white-space:nowrap`）保持完整。被否方案：预览改多行（破坏单行省略设计）/ 容器 `overflow:hidden` 硬裁（堵不是防，min-content 贡献仍在）。

参考实现：shrink 面 = `Plugins/AIAgent/web/src/components/RunRecordPanel.vue`（`.rrp__steps` / `.rrp__step` / `.rrp__plan-body`，PILOT-077 FR-4）；`Plugins/AIAgent/web/src/agent-core/styles/widget.css`（`.aiw-ask__item` 潜伏同修）。min-width 面 = `Plugins/ImGateway/web/src/ImGatewayView.vue`（`.ig-chat__side{flex:0 0 300px; min-width:0}`，2026-10-31 修复批）+ `Plugins/ImGateway/web/src/components/ChatList.vue`（内层省略链完整范例）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|---|---|---|---|
| G2 | `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts`「计划驱动执行」AC-9 几何锁 | 每次 ai-agent e2e（有模型环境分支） | 仅覆盖本实例；他插件同类容器靠 lens 问句 + Code Review |
| G2 | `ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts` 几何锁（压力数据） | 每次 im-gateway e2e | 仅覆盖本实例；压力数据阈值 60+ 字符，更极端文本（超长 URL）靠 lens |
| 前置 | plugin-development 铁律 8（散文条）+ agent-workflow B3「flex 尺寸约束双面铁律」+ DESIGN.md 新建页面 §7 | 插件样式开发/Spec/走查阶段人工 | 无机器校验，历史上已逃逸两次（本例 min-width 面 + Home 先发例） |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|---|---|---|---|
| [2026-10-31-aiagent-flex-shrink-compression](../cases/2026-10-31-aiagent-flex-shrink-compression.md) | 2026-10-31 | 执行记录步骤被压成 43px 残行，步骤 3/4 间夹裁切碎片 | 否，首次建档（Home 先发例为建档前同族，见 case §4） |
| [2026-10-31-imgateway-minwidth-overflow](../cases/2026-10-31-imgateway-minwidth-overflow.md) | 2026-10-31 | 300px 侧栏被 60 字预览撑到 ~788px，主栏挤到 ~460px | 是（分类面变体：台账仅收 shrink 面，min-width 面无条文无守卫；短种子走查不可见）→ 本例扩面 |
