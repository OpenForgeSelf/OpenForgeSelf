# Evidence

> 阶段：Stage 7｜只记录**实际发生**的事情，每项标来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。
> Task ID：PILOT-054 ｜ 日期：2026-10-07 ｜ 回滚点：HEAD `a1f7ce8`

## Task

把 `todo-tracker` 从「便签」改造成「可下发给 agent 的任务台账」：工作单元字段 + 项目路径归一关联 + 九件套工件组装正文 + append-only 执行记录 + 一键交给 AgentHub 执行；界面迁入 `Plugins/TodoTracker/web/`；插件版本 1.1.0。（01-intent / 02-spec AC-1…AC-20）

## Changed Files

工作区实测（`git status --porcelain` 计数）：**修改 21、删除 8、新增 44**。

- 契约层（新）：`ForgeSelf.Abstractions/AgentDelegationContracts.cs`
- 提供方（AgentHub）：`Services/AgentDelegationProvider.cs`（新）、`AgentHubPlugin.cs`（改：Apply 内一行 `ctx.Register`）
- 插件数据层：`Data/Model.xml`（改）、`Data/Entities/Todo.cs`（xcode 重生成）、`Todo.Biz.cs`（改）、`TaskExecution.cs` + `TaskExecution.Biz.cs`（新，xcode 生成 + 人工）、`Data/TodoTrackerTables.cs`（新）、`Data/TodoTracker.htm`（生成物，入库）
- 插件服务（新 11 个）：`ProjectPathCanonicalizer`、`TodoStage`、`DispatchPayloadBuilder`、`FileChangeList`、`TodoProjection`、`TodoBackfill`、`TodoProjectService`(+I)、`TaskExecutionService`(+I)、`ArtifactImportService`(+I)、`AgentTaskGateway`(+I)、`TodoDispatchService`(+I)；改：`ITodoService`、`TodoService`
- 控制器：新 `TodoProjectsController`、`TodoArtifactsController`、`TaskExecutionsController`、`TodoDispatchController`；改 `TodosController`（全部加类级鉴权）
- 模型：新 `TaskExecutionDtos`、`ProjectDtos`、`DispatchDtos`、`ArtifactDtos`、`TodoOpResult`、`TodoToolSchemas`；改 `TodoDtos`
- 工具函数：新 `AgentToolFunctions.cs`（5 个）；改 `TodoTrackerPlugin.cs`（服务注册 / 建表 / 回填 / 撤销菜单 / 工具登记）、`plugin.json`（entry + 1.1.0）
- 插件前端（全新）：`web/**`（scaffold 生成后重写：`index.ts`、`types.ts`、`http.ts`、`store.ts`、`notify.ts`、`actions.ts`、`TodoView.vue`、`components/TaskDetail.vue`、`components/ExecutionTimeline.vue`、`dist/index.js`+`style.css`）
- 宿主前端清理（删 → `.trash/`）：`ForgeSelf.Web/src/views/TodoView.vue`、`src/components/todo/{TodoListItem,TodoEditDialog}.vue` + 两个 `.test.ts`、`src/stores/todo.ts`、`src/services/todoApi.ts`、`src/types/todo.ts`
- 宿主前端改：`src/router/index.ts`、`src/router/dynamicPlugins.ts`、`components.d.ts`、`src/data/features.ts`、`src/router/__tests__/dynamicPlugins.test.ts`、`e2e/todo.spec.ts`（裁为首页面板组 + 地址改走 `backendUrl()`）
- 新增 e2e：`ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`
- 新增后端测试（7 个文件）：`ProjectPathCanonicalizerTests`、`TodoStageMachineTests`、`DispatchPayloadBuilderTests`、`TodoDispatchFlowTests`、`TodoAgentDelegationTests`、`TodoTrackerAuthTests`、`TodoTrackerWebAssetTests`；改 `Integration/TodosControllerTests.cs`（控制器构造加派发桩）
- 文档：`docs/02-features/005-todo-tracker.md`、`docs/01-architecture/host-capability-seams.md`（§7 新接缝登记）、`docs/07-decisions/not-taken-decisions.md`（4 条不做）
- 技能脚本修复：`.agents/skills/plugin-frontend-scaffold/scripts/scaffold-plugin-frontend.ps1`（模板路径旧 → 仓库根 `Plugins/`）
- 工件：`docs/ai/pilot/2026-10-07-todo-agent-dispatch/00…07`

## Build

Command:

```bash
cd ForgeSelf.Api && dotnet build ForgeSelf.Api.csproj      # 宿主构建图（含插件 ProjectReference）
```

Result: **PASS（Verified）**

```text
1343 个警告 / 0 个错误（Select-String ': error' 无输出）
已用时间 00:00:48.53（首次）；改完服务/控制器后复跑同样 0 error
```

铁律 12b 判据（宿主产物而不是插件目录自建）：

```text
Test-Path ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/TodoTracker/TodoTracker.dll → True
(Get-FileHash 宿主产物).Hash -eq (Get-FileHash 插件产物).Hash → True
```

## 实体生成对账（AC-1）

```bash
cd Plugins/TodoTracker/Data && xcode Model.xml          # NewLife.XCode v11.25.2026.912
diff <(git show HEAD:...Todo.cs | grep -oE 'BindColumn\("[A-Za-z]+"'|sort) <(grep -oE 'BindColumn\("[A-Za-z]+"' Todo.cs|sort)
```

