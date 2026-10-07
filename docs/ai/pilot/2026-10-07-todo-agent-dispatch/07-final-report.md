# AI-Native Pilot Result（最终汇报）

> Task ID：PILOT-054 ｜ 日期：2026-10-07 ｜ 状态：**🟡 PARTIALLY_COMPLETED**（实现 + 验证完成；交付五步的 ③④⑤ 待用户授权）
> 回滚点：HEAD `a1f7ce8`（工作区未提交，75 项变更）

## 1. Repository Understanding

我确认了（读码/实测，非推测）：`todo-tracker` 原为宿主页面 + 单表便签（`ForgeSelf.Web/src/views/TodoView.vue` + `Todo` 表）；宿主已有插件自治界面加载链（`/plugins/{id}/web/**` 由 `PluginFrontendFileMiddleware` 服务，`buildPluginAssetUrl` 拼 URL，入口 `web/dist/index.js`，导出名须等于 `plugin.json.views[0]`）；宿主有 `IProjectRegistry` 但**不做路径归一**；`AgentHub` 已有 `DelegationRuntime` + `PermissionBroker`（人在回路审批）；测试体系 = xUnit（后端）+ vitest（宿主前端）+ 单配置 Playwright e2e（globalSetup 自建宿主）。详见 `00-repository-understanding.md`。

## 2. Selected Task

把 TODO 插件升级为「可下发给 agent 的任务台账」：工作单元字段（目标/正文/允许-禁止范围/验收判据/验证命令/优先级/阶段）、项目路径关联与多格式归一（`/d/project` ≡ `D:\project`）、九件套工件组装正文、append-only 执行记录（做了什么/结果/改了哪些文件/风险/遗留 + 我补的证据/验证/耗时/下一步/阶段）、一键交给 AgentHub 执行；界面迁入插件自带 `web/`；版本 1.1.0。

## 3. Changed Files

- 契约（新）：`ForgeSelf.Abstractions/AgentDelegationContracts.cs`
- 提供方：`Plugins/AgentHub/Services/AgentDelegationProvider.cs`（新）、`AgentHubPlugin.cs`（`ctx.Register`）
- 插件后端：`Data/Model.xml` + xcode 重生成（`Todo.cs` 18 新列 / `TaskExecution.cs` 新表 / `TodoTracker.htm`）、`Data/TodoTrackerTables.cs`（新）、服务 11 个（新）、控制器 4 个（新）+ `TodosController`（重写，全类鉴权）、`Models/*` 6 个、`AgentToolFunctions.cs`（5 个 agent 工具）、`TodoTrackerPlugin.cs`（建表 + 回填 + 服务注册 + 撤菜单）、`plugin.json`（1.1.0 + `frontend.entry`）
- 插件前端（全新）：`Plugins/TodoTracker/web/**`（`http.ts`/`store.ts`/`notify.ts`/`actions.ts`/`types.ts`/`index.ts`/`TodoView.vue`/`components/{TaskDetail,ExecutionTimeline}.vue` + `dist/`）
- 宿主清理：`ForgeSelf.Web/src/{views/TodoView.vue, components/todo/*, stores/todo.ts, services/todoApi.ts, types/todo.ts}` → `.trash/todo-tracker-host-web/`；改 `router/index.ts`、`router/dynamicPlugins.ts`、`components.d.ts`、`data/features.ts`、`dynamicPlugins.test.ts`、`e2e/todo.spec.ts`
- 测试：后端 7 个新文件 + 1 个改；e2e 新增 `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`
- 文档/规则：`docs/02-features/005-todo-tracker.md`（重写）、`docs/01-architecture/host-capability-seams.md §7`、`docs/07-decisions/not-taken-decisions.md`（4 条）、`AGENTS.md §5.0`（2 条环境前置）、`.agents/skills/plugin-development/SKILL.md §七`、`.agents/skills/plugin-frontend-scaffold/scripts/scaffold-plugin-frontend.ps1`（路径修正）
- 工件：本目录 `00…07`

## 4. Validation

Build:
`dotnet build ForgeSelf.Api` = **0 错误**；铁律 12b 判据 = 宿主输出目录里的 `Plugins/TodoTracker/TodoTracker.dll` 与插件产物 **哈希相同**（Verified）。`xcode Model.xml` 重生成后 `BindColumn` 逐列 diff = **只增不改不删**（Verified）。

