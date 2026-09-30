# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。每项标注来源等级：Verified / Inferred / Unknown。

## Task

PILOT：sems 插件自洽化 + 经 MCP 中心对外提供工具（2026-09-28）

## Changed Files

业务代码：
- `ForgeSelf.Abstractions/IProjectRegistry.cs` — +`Register(root, source, out err)` / `Remove(id, out err)`（旧 2 参 Register 保留委托）
- `ForgeSelf.Api/Services/HostProjectRegistry.cs` — Source 按来源写入（空→ai-agent）；同 Root 重登记只刷 LastActiveAt（修 Name 覆盖缺陷）；Remove 级联删命令
- `ForgeSelf.Api/Entities/RunCommand.Biz.cs` — +`DeleteByProjectId`（直查库不走缓存）
- `Plugins/Sems/Services/ProjectService.cs` — 重写：`SemsOpStatus`/`SemsResult<T>` 一处真相；IProjectService 扩为读写全量（Register/Update/Remove/命令 CRUD/Browse）；RemoveProject 带运行中守卫（409）；GetProjects 逐项补 Commands（修统计/快捷访问/启动全部静默失效缺陷）
- `Plugins/Sems/Controllers/ProjectsController.cs` — +`POST api/projects`、`DELETE api/projects/{id}`、`GET api/projects/browse`；改注入 IProjectService；类级 `[Authorize("ApiKeyPolicy")]`
- `Plugins/Sems/Controllers/ProjectCommandsController.cs` — 收口到 IProjectService（路由不变）
- `Plugins/Sems/Controllers/RunsController.cs` — 注释标注 RunnerController 移除
- `Plugins/Sems/Controllers/RunnerController.cs` — 删除（移入 `.trash/Plugins/Sems/Controllers/`，全仓 grep `api/runner` 零代码引用）
- `Plugins/Sems/ToolExtensions.cs`（新）— 13 个 `sems_*` 工具（list/register/update/remove 项目、list/add/update/delete 命令、list/check/run/stop 运行），基类统一参数解析/异常兜底/用量上报
- `Plugins/Sems/SemsPlugin.cs` — Apply 注册 13 工具
- `Plugins/Sems/plugin.json` — 1.0.3 → 1.1.0

前端（`Plugins/Sems/web/`，出树构建）：
- `DirectoryPickerDialog.vue`（新）— 驱动器/子目录浏览 + 手工路径 + 可选名称
- `confirmOps.ts`（新）+ `confirmOps.spec.ts`（新）— 二次确认编排（取消→零请求），ElMessageBox 消息文案
- `SemsView.vue` — 添加项目/移除（二次确认）/空态分级（加载/错误重试/无项目引导）/版本徽标 v1.1.0/运行中统计
- `ProjectCard.vue` — 移除按钮（danger 样式）
- `CommandList.vue` / `RunPanel.vue` — window.confirm/alert → ElMessageBox/ElMessage + confirmOps；RunPanel `refresh()` 返回条数、空态分级
- `http.ts` / `types.ts` — registerProject/removeProject/browseDirectories/fetchPluginVersion + 新类型
- `package.json` — 1.1.0 + vitest ^3.2.4 + test script

测试：
- `ForgeSelf.Api.Tests/Services/HostProjectRegistryTests.cs` — +6 例（共 17）
- `ForgeSelf.Api.Tests/Plugins/Sems/ProjectServiceTests.cs`（新，18 例）
- `ForgeSelf.Api.Tests/Plugins/Sems/SemsToolExtensionTests.cs`（新，14 例）
- `ForgeSelf.Api.Tests/Plugins/Sems/SemsControllerTests.cs`（新，16 例）
- `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts` — 重写为 4 用例：远程加载冒烟+版本徽标；UI 全链（选目录→登记→加命令→启动→停止→取消移除→确认移除，零 mock 真后端）；经 MCP 网关真调 13 工具（register/list/list_tools 发现/命令 CRUD/必填拒绝/已删命令报错/移除后磁盘仍在）；无 token 401 矩阵

文档：
- `docs/02-features/028-project-workspace.md` — 按 v1.1.0 重写（含 RunnerController 收敛说明与历史遗留）
- `docs/07-decisions/not-taken-decisions.md` — +009~014（sems 不做数据 owner / 不提供任意脚本执行工具 / 不做软删除 / 保留 route-menu / AIAgent browse-directories 404 延后 / 不做运行会话持久化）

