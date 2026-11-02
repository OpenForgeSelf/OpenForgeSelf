---
id: 2026-10-31-imgateway-minwidth-overflow
title: IM 网关会话列表侧栏缺 min-width:0 被 60 字预览 min-content 撑破挤压主栏
pc: PC-019
severity: P1
evidence: Verified
---
# Case：2026-10-31 IM 网关会话列表侧栏被长预览撑破（PC-019 min-width 面）

## 1 现象

用户 :51888（v2.1.0）截图指认（输入9·2026-10-31）：会话视图「左边是列表，但是很宽，右边内容展示很窄」。实测（发布 zip `versions/2.4.22.2610311100` 的 `plugins/ImGateway/web/dist/style.css` 复核 + dev-stack 实例 getBoundingClientRect）：`.ig-chat__side{flex:0 0 300px}` 无 `min-width:0`，末条消息预览 60 字符（`white-space:nowrap`）的 min-content（≈720px）经 automatic minimum（`min-width:auto`）逐级上传把侧栏撑到 ~788px，主线程 `1fr` 被挤到 ~460px；列表内层省略链（`.ig-clist__body` min-width:0 + preview overflow:hidden）齐全但拦不住外层 flex 子项的固有宽度贡献。

## 2 复现红灯

- 几何红灯：种一条 60+ 汉字末条消息 → 侧栏宽度 = min-content（≈788px）而非 300px 基准；主栏 < 侧栏（比例反转）。走查种子若用短预览（min-content < 300px）则侧栏恒 300px → **缺陷不可见**（天然逃逸型）。
- 守卫红灯（修复后落地）：`ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts` 几何锁——压力数据（120 字中文）下断言 `.ig-chat__side` width ≤ 360 且 `.ig-chat__main` > side，且预览 ellipsis 生效（scrollWidth > clientWidth + nowrap/overflow 样式）。修复前该锁必红。

## 3 根因（5 Whys + 第 6 问）

1. 为什么侧栏被撑破？→ `.ig-chat__side` 未设 `min-width:0`，flex 子项 automatic minimum 默认 = min-content。
2. 为什么 min-content 这么大？→ 列表条目预览 `white-space:nowrap`（单行省略式设计），其后端契约截 60 字符 ≈ 720px，逐级上传到侧栏。
3. 为什么内层省略链没接住？→ 省略链（overflow:hidden + ellipsis）只限制**自身盒**的绘制，不消减 flex 布局里**外层子项**的 min-content 固有贡献——失守在外层 flex 子项本身。
4. 为什么走查没发现？→ 种子数据预览短（min-content < 300px），侧栏恰好恒等于基准宽，目测/比色全过；缺乏长文本压力数据与几何实测。
5. 为什么规范没拦住？→ 铁律 8 / PC-019 只覆盖 shrink 面（压缩裁切），「min-width 面」（撑破挤压）无条文无守卫。
- **第 6 问：为什么流程没让我一开始就写对？**→ D2 方向反转入出的同批根因：双栏布局无比例最小规范。本批立 DESIGN.md 新建页面 §7（双栏最小规范）+ agent-workflow B3「flex 尺寸约束双面铁律」+ PC-019 扩面（本 case）+ e2e 几何锁。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置 |
|---|---|---|
| `Plugins/ImGateway/web/src/ImGatewayView.vue` `.ig-chat__side` | 是（用户报告实例） | 加 `min-width:0`（本批 D1） |
| `Plugins/ImGateway/web/src/ImGatewayView.vue` `.ig-chat__main` | 否（原已 `min-width:0` 防护） | 补铁律注释，无行为变化 |
| `Plugins/TodoTracker/web/src/TodoView.vue:338` `.tt-list-panel`（flex:0 0 400px; min-width:320px） | 部分（显式 min-width 钉死等效防撑破；内部 nowrap 无 overflow 约束，长内容可视觉溢出） | 登记（低风险），跨插件小批候选 |
| `Plugins/TodoTracker/web/src/TodoView.vue:340` `.tt-board-column`（flex:0 0 230px 无 min-width:0） | 部分（父容器 overflow-x:auto 滚动兜底布局不破；列宽可漂移） | 登记（低风险），跨插件小批候选 |
| `Plugins/AgentHub/web/src/AgentHubEventStream.vue:158-161` `.ok-evt__type`（grid 固定轨道 68px + nowrap） | 否（固定轨道不被 min-content 撑破，仅溢出可见） | 登记（低风险） |
| `Plugins/DesignSystem/web/src` 展示 section 数处 nowrap 无 min-width 配对（CompareStrip:149 等 8 处） | 部分（展示型长文本压力低） | 登记（低风险），跨插件小批候选 |

## 5 修复与被否方案

侧栏补 `min-width:0` 一行（保留 `flex:0 0 300px` 基准宽；省略链保持不变）。被否方案：预览改多行截断/去 nowrap——改变既定单行省略设计；给侧栏加 `overflow:hidden` 硬裁——语义上是堵不是防（min-content 贡献仍在，仅视觉裁切），且丢失圆角阴影边界语义。`min-width:0` 是 flexbox 语义的正解（声明「此子项可收缩至 0，固有宽度不参与分栏裁决」）。

## 6 守卫

- G2：`ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts` 几何锁（压力数据 + getBoundingClientRect 断言侧栏 ≤360 / 主>侧 / 预览省略），active。
- 流程前置：DESIGN.md 新建页面 §7 + agent-workflow B3 双面铁律（Spec/走查阶段人工）+ ui-ux-design 清单「双栏比例实测留痕 / 长文本压力」项。
