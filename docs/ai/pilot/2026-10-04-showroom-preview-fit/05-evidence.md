# Evidence

> 阶段：Stage 7｜只记录**实际发生**的验证，来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。

## Task

`2026-10-04-showroom-preview-fit`（轻量档 → 因工件链门禁补齐 00-07；输入21 报障、输入22 拍板）

## Changed Files

| 文件 | 动作 |
|---|---|
| `Plugins/DesignSystem/web/src/showroom/fit.ts` | 新增（`ViewMode` / `VIEW_MODES` / `fitScale` / `parseViewMode` / `readStoredView` / `storeView` / `VIEW_STORAGE_KEY`） |
| `Plugins/DesignSystem/web/src/showroom/fit.test.ts` | 新增（12 条单测） |
| `Plugins/DesignSystem/web/src/showroom/Stage.vue` | 视图档控件 + 缩放读数 + `ResizeObserver` + `zoom` 缩放层 + 三档几何 + 溢出提示行 |
| `Plugins/DesignSystem/web/src/showroom/Showroom.vue` | `view` 状态与持久化接线 + `[data-showroom-layout]` + `.ds-showroom__layout--max`（单列、舞台置前）+ 展厅栏 `max-width: 1872px` |
| `ForgeSelf.Web/e2e/plugins/design-system/design-system-showroom.spec.ts` | 新增 E 片 6 条常驻断言（既有 A/B/C/D 片一条没改） |
| `Plugins/DesignSystem/README.md` | v3.1.0 ③ 段（三档 + 拖拽 + 记忆 + 栏宽）；已知缺口新增 **G22**（滚动条常驻由浏览器决定） |
| `docs/04-standards/agent-workflow.md` | §B3 新增三条（滚动条分轴 + 浮层条 ⇒ 不钉"占位"；`zoom` vs `transform: scale`；共享 `max-width` 封死可用宽） |
| `.agents/skills/design-system-verify/SKILL.md` | 自查表新增 **#72**（"预览看得全"要量出来 + 成因可能有第二个） |
| `docs/ai/pilot/2026-10-04-showroom-preview-fit/**` | 本工件链（00-07 + 原 mini-task） |
| `.forgeself/memory/2026-10-04.md` | 输入22 执行与读数（含两处我自己的错读原样留档） |

未碰：`Plugins/DesignSystem/**/*.cs`、导出/投影文本、宿主源码、宿主前端 `ForgeSelf.Web/src`、e2e 基建、数据库。

## 门禁一：插件前端（Verified）

```bash
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build
```

- `check`（vue-tsc `-p tsconfig.check.json`）：**无输出 = 0 error**。
- `test`（宿主 vitest 跑 `../Plugins/DesignSystem/web/src`）：**Test Files 20 passed (20)｜Tests 262 passed (262)**，其中 `src/showroom/fit.test.ts (12 tests)` 为本批新增。
- `build`：`dist/style.css 97.14 kB`、`dist/index.js 445.75 kB`，`✓ built`。

## 门禁二：宿主前端（Verified）

`cd ForgeSelf.Web && pnpm run check` → **0 error / 81 warning**（与 M3 登记的基线同数）；逐文件核对：本批改动的 `.vue` / `.ts` / e2e spec **一条告警都不在清单里**（首轮曾有 2 条 TS2339 出在我自己的探针代码上，已改为 `(el: HTMLElement)` 后归零）。

## 门禁三：插件层 e2e（真实宿主、零 mock、`--workers=1`）

### E 片定向（Verified，多次）

```bash
node node_modules/@playwright/test/cli.js test --config=playwright.config.ts \
  e2e/plugins/design-system --grep "预览视图档" --workers=1 --output=../.pw-out-ds-view
```
→ **6 passed (58.3s)**（视口 1372x768；E6 内切到 1920x1080）。逐条读数取自 `ForgeSelf.Web/screenshots/e2e/design-system/m2/showroom-E*-passed.log`：

| 判据 | 实测读数 |
|---|---|
| E1 适应不裁 | 可用宽 **780** / 稿宽 1280 ⇒ 读数 **61%**；横向溢出 **0px**、纵向溢出 **0px**（盒高 593/593）；框右缘 1076 = 容器内容右缘；4 根柱既有高度（最低 > 8px）又有非透明底色 |
| E2 1:1 可达 | `clientWidth=780 scrollWidth=1280`（前提成立）→ 滚到末端 `scrollLeft=500`（=1280-780）、`scrollTop=417`，框右缘 1076 落在可见区内；提示行 `[data-stage-hint]` 在场 |
| E3 最大化 | 三列可用宽 **780 → 单列 1308（1.68×）**，`grid-template-columns` 计算值只剩一条轨道（`1308px`）；读数 100% |
| E4 自由调尺寸 | **真鼠标**拖右下角：宽 **780 → 560**，读数 **61% → 44%**，拖完仍 0 溢出 |
| E5 记忆 | 切 1:1 → `localStorage['ds.showroom.view']=actual` → 刷新后读数 100%、radio 仍 1:1；把存储写脏（`maximise`）→ 刷新回落「适应」、舞台在、画布真上色，柱读数 `90px/rgb(0, 90, 177) 124px/rgb(156, 97, 30) 163px/rgb(4, 111, 55) 107px/rgb(142, 106, 0)` |
| E6 宽窗口 | 1372 → 1920：中列 **780 → 1280**，读数 **100%**（不放大也不裁），横向溢出 0；最大化档可用宽 **1808**、溢出 0 |

