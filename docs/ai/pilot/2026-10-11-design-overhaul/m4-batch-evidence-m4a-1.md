# M4a-1 批次证据：迁移两个高优宿主屏（AgentsManageView / SettingsView）

日期：2026-10-11 ｜ 执行：SubAgent o_000E3Lj3T2 ｜ 状态：COMPLETED_WITH_RISK

## 一、执行清单

| # | 里程碑 | 动作 | 文件 | 证据等级 |
|---|--------|------|------|----------|
| 1 | 读必读 | m4-execution-spec / DESIGN.md / 设计稿 OtherScreens.jsx / 样板 SystemMonitorView + 三组件 | — | Verified |
| 2 | AgentsManageView 冻结化 | PageHead(eyebrow/title/actions) + ForgeCard interactive 包 agent 卡 + EmptyState 三态(加载/错误重试/空) + scoped 全 token | `ForgeSelf.Web/src/views/AgentsManageView.vue` | Verified |
| 3 | SettingsView 冻结化 | PageHead + 左分类玻璃栏(active 金底10%金字) + 右内容玻璃卡，保留 10 panels v-show 与切换 | `ForgeSelf.Web/src/views/SettingsView.vue` | Verified |
| 4 | e2e 补截图 | agents/update-panel spec 首用例加 `page.screenshot(fullPage)` | `e2e/agents.spec.ts`、`e2e/update-panel.spec.ts` | Verified |
| 5 | 修复 role=button 冲突 | SettingsView 壳容器由 `<ForgeCard>` 改为全局 `.glass` 裸 div（ForgeCard 恒带 role=button，与真实按钮 getByRole 冲突） | `ForgeSelf.Web/src/views/SettingsView.vue` | Verified |

## 二、验证结果

### 守卫三连（快档）
- `npm run design:check`：**PASS**。style-tokens 合计 **2346**（基线 2501，较基线 -155；任务要求 ≤2484 禁上涨，满足）。compliance **a=244 / b=0 / c=0 / d=0**（要求 a≤244 b=0 c=0 d≤1，满足）。Verified
- `pnpm run check`（vue-tsc -b && eslint .）：本批两文件 **0 error / 0 warning**（eslint 单跑两文件 exit 0，日志空）。全量 vue-tsc 有 5 个类型红，**全部不在本批文件**：PersonalLibraryView.vue×4（并发 M4b-3 迁移中）、WorkflowLibrary.vue×1（并发 M4b-2 迁移中）。本批文件类型干净。Verified
- `pnpm run test`：全量 14 failed / 757 passed，**全部归属并发批次**（ChatView.test×2=M4b-1、pluginStore.test×3 + SkillsView.test×9=M4a-2）。本批 `SettingsView.test.ts` 单独跑 **18/18 全绿**。Verified

### dev-stack 起源码验收（复用 :7301/:7399，未起未停）
- 前端 `http://localhost:7399/` → HTTP **200**。Verified
- 鉴权 API `GET http://localhost:7301/api/api-server/status`（Bearer token）→ HTTP **200**（非 401，token 有效）。Verified