Result: **PASS（Verified）** — diff 只出现 `>` 新增列（Acceptance/AgentId/AgentTaskKey/AllowedScope/ArtifactRef/Assignee/Content/DispatchedAt/ForbiddenScope/Objective/PermissionMode/Priority/ProjectId/ProjectPathRaw/ProjectRoot/Stage/TaskKey/Verification），**无删除、无改名**；`Todo.Biz.cs` 未被覆写；生成物 `TaskExecution.cs`(404 行)/`TaskExecution.Biz.cs`/`TodoTracker.htm` 已入库。

## Unit Test（快档定向）

Command（最后一轮定向集，TRX `t7.trx`，日志 `.temp/tmp/t7.log`）：

```bash
cd ForgeSelf.Api.Tests && dotnet test --logger "trx;LogFileName=t7.trx" \
  --filter "FullyQualifiedName~Plugins.TodoTracker|FullyQualifiedName~AgentHub|FullyQualifiedName~TodosController"
```

Result: **PASS（Verified）** — 读 TRX 正文 `<Counters>`：

```text
total="226" executed="226" passed="226" failed="0"      ← t7.trx（本批 7 个测试文件全在内）
```

同类早期一轮（TRX `t6.trx`，修 8 个缺陷后）：`total=234 passed=234 failed=0`，但**那一轮不含 `TodoTrackerWebAssetTests`**（该文件在 t6 之后才新增；核对法 = 对每个 TRX `grep -c TodoTrackerWebAssetTests`：t2/t4/t5/t6 全为 **0 命中**，full.trx 有命中 ⇒ 当时根本没跑到，不是"跑过了"）。

（过程记录：首轮 145 passed / **15 failed**，逐条读 TRX `<Message>` 定位到 8 个真实缺陷（详见日记「实测缺陷与处置」表：相对路径被接受 / UNC 被拒 / filesChanged 存 PascalCase 读回空串 / prompt 出现 `- [ ] [ ]` / `FindAll` 第三参是投影列不是排序 / Cancelled 被派生成 Completed / 老行 Priority=0 更新即抛 / 503 被嗅探成 400），修完后 157/3 → 160/0 → 234/0 → 226/0。）

**本批自己引入并已修掉的一条红**（由中档全量抓到，不是定向集抓到的）：

```text
TodoTrackerWebAssetTests.vite产物名与entry一致_共享依赖全部external
Expected vite to match regex "external:\s*\[[^\]]*""vue""" because vue 必须 external,
but "…external: ['vue', 'vue-router', 'pinia', 'element-plus', '@element-plus/icons-vue'],…"
```

归属 = **我的守卫写错**（只认双引号，而 AIAgent 模板与本插件的 `vite.config.ts` 一贯单引号）；修法 = 正则改 `['""]vue['""]` 两种引号都收，断言语图不变（vue 必须 external）。修后 t7 该类 7 条全绿（`grep -o '…TodoTrackerWebAssetTests'` 在 t7 命中 21 次 = 7 条用例 × 3 引用）。

## Integration Test

结果并入上节（本仓集成测试与单测同工程：`ForgeSelf.Api.Tests/Integration/TodosControllerTests.cs` 45 条在 t7 中全绿）。**Verified**。

## 中档全量后端测试（AC-18）

Command: `cd ForgeSelf.Api.Tests && dotnet test --logger "trx;LogFileName=full.trx"`（不带 filter；日志 `.temp/tmp/fulltest.log`）

Result: **`失败 10 / 通过 2707 / 总计 2717 / 18m54s`（Verified，读数取自 TRX `<Counters>` 与日志正文）**

```text
<Counters total="2717" executed="2717" passed="2707" failed="10" error="0" timeout="0" aborted="0" … />
```

**AC-18 判据（"通过数 += 我新增用例数，失败数不增加"）的对账口径**：本仓没有一份可信的"上一次全量"计数（AGENTS §5.6 引用的项目记忆 `project-baseline-test-reds` 实际不存在 ⇒ 已记 TODO 补建），因此归属改用两份**仓库内已落盘的真实红清单**做基线：`docs/ai/pilot/2026-10-01-plugin-dev-experience/evidence/baseline-test-failures.txt` 与 `docs/ai/pilot/2026-10-02-version-rule-datecode/06-review.md`（明确记录「存量基线家族 7 = WorkflowPlanning×6 + ScriptRunnerDi×1；并行在飞任务 1 = DesignAgentToolContractTests.ExecuteAsync_空参数_不抛」）与 `docs/ai/pilot/2026-10-04-host-install-root-single-source/05-evidence.md`（记录 `HostInstallRootTests` 当时 13/13 绿）。

10 条红的逐条归属（错误消息取自 `full.trx` 的 `<Message>`，不是转述）：

