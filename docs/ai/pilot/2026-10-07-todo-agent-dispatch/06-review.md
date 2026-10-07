# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。
> Task ID：PILOT-054 ｜ 日期：2026-10-07 ｜ 审查对象：工作区（HEAD `a1f7ce8` 之上的未提交改动，`git status --porcelain` = 75 项）

## 审查八问（逐项回答）

**1. 实现是否真正满足 Intent？**
满足。Intent 的四件事都有可跑证据：① 任务变成"可下发的工作单元"（六块 prompt + `by-key/{taskKey}/records` 回报契约，B1 截图里能看到真实提示词与真实 taskKey）；② 项目路径关联 + 多格式归一（`/c/...` 写法实测归一成 `C:\Users\…`，`projectPathRaw` 留档）；③ 九件套核心件作为任务正文（实测导入 4 件、正文 52212 字符、`artifactRef` 指回来源）；④ 执行记录（做了什么/结果/改了哪些文件/风险/遗留 + 我补的 证据/验证/耗时/下一步/阶段流转，B2 截图 3 条台账可回放）。
用户"帮你补充一下"的部分：补了 `detail/verification/risks/residuals/evidence/nextStep/elapsedMs/blockReason/stageFrom→stageTo/actor`，并在界面与 REST 两侧同形。

**2. 实现是否符合 Spec？**
符合 AC-1…AC-13 与 AC-19（文档四件 + `plugin.json.Version=1.1.0`）。两处按字面判据不成立、按语义成立，已在 03-plan 偏差表留痕：
- **AC-14** 的字面 `grep 'from "vue-router"' dist/index.js` 命中：本插件前端根本不 import vue-router（提示/确认走自带 `notify.ts`），产物内只有 1 处裸导入 `from "vue"`；判据按语义改写作"产物里出现的每个共享依赖都必须是裸导入"，且 `vite.config.ts` 五个 external 声明仍被守卫用例覆盖。
- **AC-15** 的字面「`check-features.mjs` 绿」：脚本在 HEAD 就红（`pluginsDir` 指向早已搬走的 `ForgeSelf.Api/Plugins`）。本批按"不新增不一致"判定，并给出控制实验（16 条假红消失、剩 12 条无一条与 todo 相关）与改名前后 `grep TodoView` 的 3→0 对照。
- **AC-20** 五步：① 门禁 ② 插件层 e2e 已完成；③ 发布 ④ 隔离实例走查 ⑤ 运行实例只读复验 **未做**（须用户批准提交/打 tag）。

**3. 是否超出了 Scope？**
有 4 处边界触碰，全部登记：① `ForgeSelf.Abstractions/AgentDelegationContracts.cs`（新契约，架构设计铁律 2 要求，属必要外扩）；② `Plugins/AgentHub/**`（提供方注册 + 新文件，用户拍板"一键交给 AgentHub 执行"的必要面）；③ `e2e/todo.spec.ts` 里把 Home 弹窗选择器从宿主旧版对齐到插件现状（陈旧红的顺手修，非本批功能）；④ `AGENTS.md §5.0` 与技能/脚本（`scaffold-plugin-frontend.ps1` 路径修正）——属 §7"沉淀规律"的强制动作。
另外**新建了两个未跟踪二进制** `build/runtime/Plugins/{System.Data.SQLite.dll,e_sqlite3.dll}`（为解封 e2e），**是否入库需用户拍板**，未擅自 `git add`。

**4. 是否修改了不应该修改的文件？**
未发现越权改动：宿主业务源码只碰了 `ForgeSelf.Api` 的**构建图之外**的三处（Abstractions 新文件 + 两个插件目录）；`ForgeSelf.Api/Plugins/PluginManager.cs`、`AppBuilder.cs`、内核 `ForgeSelf.Core/**` **零改动**（用户拍板"路径规范化只在插件内部"，我没有去改宿主登记器）。数据表变更走 `Model.xml → xcode` 重生成，生成物未手改（铁律 9）；旧文件按铁律移入 `.trash/` 而非删除。
需要点名的一处：**宿主 `ForgeSelf.Web/src/**` 删了 8 个文件**（TodoView 及 todo 组件/store/api/types）——这是用户明确拍板的"本批迁到插件自带 web/"，且 `dynamicPlugins.test.ts` 9/9、宿主 `pnpm run test` 742/742 绿。

