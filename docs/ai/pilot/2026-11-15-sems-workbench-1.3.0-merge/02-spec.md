# Specification

> 阶段：Stage 2 — Spec（从真实 Repository Understanding 与 Intent 推导）｜规范：`docs/04-standards/ai-native-engineering-workflow.md` §2
> 任务 ID：`2026-11-09-sems-workbench-1.3.0` ｜ 日期：2026-11-09
> 上游依据：`00-repository-understanding.md`、`01-intent.md`；`workbenchStore.ts` 的 `WbRoute` 枚举与 `Plugins/Sems/web/src/workbench/views/` 9 个屏组件（真实文件）；`Plugins/Sems/web/src` 7 个经典 `.vue` + `SpecPanel.vue`；4 份评审基线（D1–D5 / G1–G9 / P1–P6 / RK-01~RK-18 / 主题·T-01~T-10 / 研发·T-01~T-14 / 视觉优化 7 类 / B1·B2）；`plugin-development` §3.4 交互设计统一要求；`ui-ux-design` 技能；模板 `docs/18-templates/ai-pilot/02-spec.tpl.md`
> 规则：① 与实际项目一致；② 不发明不存在的接口/类/模块；③ 不确定点显式记为 `Unknown`，不自行假定。

---

## Functional Requirements

### FR-0 11 屏对齐清单（原型 → 真实实现）

> S02 / S11 非独立路由（10 条 `WbRoute` + 2 个态/菜单 = 11 屏），`/specs` 为第 6 个导航扩展项。「原型来源」列 = 静态 HTML 原型；「本期处置」列 = 以评审 D1–D5 / 视觉优化 / v0.2–v0.3 为准的收敛结果。**最终功能 ≥ HTML**（真实可用功能覆盖面），不是复制死链。

| 屏 | 路由 | 原型（HTML） | 真实数据源 / 组件 | 本期处置（必做） |
| --- | --- | --- | --- | --- |
| S01 项目列表 | `/projects` | 列表行 + 内联路径 + 行内操作 + 居中搜索 | `GET /api/projects`、`ManagerProjects.vue` | 保留列表行；**列表/卡片双模式切换**（演进序 2 硬需求）；收藏置顶、行内「打开/在编辑器打开/移除」；空态/加载/失败/搜索无结果四态 |
| S02 空状态 | 非独立路由 | 0 项目态预览不可达 | 同 S01 | 补空态（图标 + serif 引言 + 副说明 + 去登记引导），G3 四态之一 |
| S03 新建·目录浏览 | `/create` | 面包屑 + 目录格 + 「在此新建项目」 | `GET /api/projects/browse`、`ManagerBrowser.vue` | 保留目录浏览式登记（优于经典 `DirectoryPickerDialog` 弹窗）；**不创建任何文件**（登记语义） |
| S04 向导 | `/create/wizard` | 4 步：项目详情/预设/功能/配置 | `POST /api/projects`（`Root`/`Name`）+ `PUT /api/projects/{id}`（`ProjectUpdate`） | **D1：砍成 2 步「登记」向导**（① 选目录 ② 填名称/类型/标签/描述）；删包管理器/Git 初始化/预设/功能/覆盖目录；按钮「创建项目」→**「登记项目」**；确认页明示「不会在你的目录中创建或修改任何文件」；`type/tags` 二次写回失败可独立重试 |
| S05 导入·目录浏览 | `/import` | 与 S03 共用浏览器「导入此文件夹」 | `POST /api/projects` | 保留；与 S03 共用 `ManagerBrowser.vue` |
| S06 仪表盘 | `/dashboard` | 4 卡；含「端口占用 8080 / 结束占用进程」 | `GET /api/runs` + 档案字段、`ProjectDashboard.vue` | **删伪概念**，换 Sems 真实信号：运行中会话数 / 已登记命令数 / 可用工具数(22) / 规格覆盖率 / 最近一次运行结果；「结束占用进程」类破坏性操作移除或改二次确认 |
| S07 插件能力 | `/plugins` | 「已安装插件」+ 官方徽标 + 添加插件/安装开发工具/下载 | 22 个 `sems_*` 工具、`ProjectPluginsView.vue` | **D2：改名「Sems 工具（22）」**；条目=工具名 + 用途说明；**删「添加插件/安装开发工具/下载」假按钮**；屏首行加不可忽略语义说明条「非插件市场，不提供安装/下载」；可链「查看工具定义」/Agent 侧 |
| S08 依赖 | `/dependencies` | 主/开发依赖分组 + wanted/latest + 「安装依赖」 | 运行命令（name/script/updatedAt）、`ProjectDependenciesView.vue` | **D2：改名「运行命令」**；字段=命令名/脚本/最近运行/最近更新；**删「安装依赖」死按钮**；加说明条「非依赖解析，只做查看，不做安装/解析」；**新增命令默认只读**（RK-12 / 研发·T-09，安全债） |
| S09 配置 | `/configuration` | 仅 1 张「项目构建配置」折叠卡（空壳） | `ProjectEditDialog` 的 `ProjectUpdate` 字段 + localStorage 显示偏好、`ProjectConfigurationView.vue` | **D3：改「项目设置」**=项目档案（name/type/tags/description）+ 工作台显示偏好；删「项目构建配置/配置你的项目构建行为」措辞；空壳屏补齐 |
| S10 任务 | `/tasks` | 左命令清单 + 右详情 + 「运行任务」 | `POST /api/commands/{id}/run` + `GET .../logs?after=n`、`ProjectTasksView.vue` / `RunPanel.vue` | 保留左右分栏；**修 3 缺陷**：运行后增量轮询返回实时日志、运行中命令记录持续可见不突然消失、命令交互反馈（loading/成功/失败可见）；禁用态给可见原因 |
| S11 更多菜单 | 非独立路由 | 面包屑/项目切换/恢复示例数据/关于 | — | **D4：补「切换经典视图」入口**，与顶栏构成双入口；保留项目切换/恢复示例数据/关于（示例数据仅 DEMO） |
| +1 规格 | `/specs` | 5 张静态能力卡 | `SpecPanel.vue` + 9 个 spec 工具、`ProjectSpecsView.vue` | 完整挂载 `SpecPanel.vue`（只追加版本/关联/反查/验收覆盖），**换成真实规格列表 + 空态**；标注「Sems 扩展」；研发·T-10 建议归入「更多」或二级 Tab，本期至少补「扩展项」标注 |
| 全局 | — | 薄荷绿顶栏 + 深色状态栏 + 时间/INFO/DEMO 徽标 | 宿主 `workshop-forge.css` | **主题重做**（见 NFR）；预览专用超长横幅**不进真机**（X6/R4）；状态栏窄屏不重叠（X5） |