| # | 测试 | 真实错误 | 隔离复跑（判"确定性"还是"并发 flaky"） | 归属 |
| --- | --- | --- | --- | --- |
| 1 | `Plugins.TodoTracker.TodoTrackerWebAssetTests.vite产物名与entry一致_…` | 守卫正则只认双引号，`vite.config.ts` 用单引号 | t7 定向集（修后）**226/226 绿** | **我的**，已修 + 已补跑 |
| 2-7 | `Integration.WorkflowPlanningIntegrationTests` ×6 | `Expected …OK{200}/BadRequest{400} but NotFound{404}`（打 `/api/ai-agent/workflow/*`） | t8 与全量**同样红**（确定性） | 存量基线家族：2026-10-01 已记录、2026-10-02 复述；`AIWorkflowController` 源码里存在（`git grep HEAD` 证），404 成因 = 测试宿主未加载 AIAgent 插件控制器；本批未碰 workflow/AIAgent 任何文件 |
| 8 | `Integration.ScriptRunnerDiIntegrationTests.GetRuntimes_ShouldResolvePluginControllerThroughCordisContext` | 同为 404（`/api/scripts/runtimes`） | 同上（确定性） | 同上基线家族，同一成因族（插件控制器在测试宿主未注册） |
| 9 | `Plugins.DesignSystemTests.DesignAgentToolContractTests.ExecuteAsync_空参数_不抛` | `Expected ….success GetBoolean to be True, but found False` | **隔离跑 4/4 绿**（t10.trx） | 并发/在飞类 flaky，2026-10-02 已按「并行在飞任务」归属；本批未碰 DesignSystem |
| 10 | `Services.HostInstallRootTests.老宿主传错安装根时代理把新版本落到真根且不嵌套` | `Expected r.NewExePlaced to be True …（pwsh exit=-1；stdout=；stderr=；updatesDir 存在=False；无日志文件）` | t9 隔离跑 **13 passed / 1 failed**（同一类，32s 整轮 ⇒ 未触 180s 超时） | 本批改动面外（未碰 `HostInstallRoot.cs` / `scripts/update-agent.ps1` / 该测试），2026-10-04 有 13/13 绿记录 ⇒ 今日为**新增的确定性红，根因待查**：症状 = 被 spawn 的代理子进程既无输出也无日志目录；未手工复现前不写根因（日记已记，`TODO.md` 立 P2 并附复现命令） |

⇒ 结论：**没有一条红来自本批未修的改动**；本批引入的 1 条（守卫正则引号）已修并复跑消失。非本批的 9 条中 8 条与仓库内既有基线记录逐字同名，1 条（`HostInstallRootTests`）为今日新增确定性红、已带原文入 `TODO.md`（P2，来源:输入2 Verify）。

## Static Analysis

| 检查 | 命令 | 结果（来源等级） |
| --- | --- | --- |
| 宿主前端类型 + lint | `cd ForgeSelf.Web && pnpm run check` | **PASS，0 errors / 76 warnings**（warnings 为存量 `no-explicit-any`、`vue/one-component-per-file` 等）（Verified，22:06 复跑；首轮 21:5x 曾在 `e2e/plugins/todo-tracker/todo-tracker.spec.ts:114` 抓到 **1 个类型错误**：把断言消息写成了 `toMatch(regex, message)`，Playwright 的消息只挂在 `expect(value, message)` 上 ⇒ 已改为 `expect(page.url(), '…').toMatch(/\/todo$/)` 后复跑 0 error） |
| 宿主前端单测 | `cd ForgeSelf.Web && pnpm run test` | **Test Files 65 passed，Tests 742 passed / 89s**（Verified，22:06）——含 `src/router/__tests__/dynamicPlugins.test.ts` 9/9（本批把两处夹具视图名从 `TodoView` 改为中性替身 `DemoRemoteView`，并给带 `entry` 的夹具走远程加载分支） |
| 发布链同参数类型检查 | `cd ForgeSelf.Web && npx vue-tsc -b`（build 模式，含 `e2e/**`） | **PASS（0 error）**（Verified；即 `pnpm run check` 内同一条命令） |
| 插件前端构建 | `cd Plugins/TodoTracker/web && pnpm install && pnpm build` | `dist/index.js 64.74 kB`、`dist/style.css 20.12 kB`，20 modules transformed，`✓ built in 988ms`（Verified，22:04 —— 含写串行队列与委派下拉两处改动后的重建） |
| external 自检 | `grep -oE 'from "[^"]+"' dist/index.js \| sort \| uniq -c` | 产物内**只有 1 处裸导入 `from "vue"`**（本插件前端不 import vue-router/pinia/element-plus：提示与确认走插件自带 `notify.ts`，不依赖宿主共享桥）⇒ "external 未漏声明"成立的具体形态与 02-spec AC-14 的字面写法不同，见 06-review 的口径说明（Verified） |
| 导出名对账 | `tail dist/index.js` | `export { An as TodoView, An as default }` == `plugin.json.views[0]`（Verified） |
| 幽灵页/孤儿门禁 | `node ForgeSelf.Web/scripts/check-features.mjs` | ⚠️ **本批不新增不一致；脚本本身在 HEAD 就是红的（基线红）**：读数 **21 处不一致**，其中 16 条是「插件信号不存在：`ForgeSelf.Api\Plugins\X`」——`check-features.mjs:23` 的 `pluginsDir` 仍指向早已搬走的 `ForgeSelf.Api/Plugins`。控制实验（临时指向仓库根 `Plugins/` 后复跑，跑完**已还原**、该文件 `git status` 为空）：16 条假红全消，剩 **12 条**且**无一条与 todo 相关**（3 历史视图迁移遗留 + 2 mcp-center 控制器信号 + 2 孤儿控制器 + 5 孤儿插件目录）。已入 `TODO.md` P2。正向证据：HEAD 的 todo 条目带 `views:['TodoView']`，若不同步删除，本批会**多出**两条不一致（缺失视图 + 孤儿视图），实测两条都不存在（Verified） |
| AC-15「无残留」缺无断言 | `grep -rn "TodoView" ForgeSelf.Web/src/ \| wc -l` 与 `grep -rn "stores/todo\b\|services/todoApi\|components/todo" ForgeSelf.Web/src/ \| wc -l` | 均 **0 命中**（Verified，21:3x 实测两条命令输出都是 `0`）。阳性对照（同两条命令在改名前的真实读数）：第一条 **3 命中**（`features.ts` 注释 1 行 + `dynamicPlugins.test.ts` 两处夹具 `views:['TodoView']`），第二条 **0 命中**（宿主 `src/` 里的真残留早在删除时就不在了，第二条命令从一开始就是 0，不能当对照） |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/todo-tracker e2e/todo.spec.ts e2e/menu-route-consistency.spec.ts --workers=2` | **四跑定档：插件组 8/8 绿**（`e2e-todo7.log`「8 passed (42.6s)」）；三组合跑 **14 passed / 3 failed**（`e2e-todo6.log`），3 条红全在首页面板组且实测"首页未发起 `/api/todos` 请求"⇒ 非本批。详见下节「插件层 e2e（AC-16/AC-17）」（Verified） |
| 工件链门禁 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-07-todo-agent-dispatch` | **PASS**（22:4x 实跑，日志正文 `PASS: 全部 PILOT 工件链齐全（00-07 八件 + 关键节）`；控制台中文按 GBK 显示成乱码，判据取正文语义）（Verified） |