宿主与其他插件：**零改动**（McpCenter、ExtensionPointManager、AIAgent 均未动）。

## Build

Command:

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
```

Result: PASS（来源等级：Verified）

```text
警告 823 个（全部存量）；错误 0。13.96s（首次基线）→ 后续多轮重建均 0 错误。
```

Command（插件前端出树构建，技能 §3.2 兜底）:

```bash
cd Plugins/Sems/web && pnpm run build
```

Result: PASS（来源等级：Verified）

```text
dist/index.js 40.26 kB + style.css 19.83 kB，22 modules。
裸导入自检：from "vue"=1、from "element-plus"=1（未内联副本，宿主 import map 正常接管）。
```

## Unit Test

Command:

```bash
dotnet test --filter "Sems|HostProjectRegistry"
```

Result: PASS（来源等级：Verified）

```text
85/85 通过（HostProjectRegistry 17 + ProjectService 18 + SemsToolExtension 14 + SemsController 16 + RunnerService 7 + ProcessMatch 8 + SemsSharedContract 4 + 其余 1）。
```

Command（宿主前端 + sems 插件前端 vitest）:

```bash
cd ForgeSelf.Web && pnpm run check && pnpm run test
cd Plugins/Sems/web && pnpm exec vitest run
```

Result: PASS（来源等级：Verified）

```text
check：0 errors / 81 存量 warnings。
宿主 vitest：43 files / 473 tests 全绿（独占跑；此前 2 项 SettingsView 超时为并发抢 CPU 假红）。
sems confirmOps vitest：8/8 通过（含「取消→零请求」）。
```

## Integration Test

全量后端回归（独占跑）：

```bash
dotnet test   # ForgeSelf.Api.Tests，独占、无并发前端门控
```

Result: PASS（附存量已知红清单，与本次无关）（来源等级：Verified）

```text
1533 通过 / 8 失败 / 1541 总计（6m4s）。
8 项失败逐条归因为存量批次E（docs/ai/pilot/batch-a-menu-route-consistency/05-evidence.md:15 同名单）：
- TerminalCommandGuardTests.Check_EncodedCommand_DestructivePayload_Rejected：护栏行为正确（reason 含 Remove-Item），断言 "remove-item" 大小写敏感 → 测试自身缺陷
- WorkflowPlanningIntegrationTests ×6 + ScriptRunnerDiIntegrationTests ×1：WebApplicationFactory 测试宿主插件控制器 404（csproj Stage 目标同注释描述的已知无效模式）
sems 相关 85/85 全绿，零新增失败。并发试跑曾报 25 失败，独占重跑回落 8 —— 已在日记记录教训：门控必须独占。
```

## E2E

Command:

```bash
cd ForgeSelf.Web && PLAYWRIGHT_BROWSERS_PATH="$LOCALAPPDATA/ms-playwright" \
  bash node_modules/.bin/playwright test --config=playwright.config.ts e2e/plugins/sems/sems.spec.ts