### 整目录回归（Verified）

`e2e/plugins/design-system --workers=1 --output=../.pw-out-ds-view2` → **36 passed / 2 failed (6.5m)**。两条红**都不是判据红**，且与本批改动无关：

| 用例 | 真实报错 | 归因 |
|---|---|---|
| `design-system-style.spec.ts:701` V3 轴视觉矩阵（跑了 1.7m） | `page.evaluate: TypeError: Failed to fetch`（栈：`design-system-helpers.ts:122 apiText` → `style.spec.ts:801`） | 页面发出的 API 请求**取不到后端**（跑到第 37 条时 e2e 宿主已不应答） |
| `design-system.spec.ts:211` 工作台全链路黄金链（12.1s 即挂） | `page.evaluate: TypeError: Failed to fetch`（栈：`spec.ts:117 apiData` → `:175 effectiveValue` → `:297`） | 同一时刻后端不可达（紧随其后，第 39 条） |

同批前一次整目录跑（放宽栏宽之前、E 片 5 条时）是 **38 passed / 0 failed (6.8m)**，两条本轮变红的用例当时都是绿的 ⇒ 与代码无关，属 `design-system-verify` §四 已登记的环境形态（本机有并行会话时端口认领 + `reuseExistingServer` 互相打死 webServer）。⇒ 按技能给的正规跑法**显式钉端口**复跑这两条 + E 片：

```bash
E2E_FRONTEND_PORT=7402 E2E_BACKEND_PORT=7502 node node_modules/@playwright/test/cli.js test \
  --config=playwright.config.ts e2e/plugins/design-system --grep "预览视图档|V3 每条轴|远程加载" --workers=1
```
→ **8 passed (3.5m)**（V3 轴矩阵 1.5m、黄金链 55.6s、E 片 6 条全绿）。**未改用例判据**，只换了跑法。

### 终态源码整目录复跑（Verified，权威一轮）

```bash
E2E_FRONTEND_PORT=7402 E2E_BACKEND_PORT=7502 node node_modules/@playwright/test/cli.js test \
  --config=playwright.config.ts e2e/plugins/design-system --workers=1 --output=../.pw-out-ds-final
```
→ **39 passed / 0 failed (6.7m)**（总数 39 = 既有 33 条 + 本批 E 片 6 条）。
E 片六条的终态读数与定向跑**逐条一致**（780/61%、`scrollLeft=500`、1308/1.68×、780→560 读数 61%→44%、1920 中列 1280 读数 100%、回落柱 `90/124/163/107px` 且四色非透明）⇒ 无"只在定向跑里绿"的判据；A/B/C/D 与 V 片、黄金链全部无回归。

### 反向探针（Verified，两级都实红后还原）

| 注入 | 结果 |
|---|---|
| `fitScale` 改成恒 `return 1` | 单测红 2 条：`expected 1 to be close to 0.50625`、`expected 1 to be less than 1`（失败 2 / 通过 10 / 总计 12） |
| 同上（重建 dist 后跑 e2e） | E 片红 2 条：E1 `适应档应真缩了（读数 100%）`、E4 `可用宽变窄后缩放比应跟着重算：100% → 100%`（2 failed） |
| 还原 | `grep Math.min` 命中 `fit.ts:27`；重建后 E 片 6/6 绿、单测 262/262 绿 |

### 开发过程中"自己红过 / 判据被证伪"的四条（不是摆设，都是判据修正的依据）

1. `E2` 断言"滚动条占位 `offsetWidth-clientWidth > 0`" → 红 `实测 0px`。先怀疑判据而非实现：加 `overflow:scroll` 空白 div 对照 + 两个轴都量 ⇒ 本机 Chrome 是**浮层滚动条**（三个读数全 0），且 `::-webkit-scrollbar{width:40px;background:#f0f}` 品红探针**一个像素都没画**。⇒ 判据改为"末端可达 + 提示行在场"，"条常驻"降级为 README **G22**（Unknown：用户机器观感）。
2. 上面那条断言本身还犯了**轴向错**：横向条吃的是 `offsetHeight-clientHeight`，不是 `offsetWidth-clientWidth`。两个轴都量才看得见真相。
3. `E6` 断言"1920 下中列应比 1372 宽出 1.8×" → 红 `780 → 1280`（比值口径错，且暴露"1372→1920 原本一点没变"）⇒ 顺藤摸出第二个成因 `.ds-mode-pane{max-width:1240px}`，改完把判据改成绝对量（`clientWidth ≥ 1270` 且读数 `= 100%`）。
4. **一条太弱的判据被读图逼出来**：`e5-persist.png` 首读是"只有骨架没有皮肤"的模特页（卡片无底色、四根柱透明）。我先加的判据是"柱高 > 8px"——**它照样绿**（90/124/162/107px），因为高度来自布局、与皮肤无关。真因＝E5 在 `page.reload()` 后直接截图，而 `loadCss()` 还没把后端文本注入（G17 登记的"取数窗口"在证据里现形）。改法＝刷新后先 `expect.poll(canvasBg).not.toBe('rgba(0, 0, 0, 0)')` 再判再拍，柱判据同时要求底色非透明 ⇒ 复跑读数见上表，图里卡底与柱色都到位。**规律：截图取证前"有尺寸"不等于"有内容"。**