### F01–F06 六条主流程（G5 要求真机走通，本期以可测断言覆盖）

| 流程 | 步骤 | 真实入口 | 验收锚点 |
| --- | --- | --- | --- |
| F01 打开项目 | S01 点项目 → 进入项目内 `/dashboard` | `GET /api/projects` + `workbenchStore.currentProjectId` | 面包屑/项目名/侧栏 5+1 项出现 |
| F02 新建（登记） | `/create` 选目录 → `/create/wizard` 2 步填档案 → 「登记项目」 | `POST /api/projects` → `PUT /api/projects/{id}` | 列表新增该档案；**目录内无新增/修改文件**；按钮文案与确认文案正确 |
| F03 导入 | `/import` 目录浏览 → 「导入此文件夹」 | `POST /api/projects` | 列表新增；不创建文件 |
| F04 管理 | S01 搜索/收藏/行内打开/编辑/移除 | `PUT` / `DELETE /api/projects/{id}` | 搜索命中、收藏置顶、移除有二次确认且只删档案不动磁盘（存活会话 409） |
| F05 浏览 + 运行 | `/tasks` 选命令 → 「运行任务」 → 看实时日志 | `POST .../run` + `logs?after=n` 增量轮询 | 日志实时追加可见；运行中记录不消失；停止有反馈 |
| F06 返回管理器 / 切经典视图 | 面包屑返回 `/projects`；顶栏或侧栏「切换经典视图」 | `viewMode` + `localStorage` | 切走再切回筛选/展开/滚动位置保留（G4）；经典视图 DOM 完整 |

### FR-1 列表/卡片双模式
首页 `/projects` 支持列表行与卡片网格切换，偏好写 `localStorage`；两种模式均带搜索/收藏/行内操作，路径在列表行内联、卡片内可见。

### FR-2 经典视图保留与双入口
原 7 个 `.vue` + `SpecPanel.vue` 行为零改动；`viewMode` 默认工作台（D4）；「切换经典视图」在**顶栏 + 侧栏（或「更多」）双处可见**；切换不丢状态；`localStorage['sems-workbench-v1'].viewMode` 记忆。

