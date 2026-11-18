# Sems 工作台重构 · 仓库维护说明

> 版本：1.3.0 · 日期：2026-11-08 · 范围：`Plugins/Sems/web/` 前端信息架构重构
> 上游依据：交接包 `HANDOFF.md` / `prd.md` / `recon.md` / `architecture.md` / `features.csv`

## 1. 边界（读代码前先确认）

- **后端未改动**：`Controllers/`、`Services/`、`SemsPlugin.cs`、`SpecTools.cs`、`ToolExtensions.cs`、`Sems.csproj` 与 `ForgeSelf.Abstractions/SemsShared.cs` 全部保持原样。22 个 `sems_*` 工具的注册顺序、`toolPrefix`、权限、端点、DTO 均不变。
- **原界面未删除**：`web/src/SemsView.vue` 重构前的完整内容复制为 `LegacySemsView.vue`；`SemsView.vue` 现在是「工作台 / 经典视图」双模式入口，切换状态存 `localStorage['sems-workbench-v1'].viewMode`。
- **宿主契约不变**：`plugin.json` 的 `Id`、`EntryAssembly`、`EntryType`、`frontend.views = ["SemsView"]`、`frontend.entry = "web/dist/index.js"` 保持原值；`vite.config.ts` 的 lib / external 配置不变。

## 2. 新增代码结构

| 路径 | 职责 |
| --- | --- |
| `web/src/workbench/workbenchStore.ts` | 唯一 `data` 层：`loadProjects / listProjects / openProject / setFavorite / renameProject / removeProject / registerProject / browse / runTask / refreshRunningCount`；派生 `projectPlugins / projectDeps / projectTasks / projectConfigs`；LIVE/DEMO 模式与 localStorage 元数据 |
| `web/src/workbench/WorkbenchView.vue` | 应用壳：顶栏三态 Tab、四步步骤条、项目侧栏（5+1）、状态栏、Toast、路由分发 |
| `web/src/workbench/views/*.vue` | S01–S11 各屏；`ProjectSpecsView.vue` 是保留规格能力的扩展屏 |
| `web/src/workbench/workbench.css` | 交接包 tokens + 布局，全部选择器已加 `.wb` 作用域前缀；`statusbar` 改 `sticky`、toast 改 `absolute`，避免污染宿主 |
| `web/src/workbench/icons.ts` | 交接包自绘内联 SVG 精灵（29 个 symbol），clean-room 无外部资源 |

页面只调 `workbenchStore` 的函数，不直接 fetch、不直接改数组 —— 与交接包 `architecture.md` 的 `data` 层约束一致。

## 3. 数据映射（避免误读）

| 工作台概念 | 实际来源 |
| --- | --- |
| 项目 | `GET /api/projects` 的 `ProjectInfo`（`root` → `path`，`lastActiveAt` → `openDate`） |
| 收藏 / 向导选项 / 配置开关 | localStorage，不写入后端 DTO |
| 重命名 | `PUT /api/projects/{id}`（`{name}`），失败会 Toast 且不改变视图值 |
| 移除 | `DELETE /api/projects/{id}`；后端在有存活会话时返回 409，前端显示原因 |
| 新建 / 导入 | `POST /api/projects`（登记已存在目录），随后 `PUT` 写回 `type` / `tags` |
| 目录树 | `GET /api/projects/browse`，`DEMO` 模式回落内置目录 |
| 插件屏 | `SemsPlugin.cs` 注册的 22 个 `sems_*` 工具（常量在 `workbenchStore.ts`，改注册时须同步） |
| 依赖屏 | 项目运行命令：`wanted = script`、`latest = updatedAt`，按名称分主/辅助 |
| 任务运行 | `POST /api/commands/{id}/run` + `GET /api/commands/{id}/logs?after=n` 增量轮询 |
| 运行会话统计 | `GET /api/runs` |

> 不要把这些映射当成后端实体：`dependencies` / `plugins` 在 sems 中并不存在，代码与页面上均已标注，禁止据此新增「安装依赖」等假接口。

## 4. 构建与验证

```bash
cd Plugins/Sems/web
pnpm install
pnpm run build     # dist/index.js 175.69 kB + dist/style.css 50.69 kB（54 modules）
pnpm run test      # 32 passed（confirmOps 8 / runDuration 2 / specOps 13 / taskState 9）
```

> `dist/style.css` 含工作台全部样式（vite lib 模式 `cssCodeSplit: false`）。若宿主加载器只取 `dist/index.js` 而不加载同名 CSS，需在插件加载流程里显式引入 `dist/style.css`，否则工作台会失去布局。

