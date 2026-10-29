# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**。来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」。

## Task

2026-10-26-aiagent-block-align-border（AIAgent 消息区左列对齐 + 边框只留工具调用）

## Changed Files

- `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.vue`（三处可折叠行首改为 `.aiw-ico` 图标槽）
- `Plugins/AIAgent/web/src/agent-core/styles/blocks.css`（新增 `.aiw-ico` 规则组；去思考/分组边框；行首内边距 11px → `--aiw-space-5`）
- `Plugins/AIAgent/web/src/agent-core/styles/widget.css`（`.aiw-msg__bubble` 去 border + box-shadow，内边距 13px → `--aiw-space-5`）
- `Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.test.ts`（旧「箭头放最左」用例改写为图标槽用例 + 展开态用例）
- `Plugins/AIAgent/web/src/agent-core/widget-contract.test.ts`（新增「边框归属」守卫：气泡/思考/分组无框、`aiw-tool__body` 必有框、`.aiw-ico` 固定 22px）
- `ForgeSelf.Web/src/components/AIAgentMessage.story.ts`（新增 `AIAgent/MessagesToolRows` 变体：两行工具 + 思考行）
- `ForgeSelf.Web/src/components/AIAgentMessage.ct.spec.ts`（修过期「完成」断言；新增几何对齐 + 边框归属两条用例）
- `ForgeSelf.Web/src/components/AIAgentMessageStoryHost.vue`（宿主底色 `--aiw-bg` → `--aiw-bg-soft`，与真实 `.aiw-body` 一致）
- `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.live.spec.ts`（外批 lint 红灯，1 行：`let sawRunning: boolean` 不赋初值）

## Build

Command:

```bash
cd Plugins/AIAgent/web; pnpm run build
```

Result: PASS（来源等级：Verified）

```text
vite v6.4.3 building for production...
98 modules transformed.
dist/style.css  176.42 kB │ gzip: 27.87 kB
dist/index.js   274.85 kB │ gzip: 75.37 kB
built in 1.07s
```

## Unit Test

Command:

```bash
cd ForgeSelf.Web; pnpm exec vitest run agent-core     # 本批相关
cd ForgeSelf.Web; pnpm exec vitest run                # 全量
```

Result: PASS（来源等级：Verified）

```text
# agent-core 过滤集
Test Files  12 passed (12)
     Tests  148 passed (148)

# 全量
Test Files  89 passed (89)
     Tests  1034 passed (1034)
```

首跑红过一处：新用例 `展开态由图标槽上的 is-open 表达` 断言「初始不含 is-open」失败 —— 工具块默认展开（`open = block.open !== false && !collapsed`）。改为显式 `collapsed: true` 起手后转绿。属用例前提写错，非产品缺陷。

## Component Test（CT 像素 + 几何）

Command:

```bash
cd ForgeSelf.Web; $env:CT_FRESH='1'; pnpm run test:ct:update; pnpm run test:ct
```

Result: PASS（来源等级：Verified）

```text
15 passed        # --update-snapshots：旧基线是「箭头在右、有框」的过期形态，全部重锁
15 passed        # 回归比对（第二次改宿主底色后又跑一轮，仍 15 passed）
```

本批新增两条 CT 用例（`AIAgentMessage.ct.spec.ts`）：

- `工具行左列对齐：图标同一竖线、与正文左缘对齐、悬浮不位移` —— 实测断言：两行 `.aiw-ico` 的 `boundingBox.x` 差 < 0.6px；图标左缘与气泡左缘相差 12px（±1px）；`hover()` 后图标槽 x 不变（<0.6px）；hover 后 glyph `opacity=0`、chev `opacity>0`；已展开行 `is-open` 且 chev 常驻。
- `边框归属：正文无框无阴影，折叠行无框，仅展开的工具体有框` —— 实测：气泡 `border-top-width=0px`、`box-shadow=none`；折叠工具行 `0px`；`.aiw-tool__body` `1px`；`.aiw-think` `0px`。

## Static Analysis

Command: `cd ForgeSelf.Web; pnpm run check` + `pnpm run design:check`

Result: PASS（来源等级：Verified）

```text
151 problems (0 errors, 151 warnings)      # 全为既有 vue/max-attributes-per-line 等告警
[component-conventions] PASS（story 4 个 / ct.spec 4 个，均已注册、配对、标注）
[style-tokens] 硬编码色值合计 1759（基线 2501）→ PASS：无新增，较基线减少 742
[design-compliance] a(token数)=209 b=0 c=0 d=0 → PASS
```

首跑 `pnpm run check` 是 FAIL（1 error）：`e2e/plugins/ai-agent/ai-agent.live.spec.ts:202 no-useless-assignment`（上一会话新写文件的既有红灯，与本次改动无关）。按「不修就永远过不了门禁」原则做了 1 行最小修复（`let sawRunning: boolean`，去掉永远读不到的初值），未改任何断言与流程。

## E2E（插件层冒烟，真实后端零 mock）

Command:

