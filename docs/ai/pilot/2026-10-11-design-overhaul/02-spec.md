# Specification — 按高保真设计稿全面改造（目标态规格）

> 阶段：Stage 2｜Task：DESIGN-OVERHAUL-2026-10-11｜日期：2026-10-11
> 规则：规格以 DESIGN.md（v2-target，唯一真源映射）为基准；本文只定义"迁移后系统必须满足的可验证标准"与各里程碑 AC，token/组件/布局数值一律指向 DESIGN.md 对应章节，不重复复制（同一事实只允许一处承载）。
> 设计稿文件：`design_files/{App.jsx, HomeScreen.jsx, OtherScreens.jsx, kit-styles.css, colors_and_type.css}`；说明 `../README.md`。

## Functional Requirements

- **FR1 主题 token 器物化**：`ForgeSelf.Web/src/styles/themes/workshop-forge.css` 全面迁移到 DESIGN.md「Tokens」节色板（墨黑/宣纸/朱砂/青铜/熔金 + 中性 90%·金 8%·朱砂 2% 配比），浅色与暗色两套齐全；`--el-*` 与 DESIGN.md 扩展 token（`--forge-*`）映射完整；`bg-image-mode.css` 保持并纳入新 token。
- **FR2 壳层**：App.vue = 背景面板体系（12 纯色 + 6 图 + 遮罩 0–90% 默认 0.55，`localStorage forgeself:bg` 持久化）+ 48px 玻璃 TopBar + 22px StatusBar（mono 10px：端口/版本/状态）+ 助手气泡悬浮入口 + 玻璃遮罩层。
- **FR3 首页**：`Plugins/Home/web/src` 按 HomeScreen.jsx 重构（Hero「傍晚好，锻造师」问候 + 常用功能 + 5 列核心看板 + 待办 + 最近活动空态）；widget 契约落地（home.hero-composer / home.function-grid / home.dashboard-boards / home.todo / home.activity-feed）。
- **FR4 功能屏**：23 个宿主视图 + 其余插件前端统一 PageHead（标题+副题+操作区）、器物卡片（玻璃/铸铁阴影/12px 圆角）、空态/反馈/边界体系；DesignSystem 插件 token 同步。
- **FR5 Plugin-First 架构**：manifest 字段（id/name/version/author/category/icon/displayName/entry/surfaces/permissions/provides/replaces）规范化为宿主校验/文档基准；surfaces=tab|home-widget|quick-action|status-badge 可被宿主消费。
- **FR6 动效与交互**：160/220/360ms + 锤落回弹动效体系；交互规格/空态/反馈/边界成文（M7 规范章节）。
- **FR7 文档与守卫**：docs/README.md §一登记 DESIGN.md；AGENTS.md §8 指向新真源；新建页面/查询规范章节；token 守卫基线重建；e2e 视觉基线。

## Input / Output

- Input：设计稿六个核心文件 + assets 5 SVG（唯一真源）；宿主/插件现有代码。
- Output：迁移后的宿主前端 + 插件前端代码；文档/规范/守卫；e2e 视觉用例与截图。

## Business Rules

- 设计稿优先：代码与设计稿冲突 → 以设计稿/DESIGN.md 为准并移除「待对齐」标注。
- 生产禁止外链图片：背景/品牌资产一律本地化（`ForgeSelf.Web/public/` 或插件 web/ 资产）。
- 禁三条红线（DESIGN.md「Do's/Don'ts」）：玻璃拟态过头（blur>24px）、emoji、胶囊按钮；另禁蓝紫渐变、可爱风。
- 插件发布遵循 plugin-development 五步闭环；宿主升级由 update-agent 自更新，agent 不启停宿主。

## Boundary Conditions

- 浅色/暗色双主题在 1280×720 与 1920×1080 均不破版（Hero 完整容纳、看板 5 列换行、StatusBar 文本不截断）。
- 背景图加载失败 → 降级为对应纯色 + 遮罩（不白屏、不报错堆栈）。
- localStorage 无 `forgeself:bg` → 默认纯色墨黑 + 遮罩 0.55。
- 插件 manifest 缺 surfaces 字段 → 按默认 tab 处理，不阻断加载。

## Error Handling

- 背景图/资产 404：本地化后不存在（无外链），但仍加 `onerror` 降级到纯色。
- 插件 widget 加载失败：widget 位显示规范空态（占位 + 重试），不拉垮整页。
- 查询/接口失败：统一反馈（toast + 空态分级文案），不静默。

## Compatibility

- 保持 Vue 3.5 + Vite + TS + Element Plus + Tailwind 4 + Pinia 技术栈不变；不引入新框架。
- 保持插件动态路由、Home 重定向机制（`ForgeSetting.HomePluginId`）、e2e 地址真源约定。
- e2e 视觉用例截图落 `ForgeSelf.Web/screenshots/e2e/`；live 复验截图落 `ForgeSelf.Web/screenshots/live-<端口>/`。

## Non-functional Requirements

- 性能：玻璃 blur(18px) 只作用于小面积卡片/面板，不整页堆叠；背景图懒加载。
- 可维护：token 单一来源（themes + DESIGN.md）；新组件引用 token 不写裸色值。
- 可验证：每个里程碑有可跑命令（见 03-plan Verification）与 AC。

## Interaction Design（交互设计 · 必写节）

> 编写与自查用 `.agents/skills/ui-ux-design`（本任务实施前重读）；设计基准 = design_files 原型实测行为。