### FR-3 不做假能力
逐屏核对每个按钮都有真实后端支撑（G7）；无支撑者删除或改为只读展示 + 语义说明条；DEMO 态禁用一切写操作并提供「重试连接」入口（RK-08 / 研发·T-08）；「后端不可达自动降级 DEMO」不得掩盖真实故障。

### FR-4 主题合规（对应 主题·T-01~T-10 / G9）
删除 `workbench.css` 全部自定义 token 定义层（`.wb{}` 内约 54 个 `--xxx:`）；颜色/字号/圆角/阴影/动效/玻璃/字体一律直接引用宿主 token；间距写字面量 px；明暗自动跟随 `html.dark`，插件不监听、不写两套 CSS；顶栏/状态栏表面明暗分别取 token；强调色 `--el-color-primary`；破坏性操作 `--cinnabar-400`；`INFO` 徽标 `--el-color-info`、DEMO 徽标 `--gold-400`；修 B1（墨黑底墨黑字）/ B2（`--sp-*` 塌陷）。

### FR-5 命令交互与实时日志（v0.2 点名缺陷）
命令运行触发后 `logs?after=n` 增量轮询并把新行追加到 `RunPanel`；运行中命令的「运行记录」在会话存活期间持续可见（不因状态刷新丢失）；停止/失败/成功均有可见反馈；禁用态给可见原因。（用户另说明「后台服务日志运行台没问题，不用担心」→ 不重写既有日志运行台，仅修上述 3 个交互缺陷。）

### FR-6 自动化资产与版本口径修正
`sems.spec.ts` 的 `SEMS_TOOLS` 由 13 补到 22（补 9 个 spec 工具名）并修 `toHaveLength`（P3 / RK-03 / 研发·T-03）；交付说明端点数 21 → **25**（7/6/3/9，P5 / RK-06 / 研发·T-05）；`plugin.json` / `web/package.json` / 文档三处 **1.3.0**（RK-11 / 研发·T-11，前两处已实测为 1.3.0，文档待同步）；宿主 e2e 增加视图前置切换或补工作台专用用例（P2 / RK-02 / 研发·T-02，验证环境仅静态适配留痕）。

### FR-7 预览与声明对齐
离线预览标注「基于交接包静态复刻、非重构后实况」或补齐 11 屏 / LIVE-DEMO 徽标 / 切换经典视图入口（P4 / 研发·T-04）；交接包入库或上传参考文档留痕（RK-07 / 研发·T-07）。

## Input

- 用户操作：路由导航、搜索输入、收藏、列表/卡片切换、经典视图切换、明暗切换、目录浏览选择、登记表单（`Root`/`Name`/`Type`/`Tags`/`Description`）、项目编辑/移除、命令选择/运行/停止、规格操作。
- 环境/存储：`localStorage['sems-workbench-v1']`（`viewMode`/显示偏好/主题偏好/收藏）；`?semsHash=1` 命名空间 hash（RK-13：宿主未正式启用，标 Unknown）。
- 后端只读契约：`GET /api/projects`、`/browse`、`/api/commands`、`/api/runs`、`/api/specs`、`GET .../logs?after=n`；写：`POST /api/projects`、`PUT /api/projects/{id}`、`DELETE /api/projects/{id}`、`POST /api/commands/{id}/run`、`POST .../stop`。**后端零改动**。

## Output

- 屏幕/状态：11 屏 + `/specs` 扩展项、四态（加载/空/出错可重试/搜索无结果）、明暗双模、双模式列表、经典视图。
- 数据：项目档案、运行命令、运行中会话与实时日志、22 工具清单、规格列表（经既有 API）。
- 工件：`docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/` 00–07；`Plugins/Sems/docs/ui-design-brief.md`（面向设计师的交互说明，规范 Stage 2 要求）；版本号/文档同步；`sems.spec.ts` 断言修正。

## Business Rules

- BR-1 **登记 ≠ 创建**：向导只登记已存在目录 + 二次写回 `type/tags/description`，不生成脚手架、不写磁盘文件；`type/tags` 写回失败可独立重试，不因登记成功跳过（设计研发评审 议题 3）。
- BR-2 **移除只删档案**：`Remove` 不动磁盘文件；有存活会话返回 409（A-13）。
- BR-3 **S08 新增命令默认只读**：不放大 `sems_add_command` 任意脚本执行暴露面（RK-12 / 研发·T-09）；该 P1 安全债单独立项（研发·T-12），不在本期修。
- BR-4 **默认工作台、经典可回退**：D4；切换不丢状态（G4）；DEMO 态禁写（RK-08）。
- BR-5 **主题自动跟随**：不监听 class、不写两套 CSS、不建自定义 token；破坏性操作统一朱砂色。
- BR-6 **22 工具 / 25 端点 / 9 spec 工具**口径为唯一事实，断言与文档以此为准。
- BR-7 **扩展项仅预留**：需求管理/开发规范/派发任务/门禁/宪法管理不实现；不引入定时任务、专家、模块注册/插槽契约（演进序 3/4）。