**5. 测试是否覆盖 Acceptance Criteria？**
后端：新增 7 个测试文件（`ProjectPathCanonicalizer/TodoStageMachine/DispatchPayloadBuilder/TodoDispatchFlow/TodoAgentDelegation/TodoTrackerAuth/TodoTrackerWebAsset`）+ 改 `TodosControllerTests`，定向集 **227/227 绿**（`t12.trx`；走查前的 `t7.trx` 226/226 已作废，见 Major-1）。
前端：宿主 `check` 0 error、`test` 742 passed、`vue-tsc -b` 0 error；插件 `pnpm build` 产物三项对账（导出名/裸导入/style.css）由守卫用例锁死。
e2e：插件层 8 条（A1 远程加载 + 入口 200/MIME、B1 下发主链路含 Git-Bash 归一与覆盖确认、B2 台账 3 条与阶段流转、C1 删除双路径、D1 401、D2 旧形状兼容、D3 委派如实回执、V1 视觉取证）**8 passed (1.8m，`e2e-final2.log`)**。
—— 加强 B1 断言后的第一次复跑是 **7/1**：红在 V1，而这条红暴露的是**我用例自身的假绿**（V1 用接口建数据后直接 `click('.tt-item')`，本插件不轮询 ⇒ 能绿只因库里躺着别人的残留，截的不是本用例那条）。已改为「点刷新 + 断 `.td-key` 等于接口回读的 `taskKey`」，单跑 1 passed 后全组复跑 8/8（过程与读图见 05）。
未覆盖项（如实列出）：>200,000 字符的极端导入（服务侧有上限拒，未测真实边界外）；符号链接/junction 判同（明确不做）；`claim_agent_task` 的**多 agent 并发真实抢占**只有单进程内的并发用例，未跑真双宿主。

**6. 是否存在明显回归风险？**
三条，按大小排：
1. **`api/todos*` 全类改为需鉴权**（铁律 17）——对**任何未带 token 的既有调用方**都是破坏性变更。已实测：浏览器侧（宿主注入 token）与插件 e2e 侧正常；应用层 `todo.spec.ts` 的裸 fetch 被打红并已修。**残留风险**：`Home` 面板现在一个 `/api/todos` 请求都没发（H1/H4 实测 2 个/0 个 `/api/` 请求），因此"Home 是否也会因鉴权而静默失败"这条**没能证伪**（它的 `addTodo` 有 `catch {}`）。已入 TODO P2 专项。
2. **表结构扩 18 列 + 新表**：靠 `TodoBackfill` 幂等修历史行（实测老行 `Priority=0` 更新即抛的缺陷由它消除）。风险面在**已发布实例**的旧库升级，需第 ⑤ 步运行实例只读复验确认。
3. **写请求串行队列**改变了前端并发形态（写不再并发）：收益是消除旧快照盖新状态，代价是快速连点时后一个请求要排队。已由 B1/B2 用例覆盖。

**7. 是否存在架构不一致？**
未发现，且有一处主动纠偏：`IAgentDelegation` 契约定义在 `ForgeSelf.Abstractions`（铁律 2）；消费侧 `AgentTaskGateway` 每次 `ctx.Get<T>()` 不缓存实例（铁律 4）；提供方在 `AgentHubPlugin.Apply` 内 `ctx.Register`（提供即 effect）；**todo-tracker 前端不直连 agent-hub 的 HTTP**（有静态守卫用例扫源码，命中即红）。
Plan 期我原本要 `BuildServiceProvider()` 注入 `DelegationRuntime`，实测会另起一份 `PermissionBroker` 单例（G2 人在回路审批队列分裂）⇒ 改为持容器每次现取，偏差与踩坑已回写 `host-capability-seams.md §7`。
一处**遗留不一致**（非本批引入，已登记）：`Home` 插件前端仍直连 `api/todos` REST（跨插件数据面），架构上更干净的形态是走能力接缝——记在 `not-taken-decisions.md` 与 `host-capability-seams.md §7` 的说明里。

**8. Evidence 是否足以证明任务完成？**
**足以证明"实现 + 验证完成"，不足以证明"交付完成"**：05 的每一项都指回一次真实工具调用与真实读数（TRX `<Counters>`、日志正文、截图文件、grep 计数），并区分 Verified/Inferred/Unknown；但 plugin-development §四 五步里的 ③④⑤ 未做（发布/隔离实例走查/运行实例只读复验），且这三步需要用户授权提交与打 tag。因此整体状态是 **🟡 PARTIALLY_COMPLETED**，不是"完成"。

## Requirement Check

**PASS**（AC-1…AC-13、AC-19 逐条有证据；AC-14/AC-15 按语义达成、字面判据的偏差已在 03-plan 与 05 双处留痕；AC-16/AC-17/AC-18 达成）

## Scope Check

**PASS**（4 处边界触碰全部登记；未碰宿主内核与 `PluginManager`；未擅自在提交里带上未跟踪二进制）

## Test Check

**PASS**（后端定向 **227/227 绿**（`t12.trx`，含走查后新增的关联守卫用例与阳性对照）；全量 2717 中 10 红已逐条归属，本批引入的 1 红已修并复跑消失；插件 e2e **8/8**（`e2e-final2.log`，V1 判据修正后复跑）；宿主前端 check/test/tsc 三项绿）
—— 未达绿的两处均为**非本批**：`HostInstallRootTests` 1 条（曾绿今红，根因待查）、`todo.spec.ts` 首页面板 3 条（Home 页未发起 todos 请求）。
**注意**：`t7.trx` 的 226/226 是走查前的读数；走查抓到功能缺陷后代码与用例都变了，**当前有效读数是 t12 的 227/227**（旧数字不得再被引为终态）。

## Architecture Check

