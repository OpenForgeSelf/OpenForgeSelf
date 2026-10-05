# mini-task：展厅预览区「太小、看不全」的结构性修复（输入21 · 2026-10-04 23:4x）

## Intent（为什么 / 做什么 / 到什么程度）
- **用户原话**：「你截图展厅看看，展厅预览区太小，看不全」。
- **为什么**：展厅是设计系统插件的**主出口**（挑预设→看效果→微调→存为新设计）。预览的是「桌面」档位的整页后台稿，而现在画布被夹在中间一列、且**没有任何等比缩放**，用户看不到全貌，只能"滚中滚"——预览的说服力直接归零。
- **到什么程度**：在 1372x768 与 1920x1080 两档窗口下，**默认不滚动就能看见当前档位稿的完整轮廓**（横向不裁、纵向可见比例 >= 60%），并保留「1:1 看细节」的通道；改完有截图读图 + 常驻 e2e 断言。

## 现场实测（Verified，:51888 只读走查，design-system 3.1.0）
- 视口 1372 x 768（dpr 1.5）；画布容器 `ds-stage` = **648 x 1101**（宽只占视口 **47%**）。
- `ds-stage__viewport` 位于 y=334、高 997 ⇒ 窗口底部只剩 **约 434px 可见（44%）**，其余靠内部滚动。
- 无 `iframe`、无 `transform: scale`、`zoom: 1` ⇒ **稿子按 1:1 塞进 648px 槽**，右侧「成交额 92,4…」卡片被直接裁掉一半（截图可见）。
- 布局真源：`Showroom.vue:485` = `grid-template-columns: 240px minmax(0, 1fr) 240px`（<=1100px 才塌成单列）；`Stage.vue:184-186` = `width:100%; min-width:0; overflow-x:auto`。
- 顶部外壳吃掉 231px（banner + 标题行 + tab + 说明行 + 两组档位 chips）。
- 截图：`ForgeSelf.Web/screenshots/live-51888/design-system-3.1.0-showroom-viewport.png`。

## Spec（规格）
- **S1 等比适应（fit）**：画布按「当前档位稿宽」等比缩放到可用宽度，`k = min(1, 可用宽 / 稿宽)`；容器高度按 `稿高 x k` 折算（不留空白、不裁切）。默认 fit。
- **S2 1:1 通道**：档位条上加「适应 / 1:1」开关；选 1:1 时允许横向内部滚动（保留看细节的能力，这是 `overflow-x:auto` 现在的唯一价值）。
- **S3 画布优先的横向分配**：衣柜与微调两栏各加折叠开关，折叠后该列收成 48px 图标条；默认在 <1500px 窗口下衣柜折叠（列表项有缩略名，展开即可）。中列下限从隐式 648px 提到 >= 视口 62%。
- **S4 去双滚动**：fit 模式下画布**不再纵向内部滚动**（纵向由整页承担），并在 <=1080px 高度时把顶部说明行与两组 chips 压成一行，回收 >= 64px 纵向。
- **S5 不破坏既有契约**：试穿/微调不落库、投影同源、`Showroom.vue` 的取数窗口口径（G17 已登记的换装窗口期行为）不改；不改后端与导出文本。

## Plan（具体到文件）
| 文件 | 动作 |
|---|---|
| `Plugins/DesignSystem/web/src/showroom/Stage.vue` | 加 fit 缩放层（`transform: scale(k)` + `transform-origin: top left` + 容器高度折算）；加「适应 / 1:1」开关；fit 时关纵向内滚 |
| `Plugins/DesignSystem/web/src/showroom/Showroom.vue` | 网格列改为「可折叠侧栏 + 中列下限」；侧栏折叠按钮与状态；说明行与 chips 合并为单行（窄高窗口） |
| `Plugins/DesignSystem/web/src/showroom/`（同目录）| 把「稿宽/稿高按档位」的定义抽成可测纯函数（若已有档位宽度表就复用，不新建第二份真相） |
| `Plugins/DesignSystem/web/src/**.test.ts` | 新增：k 计算纯函数单测（各档位 + 各视口宽度，含 k<=1 上限） |
| `ForgeSelf.Web/e2e/plugins/design-system/design-system-showroom.spec.ts` | 新增常驻断言：画布宽 >= 视口 62%、可见高 >= 稿高 x k 的 60%、fit 模式下画布无纵向内滚（`scrollHeight <= clientHeight + 2`）、1:1 档仍可横滚 |
| `Plugins/DesignSystem/README.md` | 已知缺口表加一行（若仍有窗口高度 < 700px 的残留限制，如实写） |

## Task（工作单元）
- **Objective**：让展厅预览默认「看得全」，且不牺牲看细节的通道。
- **Scope Allowed**：上表 6 处（插件前端 + 插件层 e2e + 插件 README）。
- **Scope Forbidden**：后端 `Plugins/DesignSystem/*.cs`、导出/投影文本、宿主源码、`ForgeSelf.Web/src`（宿主前端）、数据库；不动 M3 已验收的取数与换装逻辑；未授权不提交/不打 tag。
- **Acceptance Criteria**：
  - [ ] 1372x768 与 1920x1080 两档下，fit 模式**不滚动即可见当前档位稿全貌**（截图读图为证，两张都要看）
  - [ ] 常驻 e2e 断言落地并绿（`design-system-showroom.spec.ts` 新增那几条）
  - [ ] k 计算纯函数有单测（含 k 上限 1、极窄视口不为 0/负）
  - [ ] 插件前端 `pnpm run check` + `pnpm run test` + `pnpm run build` 全绿
  - [ ] 反向探针：把 k 写死成 1（不缩放）⇒ e2e 新断言必红；还原复绿
  - [ ] 五步插件门禁（改完 = 门禁 + 插件 e2e + 发布 + 隔离实例走查 + 运行实例只读复验）按 `plugin-development` 跑完
- **Verification Commands**：
  - `cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build`
  - `cd ForgeSelf.Web && node node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugins/design-system --workers=1 --output=<空目录>`
  - 只读走查：chrome-devtools 打开 `:51888/design-system#/showroom`，两档窗口各存一张 `ForgeSelf.Web/screenshots/live-51888/`

## 待拍板（默认推荐已给出）
1. **改法范围**：A 只做 S1+S2（等比适应 + 1:1，最小改动）／**C 做 S1~S4（推荐：再加折叠侧栏与去双滚动，一次解决"看不全"的两个成因）**／B 只调列宽不缩放（不推荐：桌面稿在 648px 槽里必然裁）。
2. **默认折叠哪一栏**：推荐 <1500px 时折叠**衣柜**（预设名在展开态可选，且列表最长）；若你更常调品牌色，则改成折叠**微调**。