## Boundary Conditions

- 0 项目 / 搜索无结果 / 加载中 / 加载失败可重试：四态各自有专属 UI，不白屏、不假成功。
- 后端不可达 → DEMO 降级：禁一切写操作 + 显式「重试连接」；不得让用户误以为已登记（RK-08）。
- `?semsHash=1` 前进/后退行为：宿主未正式启用，标 `Unknown`，需宿主侧确认（RK-13）。
- 375px 窄屏：保底不横向溢出（视觉优化实测 scrollWidth=clientWidth=375）；状态栏 <768px 隐藏版本串/INFO 避免重叠（X5）；不追移动端精细适配（评审 2.3）。
- `.wb` 作用域：宿主全局样式（字体栈/圆角/z-index）不被插件重置对抗（G8 / 评审 2.3）。
- `web/dist` 缺失时插件前端 404 不崩（既有降级）；`style.css` 由 `pluginViewLoader` 自动注入，无需宿主改动（A-11）。
- 运行中 DLL / 宿主进程：本任务不改后端、不启停宿主；发布动作不执行。

## Error Handling

| 场景 | 处理 |
| --- | --- |
| 列表/规格/命令加载失败 | 失败态：错误文案 + 「重试」按钮；不显示半成品 |
| 登记 `POST` 成功但 `PUT` 写回失败 | 可独立重试写回，档案不丢（BR-1） |
| 移除项目有存活会话 | 409 + 明确原因，不静默 |
| 命令运行/日志轮询失败 | 运行面板可见错误 + 保留已有日志，不清空运行中记录 |
| 搜索无结果 | 专属空态（「没有匹配的工具/项目」+ 清空/换词引导） |
| 主题切换/偏好读取异常 | 回落宿主 `html.dark` 当前态，不崩 |
| 视图切换 `localStorage` 损坏 | 回落默认工作台，不白屏 |

## Compatibility

- **经典视图零改动**：7 个原 `.vue` + `SpecPanel.vue` 的 DOM 选择器（`.sems__title`/`.pcard__*`/`.clist__*`/`.run__*`/`.dpick__*`/`.cmddlg__*`）保持，`sems.spec.ts` 经典视图断言仍可回归（配合视图前置切换，P2）。
- **后端契约零改动**：`plugin.json` 的 `frontend.views/route/entry`、`Permissions`、`Dependencies` 不变，仅版本/Description 已为 1.3.0；22 工具 / 25 端点 / DTO 不变。
- **宿主零改动**：`pluginViewLoader` / `workshop-forge.css` / import map 不改；插件自行编译样式，`.wb` 前缀隔离。
- Production 宿主行为：本任务仅前端插件产物变化，宿主与其他插件无感。

## Non-functional Requirements

| # | 要求 | 判据 |
| --- | --- | --- |
| NFR-1 设计合规 | 零自定义 token，直接引用 `--el-*` / `--gold-*` / `--ink-*` / `--paper-*` / `--cinnabar-400` / `--bronze-500` / `--fg-*` / `--border-*` / `--font-*` / `--radius-*` / `--shadow-*` / `--dur-*` / `--ease-*` / `--glass-*`；间距写 px 字面量不建 `--sp-*` | `design:check` PASS；`Plugins/Sems/web/src` 的 `a` 保持 **0**；全文件 hex=0；规则 B/C/D=0；**禁 `--update` 抹平基线**（主题·T-10 / G9） |
| NFR-2 明暗自动跟随 | 用 `var(--el-*)` / `var(--gold-*)` 等，宿主 `html.dark` 切换自动生效；顶栏/状态栏表面明暗分别取 token | 11 屏 × 明/暗 = 22 张截图逐屏比对 |
| NFR-3 不做假能力 | 每个可见按钮有真实后端支撑或为只读展示 + 语义说明条 | G7 逐屏核对无「安装依赖/添加插件/初始化 Git」残留 |
| NFR-4 保留经典视图 | 双入口可切、不丢状态、DOM 不变 | G4 切走切回实测 |
| NFR-5 最终功能 ≥ HTML | 真实可用功能覆盖原型 11 屏，且补双模式/四态/实时日志 | 11 屏清单 + F01–F06 覆盖 |
| NFR-6 不过度设计 | 无模块注册/插槽契约/定时任务/专家；加功能直接改；「能实现功能即可」 | 代码评审无新增架构层 |
| NFR-7 可访问性 | 焦点环 2px 可见、按钮有 `aria-label`、`role=tablist/tab/tabpanel`、侧栏 `aria-current` | 视觉优化实测 + X10 补齐 |
| NFR-8 零新依赖 | 不新增 npm/NuGet 包 | `package.json` 依赖不变 |
| NFR-9 对比度 | 强调色用 `--el-color-primary`（浅色 4.70:1，非 `--gold-400` 的 2.01:1） | 视觉优化 §3 实测；R5 建议 axe/Lighthouse 全量（标 Unknown 若未跑） |