## 插件层 e2e（AC-16/AC-17）— 四跑定档

跑法（`e2e-testing` 技能单配置 + globalSetup 自建宿主/签名/起前后端，地址全部取自 `e2e-env`）：

```bash
pnpm exec playwright test e2e/plugins/todo-tracker e2e/todo.spec.ts e2e/menu-route-consistency.spec.ts \
  --workers=2 --reporter=line --output=<repo>\.temp\pw-out*
```

| 轮 | 范围 | 读数（日志正文） | 结论 |
| --- | --- | --- | --- |
| 第 1 跑 `e2e-todo2.log` | 17 条（三组） | **8 failed / 9 passed** | 4 类成因定位（见下） |
| 第 2 跑 `e2e-todo3.log` | 17 条 | **4 failed / 13 passed** | 插件组只剩 B1；`menu-route-consistency` 4/4 转绿（补建 10 个插件 `web/dist` 后） |
| 第 3 跑 `e2e-todo6.log` | 17 条 | **3 failed / 14 passed** | **插件组 8/8 全绿**；剩 3 条全在 `e2e/todo.spec.ts` 首页面板组（归属见下） |
| 定档跑 `e2e-todo7.log` | `e2e/plugins/todo-tracker` 8 条 | **8 passed (42.6s)** | 含 notify 去重后的重建产物（`dist/index.js 65.48 kB`） |
| 走查后复跑 `e2e-final.log` | 同上 8 条（B1 断言已按走查结论加强） | **7 passed / 1 failed** | 唯一红 = V1（`.tt-item` 等不到）；**这条红是我的用例自己的假绿被"库被清空"揭穿的**，见下节 |
| 终档跑 `e2e-final2.log` | 同上 8 条 | **8 passed (1.8m)** | 修完 V1 判据后的当前有效读数；`e2e-v1.log` 单跑 V1 亦 1 passed (6.7s) |

### 走查之后复跑 e2e，又抓到一条我自己的假绿（V1，已修）

- **现象**：加强 B1 断言之后复跑，V1 由绿转红 —— `locator('.tt-item').first().click()` 等到超时，页面快照实测 **「共 0 条」+「还没有任务」** 空态。
- **成因（读码 + 快照确认，非推测）**：V1 是**用接口**建的任务（绕过界面），而本插件按 §3.4「防闪」要求**不做轮询**，列表只在挂载/「刷新」按钮时取 ⇒ 旧代码里那句 `click('.tt-item')` 点到的**必然是库里别人的残留**。此前它能绿，正是因为前面几轮失败跑留下了未清理的任务；本轮库干净了，假绿就露形。也就是说**旧 V1 的 `v1-detail-open.png` 截的不是本用例建的那条**。
- **修法（两条，都是"错了必红"）**：① 建完任务后**显式点「刷新」**（顺带覆盖了这个用户可见的刷新入口）；② 详情打开后用 `GET /api/todos/{id}` 取回该任务的 `taskKey`，断 `.td-key` **文本等于它**（原来只断"可见"，锁不住"点开的是不是它"）。
- **读图复核**：新 `v1-detail-open.png` 实测列表 1 条 = `E2E-超长标题-…`（单行省略号未撑破卡片），详情头键值 `5efe734ff1534860a44d0c695ef3db77` —— 断言拿的是**接口回读值**、界面显示的是**渲染值**，两个来源对上才算证据；版本徽标 `v1.1.0` 在位。
- **同类风险已扫（逐条对过，不是"应该没事"）**：其余四条（B1/B2/C1/D3）都是**先 `createTask` 再 `openTodoPage`** —— 挂载时列表里已经有这条，`openDetail` 又按**唯一标题** `hasText` 定位，所以不存在"点到别人的行"。只有 V1 是"先开页面 → 再建 → 裸 `.first()`"这个错顺序，全文件 `grep -n 'tt-item' ` 实测仅 V1 一处用裸 `.first()`（现已改为按标题 + 键值锚定）。

### 第 1 跑的 8 条红，逐条怎么定性的（每条都有现场证据）

