# 07 Final Report — topbar-tab-icon-slot（顶部栏页签预留关闭/固定图标位 · 悬浮零位移）

- date: 2026-11-23
- task-id: `topbar-tab-icon-slot`
- type: 轻量缺陷修复（UI 悬浮布局位移）· AI-Native 闭环（mini-task 裁剪）
- 状态：**COMPLETED**（未提交 git —— 等待用户批准，工作区留存待审）

## 一、任务与结果

用户指令（输入22）：「顶部栏页签增加宽度，预留关闭、固定图标等位置，避免悬浮展示图标的时候，位置变宽，所有页签都要产生位移」。

结果：非首页页签常驻预留 16px 图标槽（`visibility:hidden + opacity:0 + flex:none` 隐而占位），
hover / 键盘聚焦时图标在槽内淡入（160ms，动效 token），页签宽度悬浮前后**逐像素恒定**，
相邻页签**零位移**。首页签维持无图标不占槽。

## 二、主要变更

| 文件 | 变更 |
|------|------|
| `ForgeSelf.Web/src/components/TopNavbar.vue` | `.close-x`：删 `display:none` ↔ hover `display:inline-flex` 切换；改常驻槽位（visibility/opacity 显隐 + `flex:none` + token 过渡，移出延迟翻转 visibility 保证淡出完整） |
| `ForgeSelf.Web/src/__tests__/TopNavbar.test.ts` | 新增「页签图标槽常驻预留（悬浮零位移守卫）」4 条（源码不变量 3 条 + DOM 结构 1 条），读源方式对齐 PageHead/ForgeCard 守卫 |
| `docs/11-troubleshooting/` | 新建 PC-042 + case `2026-11-23-hover-reveal-width-jump`；`problem-classes/README.md` 台账行、`index.md` 现象速查行 |
| `docs/ai/pilot/2026-11-23-topbar-tab-icon-slot/` | mini-task.md / 05-evidence.md / 06-review.md / 07-final-report.md |
| 截图 | `ForgeSelf.Web/screenshots/dev/topbar-tab-slot/`（before-hovered / after-base / after-hovered / after-hovered-dark） |

## 三、验证结果（全部 Verified，明细见 05-evidence.md）

| 门禁 | 结果 |
|------|------|
| 守卫红灯（修复前） | 3 failed \| 5 passed（18:00，`.temp/red-test.log`） |
| 守卫绿灯（修复后） | 8 passed（18:06，`.temp/green-test2.log`） |
| 几何取证（修复前） | hover「智能体」自身 +22.0px，后续 3 页签各 +22.0px 位移 |
| 几何取证（修复后） | hover 前后所有页签 width/x **Δ0**；图标 hidden↔visible 正确；focus-within 显形、blur 延迟回隐 |
| 快档 | `pnpm run check` 0 errors；`pnpm exec vitest run` **1270 passed (122 文件)**；`pnpm run design:check` PASS（无新增硬编码色值/规范违反） |
| 问题类机器校验 | `check-problem-classes.ps1` **OK：42 个 PC 全通过** |
| 主题 | 亮/暗两态截图核对（显式 html.dark），无新颜色面 |

## 四、设计决策

1. **常驻槽位 vs display 切换**：`display:none` 摘出盒模型是位移根因；`visibility:hidden` 占位但退出
   命中测试/无障碍树，语义与原隐藏态等价 → 位移从机制归零。→ 背景/理由/准则：用户明确「零位移」诉求 +
   DESIGN.md 动效与稳定性取向 + 可回滚最小改动。
2. **过渡采用「悬入零延迟、移出延迟翻转 visibility」**：保证 160ms 淡入即时反馈、淡出完整不闪没；
   只用 `--dur-fast`/`--ease-out` token，无新字面量。
3. **守卫 G1（组件级）**：首次发生单点影响面，按 problem-class-closure 决策逻辑不升级 G2；
   类级不变量入 PC-042 lens，复发即升。
