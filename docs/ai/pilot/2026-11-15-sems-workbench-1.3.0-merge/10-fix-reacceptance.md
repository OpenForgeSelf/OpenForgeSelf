# 10 · 5 项问题整改与真机回归验收（2026-11-15 深夜）

> 触发：用户「修复问题，重新验收，更新代办」。
> 范围：`09-full-browser-reacceptance.md` 记录的 **3 处需求偏离 + 2 处实现缺陷**，依据 `02-spec.md`（AC-D1 / AC-D3 / NFR-9 / AC-F / FR-5）。
> 环境：自起 dev-stack（后端 :7301 / 宿主前端 :7399，LIVE），`?semsHash=1#sems<route>` + Playwright 真机操作；插件前端已重新 `pnpm build`（dist 22:46 产物）。
> 纪律：不改后端 `.cs`、不改 `plugin.json` 契约、不提交 git；测试数据全隔离并归零。

## 1. 结论先行

**5 项问题全部修复并在真实宿主里复验通过，32 项基线功能无退化。**

| 类别 | 数量 | 结果 |
| --- | --- | --- |
| 需求偏离修复 | 3 处 | ✅ D1 / D2 / D3 全部 Verified |
| 实现缺陷修复 | 2 处 | ✅ B1 / B2 全部 Verified |
| 新增回归单测 | 5 条 | ✅ 37/37（基线 32 + 新增 5），反向探针实红 2 条后还原 |
| 浏览器回归 | 10 屏 + 关键流程 | ✅ 假能力 0 / 横向溢出 0 / 新增 console error 0 |
| 遗留阻断项 | 0 | — |

## 2. 修复清单（文件级 + 前后对比）

| # | 问题 | 修复前 | 修复后 | 落点 |
| --- | --- | --- | --- | --- |
| D1 | 向导 4 步 + 4 个假区域 | step 0 项目详情（包管理器/覆盖目录/精简脚手架/Git）、step 1 预设、step 2 功能、step 3 确认；`WIZARD_FEATURES/PRESETS/PACKAGE_MANAGERS` | **2 步**：① 确认目录（只读路径 + 重选入口 + 未选目录禁用下一步并给可见原因）② 填名称/类型/标签/描述 + 明示不创建文件 + 「登记项目」；`type/tags/description` 经 `PUT` 真实写回，写回失败 toast 指向 S09 独立重试（BR-1） | `ManagerWizard.vue`（重写）、`workbenchStore.ts`（`WbWizardMeta` 改档案字段、`registerProject` 写回、删假常量）、`WorkbenchView.vue`（`WIZARD_STEPS`）、`ManagerBrowser.vue`（文案） |
| D2 | S09 只有折叠的假开关 | 控件藏在默认折叠的 `configOpen:false` 卡后（实测 input/switch 全 0）；3 个开关 `autoRefresh/confirmDangerous/defaultCommandPrefix` **无任何消费点**，写了不生效 | 卡片常驻；只保留**真实生效**的两项：项目列表布局（列表/卡片 → `setProjectLayout`，S01 立即生效）+ 状态栏项目路径（`showPathBadge` → `WorkbenchView` 状态栏 `v-if`）；`meta` 改 `reactive` 使开关即时反显；删除 3 个假开关（FR-3） | `ProjectConfigurationView.vue`（重写）、`WorkbenchView.vue`（状态栏）、`workbenchStore.ts`（`meta=reactive` / `projectConfigs` 精简 / 删 `configOpen`） |
| D3 | 主 CTA 绿色 | `.btn-primary{background:var(--el-color-success)}`，选中态同样绿色，与同文件注释 + NFR-9 冲突 | `.btn-primary` 改 `var(--el-color-primary)`；文字改 `var(--el-bg-color)`（浅色白字 / 暗色深字自动跟随）；选中态（layout-toggle / project-card / folder-item / switch / chip）统一 primary；**成功状态徽标保留语义绿** | `workbench.css`（8 处） |
| B1 | 维护区启动不更新状态机 | `CommandList @run` 只 toast + refresh，`startAllTasks` 只 POST；两条路径不写 `taskStatus/runHistory/logsByCommand` | 抽出统一入口 `markTaskStarted()`（状态机 / 日志桶 / 运行历史 / 解除冷态标记 / 起轮询），由 `runTask`、`startAllTasks`、`ProjectTasksView.onCommandRun(id)` 三处共用 | `workbenchStore.ts`、`ProjectTasksView.vue` |
| B2 | 冷态 404 反复刷控制台 | `ensureTaskPolling` 对选中任务无条件每 1.2s 请求 `/logs?after=0`，历史累积 42 条 error | 新增 `logUnavailable` 集合：冷态 404 只提示一次并停该命令轮询；运行中命令的 404 判为启动瞬态（继续轮询、不污染日志）；显式启动解除标记；`disposeWorkbench` 清空 | `workbenchStore.ts` |

