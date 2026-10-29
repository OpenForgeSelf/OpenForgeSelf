# AI-Native Pilot Result（最终汇报）

> 任务结束后强制格式｜状态只报事实。

## 1. Repository Understanding

我确认了：

- AIAgent 插件的消息区由 `MessageItem.vue`（`.aiw-msg__col` → `.aiw-blocks` + `.aiw-msg__bubble`）与 `BlockRenderer.vue`（各类块）渲染，样式真源是 `agent-core/styles/{widget,blocks}.css`。
- 改前「左边不齐」的机制：三种可折叠行（思考 / 工具 / 步骤分组）的行首都是一个**独立**的 `.aiw-chev` 元素（固定 15px、`opacity:0`，hover 才显现），箭头出现前后的排布与可见结构不一致，用户感知为「悬浮时多出来一个箭头、图标整列右移」。
- 改前的框：`.aiw-msg__bubble` 有 1px 边框 + 阴影；`.aiw-think` 虚线框；`.aiw-grp` 实线框；只有 `.aiw-tool__body` 是「展开后才出现的输入/输出卡片」。
- 测试体系：插件单测（宿主机 vitest 跨包收集）、宿主 CT 画廊（`ForgeSelf.Web/ct/` + `playwright-ct.config.ts`）、插件层 e2e（真实后端零 mock）。

## 2. Selected Task

2026-10-26-aiagent-block-align-border：AIAgent 消息区左列对齐（图标槽）+ 边框只留工具调用（P1，来源：用户输入1）。

## 3. Changed Files

- `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.vue`
- `Plugins/AIAgent/web/src/agent-core/styles/blocks.css`
- `Plugins/AIAgent/web/src/agent-core/styles/widget.css`
- `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.test.ts`
- `Plugins/AIAgent/web/src/agent-core/widget-contract.test.ts`
- `ForgeSelf.Web/src/components/AIAgentMessage.story.ts`
- `ForgeSelf.Web/src/components/AIAgentMessage.ct.spec.ts`
- `ForgeSelf.Web/src/components/AIAgentMessageStoryHost.vue`
- `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.live.spec.ts`（外批 lint 红灯，1 行）
- 工件：`docs/ai/pilot/2026-10-26-aiagent-block-align-border/{mini-task,05-evidence,06-review,07-final-report}.md` + 当天日记 + `TODO.md`

## 4. Validation

Build: PASS —— 插件 `pnpm run build`（`dist/index.js` 274.85 kB / `style.css` 176.42 kB）。

Unit Test: PASS —— `pnpm exec vitest run agent-core` 148 passed；全量 `pnpm exec vitest run` 1034 passed。

Component Test: PASS —— `CT_FRESH=1 pnpm run test:ct:update` 15 passed（重锁过期基线）、`pnpm run test:ct` 15 passed。

Static Analysis: PASS —— `pnpm run check` 0 error（151 条既有告警）、`pnpm run design:check` PASS（硬编码色值 1759 / 基线 2501）。

E2E: PASS（插件冒烟）—— `ai-agent.spec.ts --grep "聊天主链路冒烟"` 1 passed (59.3s)，真实后端零 mock。

Artifact: PASS（收尾补记）—— `Plugins/AIAgent/plugin.json` 1.9.5 → **1.9.6**（本批改了 `web/` 就 bump，消除「版本徽标与包内内容不一致」隐患）；`web/dist/style.css` 实测含 `.aiw-ico` / `.aiw-ico__chev`、不含旧 `.aiw-chev`，`.aiw-msg__bubble` 无 border / box-shadow。

## 5. Evidence

详见 `05-evidence.md`。关键三条：

1. **左列对齐有几何证据**：CT 实测两行 `.aiw-ico` 的 `boundingBox.x` 差 < 0.6px，`hover()` 后 x 不变（<0.6px）；图标左缘与气泡文字左缘差 12px（±1px）。改前箭头是独立 flex 元素、宽度 15px，结构上必然产生位移。
2. **边框归属有守卫**：`widget-contract.test.ts` 新增「边框归属」用例——正文气泡无 border/box-shadow、思考与分组无 border、`aiw-tool__body` **必须**有 border、`.aiw-ico` 必须 `flex: 0 0 22px`；CT 再用 `getComputedStyle` 复验一遍（0px / 1px / 0px）。
3. **真实宿主已看一眼**：`ForgeSelf.Web/screenshots/e2e/ai-agent/ai-agent-chat-smoke.png`（22:08）里正文气泡已是无边框浅灰块。