本轮（2026-11-09）在完整仓库 `OpenForgeSelf/OpenForgeSelf` 内重跑的门禁全绿：`check-design-compliance.mjs` `a=209 b=0 c=0 d=0`（与基线同值）PASS，`check-style-tokens.mjs` `1759 / 2501`（较基线减少 742 处）PASS，**未使用 `--update`**。Chromium 明暗双模逐屏验证产出 22 张截图、问题数 0，详见 `docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/05-evidence.md`。

宿主侧仍需按仓库流程验证：激活 1.3.0 后跑 `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`（经典视图应保持通过），并跑 `dotnet test` 确认后端契约无回归。验证环境 `dotnet 6.0.301` 与 `net10.0-windows` 不匹配、无 `pwsh`、无宿主实例，这几项标 `Unknown / 环境受限`，**不得声称通过**。**本次交付未执行发布、部署与 GitHub 推送**（`AGENTS.md §8.5` 必须请示项，用户未授权；工作区亦为无 `.git` 的源码树）。

---

## 4.1 v0.2 口径改造：任务屏重做 + 首页双模式（2026-11-08）

依据 `PLATFORM-VISION.md` v0.2：**HTML 原型为唯一功能基线，重复的以 HTML 为准；加功能直接改，不引入注册表/插槽抽象。**

### 改动文件

| 文件 | 改动 |
| --- | --- |
| `web/src/workbench/workbenchStore.ts` | 任务日志按 `commandId` 分桶（`logsByCommand`）+ 状态机（`taskStatus`）+ 本地运行历史（`runHistory`）；新增 `taskLogsOf / taskStatusOf / taskHistoryOf / pullTaskLogs / selectTask / stopTask / startAllTasks`；`runTask` 重写；`projectTasks` 状态改由状态机派生；新增 `meta.projectLayout` + `setProjectLayout` |
| `web/src/workbench/views/ProjectTasksView.vue` | 整体重写：左 280px 任务列表 + 右栏常驻日志区，选中即拉、实时追加、自动滚到底、状态机与起止时间 |
| `web/src/RunPanel.vue` | `runs.value = r?.runs ?? []` 改为 `mergeSessions()` 合并本地快照；日志轮询不再只拉展开项；404 给一次明确提示；已结束会话保留显示 |
| `web/src/types.ts` | `RunSession` 增加可选 `ended` 标记 |
| `web/src/workbench/views/ManagerProjects.vue` | 工具栏加列表/卡片切换；卡片模式新增 `project-cards` 网格 |
| `web/src/workbench/workbench.css` | 新增 `.log-box / .log-line / .task-detail-head / .task-script / .project-cards / .project-card* / .layout-toggle` 样式 |
| `web/src/workbench/taskState.spec.ts` | **新增**：状态机、日志分桶隔离、运行历史持久化、任务派生 4 条用例 |

### 三个缺陷的修法

| 缺陷 | 根因 | 修法 |
| --- | --- | --- |
| 日志不实时 | `pullAllOpenLogs()` 只遍历展开项；404 被静默 catch | 任务屏常驻日志区，`selectTask()` 选中即拉；运行中 1.2s 增量轮询追加并自动滚到底；`RunPanel` 拉全部在跑会话的日志；404 给一次明确提示 |
| 运行记录突然消失 | `runs.value = r?.runs ?? []` 整体替换；后端会话仅存内存 | `runHistory` / `logsByCommand` 落 localStorage，状态从本地历史恢复 —— **刷新页面后状态、日志、起止时间仍在**；`RunPanel` 合并历史快照，会话结束后标「已结束」保留显示 |
| 命令交互差 | 无状态机，日志靠逐条展开 | 任务状态机 空闲/运行中/已完成 + 旋转指示 + 完成徽标；运行/停止/启动全部就近放置；日志按 stderr/失败/成功着色 |

> **边界**：后端会话与日志落盘（跨宿主重启保留）属 C# 改动，需 .NET 宿主验证，本期由前端 localStorage 兜底，已另立事项 `rbSfq4`。

### 验证

- `pnpm run build` 通过；`pnpm run test` 27/27 通过
- Chromium 无头冒烟：卡片/列表切换、任务屏 4 项、选中即拉取、运行→运行中→已完成、切换任务日志不丢、**刷新页面后状态/日志/起止时间全部保留**

---

## 4.2 v0.3 口径收尾：11 屏对齐核查 + 放大项 + 协作机制简化（2026-11-09）

依据 `PLATFORM-VISION.md` v0.3：**对齐 HTML 原型且功能大于之**、**不做复杂设计**、**撤销定时任务与专家机制**。

### 改动文件