Unit Test:
定向 `dotnet test --filter "FullyQualifiedName~Plugins.TodoTracker|~AgentHub|~TodosController"` ⇒ **227 passed / 0 failed**（`t12.trx`，走查后补的关联守卫与阳性对照在内；走查前读数为 `t7.trx` 226/226，已作废）。
中档全量 `dotnet test` ⇒ **2717 total / 2707 passed / 10 failed / 18m54s**（`full.trx`）。10 红逐条归属：**本批 1 条**（我自己的守卫正则只认双引号 ⇒ 已修，复跑消失）、**存量基线 7 条**（`WorkflowPlanning`×6 + `ScriptRunnerDi`×1，与 `docs/ai/pilot/2026-10-01-plugin-dev-experience/evidence/baseline-test-failures.txt` 逐字同名）、**并发 flaky 1 条**（`DesignAgentToolContractTests`，隔离跑 4/4 绿）、**曾绿今红 1 条**（`HostInstallRootTests` 代理真跑用例，隔离跑 13/1 红，根因待查，已入 TODO）。
宿主前端：`pnpm run check` **0 error / 76 warning**、`pnpm run test` **742 passed**、`npx vue-tsc -b` **0 error**。
插件前端：`pnpm build` ⇒ `dist/index.js 65.48 kB` / `style.css 20.12 kB`，产物内裸导入仅 `from "vue"` 一处，导出名 `TodoView` == `views[0]`。

E2E:
`pnpm exec playwright test e2e/plugins/todo-tracker` ⇒ 终档 **8 passed (1.8m)**（`e2e-final2.log`）。走查后加强 B1 断言再复跑时曾 **7 passed / 1 failed**（`e2e-final.log`），唯一红是 V1 自己的**假绿被干净库揭穿**（详见 05 的「走查之后复跑 e2e」节），修判据后单跑 1 passed（`e2e-v1.log`）→ 全组 8/8。此前定档跑 `e2e-todo7.log` 亦 8 passed (42.6s)。
含三组共 17 条的那一轮 ⇒ **14 passed / 3 failed**，3 条红全在 `e2e/todo.spec.ts` 首页面板组，实测成因是**首页根本没发起 `/api/todos` 请求**（H1 共 2 个 `/api/` 请求、H4 共 0 个），归属 `Plugins/Home/web`（本批零改动），已入 TODO P2。
截图 7 张落 `ForgeSelf.Web/screenshots/e2e/todo-tracker/`，**逐张读图**后写进 05 的表格；读图当场发现并修掉「4 条 toast 叠住『交给 AgentHub 执行』按钮」。

Walkthrough（§四④ 的走查形态，dev 预览实例）：
自建隔离预览（后端 `:7102` + `FORGESELF_DATA_ROOT=.temp/preview-data`、前端 `:7002`），用 chrome-devtools 以用户视角从「新建任务 → 填路径 → 点关联 → 导入九件套 → 交给 AgentHub → 记执行 → 阶段流转 → 过滤 → 删除」走了一遍，并按 `plugin-development §3.4` 交互清单逐项核对（点即保存落盘、失败原因如实、阻塞无理由被拒、二次确认取消/确定两路、900px 不破版、0 控制台报错）。**当场抓到一条功能缺陷**：点「关联项目」界面显示成功、路径也归一了，但 `ProjectId` 恒为 0（项目过滤筛不到、`ListProjects` 计数恒 0、详情仍写「未关联项目」）—— 这条**单测与 e2e 双绿都没看到**：单测那条用例绕过服务入口直调 `Resolve(registerIfMissing:true)`，且 `Distinct().HaveCount(1)` 缺阳性对照（四个 `0` 也算"只有一个不同值"）；e2e B1 只断言 `projectRoot` 回显。已修 + 补三条反向守卫（`t12.trx` 227/227），浏览器复验确认详情显示 `OpenForgeSelf` + 归一根目录 + 「你的写法：/c/Users/…」，过滤下拉出现 `OpenForgeSelf（1）`。

## 5. Evidence

见 `05-evidence.md`。要点：所有数字都指回真实读数（TRX `<Counters>`、日志正文、grep 计数、截图文件），Verified/Inferred/Unknown 已分级；两处 AC 字面判据（AC-14 的 `vue-router` 裸导入、AC-15 的 `check-features` 绿）按语义达成并双处留痕；`check-features.mjs` 用控制实验证明"本批不新增不一致"（21 → 12，且 12 条无一条与 todo 相关）。

## 6. Review

`06-review.md`：**Final Decision = APPROVED**（就实现 + 验证 + 证据而言，可交闸门 2）；批准附带三条边界——① 提交/发布/走查/运行实例复验须另行授权；② Major-2（Home 鉴权影响面未证伪）与 Major-3（`HostInstallRootTests` 新红）必须进下一批；③ `build/runtime/Plugins/**` 两个未跟踪二进制不得顺手入库。Risk = **L2**。Major-1（走查抓到的关联缺陷）当场修复并补守卫。

## 7. Risk