1. **B1「四栏齐备」不出现（我的真缺陷，已修）**：界面实测文案 `还缺：验收判据、验证命令`，而宿主日志同一秒打出 **4 条 `更新待办，id=2`** ⇒ 四个 PUT 全部落库，是**响应乱序 + `applyUpdated` 用旧快照盖新状态**。修法 = `web/src/http.ts` 写请求串行队列（只串写不串读）。
2. **D3 读到 `先生成提示词`（我的用例竞态，已修）**：点完「生成提示词」立刻读按钮 `title`，此时 `s.preview` 还是 null。修法 = 先 `expect(textarea[aria-label="下发提示词"]).toBeVisible()`；顺手把禁用文案改成四态各说各话（`delegateHint`），并把 `preview.agents` 这个"接口给了但界面没入口"的死数据补成 `data-test="delegate-agent"` 下拉。
3. **H1/H4 `SyntaxError: Unexpected end of JSON input`（我的鉴权改动打到应用层用例，已修）**：本批给 `api/todos*` 全类加了 `[Authorize("ApiKeyPolicy")]`，而该用例自己 `fetch(TODOS_BASE,{method:'POST'})` **不带 token** ⇒ 401 空体。修法 = 统一 `AUTH_HEADER` + 新增 `todoJson()`（空体/非 JSON 直接把状态码与原文抛出，不再让 SyntaxError 糊掉 401）。
4. **`menu-route-consistency ②` 的 `/agent-hub` 停在 `.plugin-view-state--error` + H2/H3/H5 的 `.todo-panel` 不存在（环境缺件，非代码）**：全新 worktree 里 `Plugins/*/web/dist` 是 gitignored ⇒ **一个插件前端产物都没有**（实测 10 个插件连 `node_modules` 都没有），远程入口 404。补跑 `pnpm install && pnpm build` 全部 `Plugins/*/web`（日志 `plugin-webs-build.log`）后，第 2 跑该组即 4/4 绿。另 `global-setup.ts:250` 曾因缺 `System.Data.SQLite.dll` 直接中止（注释声称入库的 `build/runtime/Plugins` 实测不在仓库）⇒ 从本仓 `bin` 复制两件套解封。两条均已入 `TODO.md` 与 `AGENTS.md §5.0`。

### 第 3 跑暴露、当场修掉的两个"看起来对了"的缺陷

- **`REPO_ROOT` 少一层 `../` 造成的自洽假绿**：用例里 `归一后 projectRoot == REPO_ROOT` 两边同源 ⇒ 路径拿到的是 `…\OpenForgeSelf\ForgeSelf.Web`，断言照样绿，直到工件接口回 `该项目没有 docs\ai\pilot 目录` 才暴露。修法 = 仓库根改用**独有文件 `ForgeSelf.slnx` 向上锚定** + B1 开头加"前提自查"阳性对照（`existsSync(<root>/docs/ai/pilot/<task-id>)`）。
- **界面把后端不知道的原因说成知道**：`artifact-sets` 返回空数组时，前端固定显示「该项目下没有 docs/ai/pilot 工件目录」——而后端其实分得开"没目录"与"有目录但没 NN-*.md"。修法 = `http.ts` 新增 `requestEnvelope`，`listArtifactSets` 连 `message` 一起返回，空态优先显示后端原因。
- **toast 叠 4 条挡住刚点过的控件**（截图实测挡住「交给 AgentHub 执行」）：`notify.ts` 同文案同类型去重 + 同时最多 3 条。

### 第 3 跑仍红的 3 条（`e2e/todo.spec.ts` 首页面板组）—— 归属与证据

| 用例 | 现场读数 | 归属 |
| --- | --- | --- |
| H1 | 失败消息实测：**「无 todos 请求；首页共发了 2 个 /api/ 请求」** | 首页**根本没发起 `GET /api/todos`** ⇒ 不是被我的鉴权拒（被拒会留 401 响应记录）。数据链在 `Plugins/Home/web/src/homeStore.ts:init()`（本批零改动） |
| H4 | 实测：**「无 todos 请求；首页共发了 0 个 /api/ 请求」** | 同上，且更彻底：首页一个 `/api/` 都没发 ⇒ Home 页的聚合初始化没跑起来 |
| H2 | 弹窗选择器已按现状修正（`.todo-modal`/占位符「待办标题」/按钮「保存」，实测弹窗**能开能填**），失败点后移到 `waitForResponse(POST /api/todos 201)`；宿主日志里**没有**这次 `创建待办`，而 Home 的 `addTodo` 是 `catch { /* 错误静默 */ }` | 最终原因（客户端未发 or 被静默 catch 吞掉）需专项排查；已入 `TODO.md` P2。**与本批无因果**：同一 token 在同一轮里被插件组用例正常用于 201 建单（D2 绿） |

> 说明：H2 的旧选择器（`.el-dialog` + 「请输入待办标题」+「创建」）与 `Plugins/Home/web/src/HomeView.vue:569-597` 的现状（`.todo-modal` + 「待办标题」+「保存」）不一致 ⇒ 该用例自 Home 界面迁插件后就是陈旧红。本批因为要改这个文件（鉴权 + 迁移口径），顺手把选择器对齐现状，并在此留痕（属边界触碰，不是本批功能）。

## 用户视角走查（plugin-development §四④ + §3.4 清单；2026-10-07 深夜，dev 态预走查）

**定性**：这是在**本地预览实例**（隔离数据根 `.temp/preview-data`，插件产物 = 本仓 `bin/Debug` 的构建输出）上用真浏览器（chrome-devtools）跑的走查，**不冒充 §四④ 的"发布产物 + 隔离实例"走查**（那一步仍在发布之后）。但它按 §3.4 清单逐项核对，**当场抓到一个 e2e 全绿也没发现的功能缺陷**。

### 抓到的缺陷：显式「关联项目」没登记宿主档案 ⇒ ProjectId 落 0（已修）

