# M4a-2 批证据：迁移三个高优宿主屏

批次：2026-10-11 design-overhaul M4a-2
范围（专属）：
- `ForgeSelf.Web/src/views/SkillsView.vue`
- `ForgeSelf.Web/src/views/PluginStore.vue`
- `ForgeSelf.Web/src/views/PluginDetail.vue`

## 1. 执行清单

| # | 改动 | 文件 | 状态 |
|---|------|------|------|
| 1 | 页头换 PageHead（eyebrow「Skills · 技能管理」+ title「技能工坊」；搜索框+创建按钮入 #actions slot） | SkillsView.vue | done |
| 2 | 技能卡按 DESIGN.md「技能卡」规格：4 列 grid；图标 30px 启用金底 12%（--gold-tint-strong）/未启用灰 5%；名称 13px；底部 mono「分类 · N 次」+ 金色小 toggle 28×15 圆钮 knob 11px；未启用整卡 `.skill-card--off` opacity 0.6 | SkillsView.vue | done |
| 3 | 三态：加载 `.loading-state`+spinner；空/错态用 `<EmptyState>`；重试按钮入 EmptyState #action | SkillsView.vue | done |
| 4 | scoped 样式全 token 化（color-mix 派生透明度，无裸 hex/rgb，无 var(--x,#hex) fallback，无 ElXxx 显式 import） | 三屏 | done |
| 5 | PageHead（动态 headerTitle）+ actions 放排序 select；glass 卡片网格；状态 chip：已启用=青铜绿（color-mix --el-color-success 15%）、已停用/未加载=灰（color-mix --fg-muted 12%）、错误=朱红 15% | PluginStore.vue | done |
| 6 | 返回/启用禁用/更新/卸载操作迁入 PageHead #actions；detail-content glass；三态齐全 | PluginDetail.vue | done |
| 7 | 既有 vitest 单测断言同步新 DOM（类名变更同步，规格 §2.4） | `src/__tests__/SkillsView.test.ts`、`src/__tests__/pluginStore.test.ts` | done |
| 8 | e2e 选择器同步：toggle 从 `.btn-ghost` 文案匹配改为 `.skill-toggle` | `e2e/prompts-skills.spec.ts` | done |

保留项（未动）：取数（skillsApi/pluginApi）、事件、props、路由跳转、Element Plus 用法、sidebar/tabs/版本弹窗/排序逻辑全部保留。

## 2. 验证结果（分级）

| 门禁 | 命令 | 结果 | 等级 |
|------|------|------|------|
| 样式守卫 | `npm run design:check` | style-tokens 2346（基线 2501/上限 2484，较基线 -155）；compliance a=244 b=0 c=0 d=0（基线 d=1，更优）；PASS | Verified |
| 类型+lint | `pnpm run check` | 0 errors / 76 warnings（=基线 76，无新增 warning） | Verified |
| 单测 | `npx vitest run --pool=forks --maxWorkers=2` | 67 files / 771 tests 全绿 | Verified |
| 路由 | HTTP GET `/` `/skills` `/plugins` `/plugins/test-id`（前端 :7399） | 全部 200 | Verified |
| 鉴权 API | GET `/api/plugin` `/api/skills`（后端 :7301，带 token） | 200（7116 / 1135 字节） | Verified |
| 鉴权对照 | 同 API 无 token | 401（鉴权强制生效） | Verified |
| e2e /skills | `prompts-skills.spec.ts --grep "技能管理"` | 4/4 passed（真实列表加载/搜索过滤/创建技能/启停切换） | Verified |
| e2e /plugins | `plugin-store.spec.ts` | 7/7 passed（卡片渲染/版本号/启用停用标签/搜索计数/版本弹窗/打开页面入口/截图存档） | Verified |
| 视觉走查 | 读 `screenshots/e2e/plugin-store/plugin-store-walkthrough.png` | 页头三件套齐全；玻璃卡网格；状态 chip 已启用=青铜绿/运行中=灰；无溢出无白屏 | Verified |

注：完整跑 `prompts-skills.spec.ts`（含 /prompts 用例）时 /prompts 两例失败，属并发批次把 PromptsView 侧栏包成 `forge-card glass sidebar`（div role=button）导致 strict-mode violation 与 `.prompt-item` 计数 0——非本批文件（PromptsView 不在 M4a-2 范围），已用 `--grep "技能管理"` 隔离本屏用例通过。

## 3. 决策（四要素）

1. **玻璃容器用全局 `.glass` 类而非 `<ForgeCard>` 包裹**
   - 背景：样板 SystemMonitorView 直接 `class="... glass"`；e2e 强依赖 `.plugin-card`/`.skill-card` 原类名。
   - 理由：既拿到 blur/边框/背景的玻璃质感，又不破坏既有类名契约与 e2e 选择器。
   - 准则：后续屏沿用「原语义类名 + glass」组合，不强行包组件。
2. **青铜绿复用 `--el-color-success`（主题映射 #5A7A5A），不新增 token**
   - 背景：无独立 `--bronze-500` token；禁新增 token 族。
   - 理由：DESIGN.md 已将「已启用」状态映射到 success 色板。
   - 准则：状态色一律走 `color-mix(in srgb, var(--el-color-success) N%, transparent)` 派生。
3. **透明度派生用 `color-mix`，拒绝 `var(--x, #literal)` fallback**
   - 背景：守卫把 `var(--x, #hex)` 判为硬编码色值。
   - 理由：sanctioned 写法是 color-mix，既控透明度又不引入字面色值。
   - 准则：scoped 样式禁止任何 `var(--token, #...)` fallback。
4. **e2e 隔离宿主用 `E2E_SIGN_EXE=false` 跳过签名**
   - 背景：并发批次同时签名/起宿主，DLL 被残留实例占用致 sign 脚本退出 1；globalSetup 显式支持该开关。
   - 理由：e2e 隔离实例非交付物，签名仅为防拦截；跳过即可继续。
   - 准则：本批验证用，不写入仓库配置。

## 4. 未做 / 风险

- **PluginDetail 无独立 e2e 走查截图**：`/plugins/:id` 路由 HTTP 200 已验，操作按钮迁入 PageHead actions；未单独截图（e2e 隔离实例资源受限）。风险低，结构同 PluginStore。
- **/prompts 用例失败未修**：属并发批次（PromptsView 侧栏改造），非本批范围，未动。
- **vitest 首跑 OOM**：机器并发负载所致，降 `--maxWorkers=2` 后全绿；非常态失败。
- **未跑全量 e2e / 未新增插件级 e2e**：按规格门禁（深档触发条件未满足）。