## 6. Review

`06-review.md` → **APPROVED**（Requirement / Scope / Test / Architecture 四项 PASS，Risk L1）。

## 7. Risk

L1 —— 纯样式与模板结构，无数据契约 / 接口 / 迁移改动；回退 = 还原 5 个源文件。

## 8. Problems Found

- 首跑 `pnpm run check` 红：`e2e/plugins/ai-agent/ai-agent.live.spec.ts:202 no-useless-assignment`（外批既有红灯）。已做 1 行最小修复（`let sawRunning: boolean`）。
- 全量 e2e 收集期即失败：`e2e/plugins/sems/sems.spec.ts:23` 对带 BOM 的 `Plugins/Sems/plugin.json` 直接 `JSON.parse` → `SyntaxError`。本次用显式 spec 路径绕过，缺陷记入 `TODO.md`。
- 本机模型密钥 401，`流式聊天端点` 用例虽 passed，但流里回的是 `401 (Unauthorized)`，因此**没有拿到真实工具卡的一手截图**。
- CT 基线是上一会话遗留的旧形态（箭头在右、有框），本次 `--update-snapshots` 全量重锁；同时把 CT 宿主底色改成 `--aiw-bg-soft`（与真实 `.aiw-body` 一致），否则去框后基线看不出气泡。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS（两处口径歧义由用户当面拍板，记入 Spec-Q） |
| Spec → Plan | PASS |
| Plan → Code | PASS（偏差 1 处：AC4 CSS 守卫落在 widget-contract） |
| Code → Test | PASS |
| Code → 发布链 | NOT_RUN（用户 2026-10-26 拍板「先不发布」）|
| Test → Evidence | PASS（缺口如实标注：无真实工具卡截图、无运行实例复验） |
| Evidence → Review | PASS |

## 10. 问题类归档

无（本任务为**需求完善类**：用户要求「先记录并完善需求，再调整」，两条诉求均为形态改进；不是回归/返工，也无第二处同根缺陷）。
虽未建 PC，类级守卫已落地：`widget-contract.test.ts`「边框归属」+ CT 几何断言 —— 这两个断言就是「过程块随手加框」「悬浮多出元素导致位移」两类问题的机器拦截点。

## 11. 重构提案

R0（本批不重构）。理由：`.aiw-ico` 已在三处复用同一套规则，模板里重复的只有那 3 行内联 `<svg>` 箭头路径；抽公共子组件会让 `BlockRenderer` 多一个文件依赖，收益小于成本。真正值得做的是下一轮把「可折叠行」抽成 `<FoldRow>` 组件（统一 head/badge/aria），属跨块重构，需单独立项。

## 12. 悬空教训清单（必须为空）

空。本次全部「发现的问题 / 遗留 / 建议」已落成条目：

- Sems `plugin.json` 带 BOM 导致 e2e 收集失败 → `TODO.md`（P2，来源:输入1·2026-10-26）。
- 真实工具卡截图缺失（模型 401）→ `06-review.md` Major + `05-evidence.md` Known Limitations。
- `.aiw-theme-bigscreen` 空 `border-color`、`.aiw-plan` 是否去框 → `06-review.md` Minor。
- 运行实例（`:51888`）只读复验未做（用户拍板本批先不发布）→ `TODO.md` + `05-evidence.md` Known Limitations。

## 13. 交付状态

**PARTIALLY_COMPLETED**（六步闭环未走完，不是代码未完成）：

- 已完成：① 门禁（build / vitest / CT / check / design:check）、② 插件冒烟 e2e、工件链（mini-task + 05/06/07）、插件版本 bump 与构建产物核对。
- 第③步（起源码本地前后端手工走查）以**源码构建的 e2e + 现场截图**作等价证据：dev-stack 数据根隔离为空、本机模型密钥 401 发不出工具卡回合，开页面也看不到本批关键形态。
- 第④⑤步（本地目录发布 + `:51888` 只读复验）：**用户拍板先不发布**，已挂账 TODO。补做入口：

```powershell
pwsh scripts/release/release-local.ps1 -Version 2.4.16 -Sign -UpdateDir ..\updates
```

→ 页面「检查更新 → 下载 → 重启并更新」（update-agent 自更新，agent 不停/启/杀宿主）→ 升级后做只读复验：版本徽标 v1.9.6 + 一条带工具卡的会话，截图存 `ForgeSelf.Web/screenshots/live-51888/`。
