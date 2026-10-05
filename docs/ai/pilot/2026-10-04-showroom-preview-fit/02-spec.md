# Spec

> 阶段：Stage 2｜九节按模板裁剪到本任务实际有内容的部分；不确定点写 `Unknown`。

## 1. 术语与对象

- **档位（view mode）**：`fit`（适应）/ `actual`（1:1）/ `max`（最大化）三值枚举，UI 上是一个 `role="radiogroup" aria-label="视图"`，三个 radio 名恰为「适应 / 1:1 / 最大化」。
- **稿宽（frame width）**：当前设备档的框宽，唯一真源 `scenes.ts` 的 `DEVICES`（1280 / 820 / 390）。本模块**不写第二份数字**，找不到档位时与 `DeviceFrame` 用同一条回落（桌面宽）。
- **画布盒（viewport）**：`[data-stage-viewport]`（`.ds-stage__viewport`），承担滚动与拖拽改尺寸；**缩放发生在它的子层** `.ds-stage__scaler`。

## 2. 功能规格

- **FR1 适应档缩放比**：`k = min(1, 可用宽 / 稿宽)`，可用宽 = 画布盒 `clientWidth`（已扣除滚动条占位）。
- **FR2 缩放实现**：`.ds-stage__scaler { zoom: k }`。选 `zoom` 而非 `transform: scale()` 的理由：`zoom` 参与布局，盒子尺寸与滚动范围随 `k` 一起变，不需要第二个观测器去测内容高再折算容器高（那会与滚动条互相抖）。
- **FR3 重算时机**：`ResizeObserver` 观测画布盒（挂载即先量一次），拖拽改宽、窗口改宽、两侧栏让位都走同一条路径 ⇒ 无"哪个入口忘了刷新"的分支。
- **FR4 三档几何**：`fit` = `resize: horizontal` + `max-height: min(80vh, 940px)`；`actual` = `height: min(70vh, 900px)` + `resize: both`；`max` = `height: calc(100vh - 300px)` + `min-height: 320px` + `resize: both`。三档共用 `overflow:auto; min-width:240px; max-width:100%`。
- **FR5 最大化让位**：`Showroom` 在 `max` 档给网格加 `.ds-showroom__layout--max`（单列 `minmax(0,1fr)`）并让 `.ds-stage { order: -1 }` 排到最前。用 `> .ds-stage` 而不是 `> :nth-child(2)`：舞台是 `v-if` 的，nth-child 会在舞台缺席时把微调栏提到最前。
- **FR6 提示行**：`view !== 'fit'` 时渲染 `[data-stage-hint]`：「画布外的部分：滚轮或拖滚动条即可到达；拖画布右下角可改画布尺寸。」适应档不渲染（它横向不溢出，提示就是谎）。
- **FR7 记忆**：档位写 `localStorage['ds.showroom.view']`；读时只认三值，其余（含 `null`、脏串、非字符串）回落 `fit`；localStorage 不可用（隐私模式）读写静默。
- **FR8 读数**：`[data-stage-zoom]` 显示 `round(k*100)%`，是非适应档"为什么没有缩"的唯一可见解释。

## 3. 纯函数规格（`fitScale`）

| 输入 | 期望 | 为什么 |
|---|---|---|
| `avail=648 frame=1280` | `0.50625` | 等比 |
| `avail=1920 frame=1280` / `9999/390` | `1` | 上限 1，**不放大** |
| `avail=0 / -500 / NaN / Infinity` | `1` | 拿不到可用宽时交给 1:1 + 滚动条；返回 0 会把画布整个压没，是更坏的失败 |
| `frame=0 / NaN` | `1` | 同上（稿宽异常不许缩成 0） |
| `avail=1 frame=1280` | `0 < k < 1` | 极窄也必须是个正的、可用的数 |

## 4. DOM 契约（e2e 名即判据）

| 钩子 | 语义 |
|---|---|
| `[data-stage] role=radiogroup[aria-label="视图"]` + 三个 `role=radio`（适应 / 1:1 / 最大化） | 档位切换入口 |
| `[data-stage-zoom]` | 当前缩放比读数（唯一出口，e2e 不读组件内部变量） |
| `[data-stage-viewport]` | 画布盒（滚动 + `resize` 的承担者） |
| `[data-stage-frame]` | 设备框（稿），其 `getBoundingClientRect()` 即"屏幕上真画到哪" |
| `[data-stage-hint]` | 溢出提示行（仅非适应档） |
| `[data-showroom-layout]` | 三列/单列网格容器（最大化判据挂这里） |

## 5. 界面与样式约束

只使用既有令牌：`--ds-fg-2`（提示与读数次要色）、`--ds-space-*`、`--ds-fs-small`；不新增色值、不引入 `--el-*`。控件排布沿用控制条现有 `ds-stage__control` 结构，视图组放在设备组之后、读数之前，读数 `margin-left:auto` 靠右。

## 6. 边界与失败模式

- 元素未挂载 / 父级 `display:none` ⇒ `clientWidth=0` ⇒ `k=1`（1:1 兜底，不压没画布）。
- 拖到极窄（`min-width:240px` 为下限）⇒ `k≈0.19`，仍可读、仍不裁。
- localStorage 脏值 ⇒ 回落 `fit`，不抛错、不让舞台消失。
- 无 `ResizeObserver` 的环境（jsdom）⇒ 观测器不建，只保留挂载时的一次测量；组件测试不因它崩。

## 7. 兼容性 / 回归面

- D 片视觉矩阵在 2200 宽下 `k=1`（三列时中列 ≈1660 > 1280）⇒ 30 张图与对比判据不受影响。
- B1 读的是 `[data-stage-frame]` 的**内联 style.width**（不是渲染宽），`zoom` 不改内联值 ⇒ 三档设备框宽判据不变。
- C6 的"页面横向溢出 ≤2px"判据：适应档把稿缩进容器，反而更不容易溢出。
- A1/A2/B2/B3 无画布内点击，`zoom` 不影响 `toBeVisible` / `count()` / `getComputedStyle`。

## 8. Unknown

- **U1**：用户机器上 Chrome 的滚动条策略是否会在真实交互中画出可见条（e2e 的静态截图量到 0 占位）。→ 记 README 已知缺口，不做承诺。
- **U2**：`zoom` 在 Windows 高 DPI（dpr 1.5）下的文字重排精度（是否出现 1px 抖动）。→ 靠两档窗口截图读图人工判，机器判据只覆盖"不裁 / 可达 / 比值"。
- **U3**：模特页在 `max` 档 `calc(100vh - 300px)` 的封顶是否合适（外壳实际占位随宿主版本变）。→ 若用户觉得仍偏矮，改的是这一个常量，不影响判据结构。

## 9. 验收判据（与 04-task 的 AC 一一对应）

见 `04-task.md`；全部为机器判据（vitest 12 条 + 展厅 e2e E 片 5 条），加两档窗口截图读图（人工，Level 3 清单）。