| 文件 | 改动 |
| --- | --- |
| `web/src/workbench/workbenchStore.ts` | 新增 `projectLayout` / `setProjectLayout`（列表·卡片模式偏好）；新增 `projectPorts()`（从命令 url 解析端口，派生运行中/空闲）；新增 `listProjects()`（搜索 + 类型筛选 + 排序：最近活跃/名称/命令数，收藏置顶）、`projectTypes()`、`projectLastRun()`；store 新增 `typeFilter` / `projectSort` |
| `web/src/workbench/views/ManagerProjects.vue` | 工具栏加列表/卡片切换、类型筛选 chip 条、排序下拉；列表与卡片均显示「最近任务状态」 |
| `web/src/workbench/views/ProjectDashboard.vue` | 端口信息来自 `projectPorts()`（解析命令 URL 端口 + 运行/空闲）；v1.3.0 评审整改后改称**「命令端口（只读）」**，明示「端口由命令访问地址解析；Sems 不提供进程管理」，删除原型「结束占用进程」伪操作 |
| `web/src/workbench/workbench.css` | 新增 `.wb-filter-bar / .wb-chips / .chip / .wb-select / .port-row` 等样式 |
| `web/src/workbench/taskState.spec.ts` | 扩充至 9 条：新增收藏置顶 + 最近活跃排序、类型筛选、名称排序、`projectTypes`、端口解析与运行状态 |

### HTML 原型 11 屏对齐核查

用无头 Chromium 逐屏 audit（项目/新建/导入 + 6 个项目内屏）：

| 屏 | 对齐 | 超出（sems 真实能力） |
| --- | --- | --- |
| 项目列表 | ✓ | 类型筛选、排序、最近任务状态 |
| 新建项目 | ✓ | 路径校验与可达性提示 |
| 新建向导 | ✓ | 校验前置 |
| 导入项目 | ✓ | 扫描结果复用真实目录探测 |
| 项目仪表盘 | ✓ | **命令端口（只读）**（从命令 URL 真实解析；不承诺进程管理） |
| 插件 / 依赖 | ✓ | 保留 sems 原语义 |
| 配置 | ✓ | 命令维护与排序 |
| 任务屏 | ✓ | 停止会话、启动全部、本机进程检测 |
| 规格 | ✓ | 复用原有规格能力 |

结论：**11 屏全部对齐**，唯一缺口（仪表盘「端口占用」）已补齐；超出共 8 项；未引入任何新抽象。

### 协作机制变更（v0.3 修订）

| 项 | v0.2 | v0.3 |
| --- | --- | --- |
| 定时任务 | 调度器 + 7 条专家单次任务 | **全部删除**，`automation_list total: 0` |
| 专家机制 | 召唤平台专家执行 | **取消**，改为 AI 直接实现 |
| 落地流程 | 自动化派发 → 专家执行 | AI 直接实现 → 自验证 → 回写文档与事项 → **人工评审确认** |

### 验证

> `175.33 kB / 50.32 kB` 是本地功能扩展工作区 历史实测，非本仓库数据；合入完整仓库后已重跑：

- `pnpm run build` 通过（`dist/index.js` **175.69 kB** / `style.css` **50.69 kB**，54 modules）
- `pnpm run test` **32/32 通过**
- 设计守卫与硬编码色值棘轮均 PASS（未 `--update`）
- Chromium 无头逐屏：11 屏 × 明暗双模 + 375px = 22 张截图，逐屏内容 **10/10 互异**，假能力词 `[]`、胶囊 0、横向溢出 0、`PAGEERROR 0`；明暗强调色 `#B45309` / `#F59E0B`；自定义 `--sp-2` 已不存在
- 历史功能冒烟结论仍成立：chips `['全部 3','frontend 2','tool 1']`、筛选 2/恢复 3、两种排序正确、卡片 3 张；**仪表盘端口行已按评审降级为只读语义，不再写「结束占用进程」**

---

## 4.3 评审整改 + AI-Native 全量闭环（2026-11-09，完整仓库）

本任务已获开工授权并按项目内流程规范执行，在完整仓库内按 `AGENTS.md §11` 九阶段闭环推进，工件落在 `docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/`（00–07 齐备）。

### 整改动作