- **症状（浏览器实测）**：点「关联」后，列表项显示项目路径，**详情面板却仍说「未关联项目」**；项目过滤下拉里根本不出现该项目（下拉只有「全部项目」）。
- **实测数据**：`GET /api/todos/2` 回来 `projectId=0` 而 `projectRoot="C:\Users\…\OpenForgeSelf"`、`projectPathRaw="/c/Users/…"` ⇒ 根存了、id 没存。界面按 `projectId` 判关联、列表按 `projectRoot` 显示 ⇒ 两边分叉。
- **影响面**：项目过滤筛不到、`ListProjects` 的 taskCount 恒 0、"一致的路径认为是同一个项目"（用户输入1 的核心要求）**在数据层没落地**。
- **根因（读码确认，非推测）**：`TodoService.ApplyProjectFields` 无条件用 `registerIfMissing: false` 调 `Resolve`，而它同时服务**建单**（不该登记）与**显式关联**（必须登记）两个入口 ⇒ 宿主没有该目录档案时 `hit=null` → `ProjectId=0`。
- **为什么测试没抓到**：`TodoDispatchFlowTests` 的"四种写法"用例直接调 `_projects.Resolve(form, registerIfMissing: true)`，**绕过了服务入口**；且断言是 `Distinct().HaveCount(1)` —— **四个 0 也算"只有一个不同值"**（自洽假绿）。e2e B1 只断言了 `projectRoot`，没断言 `projectId` 与详情面板状态。
- **修法**：`ApplyProjectFields(todo, path, projectId, bool registerIfMissing)`；建单传 `false`、`LinkProjectAsync` 传 `true`。
- **补的反向守卫（三条，都是"错了必红"）**：
  1. 新用例 `点关联应登记宿主档案并让任务拿到真实项目Id`（走服务入口，自带注册表避免 static 污染）：断 `ProjectId > 0`、档案数 +1、换写法再关联命中同一 id 且不新增、建单入口仍不登记（`ProjectId == 0` 且档案数不变）。
  2. 原"四种写法"用例加阳性对照：`ProjectId` 必须**逐个 > 0**，再断 `Distinct()==1`。
  3. e2e B1 加 `.td-proj-line b` 可见断言 + `linked.projectId > 0`。
- **复跑读数**：`dotnet build` 0 error；定向集 `t11.trx` 曾 **225/2**（两条都是我自己新用例的断言写错：casual 断 0 却命中已登记档案、以及往 static 注册表里登记污染了别处断言）⇒ 改为自带注册表 + 调整断言顺序后 `t12.trx` **227 passed / 0 failed**（含新增守卫）。
- **浏览器内复验（修复后同一实例）**：关联后详情面板显示 **`OpenForgeSelf` + 规范根 + 「你的写法：/c/Users/…」**，toast 端出归因文案，**项目过滤下拉出现 `OpenForgeSelf（1）`** ⇒ 两个症状同时消失。

### §3.4 清单逐项核对（全部浏览器实测，Verified）

| 清单项 | 实测证据 |
| --- | --- |
| 点即保存是否真落盘 | 连续填四栏（`fill_form` 一次灌四栏）⇒ 界面翻成「四栏齐备，可下发」，**下发按钮由 disabled 变可点**；`还缺：…` 红字消失；列表项同步显示 objective。这是写串行队列修复后的真实形态（修复前实测"四个都落库、界面仍说缺两栏"） |
| 操作成败可见 | 关联/导入/下发/补记/流转/完成/删除**每一步都有 toast**，且失败原因原文端出（实测 Blocked 无原因 ⇒ 「进入「阻塞」必须写明阻塞原因（缺什么、要谁处理）」且**阶段未被改动**，不静默） |
| 破坏性操作二次确认 | 覆盖正文：弹层 + 文案「这是覆盖动作，不是追加；确认前原文仍在，确认后不可自动还原。」；标记完成：弹层；删除：弹层 + 「该任务的 7 条执行记录会一并删除，删除后不可撤销。」 |
| 取消路径真的没动手 | 删除点「取消」后列表仍是 2 条（含本任务）；点「确认」后剩 1 条 + 「已删除」 |
| 空态分级 | 未选中任务 / 未关联项目 / 无执行记录 / 没点列目录，四种文案各不相同（快照逐条读到） |
| 筛选 + 分页边界 | 点「完成」chip ⇒ 计数文案「共 1 条 阶段「完成」 （筛选中）」且命中 1 条；清空搜索后回到「共 2 条」；关键字「走查-WALK」精确命中 1 条 |
| 长文本/窄屏 | 900px 宽下 `scrollWidth == clientWidth == 902` ⇒ **无横向溢出**；taskKey 与长路径均单行省略且带 title |
| 工件导入链路 | 列目录返回 **24 个**工件目录（含本任务目录）；默认勾选核心四件 `01/02/03/04`；导入后正文 **52,212 字符**、含 `### 0` 分节、来源行写明 `## 来源工件：2026-10-07-todo-agent-dispatch（项目 C:\…）` |
| 下发提示词 | 长度 53,596；含 `Bearer <token>` 占位、含 `/api/todos/by-key/{taskKey}/records` 回报契约、含验证命令原文；**无真实密钥** |
| 执行记录台账 | 补记后时间线 6 条、首条为 `#6 走查：浏览器端跑一遍主链路 manual`，改动文件渲染成 2 个 `code`（含 M 前缀），验证栏原文可见 |
| 阶段流转 | 已下发 → 执行中（toast「已流转：已下发 → 执行中」）→ 标记完成 → `.td-stage` = 完成 |
| 版本徽标 | 全程 `v1.1.0`（与 plugin.json 一致） |
| 控制台 | 整轮走查 `list_console_messages(error,warn)` = **0 条** |
| 测试数据清理 | 只删自己建的 `走查-WALK-*`；用户在此期间自己建的「测试」任务**原样保留**（未动） |

**截图**：`ForgeSelf.Web/screenshots/e2e/todo-tracker/walk-detail-dispatched.png`（关联 + 导入 + 下发后的详情，逐张读过）、`walk-narrow-900.png`（900px 窄屏）。

### 走查遗留的一处未定性观察（不写根因）