### 交互规格（点什么出现什么）— 总则级

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 背景面板（顶栏入口） | 点击背景按钮 | 弹出玻璃面板：12 纯色 / 6 图 / 遮罩滑杆 0–90% | 面板开/关 | 选择即预览并落盘 localStorage | 无（预设恒在） | 遮罩滑杆 0 与 90 端点可达 |
| Tab 切换 | 点击 TopBar Tab | 金线指示条移至该 Tab；内容区切换 | 激活/未激活 | 160ms 指示条过渡 | 无 | 动态 Tab 可关闭，首页 Tab 不可关 |
| StatusBar | 常驻 | 显示端口/版本/服务状态（mono 10px） | 恒显示 | — | — | 长文本省略，不撑破 22px |
| 助手气泡 | 点击悬浮气泡 | 展开助手输入框（消息流 + 发送） | 折叠/展开 | 展开 220ms 动效 | 无历史时显示引导语 | 关闭再开保留会话上下文 |
| 首页 Hero | 载入 | 「傍晚好，锻造师」按时段问候 + 副题 + 推荐动作 | 时段分态（早/午/晚） | 进入 160ms 淡入 | — | 时段判定边界：06–11 早 / 11–17 午 / 17–24 晚（与设计稿一致，实施时核对） |
| 核心看板 5 列 | 载入 | 5 列玻璃卡片（标题+数值+趋势） | 数据/加载/失败三态 | 加载骨架、失败重试 | 无数据时显示 0 值占位文案 | 列数不齐时其余列显示规范空位 |
| 待办/活动 | 载入 | 待办列表 + 「查看全部」跳 /todo；最近活动空态文案 | 有数据/空态 | 跳转后 Tab 新增 | 空态：引导语 + 下一步 | 长列表滚动，不破版 |
| 功能屏 PageHead | 载入 | 标题 + 副题 + 操作区 | — | — | — | 标题/副题溢出省略 |
| 查询/筛选 | 提交 | 结果区更新 + 计数；失败 toast | 加载/结果/空/失败 | loading + toast | 空：文案+引导 | 分页/筛选边界不重复不遗漏 |

### 交互验收标准（并入 Acceptance Criteria）

- 每个交互点 ≥1 条可测 AC，断言锚在真实服务入口返回（非仅界面回显）。
- 空态分级文案 + 下一步引导可测；禁用/不可用状态可见原因。
- 破坏性操作二次确认；状态变更点即保存落盘。
- 动效时长与 DESIGN.md 一致（160/220/360ms + 锤落回弹），可测（transition-duration 断言或截图时序）。

### 走查符合性（DoD 绑定）

- 每个里程碑走查按 `ui-ux-design`「走查 UI 符合性清单」逐项核对：CRAP 四原则、触发→结果与实现一致、点即保存、轮询无闪动、筛选/分页边界、长文本/窄屏不破版。
- e2e 覆盖交互路径（确认/取消、空态、边界）；纯视觉用截图读图对照设计基准（字号/间距/颜色 token）。

## Acceptance Criteria（总 AC，逐条可测）

- AC1 迁移：`ForgeSelf.Web` 下 `pnpm run check` 0 error、`pnpm run test` 全绿（记录数字）；涉及插件则 `Plugins/<X>/web && pnpm run build` 通过。
- AC2 壳层：TopBar 高度 48px、StatusBar 22px 且显示端口/版本；背景面板 12+6+遮罩可用、刷新后持久化（localStorage forgeself:bg）；助手气泡可展开/折叠。
- AC3 首页：Hero/常用功能/5 列看板/待办/活动与 HomeScreen.jsx 布局对齐（e2e 截图对照）；widget 契约五个 surface 生效。
- AC4 功能屏：抽查 ≥3 个宿主视图 + ≥2 个插件屏走查清单 0 命中；PageHead/卡片/空态统一。
- AC5 文档守卫：docs/README.md §一有 DESIGN.md 行；AGENTS.md §8 指向新真源；新建页面/查询规范章节存在；token 守卫基线重建后 0 新增漂移；DESIGN.md 待对齐清单全勾销。
- AC6 品牌：logo/纹理/语汇（锻造师/淬炼/铸）在壳层与首页落地；三禁红线走查 0 命中。
- AC7 发布（涉及插件/宿主发布时）：插件五步闭环齐备；宿主升级由 update-agent；运行实例只读复验通过。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 设计稿 StatusBar 显示 `:7788` vs 项目实际端口 7102/7002 | StatusBar 数值显示口径 | 实施 M2 时拍板：展示实际服务端口（推荐）或按设计稿示例值；默认推荐实际端口 |
| Hero 时段问候判定边界（早/午/晚） | 文案分态 | 实施 M3 时对照 HomeScreen.jsx 原文；默认 06–11/11–17/17–24 |
| 宿主屏「宿主内改版 vs 转插件」边界（Agent/Skills/MCP/Monitor/Settings 设计稿当作插件屏） | 迁移工作量与架构 | 默认：**宿主内改版**（不新增插件，保持现有路由/权限），仅 Home 保持插件；用户可拍板改 |
| 网络字体（Noto Serif SC / LXGW WenKai）引入方式 | 字体栈落地 | 默认系统字体栈回退 + 可选本地字体；不引入大规模依赖（规范 §1.4） |
| 熔炉背景图本地化图源 | 资产 404 修复 | 默认：从设计稿 bundle/免费图源补一张同意境图，本地化到 `ForgeSelf.Web/public/backgrounds/` |
| 首页 widget 化的宿主消费机制（现为整页 iframe 加载插件） | 架构改动面 | 实施 M3/M5 时评估：先保证视觉/布局对齐，widget 契约可后置（不阻塞 AC3 的布局对齐判据） |