**PASS**（契约入 Abstractions、接缝消费不缓存、无跨插件直连 HTTP 且有静态守卫；菜单单一来源 = `plugin.json`）

## Risk

**L2**（含数据库结构变更与公共 API 鉴权语义变更两项高风险动作，但均已按 TDD 覆盖、可回滚点明确 = HEAD `a1f7ce8`，且发布/升级动作尚未执行）

## Findings

### Critical

无。

### Major

1. **走查抓到一条功能缺陷（已修 + 已补三条反向守卫）**：`ApplyProjectFields` 把「建单随手写路径」和「显式点关联」两个入口合并成一条 `registerIfMissing:false` 的调用，导致点关联只写了归一路径、`ProjectId` 恒为 0 —— 于是"关联到同一个项目"在**数据层从未成立**：项目过滤筛不到、`ListProjects` 的 taskCount 恒 0、详情面板按 `projectId` 判关联仍显示「未关联项目」（实测 `GET /api/todos/2` 回 `projectId=0` 但 `projectRoot` 有值 ⇒ 界面与列表两边分叉）。**用户输入 1 里"一致的路径认为是同一个项目"这条要求，在 226/226 单测与 8/8 插件 e2e 双绿的状态下仍然是错的**：单测那条"四种写法"用例走的是 `TodoProjectService.Resolve(registerIfMissing: true)`（**绕过服务入口**，而坏的恰恰是服务入口的传参），并且断言是 `Distinct().HaveCount(1)`——**四个 `ProjectId=0` 也满足"只有一个不同值"**（缺阳性对照）；e2e B1 只断言了 `projectRoot` 回显，没断言 `projectId` 与详情面板的关联态。修复 = `ApplyProjectFields` 增显式 `registerIfMissing` 形参（建单传 `false`、关联传 `true`）；守卫 = ① 关联后 `ProjectId > 0` ② 四种写法关联到同一 `ProjectId` ③ 重复关联不新增宿主档案（`ForgeSelf.Api.Tests/Plugins/TodoTracker/TodoDispatchFlowTests.cs`，t12 227/227）。**流程结论：§四④ 的浏览器走查不是可跳过仪式**——它抓到的是单测与 e2e 结构性看不见的缺陷，本批直到用户追问"你做了没"才补做。
2. **鉴权变更的影响面没有被完整证伪**：`Home` 面板现在不发 `/api/todos` 请求（H1/H4 实测），所以"Home 消费 todo API 是否会被新鉴权静默打断"仍是 Unknown；它的 `addTodo` 用 `catch {}` 吞错，即使被打断也不报。**下一步必须专项验证**（打开 Home 面板看 network，或给 `addTodo` 补上可见错误）。
3. **`HostInstallRootTests` 属"曾绿今红"**：`docs/ai/pilot/2026-10-04-…/05-evidence.md` 记录 13/13 绿，今天隔离复跑 13 passed/1 failed，且代理子进程 `pwsh exit=-1`、零输出零日志。本批无因果（未碰相关文件），但**它 spawn `pwsh` 并对传入 PID 走 `Stop-Process -Force`**，在多会话并发机器上有反杀别人进程的风险 —— 需要专项处理，不该长期留在队列里。
4. **"点即保存 + 整行快照响应"这一类缺陷具有普适性**：本插件已用写串行队列堵住；其他自带界面的插件（AIAgent/QuickLinks/Home…）若也是"每次失焦发一个 PUT、响应整行覆盖本地状态"，同样会犯。建议沉淀为 `plugin-development` 的显式铁律/守卫（本批已回写技能 §七，但**没有**给别的插件加守卫）。

### Minor

1. toast 固定在右下角，最多 3 条时仍会压住详情面板右下内容（去重前是 4 条叠加挡住按钮，已缓解）。
2. `V1`/`v1-list-default.png` 截图时刻版本徽标仍是「加载中…」——徽标证据应以 A1 断言与 b1/d3 截图为准（用例本身没问题，是取证时机）。
3. `check-features.mjs` 的 `pluginsDir` 过期、`global-setup` 的 SQLite provider 候选源不在仓库、`Plugins/*/web/dist` 需手工构建 —— 三条环境/工具债已入 `TODO.md` 与 `AGENTS.md §5.0`，但**债还在**。
4. `build/runtime/Plugins/` 两个未跟踪二进制：入库 or 改 `globalSetup` 候选源，需用户拍板。

## Final Decision

**APPROVED**（就"本批实现 + 验证 + 证据"而言，可交闸门 2 用户验收）

批准的条件与边界（不得被当成"任务已完成"）：
1. 提交、打 tag/发布、隔离实例走查、运行实例只读复验（AC-20 的 ③④⑤）**待用户授权后执行**；本 Review 不构成发布许可。
2. 上面 Major-2（Home 鉴权影响面）与 Major-3（`HostInstallRootTests` 新红）**必须**在收口后的下一条批次里被处理，不得只留在 TODO 里；Major-1 已在走查当场修复并补守卫，其**流程教训**（走查不可跳）已回写 `plugin-development`。
3. `build/runtime/Plugins/**` 两个未跟踪二进制**不得**被顺手 `git add`。