## Interaction Design（交互设计 · 必写节）

> 依据：规范 §2 Stage 2（2026-10-08 用户立「涉 UI 必写」+ 2026-10-30 用户立「所有插件必写」）；`plugin-development` §3.4；`ui-ux-design` 技能（CRAP 四原则 + 配色 + 字体）。缺本节 = 闸门1 不通过。有页面的插件另需维护 `Plugins/Sems/docs/ui-design-brief.md`（面向不读代码的设计师：功能、旅程、界面交互行为）——列为 Implement 交付物。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 列表/卡片切换 | 点 S01 视图切换按钮 | 同一批项目在列表行 ↔ 卡片网格间切换，偏好落 localStorage | `viewMode: list/card` | 按钮激活态 + 即时重排 | 0 项目仍显示切换按钮 + 空态 | 偏好损坏回落 list |
| 全局搜索 | S01 顶栏输入框键入 | 实时过滤项目；无命中显示专属空态 | 有结果/无结果/加载中 | 输入即时反馈 | 「没有匹配的项目」+ 清空引导 | 长路径省略号截断 |
| 收藏置顶 | 点项目星标 | 该项目移到列表/卡片顶部并持久化 | 已收藏/未收藏 | 星标填充 + 重排 | — | 收藏与筛选叠加 |
| 经典视图双入口 | 点顶栏 或 侧栏「切换经典视图」 | 切到 `LegacySemsView`；URL/标题可区分 | `viewMode: workbench/classic` | 顶栏入口反显「切换工作台」 | — | 切走再切回保留筛选/展开/滚动（G4） |
| 明暗切换 | 点状态栏月亮图标 | `html.dark` 切换，全站 token 自动跟随，偏好持久化 | light/dark | 图标变化 + 即时换肤 | — | 不监听 class、不写两套 CSS |
| 目录浏览选目录 | S03/S05 点目录格/面包屑 | 进入子目录，底部「在此新建/导入」可用 | 可进入/不可进入（无权限） | 面包屑高亮 | 空目录提示 | 路径穿越由后端 `/browse` 兜底 |
| 2 步登记向导 | 第 1 步选目录 → 第 2 步填档案 → 点「登记项目」 | `POST` + `PUT` → 回 S01 且列表新增；确认页明示「不会创建或修改任何文件」 | step1/step2/submitting/success/写回失败可重试 | toast + loading + 写回失败独立重试 | — | 未选目录禁用下一步且可见原因 |
| 项目移除 | 行内「移除」 | `ElMessageBox.confirm` → 确认后 `DELETE` → 列表移除；不动磁盘 | 可移除/有存活会话(409) | confirm + toast；409 给原因 | — | 破坏性操作二次确认 + 朱砂色 |
| 运行命令 | `/tasks` 点「运行任务」 | `POST run` → 右侧 `RunPanel` 经 `logs?after=n` 增量追加实时日志；运行中记录持续可见 | idle/running/success/failed | loading + 日志流 + 终态徽标 | 无命令时空态引导 | 禁用态给可见原因；停止二次确认 |
| 停止命令 | 点「停止」 | 停止会话，记录转为终态且**不消失** | running/stopped | toast + 终态 | — | 不误删运行记录 |
| S07/S08 只读 | 点「查看工具定义」/命令项 | 展示定义/详情，不触发安装 | 只读 | 说明条常驻 | 无工具/无命令空态 | 无任何「安装/下载/解析」按钮 |
| 项目设置 | S09 编辑字段 → 保存 | `PUT` 写回 `name/type/tags/description` + 显示偏好 | editing/saving/saved | 点即保存 + toast | — | 失败可重试 |

