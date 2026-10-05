# Plan

> 阶段：Stage 3｜具体到真实文件；与输入21 方案的偏差在末尾"偏差记录"里如实写，不改口。

## 实施改动清单

| # | 文件 | 动作 | 完成后判据 |
|---|---|---|---|
| 1 | `Plugins/DesignSystem/web/src/showroom/fit.ts`（新增） | 纯函数与持久化：`ViewMode` / `VIEW_MODES` / `fitScale` / `parseViewMode` / `readStoredView` / `storeView` / `VIEW_STORAGE_KEY` | 单测可独立跑（不依赖 DOM） |
| 2 | `Plugins/DesignSystem/web/src/showroom/Stage.vue` | 加视图 radiogroup + `[data-stage-zoom]` 读数；画布盒 `ref` + `ResizeObserver` → `scale` → 内层 `.ds-stage__scaler { zoom }`；三档几何（`resize` + 高度封顶）；非适应档提示行 | `check` 无 TS 错；e2e E1/E2/E4 可寻址 |
| 3 | `Plugins/DesignSystem/web/src/showroom/Showroom.vue` | `view` ref（读存储 + `watch` 写回）；`[data-showroom-layout]` 钩子；`.ds-showroom__layout--max`（单列 + `.ds-stage{order:-1}`）；给 Stage 传 `:view` / `@update:view`；**展厅这一栏的 `max-width` 单独放宽到 1872**（见"追加改动"） | e2e E3/E5/E6 可寻址 |
| 4 | `Plugins/DesignSystem/web/src/showroom/fit.test.ts`（新增） | 12 条：`fitScale` 数学（含上限 1、异常输入、极窄）、三档名恰为契约值、`parseViewMode` 脏值、存储 round-trip | `pnpm run test` 绿 |
| 5 | `ForgeSelf.Web/e2e/plugins/design-system/design-system-showroom.spec.ts` | 新增 E 片 **6** 条常驻断言（E1~E5 在用户报障那一档 1372x768；E6 在 1920x1080） | `--grep "预览视图档"` 6 passed |
| 6 | `Plugins/DesignSystem/README.md` | 展厅一节写清三档 + 拖拽 + 记忆；已知缺口表加"滚动条是否常驻由浏览器决定"一行 | 文档与实现一致（无会漂数字） |
| 7 | `docs/04-standards/agent-workflow.md` §B（UI 判据） | 沉淀两条踩坑：量滚动条必须**分轴**（横向条吃高度）；本机 Chrome 浮层条 ⇒ 不得把"条占位"当判据 | 后续会话不再重复试错 |

## 顺序与依赖

1 → 4（纯函数先有单测口径）→ 2 → 3 → 5（e2e 判据对着真实 DOM 写）→ 6 → 7。
每步改完即可单独验证：1/4 用 vitest，2/3 用 `check`+`build`，5 用定向 e2e。

## 风险与回滚

- 风险：`zoom` 参与布局，可能与既有"框宽内联 style"判据（B1）或视觉矩阵（D1）互相影响 ⇒ 用整目录 e2e 复跑收口，不靠推断报绿。
- 风险：`ResizeObserver` + `max-height` 存在"滚动条吃掉宽度 → 重算 → 再吃"的回环 ⇒ `k` 单调下降且下界为 `min-width:240px`，收敛；实测读数多次一致（放宽栏宽前 51%、放宽后 61%）。
- 回滚点：本批全部改动集中在 3 个源文件 + 2 个测试文件 + 文档，`git checkout --` 即回；未提交前 HEAD 为 `5dd975e`。

## 追加改动（验证期发现的第二个成因，不在原方案里）

写 E6（1920x1080 那一档）时实测到：**把窗口从 1372 放大到 1920，中列可用宽一点没变**（仍是 648）。
根因不在舞台，而在展厅这一栏与工作台**共用**了 `.ds-mode-pane { max-width: 1240px }`（`styles/base.css:176-182`）——
"预览区太小"因此有**两个**成因：① 没有等比缩放（已由 fit 解决）② 栏宽封顶（缩放只能缓解、不能消除）。
处理：`Showroom.vue` 的 scoped `.ds-showroom` 单独放宽到 **1872**（= 1280 稿 + 2×240 侧栏 + 2×24 间距 + 2×32 内衬），
工作台/开始/交付三栏仍 1240 不动。scoped 选择器是 `.ds-showroom[data-v-*]`（0-2-0）压过 `.ds-mode-pane`（0-1-0），不依赖注入顺序。
效果（e2e 实测）：1372 下中列 648 → **780**（读数 51% → 61%）；1920 下 **1280** ⇒ 「适应」档读数 100%、零裁切，即"大屏就该看到 1:1 全幅"。
这条改动的判据是 E6，反向腿是"读数 > 100 或中列 < 1270 即红"。

## 偏差记录（相对输入21 的 mini-task 方案）

| 原方案 | 实际做法 | 为什么改 |
|---|---|---|
| S3「衣柜/微调各加折叠开关，折叠后收成 48px 图标条，<1500px 默认折衣柜」 | 改为**「最大化」档**：整幅塌单列、舞台排最前 | 输入22 用户直接点名"可以最大化"；一个档位开关比两个折叠状态 + 默认策略更少状态、更少判据，且效果更强（实测 1372 窗口下 780 → 1308，1.68×） |
| S4「fit 模式去纵向内滚 + <=1080px 高度把说明行与 chips 压成一行，回收 >=64px」 | 保留画布盒内滚（`max-height: min(80vh,940px)`），**不改顶部外壳**；非适应档加一行提示 | 缩放之后纵向可见比例已随 `k` 同步改善（1101 → 约 560），压外壳属另一处布局契约（会牵动 C6/D1 的截图），收益不再大于代价 |
| 「容器高度按 `稿高 x k` 折算」（S1 原文） | 用 CSS `zoom` 让盒子自己跟着缩，不手算稿高 | 稿高是内容驱动（模特页不定高），手算要第二个观测器，且与滚动条互相抖 |
| 判据「画布宽 >= 视口 62%」「可见高 >= 稿高 x k 的 60%」 | 改为「适应档横向溢出 ≤2px + 框右缘不越界 + 框渲染宽 ≥ 可用宽 90%」「最大化后 ≥ 三列时 1.5×」「拖窄后读数下降」 | 原两条依赖"稿高"这个不存在的稳定量；新判据全部可从 DOM 直接量，且反向探针（`k` 写死 1）能实红 |