```

Result: PASS（来源等级：Verified）

```text
4 passed (1.5m)：smoke（远程加载+版本徽标 v1.1.0）/ UI 全链（选目录→登记→加命令→启动→停止→
取消移除→确认移除）/ MCP 链路（经网关真调 13 工具：发现、登记、list、命令 CRUD、必填拒绝、
已删命令报错「命令不存在」、移除后磁盘仍在）/ 无 token 401 矩阵。
```

迭代过程（每轮失败均定位到真实缺陷并修复，非测试宽容化）：
1. 首轮 `.sems` 不渲染 → **缺 auth 注入**：sems 是唯一没 `injectRealApiKey` 的 UI e2e，`/api/plugin/frontend-manifest` 401 → 动态路由未注册 → `/sems` 空 main。补注入后过。
2. MCP 登记工具报 `No service for type 'IServiceScopeFactory'` → **真实产品缺陷**：工具基类把插件 IContext 当 scope factory。修 `Plugins/Sems/ToolExtensions.cs`（经 Context 解析宿主 IServiceProvider 再 CreateScope）+ 回归测试，31/31 绿。
3. 保存命令后卡片折叠 → **真实 UX 缺陷**：每次刷新 `loading=true` 使 `v-if` 整体换掉网格 → ProjectCard 重挂载丢失展开态。改为占位仅在无数据时显示。
4. 启动后运行面板恒空、每次启动必 409 → **真实产品缺陷**：`CommandList.run` 已 POST 并 emit，`SemsView.runOne` 又 POST 一次 → 后到请求 409 → catch 路径 → 刷新永不执行（POST body pid + 直连 GET /api/runs total=1 取证）。runOne 改为仅刷新。
5. 偶发 SQLite `database is locked` → 500（并行 worker 共享同一临时宿主）→ `test.describe.configure({ mode: 'serial' })` + `--workers=1` 后稳定。
6. 全局命令统计断言 `1` 遇并行抢跑得 `2` → 改为基线相对断言（`baselineCmds + 1`）。
7. 第 10 轮冒烟偶发 401（token 在位）单跑复绿；已在 attachCollectors 增加全量非 2xx `/api` 请求取证，未再复现，根因未定（记入 Known Limitations）。

另修正测试侧断言两处（非产品缺陷）：MCP 信封为 `\uXXXX` 转义文本，`toContain('命令不存在')` 需先 JSON.parse；MCP 链路首轮失败系把网关错误信封误读为空数组。

环境事实（Verified）：本 worktree 无 `.playwright-browsers`，以 `PLAYWRIGHT_BROWSERS_PATH=$LOCALAPPDATA/ms-playwright`（chromium_headless_shell-1228）覆盖；globalSetup 所需 `<repo>/publish/{System.Data.SQLite.dll,e_sqlite3.dll}` 从 `build/runtime/Plugins/` 拷入（publish/ 已 gitignore）。首次 MCP 链路用例失败为**测试侧解析缺陷**（把网关错误信封误读为空数组）；宿主侧 backend.log（.temp/e2e/2026-09-28T05-45-23/backend.log）已证实 13 个 `sems_*` 全部注册成功（`[ToolRegistry] 注册工具成功: sems_*`，插件 v1.1.0 加载）。修正断言（原始报文进失败消息、宽容解析 toolsOf、sems_register_project 前置）后重跑。

## Regression（AC18 相邻用例）

Command:

```bash
cd ForgeSelf.Web && PLAYWRIGHT_BROWSERS_PATH="$LOCALAPPDATA/ms-playwright" \
  pnpm exec playwright test e2e/menu-route-consistency.spec.ts e2e/plugins/mcp-center/mcp-center.spec.ts --workers=1
```

Result: menu-route-consistency **4/4 PASS**（补 dist 后连续两轮稳定）；mcp-center **3/4**（来源等级：Verified）

```text
1) 前置环境修正（两次）：
   a. 首轮红含 401/空 manifest → .temp/e2e/2026-09-28T16-18-22/backend.log 实锤
      `Failed to bind to address http://0.0.0.0:7102: address already in use`
      （僵尸临时宿主占端口，新宿主崩溃、测试误打旧实例；端口释放后不再复现；
       此根因同时解释 sems 冒烟第 10 轮偶发 401）。
   b. 持续红为 agent-hub 错误态 DOM 明文 `Failed to fetch dynamically imported module:
      .../plugins/agent-hub/web/dist/index.js` → 本 worktree 新检出缺 gitignore 的
      插件 web/dist 构建物 → 逐个补建 7 个（AgentHub/McpCenter/AIAgent/DesignSystem/
      Home/ImGateway/QuickLinks）后 menu-route 4/4 绿 ×2。
   两项均为环境缺口，非本次改动、非产品缺陷；已记 TODO（新 worktree e2e 前置检查单）。
2) mcp-center 212（/mcp-center 界面渲染）套内必红、单跑必过（单独执行 1 passed 40.8s）：
   失败形态 = SPA main 空（manifest/动态路由 5s 内未就绪），属套内顺序/冷启动等待过紧的
   存量 flake——同环境 menu-route ② 真实导航 /mcp-center 渲染通过；批次A 证据（09-27）
   已有 mcp-center 存量红先例。与本次改动无关，记 TODO P2（sems 任务不动他人 spec）。
```

## Static Analysis

Result: PASS（来源等级：Verified）

```text
pnpm run check（ForgeSelf.Web）：vue-tsc 0 errors；eslint 0 errors / 81 存量 warnings（本次未新增）。
dotnet build：0 错误；CA1416（WMI）维持既有文件作用域 pragma，未新增抑制。
```

## Publish（运行实例插件侧载，2026-09-29 用户指令）

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish-plugin.ps1 `
  -Plugin Sems -PluginsRoot "D:/src/tools/ForgeSelf/Plugins" -Force