### 视觉规格（目标态）

- **对齐线与几何**：顶栏由原型 84px 竖排居中改为 **56px 单行左对齐**（品牌行 + 导航行分层，视觉优化 §4）；列表行 = 1px 分隔线 + hover 金染；卡片 = 1px 边框 + 极低阴影；仪表盘 = 4 磁贴 + 1.5:1 双栏。几何断言进 e2e（`getComputedStyle`/rect）。
- **主题矩阵**（逐表面 × light/dark，全部取宿主 token，零硬编码）：

| 表面 | light | dark |
| --- | --- | --- |
| 页面 canvas | `var(--el-bg-color-page)` | `var(--ink-950)` |
| 卡片/顶栏/侧栏 | `var(--el-bg-color)` + `var(--el-border-color)` 1px | `var(--el-bg-color)` + 玻璃质感 |
| 主文字 | `var(--fg-primary)` | `var(--fg-primary)` |
| 次要文字 | `var(--el-text-color-secondary)` | `var(--fg-secondary)` |
| 强调/CTA | `var(--el-color-primary)`（#B45309） | `var(--el-color-primary)`（#F59E0B） |
| 破坏性 | `var(--cinnabar-400)` | `var(--cinnabar-400)` |
| 成功/状态点 | `var(--bronze-500)` | `var(--bronze-500)` |
| 状态栏 | `var(--el-bg-color-overlay)` + 上边框 | `var(--ink-950)` + `var(--fg-secondary)` 字 |
| 玻璃卡 | `var(--glass-*)`（blur 18–24px，不调高） | 同左 |

- **状态 × 主题**：四态（加载骨架/空/出错可重试/搜索无结果）在明暗两态均需可读；B1（墨黑底墨黑字）与 B2（`--sp-*` 塌陷）不得复发；DEMO 徽标用 `--gold-400`、INFO 用 `--el-color-info`，全站无紫色（规则 D）。
- **字阶**：eyebrow mono 10px/.18em → 标题 `--font-display` 20px(屏内)/26px(页头) → 正文 14px → 辅助 12–13px → 数据 mono；圆角 `--radius-md`，禁 999px 胶囊。
- **来源教义**：`DESIGN.md`（v2-target）组件规格、`workshop-forge.css` 色板、《视觉优化评审意见》§七照抄清单、《主题口径变更评审意见》§二落地做法。

### 交互验收标准（并入 AC，逐条可测）

- 每个交互点至少一条 AC：断言「触发后页面出现什么」（toast/弹窗/徽标/跳转/日志追加），断言锚在真实服务入口返回（非仅界面回显）。
- 空态分级：每种空态文案 + 下一步引导可测。
- 禁用/不可用状态必须可见原因（不无声禁用）。
- 破坏性/状态变更操作二次确认（`ElMessageBox.confirm` + 可单测编排函数 `confirmOps.ts`）。
- 运行日志断言「增量追加」而非「静态回显」；运行中记录断言「存活期间不消失」。

### 走查符合性（DoD 绑定）

- 走查（`plugin-development` §四 第 ③ 步）按 `ui-ux-design`「走查 UI 符合性清单」**全项逐条核对并留痕**：交互规格「触发→结果」与实现一致、CRAP 层级/重复/对齐/亲密性、点即保存落盘、轮询无闪动、筛选/分页边界、长文本/窄屏不破版；对比度亮/暗两态实测进 05-evidence。
- e2e 覆盖交互路径（确认取消两条路、空态、边界、视图切换不丢状态）；纯视觉用截图读图对照本节目标值。
- ⚠️ 验证环境无 `pwsh` / .NET 10 → `dev-stack.ps1` 起源码前后端走查与宿主 e2e **无法完整执行**，走查符合性以 Chromium 对离线预览/构建产物的渲染冒烟 + 静态核对留痕，完整走查标 `Unknown / 环境受限`（见 04-task / 05-evidence）。

## Acceptance Criteria（闸门1 确认的清单）

**门禁 G1–G9（需求与 UIUX 评审 §3.4 + 主题评审 §六）**