### e2e 走查（隔离实例 + 全页截图读图）
- `e2e/agents.spec.ts`：**3/3 passed**。截图 `ForgeSelf.Web/screenshots/e2e/agents/agents-list.png`（136KB）。读图：页头三件套齐全（eyebrow「AI AGENT · 智能代理」+ title「接入·调度·记忆」+ 右金徽标「7 个 Agent」+ 金实心「创建 Agent」按钮）；玻璃卡栈带金色左条(启用态)、图标金底、meta 行 mono；无横向溢出、无白屏。Verified
- `e2e/update-panel.spec.ts`：**2/3 passed**。修复前因我 role=button 冲突 strict-mode violation 失败的「面板渲染」「status 回填」两用例现通过；截图 `screenshots/e2e/update-panel/settings-update.png`（112KB）读图：页头（eyebrow「SETTINGS · 通用设置」+ title）+ 左分类玻璃栏（「版本更新」active 金字金左条金底）+ 右内容玻璃卡（更新源配置 GitHub 下拉 + 当前版本 v2.3.0.2610111236 + 检查更新按钮）；无溢出/白屏。Verified
- 第 3 用例「点击检查更新→45s 内出现结果文案」失败：外网 GitHub 检查更新无结果文案返回（spec 注释明示外网不可达时后端返回失败信息；UpdatePanel 内部逻辑范围外，非本批壳层改动引入）。Inferred（环境/网络依赖，非本批回归）
- `e2e/menu-route-consistency.spec.ts`：globalSetup 清理阶段失败（`cmd timeout` 清理残留宿主 PID 时被并发文件占用），属并发 worktree 基建竞争；该 spec 测插件菜单/路由一致性，与本批两个宿主视图无关。Unknown（未跑完，基建竞争，非本批引入）

## 三、决策（四要素）

**决策1：SettingsView 壳容器用全局 `.glass` 裸 div，不用 `<ForgeCard>`**
- 背景：ForgeCard.vue 第 25 行恒输出 `role="button"`（interactive=false 时也输出）。把非交互内容容器包成 role=button 后，`getByRole('button',{name:'检查更新'})` 同时命中内容卡（含「检查更新」文案）与真按钮 → strict-mode violation（update-panel 首跑实证 2 failed）。
- 理由：ForgeCard 是只读组件（M6 并发占用，禁改）；内容卡是面板容器，语义上不应是按钮。`.glass`（workshop-forge.css:297）本就是 ForgeCard 的玻璃视觉来源，裸 div + 手动 padding 视觉一致但不带伪 button 角色。
- 准则：**非交互容器（面板/分栏壳）用 `.glass` 裸 div；只有真正可点的卡（agent 卡）才用 `<ForgeCard interactive>`**。后续批次遇同样 getByRole 冲突按此判。

**决策2：SettingsView nav active 保留 Tailwind `text-primary` 类字符串**
- 背景：`SettingsView.test.ts:71` 断言点击 navItems[3] 后按钮 class 含 `text-primary`。
- 理由：Tailwind @theme `--color-primary: var(--el-color-primary)`=金，`bg-primary/10 text-primary` 即「金底10%金字」，已合设计规格，无需改 token；保留类字符串既过单测又过 e2e。
- 准则：既有单测断言的 class 字符串优先保留，勿为视觉重构破坏。

**决策3：三态 EmptyState 图标用 currentColor SVG 字符串（v-html）**
- 背景：EmptyState `icon` prop 经 v-html 渲染。
- 理由：用 `stroke="currentColor"`/`fill="none"` 内联 SVG，无裸色值（不触发 style-tokens 守卫），金色由 EmptyState 容器 `color: var(--gold-400)` 提供。
- 准则：空态/错误态图标一律 currentColor，禁内联 hex/rgb。

## 四、未做 / 风险（如实）

- **未跑全量 vue-tsc 绿**：全量 5 个类型红（PersonalLibraryView×4、WorkflowLibrary×1）均为并发批次在途迁移文件，本批两文件 0 红；未替并发批次改其文件（越界）。
- **未跑全量 vitest 绿**：14 个失败全属并发批次（ChatView/pluginStore/SkillsView），本批 SettingsView 单测 18/18 绿。
- **menu-route-consistency e2e 未跑完**：globalSetup 清理被并发 worktree 竞争卡住（基建问题，非本批视图）。
- **update-panel 第 3 用例失败**：外网检查更新无结果文案返回（UpdatePanel 内部，范围外；非本批壳层回归）。
- **并发基建竞争**：e2e 隔离实例在共享 worktree wt-b26d4625 上被多批次互相重启宿主/清理，agents.spec.ts 重试 3 次才通；已按「解决不了如实上报」记录。
- 未做：插件级 e2e、全量 e2e、git 提交（按硬约束禁 commit/push）。