`walk-detail-dispatched.png` 里，详情面板的项目名粗体行显示的是**截断路径** `…r-cn\worktrees\app\74f462\OpenForgeSelf`，而**关联那一刻**的 DOM 读数是项目名 `OpenForgeSelf`；列表项同样显示截断路径。代码上 `TodoService.GetTodosAsync` 有批量取名（`_projects.NamesFor(list.Select(t => t.ProjectId))`）、单条写路径也带 `ProjectName(...)`，所以"名字从哪一步开始丢"未定位。已入 `TODO.md` P3（复现路径：关联 → 刷新列表 → 读 `.td-proj-line b` 与 `.tt-proj` 文本，并看 `GET /api/todos` 响应里 `projectName` 是否为空）。属显示层退化（下一行仍完整显示规范根），不影响过滤/计数正确性。

## Screenshots

`ForgeSelf.Web/screenshots/e2e/todo-tracker/`（定档跑 `e2e-todo7.log` 产物，7 张，逐张读图核对 Level 3）：

| 文件 | 读图结论（图标/间距/颜色/留白/对齐/遮挡/溢出/数值自洽/版本徽标） |
| --- | --- |
| `a1-remote-loaded.png` | 宿主壳 + 插件页渲染正常；徽标 **v1.1.0** 与 `plugin.json.Version` 一致（铁律 13） |
| `b1-dispatch-preview.png` | 工件清单显示 `02-spec.md 32.1 KB 核心` 等真实大小与"核心"标 ✓；来源 `docs\ai\pilot\2026-10-07-todo-agent-dispatch` ✓；提示词框内 `# 任务 04883be…`、`项目路径: C:\Users\…OpenForgeSelf` 与"你的写法 `/c/Users/…` 归一"回显 ✓；**读图发现的缺陷**：4 条「已保存」toast 叠在右下挡住「交给 AgentHub 执行」⇒ 已修（去重 + 上限 3） |
| `b2-execution-timeline.png` | 执行记录 **3 条**与用例预期一致（Draft→Ready、下发、手工补记）；`#3 跑定向后端测试 manual` 下两条 `code`（改动文件）✓；`#2 下发任务 rest · Ready→Dispatched` 结果「验证命令 1 条、判据 1 条」✓ 数值自洽；阶段 chips 为中文（执行中/阻塞/就绪/已取消）✓ |
| `d3-delegate.png` | 提示词已生成；黄色 warning「未关联项目：交给 AgentHub 时将使用 agent 的默认工作目录」✓；toast 端出后端原文「一键交给 AgentHub失败：AgentHub 运行时不可用（插件未正确加载）」⇒ **U-2 的现场答案**：本 worktree e2e 实例里一键委派是**如实失败**，不静默 |
| `v1-list-default.png` | 过滤 chips 一行、阶段中文、右侧空态「未选中任务」分级文案 ✓；「新建」按钮在未填标题时 disabled ✓；此张徽标为「加载中…」（异步未回）⇒ 徽标以 A1 断言与 b1/d3 截图为准 |
| `v1-detail-open.png` | 详情块分组清晰；项目路径空态文案 + 关联按钮 disabled ✓；「四栏齐备，可下发」绿色与数据一致 ✓；taskKey 单行截断不破版 ✓ |
| `v1-long-title.png` | 56 字长标题单行省略号、未撑破卡片（用例另测 `boundingBox` 宽度 ≤ 行容器）✓；红字「还缺：可验证目标、任务正文、验收判据、验证命令」✓ |

**未消除的遮挡（记录为已知限制）**：toast 浮层仍固定在右下角，最多 3 条时会压住详情面板右下约 1/5 高度的内容；位置改成宿主同款（顶部居中）属另一批 UI 收口，本批不动。

## 提交与集成（闸门3 的"提交"步，2026-10-08 输入1 授权）

分支 `feat/todo-agent-dispatch`（原 HEAD 是 detached `a1f7ce8` ⇒ 先建分支再提交，避免提交落在游离头上丢失归属）。
按归属拆 **10 个 commit**（`git log --oneline github/main..HEAD` 原文）：

| # | 哈希 | 内容 |
| --- | --- | --- |
| 1 | `8a0c839` | 数据层：Model.xml + xcode 生成物 + TaskExecution 实体/Tables.cs/建表页（7 文件） |
| 2 | `66dba35` | Abstractions 委派契约 + AgentHub 提供方与注册（3 文件） |
| 3 | `5366fd8` | 插件后端服务/控制器/DTO（30 文件） |
| 4 | `8410404` | 5 个 agent 工具 + 插件装配 + `plugin.json` 1.1.0（4 文件） |
| 5 | `42af08e` | 插件自带前端 `web/`（16 文件，`node_modules/`、`dist/` 由插件自身 .gitignore 排除） |
| 6 | `f06ed9b` | 宿主内置待办页面/组件/store/api 删除 + 路由与 features 接管（13 文件） |
| 7 | `31cfafb` | 后端用例 7 新一改 + 插件层 e2e + 应用层用例鉴权对齐（10 文件） |
| 8 | `51bcad0` | PILOT 工件 00–07（pre-commit 工件门禁 PASS） |
| 9 | `c1d39ea` | 功能文档 / 能力接缝 §7 / 未采纳决策 4 条 |
| 10 | `d23d888` | 技能与 AGENTS 回写（预览、走查、e2e 取证锚定、worktree 环境前置） |

**未提交（有意排除）**：`build/runtime/Plugins/{System.Data.SQLite.dll,e_sqlite3.dll}` —— 06-review 批准条件 3 明写"不得顺手 git add"，是否入库仍待拍板。
`.temp/`、`screenshots/`、`*.trx`、`*.log`、`.forgeself/`、`TODO.md` 由仓库 `.gitignore` 挡下（实测 `git check-ignore -v` 逐条命中）。

