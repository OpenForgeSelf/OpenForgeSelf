# 05 Evidence — topbar-tab-icon-slot（顶部栏页签预留图标槽 · 悬浮零位移）

- date: 2026-11-23
- 验证等级标注：**Verified** = 本会话亲自实跑拿到真实输出；Inferred/Unknown 不存在未标注项。

## 1 证据总表

| # | 验证项 | 命令 / 手段 | 结果 | 等级 | 证据落点 |
|---|--------|-------------|------|------|----------|
| E1 | 守卫红灯（修复前） | `pnpm exec vitest run src/__tests__/TopNavbar.test.ts` | 3 failed \| 5 passed（display:none 在场 / hover display 切换 / 槽位规格缺失 三条红；DOM 恒渲染断言绿） | Verified | `.temp/red-test.log`（18:00） |
| E2 | 守卫绿灯（修复后） | 同上 | **8 passed (8)** | Verified | `.temp/green-test2.log`（18:06） |
| E3 | 修复前几何（缺陷复现） | Playwright 量测 `getBoundingClientRect`（悬浮「智能体」前后） | 页签自身 67.8→89.8（**+22.0**）；技能管理 x 254.0→276.0、系统监控 337.1→359.1、任务中心 420.1→442.1（**全部 +22.0 位移**） | Verified | 本文件 §2 ＋ `ForgeSelf.Web/screenshots/dev/topbar-tab-slot/before-hovered.png` |
| E4 | 修复后几何（零位移） | 同上 | 悬浮「智能体」：自身 width 89.8→89.8（**Δ0**）；技能管理 276.0→276.0、系统监控 381.1→381.1、任务中心 486.1→486.1（**全部 Δ0**）；图标 visible/1 正常显形 | Verified | 本文件 §3 ＋ `after-base.png` / `after-hovered.png` |
| E5 | 键盘可达（focus-within） | Playwright evaluate focus/blur | idle `visibility:hidden` → focus `visible` → blur 160ms 延迟翻转回 `hidden`（opacity 0→1→0，过渡按 token 生效） | Verified | 本文件 §4 |
| E6 | 暗色主题无回归 | 显式 `html.dark` + 悬浮截图 | 深色顶栏下 × 正常显形，无新增颜色面（复用 `var(--fg-tertiary)`） | Verified | `after-hovered-dark.png` |
| E7 | 快档门禁 | `pnpm run check` / `pnpm exec vitest run` / `pnpm run design:check` | check：**0 errors**（189 warnings 皆为存量 lint warning，非本批引入）；vitest 全量：**122 文件 / 1270 tests 全绿**（含本批 8 条）；design:check：style-tokens PASS（1768 < 基线 2501，无新增硬编码色值）+ design-compliance PASS（a/b/c/d 与基线持平） | Verified | `.temp/gate-check.log` / `gate-vitest.log` / `gate-design.log`（18:13） |
| E8 | 问题类闭环机器校验 | `pwsh scripts/check-problem-classes.ps1` | **OK：42 个 PC 签名/allowlist/guards 全部通过**（含新建 PC-042，status=guarded 完成扫描） | Verified | `.temp/gate-pc.log`（18:14） |

## 2 修复前量测（E3 明细）

服务实例：并行会话 worktree（`dsh-36a91095-ef3c`）的 dev-stack（:7399），其 TopNavbar 源码与修复前
基线一致（活页 computed cssText 与仓库 `HEAD` 版逐属性相符：`display:none` 主规则 + hover `display:inline-flex`，
无本批改动）。量测于 2026-11-23 18:01：

| 页签 | 修复前 idle | 修复前 hover「智能体」 | Δ |
|------|------------|------------------------|---|
| 首页 | x=127.7 w=54.5 | x=127.7 w=54.5 | 0 |
| 智能体 | x=184.2 w=67.8 | x=184.2 **w=89.8** | **+22.0**（close computed display: flex） |
| 技能管理 | x=254.0 w=81.1 | x=**276.0** w=81.1 | **+22.0 位移** |
| 系统监控 | x=337.1 w=81.1 | x=**359.1** w=81.1 | **+22.0 位移** |
| 任务中心 | x=420.1 w=81.1 | x=**442.1** w=81.1 | **+22.0 位移** |

首页签 close computed display: `(none rendered)`（模板不渲染，符合 FR-4 前置现状）。

## 3 修复后量测（E4 明细）