**L2**。最高两项：数据库结构变更（+18 列 / +1 表，靠 `TodoBackfill` 幂等修历史行）与 `api/todos*` 全类改为需鉴权（对未带 token 的既有调用方是破坏性变更）。两者都还没进任何已发布实例，第 ⑤ 步（运行实例只读复验）是它们的真实验证点。

## 8. Problems Found

现场实测发现并修掉 12 个缺陷（前 8 个由后端测试逼出，中间 3 个由 e2e/读图逼出，最后 1 个只有浏览器走查能抓到）：① 相对路径被当绝对（`GetFullPath` 之后才判盘符）；② UNC 被兜底判据误拒；③ `filesChanged` 存 PascalCase 读回空串；④ prompt 里出现 `- [ ] [ ] AC-1`；⑤ `FindAll` 第三参是投影列不是排序（记录列表字段全空）；⑥ `Cancelled` 被派生成 `Completed`；⑦ 历史行 `Priority=0` 更新即抛；⑧ 接缝缺席被嗅探成 400（应 503）；⑨ **连续失焦的并发 PUT 用旧快照盖掉新状态**（四个都落库、界面仍说"还缺两栏"）；⑩ 空工件清单时**界面把后端不知道的原因说成知道**；⑪ **4 条同文案 toast 叠住刚点过的控件**；⑫ **点「关联项目」显示成功但 `ProjectId=0`**（显式关联走了建单的 `registerIfMissing:false` 分支 ⇒ "一致的路径认为是同一个项目"在数据层从未落地；单测绕过服务入口、e2e 断言同源自洽，两道门禁双绿都没抓到）。
另有三处"我自己写的判据/证据不成立"：守卫用例正则只认双引号（假红）、e2e 的 `REPO_ROOT` 少一层 `../`（**自洽假绿**，两边同源，直到工件接口回"没有该目录"才暴露）、**V1 用裸 `.tt-item` 第 1 项做视觉取证**（接口建的数据不刷新永远看不见，能绿只因库里躺着别人的残留 ⇒ 截的不是本用例那条；走查后复跑 e2e 才暴露，已改为点「刷新」+ 断 `taskKey` 与接口回读相等）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS（四个分叉都在闸门 1 前交给用户拍板） |
| Spec → Plan | PASS（偏差 12 条逐条登记，未静默绕过） |
| Plan → Code | PASS |
| Code → Test | PARTIAL（新增守卫文件后**没有立刻重跑定向集**，那条红拖到 18 分钟全量才被发现） |
| Test → Evidence | PASS（含"grep TRX 类名证明某轮跑没跑到"的核对法） |
| Evidence → Review | PASS |
| §四④ 走查 | **PARTIAL（做对了但做晚了）**：门禁与 e2e 绿之后就停住，直到用户追问"技能流程是否有走查流程，你做了没"才补做；补做当场抓到 Major-1 功能缺陷。结论已回写 `plugin-development`（走查不是仪式，双绿不能替代它） |

## 10. 最重要的问题

**"绿灯的范围"没人管**：同一批里出现三次"绿得没有意义"——① 定向集全绿，但那条红所在的守卫文件是**绿之后才新增的**（`grep -c 类名 *.trx` = 0 命中，等于没跑到）；② e2e 里 `归一后 == REPO_ROOT` 两边同源，路径写错一层照样绿；③ **单测 226/226 + e2e 8/8 双绿的同时，用户明确要求的那条"一致的路径认为是同一个项目"在数据层是坏的**（关联后 `ProjectId=0`）——因为单测绕过了真实服务入口直调 `Resolve(registerIfMissing:true)`，而 e2e B1 只断言了 `projectRoot` 没断言 `projectId` 与详情面板的关联态。三者都不是实现缺陷，而是**判据自身没有阳性对照、也没锚在真实入口上**。已沉淀：`AGENTS.md §5.0` 新增"同源自洽的断言不算证据"，用户级记忆 `feedback-rerun-scope-by-loaded-artifacts` 补上"用 TRX 类名命中数证明某轮跑没跑到"的核对法，`plugin-development §七` 加了这条坑，并把"④ 浏览器走查"从可选项改成双绿之后仍必须做的一步。

## 11. 下一步建议

只做一件、且能自己关：**给"点即保存 + 整行快照响应"这一类缺陷加机器守卫**——在 `plugin-development` 的收口清单里要求任何自带界面的插件，其前端 HTTP 封装必须把非 GET 请求串行化（或按序号丢弃旧响应），并写一条静态守卫用例扫各插件 `web/src/http.ts` 是否具备该形态。这样第 ⑨ 号缺陷不会在别的插件重演。
（紧随其后、但需用户先授权的：AC-20 的 ③④ 三步——提交 + 打 tag/本地目录发布 + 隔离实例走查 + 运行实例只读复验。）
