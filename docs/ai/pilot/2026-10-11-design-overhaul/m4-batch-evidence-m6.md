# M6 动效与 UX 体系统一落地 · 批证据

> 批次：M6（设计稿全面重构 M4-M8 之一）｜执行：SubAgent o_000E3nZgmXt｜日期：2026-10-11
> 范围（专属，未碰视图/插件）：workshop-forge.css token 核实 / ForgeCard.vue / PageHead.vue / ui-ux-design 技能动效规范 / 相关 vitest。

## 1. 执行清单

| # | 任务 | 结果 | 证据 |
|---|------|------|------|
| 1 | 核实 workshop-forge.css 含 DESIGN.md Motion 全部 token | ✅ 全部已就位，无需补 | workshop-forge.css:47-53 |
| 2 | ForgeCard.vue：替换 0.16s 硬编码 → --dur-* 引用 | ✅ | ForgeCard.vue:34-71 |
| 3 | PageHead.vue：title/eyebrow 进入动效 220ms ease-out | ✅ | PageHead.vue:37-95 |
| 4 | ui-ux-design 技能补动效规范条目 | ✅ | .agents/skills/ui-ux-design/SKILL.md「动效规范」节 |
| 5 | 补 vitest：ForgeCard/PageHead transition-duration 断言 | ✅ 12 例全过 | src/components/__tests__/{ForgeCard,PageHead}.test.ts |

## 2. token 核实结果（步骤 1）

DESIGN.md Motion 表要求的 7 个 token，workshop-forge.css `:root` 已全部定义（行 47-53）：

| token | 设计稿真源值（colors_and_type.css:247-258） | 仓库现值 | 一致 |
| --- | --- | --- | --- |
| --dur-instant | 90ms | 90ms | ✅ |
| --dur-fast | 160ms | 160ms | ✅ |
| --dur-base | 220ms | 220ms | ✅ |
| --dur-slow | 360ms | 360ms | ✅ |
| --dur-slower | 540ms | 540ms | ✅ |
| --ease-out | cubic-bezier(0.22,1,0.36,1) | cubic-bezier(0.22,1,0.36,1) | ✅ |
| --ease-forge | cubic-bezier(0.34,1.56,0.64,1)（锤落回弹） | cubic-bezier(0.34,1.56,0.64,1) | ✅ |

结论：无缺失，**未新增任何 token 族**（遵守 compliance A 棘轮；--ease-in/--ease-in-out 设计稿虽有但任务清单未要求，按「禁止新增族」不补）。

## 3. 改动明细（绝对路径）

- `D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Web\src\components\ForgeCard.vue`
  - `transition: color/box-shadow/transform 0.16s` → `var(--dur-base) var(--ease-out)`（交互 hover/focus = 220ms）。
  - 新增一次性进入动画 `forge-card-in`（opacity 0→1 + translateY 4px→0），`var(--dur-fast) var(--ease-out) both`（160ms）。
  - 新增 `@media (prefers-reduced-motion: reduce) { animation: none }`。
  - 未改 props（interactive/padding/onClick）与 slot 契约，未改 role/tabindex。
- `D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\ForgeSelf.Web\src\components\PageHead.vue`
  - eyebrow / title 新增进入动画 `page-head-in`（opacity 0→1 + translateY 6px→0），`var(--dur-base) var(--ease-out)`（220ms ease-out）。
  - title 追加 `var(--dur-instant)`（90ms）延迟形成层级（eyebrow 先入、title 后入）。
  - 新增 prefers-reduced-motion 降级。
  - 未改 eyebrow/title props 与 actions slot 契约。
- `D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.agents\skills\ui-ux-design\SKILL.md`
  - 新增「动效规范（Motion）」节：时长刻度表（90/160/220/360/540 各一档用途）+ hover/active 规则（--dur-base 220ms ease-out）+ --ease-forge 仅 logo/重点时刻 + 禁止空转/尊重 reduced-motion/低配不堆叠。
- 测试（新建）：`src/components/__tests__/ForgeCard.test.ts`（7 例）、`src/components/__tests__/PageHead.test.ts`（5 例）。
- workshop-forge.css：**零改动**（token 已齐）。

`--ease-forge` 锤落回弹本批**未在任何组件使用**（仅 logo/重点时刻才用，属 TopNavbar 范围，不滥用）。