服务实例：本 checkout `pnpm run dev:web`（:7002，含修复的源码）。量测于 2026-11-23 18:07：

| 页签 | idle（含预留槽） | hover「智能体」 | Δ |
|------|------------------|-----------------|---|
| 首页 | x=127.7 w=54.5 | 同 | 0 |
| 智能体 | x=184.2 w=89.8 | x=184.2 w=89.8 | **0**（close: hidden/0 → visible/1） |
| 技能管理 | x=276.0 w=103.1 | x=276.0 w=103.1 | **0** |
| 系统监控 | x=381.1 w=103.1 | x=381.1 w=103.1 | **0** |
| 任务中心 | x=486.1 w=103.1 | x=486.1 w=103.1 | **0** |

非首页签 idle 宽度较修复前 +22.0（67.8→89.8、81.1→103.1）＝预留槽常驻占位；首页签 54.5 不变（不占槽）。
悬浮全程所有页签 width/x 与 idle **逐像素相等** —— 用户诉求「所有页签都不产生位移」达成。

## 4 显隐生命周期（E5 明细）

Playwright evaluate 逐态断言（2026-11-23 18:07–18:09）：

1. idle：`visibility: hidden`、`opacity: 0`（占位但不可见、不参与命中测试）；
2. `tab.focus()`：`visibility: visible`、`opacity: 1`（`:focus-within` 生效，键盘可达）；
3. blur（无 hover）：350ms 后 `opacity: 0`，`visibility` 经 `0s linear 0.16s` 延迟翻转回 `hidden`
   （首次在 160ms 窗口内读到 visible 属过渡设计内延迟，非缺陷；复测含 400ms 等待后终态 hidden）；
4. computed transition：`opacity 0.16s cubic-bezier(0.22,1,0.36,1), visibility 0s 0.16s`（token 实际生效）。

## 5 走查符合性（ui-ux-design 清单摘录）

- 对齐/一致性：所有非首页签统一 +22px 槽位，页签栏节奏一致；首页签无图标不占槽（有意保留的语义差异）。
- 反馈：hover 淡入 160ms（`--dur-fast`+`--ease-out`），移出延迟翻转无「闪没」。
- 主题：亮/暗两态截图核对，无新增颜色面，暗色下图标对比度沿用既有 `--fg-tertiary` 规格（PC-025 面无变化）。
- 截图：`ForgeSelf.Web/screenshots/dev/topbar-tab-slot/`（before-hovered / after-base / after-hovered / after-hovered-dark，均已读图核对）。

## 6 问题类闭环（E8 回填）

- `pwsh scripts/check-problem-classes.ps1`：**OK：42 个 PC 签名/allowlist/guards 全部通过**（2026-11-23 18:14，exit 0；含新建 PC-042 status=guarded）。
- 归档：`cases/2026-11-23-hover-reveal-width-jump.md`（11 节全）＋ `problem-classes/PC-042-hover-reveal-width-jump.md`
  （invariant/signature/G1/lens）＋ `problem-classes/README.md` 台账行 ＋ `index.md` 现象速查行（2026-11-23）。
- 同类扫查：见 case §4 命中表 —— 其余 `display:none` 命中均为媒体查询/弹层/可访问性隐藏，非同型；无需处置。

## 7 过程偏差记录（如实）

1. **HMR 未生效假象**：首次走查在并行 worktree 的 dev-stack 实例上进行 —— 本 checkout dev-stack 的
   端口顺延选中了 7301/7399，但该两端口已被并行 worktree 实例（`--strictPort`）占用；dev-stack 的
   readiness 探测拿到 200 误判自家就绪（状态文件记录的 PID 实际已死）。缺陷实例 → 记 `TODO.md`
   （dev-stack 端口占用假就绪），本批不修（范围控制）。
2. **守卫测试三次迭代**：① `import.meta.url` 在 vitest jsdom 下非 `file:` → 改 `process.cwd()` 锚定
   （与 PageHead/ForgeCard 守卫同款）；② 修复后仍 2 红 —— 新增 CSS 注释被朴素 CSS 规则解析器并进
   选择器 → 解析前剥注释（守卫自身健壮性修复）；③ 全绿。时间线：18:00 红（3F）→ 18:05 2F（解析器）→ 18:06 绿（8P）。
3. 截图落点：Playwright MCP 工作区根与 repo 不同，已复制入仓 `ForgeSelf.Web/screenshots/dev/topbar-tab-slot/`。