**与远程的集成**：`git fetch` 后本地 `main`/`github/main` 已领先 4 个 commit（工具桥 PILOT-053、签名时间戳多点回退、AGENTS §2.5、agent-workflow 拆分）。
顺序是**先提交再 `git rebase github/main`**（保持线性历史；脏工作区直接 rebase 需 autostash，75 项改动不该冒这个险）。
冲突 1 处：`docs/07-decisions/not-taken-decisions.md` —— 远程把条目编号排到 039，本批原来是"日期标题 + 1)2)3)4)"的写法；
按该文件既有约定改为 **040–043** 四条并保留原文，未丢任何一方内容。rebase 结果：`0 behind / 10 ahead`，`git diff --shortstat github/main..HEAD` = **99 files, +13569 / −1634**。

**rebase 换了基线 ⇒ 快档门禁重跑（全部 Verified，看日志正文/TRX 计数，不看 exit code）**：

| 门禁 | 读数 | 证据 |
| --- | --- | --- |
| `dotnet build ForgeSelf.Api` | **0 错误** / 1343 警告（存量 nullable 噪声） | `build-postrebase.log` 正文 |
| 后端定向测试 | **227 passed / 0 failed** | `post-rebase2.trx` `<Counters>`；阳性对照 `grep -c 类名`：TodoDispatchFlow 69 / ProjectPathCanonicalizer 96 / TodoAgentDelegation 24 / TodoTrackerWebAsset 21 |
| 插件层 e2e | **8 passed (1.3m)** | `e2e-postrebase.log` 正文 |
| 宿主前端 `pnpm run check` | **0 error / 76 warning** | `check-postrebase.log` |
| 宿主前端 `pnpm run test` | **742 passed (65 files)** | `test-web-postrebase.log` |

**这一轮重跑当场抓到的一个坑**：第一次跑定向测试用了 `--filter "FullyQualifiedName~A|~B|~C"` 的简写 ⇒ vstest 报
`TestCaseFilter 错误: 无效条件"~AgentHub"`，**TRX 计数 total=0 而 exit code 是 0**（`post-rebase.trx` 实证）。
每个条件都必须自带 `FullyQualifiedName~` 前缀。这是"exit code 不是证据"的又一个现场样本，已按此重写命令。

**未跑（如实标注）**：中档全量 `dotnet test`（2717 条）与深档全量 e2e 在 rebase 后**未重跑**；打 tag/发布、
发布产物版隔离实例走查、运行实例只读复验仍未做（需授权）。

## Known Limitations

- 路径同一性只在 todo-tracker 入口成立：宿主 `HostProjectRegistry.Register` 仍按 `Path.GetFullPath` + 精确匹配（用户拍板"插件内部支持多种格式"），故 AIAgent/sems 若写入怪异写法仍可能形成第二条宿主档案；插件比对侧对双方都归一，读取侧不会误判（`docs/07-decisions/not-taken-decisions.md` 第 **040** 条）。
- 符号链接 / junction / 8.3 短名 / 网络盘映射不判为同一目录（需 OS 互操作，代价与收益不对称）。
- 不做 agent 回报**自由文本**解析（无真实样例，U-1）；只收结构化入参。
- 插件前端 `web/` 无 vitest 配置，纯函数单测落在后端 xUnit 侧 + e2e（偏差记录 20:30）。
- 一键委派依赖 `agent-hub` 在场且 agent 已注册、cwd 在其白名单内；缺席时 503 原文（不是静默失败）。
- `Content` 列宽 262144：以 12,000 字符真实往返测试证明不被裁短（AC-3），未测 >200,000 字符的极端导入（导入服务本身按 200,000 字符上限拒）。

## Unresolved Issues

1. **交付五步做到 ①② + dev 态预走查 + 提交**（plugin-development §四）：① 门禁 ② 插件层 e2e 已跑（rebase 后重跑仍绿，见上表），走查已在 dev 预览实例补做一遍（抓到 ProjectId 缺陷），**提交**已按 10 个 commit 落到 `feat/todo-agent-dispatch` 并 rebase 到 `github/main`；③ 发布（打 tag / 本地目录更新源）④ **发布产物版**隔离实例走查 ⑤ 运行实例只读复验 **仍未做** —— 需用户授权（AGENTS 发布规范：agent 不得停/启/杀宿主，也不得擅自打 tag 发布）。⇒ 任务状态 🟡 PARTIALLY_COMPLETED。
2. **鉴权变更的影响面未完整证伪**：`Home` 面板现在不发 `/api/todos`（实测 H1 共 2 个、H4 共 0 个 `/api/` 请求），所以"Home 消费 todo API 会不会被新鉴权静默打断"仍是 **Unknown**；`addTodo` 的 `catch {}` 让它在界面上也不可见。已入 `TODO.md` P2（含复现命令与读图/日志证据）。
3. **`HostInstallRootTests` 一条"曾绿今红"**：本批无因果但今天确定性复现（`t9.trx` 13/1），根因**未查**（不写根因）；附带风险是它会对传入 PID 走 `Stop-Process -Force`，多会话并发机器上可能反杀别人的进程。已入 `TODO.md` P2。
4. **环境/工具债 4 条仍在**：`check-features.mjs` 的 `pluginsDir` 过期；`global-setup` 的 SQLite provider 候选源不在仓库（本批临时放了两个**未跟踪**二进制进 `build/runtime/Plugins/`，是否入库需拍板）；`Plugins/*/web/dist` 需手工构建才能跑插件 UI e2e；AGENTS §5.6 曾指向一份不存在的基线记忆（本批已在项目记忆里建立）。
5. **未测边界**（Known Limitations 已列）：>200,000 字符导入、符号链接判同、多宿主进程真实并发领取。