- [ ] AC-G1 代码入库（当前落点已在完整仓库内）+ `docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/` 00–07 工件齐全。
- [ ] AC-G2 插件前端 `pnpm run build` + `pnpm run test` 通过（合入后重跑）；`dotnet test` 标 `Unknown / 环境受限`。
- [ ] AC-G3 11 屏 × 4 态（空/加载中/加载失败可重试/搜索无结果）截图；主题评审 §六**加强为 × 明/暗 = 44 张**（验证环境以 Chromium 渲染冒烟覆盖，完整截图标受限）。
- [ ] AC-G4 经典视图 ↔ 工作台切换不丢状态（筛选/展开/滚动位置）。
- [ ] AC-G5 F01–F06 六条流程走通（验证环境静态/构建产物冒烟 + 留痕）。
- [ ] AC-G6 宿主 e2e 适配（视图前置切换/工作台用例）+ 工具数断言 13→22（静态适配留痕；完整执行 `Unknown / 环境受限`）。
- [ ] AC-G7 真机逐屏核对无「假能力按钮」残留。
- [ ] AC-G8 `.wb` 作用域验证，宿主其他页面样式未被污染。
- [ ] AC-G9 明暗双模 11 屏截图 + `pnpm run design:check` PASS 且 `Plugins/Sems/web/src` 的 `a` 保持 **0**；**禁 `--update` 抹平基线**。

**决策 D1–D5 / 阻断 P1–P6 / 主题·T-01~T-10**

- [ ] AC-D1 向导 2 步「登记」、按钮「登记项目」、确认页明示不创建文件、删 4 个假区域。
- [ ] AC-D2 S07「Sems 工具（22）」/ S08「运行命令」改名 + 删假按钮 + 屏首语义说明条。
- [ ] AC-D3 S09「项目设置」= 项目档案 + 显示偏好，删「项目构建配置」措辞。
- [ ] AC-D4 默认工作台 + 顶栏/侧栏双入口切经典视图 + 不丢状态。
- [ ] AC-D5 宿主顶栏归属/高度：**标 `Unknown`（需宿主确认）**，不自行假定；顶栏改 56px 单行、不进超长预览横幅。
- [ ] AC-P1 代码入库 + pilot 00–07；AC-P2 宿主 e2e 适配；AC-P3 工具数 13→22；AC-P4 预览与声明对齐/标注；AC-P5 端点数 21→25；AC-P6 `plugin-development` 六步门禁全流程——**④⑤ 及后端跑标 `Unknown / 环境受限`**，其余留痕。
- [ ] AC-T01~T10 主题化十项（删自定义 token 层 / 颜色走宿主刻度 / 无 `--fs-*`/`--sp-*` / 器物切角禁胶囊 / 清紫色 / 顶栏状态栏明暗分别取 token / 破坏性朱砂 / INFO·DEMO 徽标换色 / 明暗双模 22 张 / `design:check` PASS 且 `a=0` 禁 `--update`）。

**视觉优化 / 缺陷 / 版本**

- [ ] AC-B B1（状态栏可读）、B2（间距不塌陷）不复发；删 3 个死按钮；端口占用换真实信号；侧栏补「切换经典视图」；明暗切换可用。
- [ ] AC-F 3 个交互缺陷有可测修复（实时日志增量、运行中记录不消失、命令交互反馈）。
- [ ] AC-V `plugin.json` / `web/package.json` / 文档三处 1.3.0；`Plugins/Sems/docs/ui-design-brief.md` 维护。
- [ ] AC-N 本期不做项零实现（见下）；零新依赖；定时任务/专家/模块注册/插槽契约零引入。

## 本期不做（明确范围边界）

> 演进序 2 列「后续要扩展」、序 3/4 反复强调「仅设计预留」「还有没做的功能可先不实现」。以下**仅预留 IA/状态展示位，不实现任何后端/页面逻辑**：

| 扩展项 | 本期处置 |
| --- | --- |
| 需求管理 | 仅预留导航/首页状态展示位，不建实体/API/页面 |
| 开发规范 | 同上 |
| 派发任务 | 同上；且演进序 4 明确「撤掉之前的定时任务、不需要专家」 |
| 门禁（产品化门禁管理） | 同上；注意与流程 G1–G9 门禁（验收检查项）区分，后者是验收动作非功能 |
| 宪法管理 | 同上 |
| 真插件市场 / 依赖解析 / 脚手架生成 | 明确不做（评审 议题 1/3）：若要做得另立需求、涉及后端新增实体与 API |
| 模块设计 / 模块注册 / 插槽契约 | 明确不做（演进序 3 强调不要过度设计）；加功能直接改、新模块状态直接在首页改展示 |
| 定时任务 / 专家 / 后台服务日志运行台重写 | 不做（演进序 4 明确撤销定时任务、日志运行台无需重做） |
| 移动端精细适配 | 不做，保底不溢出即可（评审 2.3） |
| P1 安全债 `sems_add_command` 任意脚本 | 不在本期修，单独立项（研发·T-12）；本期仅 S08 新增命令默认只读 |
| 发布 / git 提交 / 打 tag / 运行实例复验 | 不执行（用户授权范围 + 无 `.git` + `AGENTS.md` §8.5 须请示） |