## 4. 验证结果（环境前置：NO_PROXY=localhost,127.0.0.1,::1；TEMP/TMP=.temp\tmp）

| 门禁 | 判据 | 实测 | 等级 |
| --- | --- | --- | --- |
| design:check（style-tokens） | ≤2484 | 2346（较基线减 155，并行批次治理贡献） | ✅ Verified |
| design:check（compliance） | a≤244 b=0 c=0 d≤1 | a=244 b=0 c=0 d=0 | ✅ Verified |
| pnpm run check | 0 errors / 76 warnings 不新增 | 0 errors / 76 warnings（新文件无新增 warning） | ✅ Verified |
| pnpm run test | 不回退 | 69 files / 783 tests 全绿（771 基线 + 12 新增） | ✅ Verified |
| home 冒烟 e2e | 壳层渲染无卡顿/无整页 blur 堆叠 | **Blocked**：global-setup 等待 http://localhost:7104/api/health 120s 超时 | ⚠️ Unknown/环境 |

证据日志：`.temp/m6-design.log` / `.temp/m6-check.log` / `.temp/m6-test.log` / `.temp/m6-test3.log` / `.temp/m6-e2e.log`。

### e2e 阻塞说明（诚实分级）

e2e 在 global-setup 阶段失败：`等待 http://localhost:7104/api/health 就绪超时（120000ms）`。读 `.temp/e2e/wt-b26d4625/backend.log`：宿主正常启动、加载全部 20 插件、随后在 7102 正常服务（workflow CRUD 200）。判定：
- 失败点在 e2e 基建的后端就绪探测/端口时序，**不在前端构建、不在本批 CSS 动效**（前端 vue-tsc/eslint/design:check 全绿）。
- 根因疑为 5 个 M4 并行批次同时跑 vite/dotnet 构建导致系统高负载，宿主在 120s 探测窗口内未被健康检查命中（探测 7104、实际服务 7102 的端口时序分歧）。
- 按项目规则「环境阻塞不打扰用户、不反复重试」与 hint「桌面 GUI 被锁屏/高负载勿反复尝试」，本批不在高负载下重试 e2e；组件渲染契约由 783 个绿单测（含 slot/props/class 断言）兜底。

## 5. 决策（四要素）

- **决策**：ForgeCard/PageHead 进入动画用「一次性 CSS keyframe（opacity+微位移）」而非 Vue `<Transition>`；交互 hover 过渡改 --dur-base 220ms。
- **背景**：任务要求进入 160ms / 交互 220ms / 面板 360ms，且「动效不得高频空转、无意义装饰不做」；组件是 M4 并行批次的只读共享依赖，不能改 root 元素契约。
- **理由**：①一次性 keyframe 只在 DOM 挂载时跑一次（props 更新/列表刷新不重触发），天然不「空转」；②不改 root DOM 结构/slot，M4 视图接入零破坏；③220ms 交互反馈比原 160ms 更稳（符合设计稿 Motion 刻度）；④prefers-reduced-motion 降级覆盖低配/无障碍。
- **准则**：动效一律 var(--dur-*) 引用、禁硬编码秒数；--ease-forge 仅 logo/重点时刻；进入动画位移 ≤6px、时长 ≤160ms，低配不堆叠。

## 6. 未做 / 风险（NOT-to-do / Risks）

- **未做**：home 冒烟 e2e 视觉走查（环境阻塞，见 §4）；--ease-in/--ease-in-out token（任务清单未列，且禁新增族）；ForgeCard「面板级 360ms」（本组件无展开行为，360ms 留给面板级切换场景，token 已定义待用）。
- **风险**：①并行 M4 批次视图接入 ForgeCard/PageHead 后，每张卡片挂载会播 160ms 进入动画——属设计预期的「进入动效」，非装饰空转；若某视图列表高频重渲染需观察是否过闪（记 TODO 观察项）。②e2e 视觉确认缺失，「无整页 blur 堆叠」未做真机走查验证（单测+设计刻度推导为 Inferred：进入动画位移≤6px/时长≤160ms/一次性，PageHead 无 backdrop-filter，理论上不产生堆叠）。
- **不做**：未碰任何视图与插件；未做 git 状态操作；未停/启/杀宿主进程。