```

Result: PASS（exit 0；来源等级：Verified）

```text
落盘：Plugins/Sems/versions/1.1.0/（Sems.dll+Sems.deps.json+plugin.json+web/dist+System.CodeDom/System.Management+runtimes）
      + _backups/sems/1.1.0/（stage 区）；在用 1.0.3 原样复制成 versions/1.0.3/ 作回滚位。
产物确认：probe-dll-string versions/1.1.0/Sems.dll sems_register_project → FOUND；sems_stop_run → FOUND。
现场实验：只改 current 指针（1.1.0↔1.0.3），GET /plugins/sems/web/dist/index.js 伺服字节数即 40184↔30220 变化
         → 指针是热开关；而 GET /api/plugin 仍报 version=1.0.3（内存程序集未换）→ 交付态保持 current=1.0.3，
           激活（current=1.1.0 或 POST /api/plugin/update/sems）待宿主侧本次改动部署后由用户决定。
未清残留：versions/1.1.0 与 _backups/sems/1.1.0 内含 ForgeSelf.Abstractions.pdb / ForgeSelf.Core.pdb
         （publish-plugin.ps1 共享程序集过滤只匹配 .dll，漏 .pdb）；agent 删工作区外文件被安全策略拦下 → 记 TODO P2。
未做（按用户「只做这一步，别的不用你管」）：未动宿主进程、未部署宿主二进制、未触发插件热切换。
```

## Post-Sync Re-Verification（基线 `b357a32`，2026-09-30）

四次快进同步（`78d065c → 81b9609 → 8acac95 → f501077 → b357a32`，共 18 个上游提交）后重跑门禁（来源等级：Verified）：

| 门禁 | 真实输出 |
|------|----------|
| `dotnet build ForgeSelf.Api` | 0 错误 |
| `pnpm run test`（宿主 vitest） | 46 files / 490 tests 全绿 |
| `pnpm run check` | 1 error = 上游 `ForgeSelf.Web/e2e/global-setup.ts:183` `preserve-caught-error`（非本任务文件）；本任务 `sems.spec.ts:171` `no-useless-assignment` 已修 |
| sems 过滤集 `dotnet test`（独占） | **失败 0 / 通过 86 / 总计 86**（1m2s） |
| sems 定向 e2e（新动态端口基建首跑） | **4 passed (1.7m)**：`.temp/e2e/wt-ae077d4f`、宿主 exe 已签名、MCP 网关端口 **19352** |

上游 PILOT-050 带来的两项适配（已随本任务完成）：
1. `sems.spec.ts` 后端地址改取 `e2e/helpers/e2e-env.ts` 的 `backendUrl()`（新规：spec 禁止硬编码 7102/7002；MCP base 仍读 `FORGESELF_MCP_GATEWAY_PORT`，与上游 `mcp-center.spec.ts` 同款）。
2. pilot 目录日期前缀新规注明「旧目录不回溯重命名」→ 本任务 `docs/ai/pilot/sems-selfcontained-mcp-tools/`（09-28 建）保持原名。

先前两遍全量里的 `RunnerServiceTests` 2 例红（`taskkill /T /F` 返回非 0）在独占跑下 **86/86 全绿** → 定性为并发/负载下的外部进程终止时序 flake（两遍都紧接 26–34 分钟全量之后），非代码回归、非本任务引入。

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/sems/sems.png` / `sems-after.png`（冒烟用例 fullPage 截图，含版本徽标/添加项目按钮）
- UI 全链与 MCP 链路用例产物：`ForgeSelf.Web/screenshots/e2e/sems/` + `.temp/e2e/<timestamp>/`（backend.log 等取证）

## Known Limitations

- 宿主重启丢 Launched 运行态（设计如此，NFR 已接受项）；「检查」可恢复 Detected 捕获。
- `sems_list_runs` 返回实时状态，无历史持久化（决策 014）。
- AIAgent 选目录弹窗 `/api/project/browse-directories` 404 为**存量缺陷**，不在本次范围（决策 013，TODO 已记 P2）。
- 全量 dotnet test 8 项存量红（批次E），属并行会话/在制品，本次不动。
- 冒烟用例出现过一次偶发 401（token 在位、单跑复绿，未再复现）：取证钩子（全量非 2xx `/api` 请求记录）已在位，若复现可按 evidence dump 定位。

## Unresolved Issues

- e2e 已回填（4/4 绿）；回归用例（menu-route-consistency、mcp-center）见日记与下方补充。