```bash
cd ForgeSelf.Web; pnpm exec playwright test e2e/plugins/ai-agent/ai-agent.spec.ts --grep "聊天主链路冒烟"
```

Result: PASS（来源等级：Verified）

```text
[1/1] ai-agent.spec.ts:321 › 聊天主链路冒烟（v1.8.0 agent-core 组件）：输入 → 发送 → 用户气泡 + 回复态 + 无致命报错
  1 passed (59.3s)
```

另跑 `--grep "流式聊天端点"`：用例 1 passed，但响应体是
`data: {"type":"error","content":"Response status code does not indicate success: 401 (Unauthorized)."}` —— 本机当前模型密钥不可用，**本次没有拿到真实工具卡数据**（如实记录，不当作通过项）。

## Screenshots

- CT（组件级，已逐张读图）：`.temp/ct-check/align2.png`（两行工具 + 思考 + 正文）、`tooltext.png`、`text.png`。
  读图结论：两行工具行首（图标/箭头）落在同一条竖线；展开行显示箭头、折叠行显示类型图标；**只有**展开的工具调用「输入 / 输出」卡片带框；正文与思考行无框。
- E2E（真实宿主，已读图）：`ForgeSelf.Web/screenshots/e2e/ai-agent/ai-agent-chat-smoke.png`（2026-10-26 22:08）。
  读图结论：欢迎语气泡在真实宿主下已是**浅灰块无边框**（改前有 1px 边框 + 阴影）；页面徽标 v1.9.5；模型 401 导致本次无工具卡可看。

## Known Limitations

- 「运行实例只读复验」未做：用户 2026-10-26 拍板本批**先不发布**，`:51888` 仍是旧包，复验没有可观测对象。
- 真实宿主里的工具行对齐只有 CT 级证据（真实浏览器 + 真实 CSS + 真实组件），缺「真实会话 + 真实工具卡」的一手截图（模型 401）。
- 目标平台只覆盖 Chrome（CT 配置 `channel: 'chrome'`）；Safari/Firefox 的 `@media (hover:none)` 行为未实测（Inferred：按标准实现）。

## Unresolved Issues

- 「全程 e2e 一起跑」被外批缺陷打断（Verified）：`pnpm run test:e2e` 收集阶段即失败 ——
  `e2e/plugins/sems/sems.spec.ts:23` 对 `Plugins/Sems/plugin.json` 直接 `JSON.parse`，而该文件是 UTF-8 **带 BOM**，报 `SyntaxError: Unexpected token '﻿'`。
  本次用「显式指定 spec 文件路径」绕过，BOM 缺陷本身已记入 `TODO.md`（属 Sems 批，不在本任务内顺手改）。

## 收尾（输入2「继续」补记）

### 插件版本 bump

`Plugins/AIAgent/plugin.json` 的 `Version` 1.9.5 → **1.9.6**，Description 追加 v1.9.6 形态说明。（来源等级：Verified，读回确认）

原因：本批改了 `Plugins/AIAgent/web/`，而 2.4.15.2610261556 已留下「web 改了但 plugin.json 未 bump ⇒ 包内版本徽标与内容对不上」的 P2 隐患（`TODO.md`）。改 web 就 bump 是本批唯一能当场消除的那半边问题；机制层守卫（发布脚本对未 bump 告警/拒发）仍留在 TODO。

### 构建产物核对（Verified）

取自 `Plugins/AIAgent/web/dist/style.css`：

- 176,414 字符；含 `.aiw-ico`、`.aiw-ico__chev`；**不含**旧 `.aiw-chev`。
- 主规则 `.aiw-msg__bubble{padding:9px var(--aiw-space-5);border-radius:14px;word-break:break-word;overflow-wrap:anywhere;background:#fff;font-size:13.5px;line-height:1.65}` —— **无 border、无 box-shadow**。
- 唯一残留 `border-color:#1e3a5f` 在 `.aiw-theme-bigscreen .aiw-msg__bubble`，无 border-width 时零效果（已在 `06-review.md` Minor 登记）。

### 第③步 起源码本地前后端：以 e2e 等价证据替代手工走查

未做手工 dev-stack 走查，等价证据是上面那条冒烟 e2e —— globalSetup 从**源码**构建宿主并起前端 dev server，截图落在 `ForgeSelf.Web/screenshots/e2e/ai-agent/ai-agent-chat-smoke.png`。

dev-stack 实例（`.temp/dev-stack.json`：:7301 / :7399，token 已验证有效）虽在运行，但：① 数据根隔离为空（`.temp/dev-stack/data`），没有历史会话；② 本机模型密钥 401，发不出带工具卡的回合 ⇒ 即使打开页面也观察不到本批的关键形态（工具行图标槽 / 展开体边框）。

### 第④⑤步 发布 + 运行实例复验：未执行

用户在本轮明确拍板「**先不发布**」（不产出新 zip、不动 `:51888`）。因此本任务状态 = **PARTIALLY_COMPLETED**，六步闭环第④⑤步挂账到 `TODO.md`，待用户发起后补。