## Unknown（不确定点，不自行假定）

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| D5 宿主顶栏是否保留独立 84px / 是否与宿主全局导航合并 | 双顶栏、宿主页面被挤压 | 标 `Unknown`，需宿主前端确认；本期顶栏改 56px 单行、不进预览横幅，不自行合并宿主导航 |
| `?semsHash=1` 命名空间 hash 是否在宿主启用 | 浏览器前进/后退行为 | RK-13，标 `Unknown`，需宿主实测记录 |
| 浅色 canvas 与 surface 色差偏近（R2） | 卡片边界发虚 | 优先 1px 边框；若需对 canvas 用 `--el-fill-color` 须宿主确认，标 `Unknown` |
| 全量对比度/色盲模拟（axe / Lighthouse，R5） | 无障碍完备性 | 验证环境未跑则标 `Unknown / 环境受限`；已验强调色 4.70:1（视觉优化 §3） |
| 「规格」是否归入「更多」/二级 Tab（研发·T-10 / RK-10） | IA 外扩、回归基线 | 本期至少标「Sems 扩展」；最终层级待产品确认，标 `Unknown` |
| 宿主 e2e 新增「视图前置切换」的具体选择器 | P2 落地形态 | 验证环境仅静态适配与留痕，完整 e2e 标 `Unknown / 环境受限` |
| 后端 `dotnet build/test`、`dev-stack` 走查、发布链 | Verify 完整性 | 环境缺 .NET 10 / pwsh / .git，一律 `Unknown / 环境受限`，Evidence 禁写 PASS |
| 本次 3 个交互缺陷的同类 PC 归档 | 问题类闭环 | 台账存在，Implement/Test 阶段按 `problem-class-closure` 补齐，当前无已命中 PC id，不预填 |

## Failure Modes（规范 v1.2.0 · 缺陷类增补节）

> 由v0.2 点名 3 个缺陷 + 评审 B1/B2 两个自引入 bug 逐条预填，确保「设计时漏考虑的情况」被显式覆盖。

| 问题（来源） | 本任务如何满足 / 为何不适用 |
| --- | --- |
| 命令交互差（v0.2） | 交互规格表「运行命令/停止命令」行：loading + 终态徽标 + 禁用态可见原因；AC-F 可测 |
| 实际使用不返回实时日志（v0.2） | FR-5 `logs?after=n` 增量轮询追加到 `RunPanel`；AC 断言「增量追加」非「静态回显」 |
| 运行中命令运行记录突然不见（v0.2） | FR-5 会话存活期间运行记录持续可见、停止后转终态不消失；AC 断言「存活期间不消失」 |
| B1 状态栏墨黑底墨黑字（视觉优化自引入） | NFR-2 主题矩阵：状态栏明暗分别取 `--el-bg-color-overlay` / `--ink-950` + `--fg-secondary` 字，明暗双模截图验收 |
| B2 `--sp-*` 间距塌陷（视觉优化自引入） | NFR-1：间距写 px 字面量，不建 `--sp-*`；`workbench.css` 删自定义 token 层后逐个映射 |
| 假能力死链（RK-18） | FR-3 + G7 逐屏核对；每个按钮有真实后端支撑或只读展示 + 语义说明条 |
| DEMO 降级掩盖后端故障（RK-08） | Boundary / BR-4：DEMO 禁写 + 「重试连接」入口 |
| 向导「登记/创建」语义混淆（评审 1.4） | BR-1 + D1：2 步登记、按钮「登记项目」、确认页明示不创建文件 |
| 宿主经典视图 e2e 必红（RK-02） | FR-6 / P2：视图前置切换或补工作台用例；经典视图 DOM 零改动 |
| 工具数/端点数/版本号口径失配（RK-03/06/11） | FR-6 统一为 22 / 25 / 1.3.0 并加守卫对照 |
| 主题从合规退回不合规（主题评审 §1.4） | NFR-1 + G9：删 `.wb{}` 54 个自定义 token，`a` 保持 0，禁 `--update` |