## 截图读图（Verified，Level 3 清单：图标/间距/颜色/留白/对齐/遮挡/溢出）

`ForgeSelf.Web/screenshots/e2e/design-system/m2/`：`e1-fit.png`、`e2-actual-scrolled-right.png`、`e3-max.png`、`e4-resized-narrow.png`、`e5-persist.png`、`e6-fit-1920.png`、`e6-max-1920.png` 逐张看过。

- `e1-fit`（1372 适应）：四张统计卡 + 导出/数据订阅 + 四根柱 + 订单表**全幅入镜**，无右侧裁切（对照输入21 的现场旧图 `live-51888/design-system-3.1.0-showroom-viewport.png`，旧图"成交额"卡片被切掉一半）。
- `e6-fit-1920`：1:1 全幅，字距与真实设计尺寸一致（不是拉伸出来的清晰度）。
- `e3-max` / `e6-max-1920`：控制条一行含 明暗/疏密/设备/**视图**四组 + 右端读数；提示行在画布下方左对齐；最大化时画布盒比稿宽，**右侧留白是刻意的**——不在盒内居中，因为 `overflow:auto` 下 flex 居中会把溢出内容的左边缘推出可视区，那会造出新的"看不到"。
- `e5-persist` 首读时看到"柱子是空的" ⇒ 不当眼力活收场，也不拿"柱高 > 8px"交差（那条判据当时照样绿）：最终改成"刷新后先等画布被后端文本上色，再判 4 根柱**既有高度又有底色**，再拍"。终态图里卡底、柱色、徽章色全部到位（读数见上表 E5 行）。

## 门禁四：本地插件包按**包内容**验真（Verified）

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/package-plugin.ps1 -Plugin DesignSystem -Force
```
→ `Package OK: artifacts/plugin-packages/design-system-3.1.0.forgeself-plugin`，`SHA256: FFD2733F6C8586FA28A749D2DBFB9C0CAF60D53614CB19392713D2C631CF0EE1`。
包内 8 个条目逐项核对（不是只看脚本说成功）：

| 检查 | 结果 |
|---|---|
| `plugin.json` 的 `Version` | `3.1.0`（与 `plugin.json` 一致；route `/design-system`） |
| 入口 `DesignSystem.dll` 在包内 | 是 |
| 宿主共享程序集是否混入（`ForgeSelf.*` / `NewLife.*` / `XCode.dll` / `MX.dll`） | **0 个**（只带 `ForgeSelf.Core.pdb` / `ForgeSelf.Abstractions.pdb` 两个符号文件，非 DLL） |
| `web/dist/index.js` 字节数 | 445,751 B ＝ 本批 `pnpm run build` 的 445.75 kB（同一份产物，不是旧包） |
| `web/dist/style.css` 字节数 | 97,136 B ＝ 97.14 kB 同上 |
| 新代码在产物里 | `ds.showroom.view`、`data-stage-zoom`、`data-stage-hint`、三档标签（含「最大化」）全部命中 |
| 新样式在产物里 | `max-width:1872px`、`ds-stage__viewport--fit`、`resize:horizontal`/`both`、`.ds-showroom__layout--max[data-v-b6209902]{grid-template-columns:minmax(0,1fr)}`、`.ds-showroom__layout--max>.ds-stage[...]{order:-1}` 全部命中 |
| 探针残留 | 包内 **无** `::-webkit-scrollbar{width:40px`（品红探针未进交付物） |

## Known Limitations（不假装做到）

1. **滚动条是否常驻可见由浏览器决定**（G22）：本机 Chrome 浮层条 ⇒ 只能保证"可达 + 有提示"。彻底解法是自绘滚动条组件（新依赖 + 新交互面），本批未做。
2. **`fit` 档的 `max-height: min(80vh, 940px)` 是估值**：矮窗口下画布盒内会出现纵向滚动（内容仍可达）。若用户觉得矮，改的是这一个常量，判据结构不变。
3. **最大化档在超宽屏右侧留白**（见上）：换取"任何像素可达"，不做居中。
4. **未验证**：`zoom` 在 dpr≠1 的真实多显示器环境下的文字重排精度（e2e 跑在 dpr 1）；用户 :51888 实例上的观感要等升级后只读复验（AGENTS.md §0 第⑤步）。