| 类别 | 动作 |
| --- | --- |
| 主题口径 | 删除 `workbench.css` 的 `.wb{}` 原型 54 个自定义 token（薄荷绿 / 紫色 / `--sp-*` / `--fs-*` 等），颜色直接引用宿主 `--el-*` / `--gold-*` / `--ink-*`，间距字号写字面量 px，强调色统一 `--el-color-primary`，明暗由 `html.dark` 自动跟随；圆角改 `--radius-sm/--radius-md`，禁用 `999px` 胶囊 |
| 假能力 UI | 向导改「登记项目到 Sems」+ 不可忽略说明条（不创建/修改文件、不执行 npm/git）；「依赖」屏改「运行命令」并删「安装依赖」；「插件」屏改「Sems 工具（N / 22）」并删添加/下载/升级；「配置」屏改「项目设置」并删「保存配置」；仪表盘「端口占用 / 结束占用进程」改「命令端口（只读）」 |
| 双入口 | 顶栏 `wb-legacy-top` + 侧栏 `wb-legacy-side`；`SemsView` 模式条另有「经典视图」按钮 |
| 顶栏挤压修复 | `.wb .topbar .corner button{width:26px}` 无差别覆盖新增宽按钮，导致「经典视图」竖排文字；改 `button:not(.wb-legacy-top)`，`.wb-legacy-top` 补 `width:auto;flex:none;white-space:nowrap`；≤640px 缩为图标钮并给 h1 加 `max-width` 防重叠 |
| e2e 适配 | `SEMS_TOOLS` 13→22（补 9 个 spec 工具名）；新增 `injectSemsClassicView(page)` 在页面脚本执行前预置 `sems-workbench-v1.viewMode='legacy'`，覆盖 3 处 `goto` 与 `reload`；补模式条双入口断言；注释/标题口径改 22 |
| 口径统一 | 端点数 21→25（实测 7/6/3/9）；`plugin.json` / `web/package.json` 均 1.3.0；`PLUGIN_VERSION` 动态读取清单 |

### 验证与一次必须披露的缺陷

初版 Chromium 逐屏脚本用 `location.hash='/dashboard'`，但 `workbenchStore.ts` 默认内存路由、只有 URL 带 `?semsHash=1` 才启用 `#sems<route>` 命名空间 hash。**旧脚本 10 张截图实为同一首页，报告却显示 10 屏全绿——属于假验证**。已修正为 `?semsHash=1` + `#sems<route>`，并加入 hash 值校验、排除状态栏实时时钟后的主内容 SHA1 互异、h2 标题命中三重断言，非 `/api/*` 404 与 `PAGEERROR` 一律判失败。

修正后实测：明暗两态逐屏互异 **10/10**、22 张截图、问题数 0；`--el-color-primary` light `#B45309` / dark `#F59E0B`；状态栏可读、自定义 token 不存在、胶囊 0、假能力词 0、375px 无横向溢出。工具名三方一致守卫 `check-tool-parity.mjs` = 22/22/22（e2e 数组 / 后端 22 个 `base(pluginId,"sems_*")` / `SemsPlugin.cs` 22 次 `Add`）。

完整证据与未执行项见 `05-evidence.md`，逐项验收结论见 `06-review.md`，最终汇报见 `07-final-report.md`。

### 视觉优化 §4 整套版式重做（已落地）

视觉优化评审 §4 的整套版式重做（顶栏 56px 单行左对齐、mono/serif 字阶、4 磁贴 + 1.5:1 双栏、列表三段式等）**已在本次交付中落地**：顶栏 56px 单行、项目列表三段式、仪表盘 4 磁贴 + 双栏、卡片/列表/空态精修全部完成；22 张明暗双模截图验证通过。理由与结论见 `06-review.md §4`。

---

## 5. 已知待办

> 已迁移到仓库根 `TODO.md` 并分配稳定 id，供 `07-final-report.md` §10/§12 检索；此处保留摘要。

- [ ] `TODO-SEMS-01/02/03/04`：`dotnet build/test`、`pwsh` 起栈、宿主 e2e 完整执行、发布链（环境受限 / 闸门 3 未授权）
- [ ] `TODO-SEMS-05`：视觉优化 §4 整套版式重做（已在本次交付中落地，见 §4.3「视觉优化 §4 整套版式重做（已落地）」）
- [ ] `TODO-SEMS-06`：G3 四态截图 + G4/G5/G8 真机走查
- [ ] `TODO-SEMS-07`：`?semsHash=1` 是否在宿主正式启用（RK-13）
- [ ] `TODO-SEMS-08`：本地功能扩展工作区 历史工作区归档，避免双份真相
- [ ] `TODO-SEMS-09`：后端会话与日志落盘（`RunnerService` 的 `_launched` / `_logs` 目前仅存宿主内存）→ 原事项 `rbSfq4`
- [ ] `TODO-SEMS-10`：设计守卫分项基线 `a=0,b=0,c=0,d=0` 与 style-token 棘轮后续复核
- [ ] 若产品侧确认「插件 / 依赖」两屏需要真实实体，应另立需求并先改后端，不要在本重构里补假数据
- [ ] 第 6 项「规格」导航是否纳入正式 IA，待设计评审确认
- [ ] 首页双模式默认值（现为列表）与项目级配置覆盖，待产品确认 → 基线待确认问题 4
