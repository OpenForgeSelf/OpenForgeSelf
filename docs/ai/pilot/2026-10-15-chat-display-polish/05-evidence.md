# 05 Evidence — 2026-10-15 聊天页展示优化三项

所有数据来自本机实跑，命令与原始输出位置如实标注。

## 1. 静态门禁

| 门禁 | 命令 | 结果 |
| --- | --- | --- |
| 插件前端构建 | `cd Plugins/AIAgent/web && pnpm run build` | ✅ 87 modules；`dist/index.js` 209.48 kB / `dist/style.css` 158.11 kB |
| 宿主类型+lint | `cd ForgeSelf.Web && pnpm run check` | ✅ 0 error / 143 既有 warning |
| 设计门禁 | `pnpm run design:check` | ✅ style-tokens `1704`（基线 2501，减少 797）；design-compliance a=209 b/c/d=0，与基线一致 |
| 宿主+插件全量单测 | `npx vitest run` | 804 passed / 2 failed（**2 红为既有** `src/__tests__/AppearancePanel.test.ts` `.bg-swatch` 期望 12 实得 0，P3 design-overhaul 遗留，非本批） |
| 本批新增单测 | `npx vitest run ../Plugins/AIAgent/web/src/agent-core/components/MessageItem.test.ts` | ✅ 6/6 |
| 既有组件单测 | `agent-core.test.ts` | ✅ 7/7 |

### 新增单测覆盖（`MessageItem.test.ts`，6 条）
锁住的用户可见规则：
1. `showBotAvatar:false` → 助手消息无头像、带 `is-noavatar`；
2. **不给该键时仍带头像** —— 组件独立交付守 FR-205，防止有人把宿主裁剪写进默认值；
3. 用户消息永不折叠（即使 stub `scrollHeight=900`）；
4. 助手长回复默认折叠 + 折叠态**正文仍在 DOM**（`text.length > 1000`）+ 点「展开全文」翻 `aria-expanded` 与截断类；
5. 短回复不折叠（无按钮无截断类）；
6. 流式中不折叠。

> jsdom 不跑布局：用 stub `scrollHeight` 驱动判定分支；真实像素（240px 截断、mask）由 e2e / 现场 `getComputedStyle` 断言。

## 2. 插件冒烟 e2e（维护闭环②）

```
npx playwright test e2e/plugins/ai-agent/ai-agent.spec.ts -g "聊天主链路冒烟" --output=.temp/e2e-out-1015
→ 1 passed (1.5m)
```
用例路径：`聊天主链路冒烟（v1.8.0 agent-core 组件）：输入 → 发送 → 用户气泡 + 回复态 + 无致命报错`（真实宿主 + 真实后端，零 mock）。

## 3. 现场验收（dev-stack 起源码前后端，:7399 / :7301）

### 3.1 状态提示条（原文①）

用 fetch 先占住 agent Running，再让组件发送 → 撞后端 409「该会话上一轮对话仍在运行」→ 组件渲染 error 条。DOM 实测：

| 量 | 值 | 判读 |
| --- | --- | --- |
| `.aiw-status` top / bottom | 425 / 479 | 紧贴 footer |
| `.aiw-footer` top | 479 | 与状态条底边**严丝合缝** |
| `.aiw-body` bottom | 425 | 状态条已在消息区之外 |
| `status.closest('.aiw-body')` | `false` | 不随消息滚动（核心诉求） |
| footer `border-top-width` | `0px` | 双分隔线已消 |

error 文案实测：`会话 mv1oetcz-bmrlsc 已有回合在运行，不允许并发 RunAsync`（并发护栏触发，验证时使用）。

### 3.2 刻度条（原文②）

| 操作 | `is-on` 索引 | 佐证 |
| --- | --- | --- |
| 页面加载（贴底） | 3（最后一条，共 4 条） | 默认亮最下面那条 |
| 点最后一条刻度 | **3** | `scrollTop=2229` = 目标消息内容顶 `2241 − 12`；`gapBottom=379`（非底部分支判定正确） |
| 点第 2 条刻度 | 1 | 中间刻度可达 |
| 手动 `scrollTop=400` | 0 | 滚动跟随手滚 |
| 回底后再点最后一条 | 3 | 与贴底分支一致 |
| `is-on` 同时数量 | 恒 1 | 无多亮 |

### 3.3 展示精简（原文③）

| 量 | 实测 | 判读 |
| --- | --- | --- |
| 助手消息数 / 带头像数 | 5 / **0** | 头像已全部移除 |
| `is-noavatar` | 5 | 生效于全部助手消息 |
| 助手列 `max-width` | **92%** | 让出原头像宽度 |
| 用户列 `max-width` | 78% | 用户侧未受影响 |
| 用户消息折叠按钮数 | **0** | 用户消息永不折叠 |
| 默认折叠条数 | 4 | 超 240px 的助手回复默认折叠 |
| 折叠气泡 `max-height` / `mask-image` | `240px` / `linear-gradient(...)` | CSS 生效（非仅挂类名） |
| 展开后高度 | 240 → **522px**，`mask = none`，按钮「收起」，正文 227 字 | 展开/收起交互 + 全文保留 |

截图存档（已逐张 Read 读图复核）：
- `ForgeSelf.Web/screenshots/dev/ai-agent/compact-01-collapsed.png` — 浅色、无头像、长文折叠 + 渐隐 + 「展开全文」
- `.../compact-02-expanded.png` — 展开态全文渲染、无头像、气泡铺宽
- `.../compact-03-dark.png` — 深色主题下同样生效（橙主色、无头像）

## 4. 未验证 / 局限

- 折叠阈值 240px 是**行业通用量级**（约 14 行）而非从真实用户数据推导，长期应观察「平均展开率」再定；
- `toolEvents` 仍不渲染（既有设计，非本次范围）；
- 真实模型链路（:1234）本次未复跑，冒烟用例走的是宿主真实后端 + 无模型时的 error 分支。

## 5. 来源等级标注（2026-10-16 补记，闸门2 门禁格式要求）

本文 §1–§3 全部条目均为本会话亲自在本机实跑/现场实测，来源等级：**Verified**（命令与原始输出位置已逐节标注；截图已 Read 读图复核）。唯一非实测项 = §4 局限所列（阈值数据校准、blocks 数据源、真实模型链路）——来源等级：未验证（设期）。