4. **G1 守卫读源方式**：与 PageHead/ForgeCard 守卫同款 `process.cwd()` 锚定（`import.meta.url` 在
   vitest jsdom 下非 `file:`，实测三次迭代确认），并在解析前剥 CSS 注释（注释并入选择器会假阴）。

## 五、代价与收益（运行经济学）

- 代价：所有非首页签常驻 +22px（页签栏总宽增加，tab-nav 滚动吸收）；每页签一个恒渲染 span（DOM +N，可忽略）。
- 收益：消除页签栏持续性布局抖动（CLS 面），hover 交互从「位移跳变」变「槽内淡入」；同类交互有了
  类级不变量与检索入口（PC-042）。

## 六、问题类归档（缺陷类任务硬性节）

- **新建 PC-042**：`docs/11-troubleshooting/problem-classes/PC-042-hover-reveal-width-jump.md`
  —— invariant「行内流式布局中 hover/focus 显形的图标与操作钮必须常驻占位，禁止 display 切换挤占布局」，
  status=guarded，G1 守卫 active（TopNavbar 守卫 4 条），lens 2 问。
- **case**：`docs/11-troubleshooting/cases/2026-11-23-hover-reveal-width-jump.md`（11 节全，severity P1，evidence Verified）。
- **同类扫查**：仓内其余 `display:none` 命中均为媒体查询/弹层/可访问性隐藏，非同型（case §4 命中表）。
- **机器校验**：`pwsh scripts/check-problem-classes.ps1` → OK（42 PC 全通过，2026-11-23 18:14）。

## 七、重构提案（硬性节）

- **R1（提案，未排期）**：页签 × 图标改为 `<button>` + `aria-label`（现存缺口：span 不可键盘聚焦，
  仅靠父级 `:focus-within` 借光显形；键盘用户无法直接触达关闭操作）→ 已记 `TODO.md` 待排。
- **R0（本批已做）**：TopNavbar `.close-x` 常驻槽位化。
- R2/R3：无（无结构性重构收益面）。

## 八、悬空教训（硬性节）

1. **hover-reveal 类交互缺静态守卫的全仓机械化**：PC-042 signature 目前只挂了 TopNavbar 组件守卫（G1），
   仓内其他组件未来引入同型交互（v-show / JS display 赋值）不被机械拦截 —— 依赖 lens 问题与 PC 检索。
   若二次复发，按规则强制升 G2（枚举/AST 级扫描）。
2. **dev-stack 端口占用「假就绪」**（过程暴露，非本缺陷域）：端口顺延后 readiness 探测无法区分自家实例
   与并行 worktree 实例，状态文件记录死 PID → 已记 `TODO.md`（PC-007 邻域），本批不修。
3. **CSS 注释会破坏朴素 CSS 规则解析**（守卫工程教训）：规则前置注释并入选择器导致匹配失败；
   已在本守卫内剥注释，其他源码扫描型守卫如复用该解析模式需同样处理。

## 九、不做事决策（NOT-to-do）

- 不改 tabs store / 其他组件 / 全局 tokens / 后端（用户诉求只在 TopNavbar 显隐几何面）。
- 不给首页签加图标槽（无操作语义，白占空间，维持现状即用户预期）。
- 不在本批修 dev-stack 假就绪缺陷（范围控制 → TODO）。
- 不自行 git commit/push（用户未批准；变更留存工作区待审）。
- 以上已按规约记入 `docs/07-decisions/not-taken-decisions.md`（见该文件 2026-11-23 节）。

## 十、风险

- 页签栏总宽 +22px/签：页签极多时滚动条更早出现（滚动容器吸收，走查无异常）。
- AFTER 量测实例为 `pnpm run dev:web` 单前端（后端 401 噪音不涉顶栏几何）；BEFORE 量测实例为并行
  worktree 栈（源码一致性有 computed cssText 比对背书）—— 口径已在 05 §2/§7 披露。
- 无未验证场景残留；无阻塞。

## 十一、结论

自审（06 Review）Final Decision = **APPROVED**。AC-1~AC-4 全 Verified，快档门禁与问题类闭环机器校验全绿，
用户四点诉求逐条量化达成。**可交付；git 提交等待用户明示批准。**