> 关键决策：**D2 不是简单「展开卡片」**。核对源码后确认原控件写 localStorage 但无任何消费点，属 FR-3 禁止的假能力；故改为「删假的 + 接真的」，而非把假开关暴露出来。**B2 不能一律 404 就停轮询**，否则命令刚启动时后端日志缓冲未建立会导致实时日志永久停掉，故用 `taskStatusOf(id) === 'running'` 区分启动瞬态。

## 3. 构建与单测证据

| 命令 | 结果 |
| --- | --- |
| `pnpm build`（Plugins/Sems/web） | ✅ vite 6.4.3，54 modules，`dist/index.js` 175.42 kB / `style.css` 55.12 kB，3.49s |
| `pnpm test` | ✅ **37/37**（原 32 基线 + 新增 `regression-fixes.spec.ts` 5 条） |
| 反向探针 | 临时移除 `logUnavailable.add(commandId)` ⇒ **失败 2 / 通过 35**（`regression-fixes.spec.ts` 实红）；还原后 `cmp` 逐字节一致 + 37/37 复绿 |
| dist 内容守卫 | `确认目录`/`填写档案`/`工作台显示偏好`/`状态栏显示项目路径` 均命中；`包管理器`/`初始化 Git`/`精简脚手架`/`覆盖目录`/`默认预设` **全部 0 命中** |
| 新增单测覆盖 | B2 冷态 404 / B2 启动瞬态 404 / B1 `markTaskStarted` / D2 偏好落盘 / D1 只写回真实档案字段（断言 PUT body 不含任何脚手架假字段） |

## 4. 真机浏览器复验矩阵

### 4.1 五项修复逐项

| 项 | 验收动作 | 实测结果 |
| --- | --- | --- |
| **D1** | `/create` 选 `D:\sems-fix-20261115` → 「在此新建项目」→ 步骤条 / 假词扫描 → 下一步 → 填 4 字段 → 「登记项目」 | 步骤条 **2 步**（确认目录 / 填写档案）；假区域词 **0**；第 1 步只读路径 `D:\sems-fix-20261115`；第 2 步 4 个档案字段 + 确认说明；提交后自动进 `/dashboard`；后端 `GET /api/projects` 返回 `type=frontend / tags=验收,回归 / description=2 步向导写回档案字段验证`；**磁盘文件数 0**（BR-1）✅ |
| **D2** | `/configuration` 控件清点 → 点「卡片模式」→ `/projects` 看布局 → 切回列表 → 关「状态栏显示项目路径」→ reload → 恢复 | 进屏即有 `switch=1` + 布局按钮 2；假开关文案 0；卡片模式落 `localStorage.projectLayout=card` 且 `/projects` 渲染 `.project-cards > *` 1 卡；关闭开关后 `.status-path` 消失、`aria-checked=false`、落 `configs['6'].showPathBadge=false`；**reload 后仍隐藏**；恢复后状态栏路径回来 ✅ |
| **D3** | `getComputedStyle` 对比 CTA 背景与 `--el-color-primary`（明/暗两态） | 浅色 `#B45309` → CTA `rgb(180,83,9)`、文字 `rgb(251,247,238)`；暗色 `#F59E0B` → CTA `rgb(245,158,11)`、文字 `rgb(20,17,15)`；**两态严格相等且自动反色**；读图确认琥珀/金色、无绿色 ✅ |
| **B1** | 三条启动路径分别验状态机：维护区「启动」、「启动全部」、详情区「运行任务」 | 维护区启动：左侧行「运行中」、详情徽标「运行中」、停止按钮 enabled、运行按钮 disabled、日志 `$ echo…`/`> 正在…`/`FIX-L1` 实时追加、顶部「运行中 1」；启动全部：两条命令均「运行中」、详情区切到冷态命令且状态「运行中」、`COLD-ONLY` 日志出现、失败提示 0；详情区运行任务：状态同步、`FIX-L1` 日志 ✅ |
| **B2** | 新增**从未运行**的命令 id=4 → 干净加载 /tasks → 选中 → 监听 404 与 console error → 静置 6s → 再选一次 | `/api/commands/4/logs?after=0` **404 = 1 次**、console error = 1；静置 6 秒后 **仍为 1**；再次选中 **仍为 1**；UI 保留**一次**明确提示「! 读取日志失败：请求失败(404): 暂无该命令的输出日志（须由本页启动且宿主未重启）」（FR-5 降级语义未丢）✅ |

