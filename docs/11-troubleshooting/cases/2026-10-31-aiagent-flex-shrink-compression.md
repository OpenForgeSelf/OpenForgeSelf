---
id: 2026-10-31-aiagent-flex-shrink-compression
title: AIAgent 执行记录步骤时间线被 flex 压缩裁切成残行
pc: PC-019
severity: P1
evidence: Verified
---
# Case：2026-10-31 AIAgent 执行记录步骤时间线被 flex 压缩裁切

## 1 现象

用户截图（`ForgeSelf.Web/screenshots/dev/ai-agent/run-detail-fresh-20261030-2115.png`）与反馈：执行记录 Run 详情里步骤 3、4 之间夹着一段其他内容（残行），步骤详情被压扁看不全。实测（2026-10-30 getComputedStyle）：`.rrp__step` 高度仅 43px，其详情子块自然高度 195px，且两条步骤之间露出被裁切的 `.rrp__label`（目标 字样，蓝色 rgb(64,158,255)）。

## 2 复现红灯

- 后端实体层：步骤数据完整（AgentStepRun 输出齐全），纯前端渲染缺陷。
- 几何红灯：flex 列滚动容器 `.rrp__steps`（overflow-y:auto）内的 `.rrp__step` 缺 `flex-shrink:0`，步骤总高超出面板可视高时被逐个压扁；`.rrp__step` 自身 `overflow:hidden` → 内容裁切，裁剩的行头碎片落在两条步骤之间（用户看到的「中间显示了其他内容」）。
- 守卫红灯（修复后落地）：`ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts`「计划驱动执行」用例新增 AC-9 几何锁——断言每个步骤行 `flexShrink === '0'` 且 `scrollHeight - clientHeight ≤ 4px`（压缩即残行，该断言在修复前必红）。

## 3 根因（5 Whys + 第 6 问）

1. 为什么步骤被压扁？→ `.rrp__step` 未设 `flex-shrink:0`，flex 列容器按比例压缩子项。
2. 为什么会超出可视高？→ 步骤数多时列内容总高天然超过滚动容器可视高，这正是滚动容器存在的意义。
3. 为什么压缩成残行而不是整体滚动？→ 开发者把「滚动」写在容器上，却忘了子项默认 `flex-shrink:1` 会被先压缩。
4. 为什么此前没发现？→ 步骤少、面板高时不触发；同类问题在 Home 插件已犯过一次（2026-10-26，沉淀为 plugin-development 铁律 8），但只是技能条文，没有类级守卫与台账，AIAgent 新代码照犯。
5. 为什么铁律没有拦住新代码？→ 铁律是散文记忆，不在任何机器校验里；新插件写样式时无从触发检查。
- **第 6 问：为什么流程没让我一开始就写对？**→ 缺少「flex 滚动容器子项必须 flex-shrink:0」的可机器扫描问题类。本批建 PC-019：e2e 几何锁（G2）+ 台账 + README 检索行，把铁律 8 从散文升级为类级守卫。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置 |
|---|---|---|
| `Plugins/AIAgent/web/src/components/RunRecordPanel.vue` `.rrp__step` | 是（用户报告实例） | 加 `flex-shrink:0`（PILOT-077 FR-4） |
| `Plugins/AIAgent/web/src/components/RunRecordPanel.vue` `.rrp__plan-body`（max-height 无 overflow） | 同族变体（溢出叠画而非压缩） | 加 `overflow-y:auto` |
| `Plugins/AIAgent/web/src/agent-core/styles/widget.css` `.aiw-ask__item`（容器 `.aiw-ask` flex 列滚动） | 是（潜伏，同根因未发作） | 加 `flex-shrink:0` |
| `Plugins/AIAgent/web/src/agent-core/styles/widget.css` `.aiw-body`（普通块滚动，非 flex 列） | 否 | 不命中 |
| Home 插件实例（2026-10-26，铁律 8 起源） | 是（同类先发，插件已修） | 已修；作为本 PC 首例记录，不再回改 |

## 5 修复与被否方案

子项统一补 `flex-shrink:0`（容器滚动语义保持不变）；`.rrp__plan-body` 补 `overflow-y:auto`。被否方案：给 `.rrp__steps` 改用非 flex 布局/给步骤固定高度——前者改动面大且时间线依赖 flex 间距，后者丢失内容自适应；且均不解决类问题（下次新面板还会犯）。

## 6 守卫

- G2：`ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts`「计划驱动执行」AC-9 几何锁（flexShrink + 裁切像素断言），active。