### 4.2 回归（确认修复未引入退化）

| 项 | 结果 |
| --- | --- |
| 10 路由批量扫描 | 10 屏全部可达、正文长度互异、假能力词 **0**、横向溢出 **0**、扫描期间新增 console error **0**；S09 有 switch、向导步骤条为 2 步 |
| F03 导入（registerProject 无 meta 路径） | 目录浏览器 → 「导入此文件夹」→ total 1→2，toast「已导入项目」；不写 PUT、不创建文件 |
| F04 移除二次确认 | 弹窗明确「只删除 sems 档案…不会删除磁盘目录与文件」；取消分支 total 保持 2、确认分支 2→1；磁盘目录仍在 |
| BR-2 409 保护 | 起存活会话（PID 82736）→ `DELETE /api/projects/6` = **HTTP 409**「项目有 1 个运行中的命令，请先停止后再移除」；停会话后清理成功 |
| G4 经典视图 | `.sems__title`/`.pcard`/`.run` DOM 完整；切回工作台后搜索词「不存在的项目xyz」保留、`viewMode=workbench` |
| 搜索无结果空态 | 「没有匹配的项目」专属空态出现 |
| 明暗双模 / 375px / X5 | CTA 两态等于 primary；`scrollWidth=clientWidth=375`；<768px 时 INFO 徽标与版本串 `display:none` |
| 规格端点抽样 | `GET /api/projects/6/specs` = HTTP 200 |
| 控制台 | 历史 `command/1` 404 来自上一轮遗留的 `sems-run-sessions-v1` 本地会话缓存（清理后新加载不再出现）；本轮新增 error 仅上述 B2 的 1 条设计内 404 |

## 5. 证据文件

| 文件 | 说明 |
| --- | --- |
| `.temp/sems-verify/sems-fix-light-live.png` | 修复后浅色 LIVE 工作台（含临时测试项目，证明写库→UI 闭环；该档案已清理） |
| `.temp/sems-verify/sems-fix-dark-375.png` | 暗色 + 375px 组合态：亮琥珀 CTA、无溢出、状态栏 INFO/版本隐藏 |
| `docs/ai/pilot/…/09-full-browser-reacceptance.md` | 整改前的问题定性（本文件的前置依据） |
| `.workbuddy/reports/2026-11-15-sems-1.3.0整改与回归验收.{json,html}` | 本轮工作汇报 |

## 6. 未覆盖 / 观察项（不伪造通过）

1. 需人为断网才能触发的 **DEMO 降级禁写**与**加载失败可重试**两态未测；真后端全程在线。
2. 宿主 e2e 完整套件（`sems.spec.ts`）、`dotnet test` 全量回归、axe/Lighthouse 对比度与色盲模拟未跑；本轮为插件前端改动，后端零改动。
3. **启动瞬态 404**：命令刚 POST run、后端日志缓冲尚未建立时，浏览器仍可能自动记录至多 1 条 404（前端已按运行态静默、不污染日志区、不停轮询）。与整改前「冷态反复刷屏 42 条」性质不同，非功能阻断。
4. 上一轮遗留的 `sems-run-sessions-v1` 本地会话缓存会让 RunPanel 对**旧 commandId** 拉 `/logs` 产生历史 404；属验证环境污染，清理后消失，不是插件逻辑缺陷。
5. `workbench.css` 引用 `--radius-sm` / `--gold-tint` / `--ink-950` 等宿主变量（插件内未定义），与项目「0 自定义 token」铁律的边界仍待裁定（沿用 08/09 观察项，本轮未改）。
6. 未提交 git；TODO 中本轮整改项按 AGENTS.md「完成即移除」归档到日记。

## 7. 一句话

**5 项问题全部修完且真机复验通过**：2 步向导真实写回档案、S09 显示偏好从「折叠的假开关」变成「常驻且生效」、CTA 明暗两态统一琥珀色、三条启动路径共用状态机、冷态 404 从 42 条收敛为 1 次明确提示；37/37 单测 + 反向探针有效，10 屏与关键流程零退化，测试数据归零，未提交 git。
