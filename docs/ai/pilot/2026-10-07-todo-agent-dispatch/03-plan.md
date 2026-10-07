# Plan

> 阶段：Stage 3｜具体到真实文件路径。Task ID：PILOT-054
> 顺序原则：**实体先行**（Model.xml → xcode → 对账）→ 契约 → 后端服务/控制器/工具 → 插件前端 → 宿主清理 → 测试与 e2e → 文档。每步改完即可跑对应检查命令。

## Files To Change

### A. 契约层（跨边界，architecture-design 铁律 2）

- file: `ForgeSelf.Abstractions/AgentDelegationContracts.cs`（新增）
  reason: 新增 L1 能力接缝 `IAgentDelegation{ AgentDelegationOutcome Submit(AgentDelegationRequest); AgentDelegationSnapshot? Find(string taskKey); }` + DTO（`AgentDelegationRequest{Prompt,AgentId?,Cwd,PermissionMode,CreatedBy}`、`AgentDelegationOutcome{Success,Error,TaskKey,AgentId,AgentName,Status}`、`AgentDelegationSnapshot{TaskKey,Status,ExitCode?,ErrorCode?,ElapsedMs,ResultText,ArtifactsJson}`）。Abstractions 仅依赖 DI.Abstractions（plugin-development 铁律 12b），DTO 必须是纯 POCO，禁止引用 NewLife/XCode 类型。

### B. AgentHub（提供方，最小侵入）

- file: `Plugins/AgentHub/Services/AgentDelegationProvider.cs`（新增）
  reason: 适配器包住既有单例 `DelegationRuntime`（`AgentHubPlugin.cs:74` `AddSingleton<DelegationRuntime>`）与 `DelegationTask` 实体读回；不修改状态机、控制器、DTO。
- file: `Plugins/AgentHub/AgentHubPlugin.cs`（编辑，仅 Apply 内加 eager 构造 + `ctx.Register<IAgentDelegation>(provider)`）
  reason: 与 `Plugins/AIAgent/AIAgentPlugin.cs:68` 同构的提供方注册形状；提供即 effect，卸载自动摘除（`ForgeSelf.Core/IContext.cs` Register 注释）。

### C. TodoTracker 数据层

- file: `Plugins/TodoTracker/Data/Model.xml`（编辑）
  reason: 列的唯一真源 —— `Todo` 追加 18 列 + 新增 `TaskExecution` 表 + 索引（02-spec BR-1）。
- file: `Plugins/TodoTracker/Data/Entities/Todo.cs`（**xcode 生成，禁手改**）
- file: `Plugins/TodoTracker/Data/Entities/TaskExecution.cs`（**xcode 生成**）
- file: `Plugins/TodoTracker/Data/Entities/TaskExecution.Biz.cs`（新增，人工）
  reason: `Valid()` 校验（Action 非空、Seq 补齐用直查 `FindAll(TodoId==)` 取 max+1，不读缓存）、`FindAllByTodoId`、分页读；同 `Todo.Biz.cs:29-81` 形状。
- file: `Plugins/TodoTracker/Data/TodoTracker.htm`（xcode 附产数据字典，入库）
- file: `Plugins/TodoTracker/Data/TodoTrackerTables.cs`（新增）
  reason: 铁律 12 —— `ConnName="TodoTracker"` + `EntityTypes` + `EnsureCreated()`（`EntityFactory.InitConnection` 后 `DAL.Create(ConnName).Db.ServerVersion` 探活），照 `Plugins/FileTools/Data/FileToolsTables.cs`；连名已在 `ForgeSelf.Api/Data/XCodeConfig.cs:32` `PluginDbs` 登记，无需改宿主。

### D. TodoTracker 服务层

- file: `Plugins/TodoTracker/Services/ProjectPathCanonicalizer.cs`（新增，纯函数）
  reason: BR-3 归一 13 条规则的唯一实现点（返回 `(Root, Key, Error)`）；纯 `System.IO`，插件内单点，不共享到 Core（避免内核层依赖变更）。
- file: `Plugins/TodoTracker/Services/ITodoProjectService.cs` + `TodoProjectService.cs`（新增）
  reason: `ctx.Get<IProjectRegistry>()` **每次取**（`IProjectRegistry.cs:11` 告诫 + Sems `ProjectService.cs:147` 样例）；Resolve/Link/Unlink/项目列表 + 任务计数。构造注入 `IContext`（照 `Sems/Services/ProjectService.cs:136-141`）。
- file: `Plugins/TodoTracker/Services/IArtifactImportService.cs` + `ArtifactImportService.cs`（新增）
  reason: BR-5 工件目录列举 + 白名单读取 + 组装（防穿越、限额、reason 自证成因）。
- file: `Plugins/TodoTracker/Services/ITaskExecutionService.cs` + `TaskExecutionService.cs`（新增）
  reason: FR-4 追加/分页读/批量 `recordCount` 聚合；append-only。
- file: `Plugins/TodoTracker/Services/DispatchPayloadBuilder.cs`（新增，纯函数）
  reason: Output-3 prompt markdown + AgentHub 兼容 JSON 的**唯一组装点**（纯函数便于 vitest/xUnit 直接锁形状，避免界面自己拼字符串）。
- file: `Plugins/TodoTracker/Services/TodoStage.cs`（新增）
  reason: 状态机常量 + 合法边集合 + `TryParse(名称|数字)`（非法值 400 并列出可用值，plugin-development §C）；与既有 `TodoStatus`（`TodoTrackerPlugin.cs:136-156`）并置但互不覆写。
- file: `Plugins/TodoTracker/Services/IAgentTaskGateway.cs` + `AgentTaskGateway.cs`（新增）
  reason: TodoTracker 侧消费 `IAgentDelegation`（`ctx.Get`，缺席 ⇒ 明确 Unavailable→503）；权限模式黑名单在本层先拒（BR-6）。
- file: `Plugins/TodoTracker/Services/ITodoService.cs` + `TodoService.cs`（编辑）
  reason: 新字段读写、`projectId/stage/q` 过滤、`complete/reopen` 与 `Stage` 同步、`recordCount` 批量补齐、TaskKey 补齐与启动回填。
- file: `Plugins/TodoTracker/TodoTrackerPlugin.cs`（编辑）
  reason: 注册新服务；调 `TodoTrackerTables.EnsureCreated()` 失败 `Warn`；**删除** `RegisterMenuExtensions`（铁律 19②：菜单只在 `plugin.json.frontend` 声明一处）；注册 6 个新工具函数（description 按铁律 18 写全取值枚举）；启动回填历史行 `TaskKey`（只补空值，幂等，记数）。
- file: `Plugins/TodoTracker/ToolExtensions.cs`（编辑）
  reason: 沿用 `TodoToolProvider.Resolve` + `CreateScope` 形状（`:19,83-88`）新增 6 个 `IToolFunctionExtension`。

### E. TodoTracker 控制器 / DTO / 清单

- file: `Plugins/TodoTracker/Controllers/TodosController.cs`（编辑）
  reason: 加类级 `[Authorize("ApiKeyPolicy")]`（铁律 17）；新查询参数与新字段；`/{id}/stage` 端点；`by-key` 只读端点。**既有 7 端点的路径/方法/封套不变**（Home 兼容）。
- file: `Plugins/TodoTracker/Controllers/TodoProjectsController.cs`（新增）
- file: `Plugins/TodoTracker/Controllers/TodoArtifactsController.cs`（新增）
- file: `Plugins/TodoTracker/Controllers/TaskExecutionsController.cs`（新增）
- file: `Plugins/TodoTracker/Controllers/TodoDispatchController.cs`（新增：`dispatch` 预览/执行、`dispatch-to-agent`、`agent-status`、`agent-status/record`、`agent/next`）
  reason: 按能力分文件（照 AgentHub 的 `AgentHubTasksController`/`AgentHubAgentsController`/`AgentHubSettingsController` 切分）；每个都带类级鉴权特性。
- file: `Plugins/TodoTracker/Models/TodoDtos.cs`（编辑）+ `Models/TaskExecutionDtos.cs` + `Models/ProjectDtos.cs` + `Models/DispatchDtos.cs`（新增）
  reason: 出参 camelCase 由默认 JSON 选项保证；`TodoDto` 只增不减（兼容性 AC-12）。
- file: `Plugins/TodoTracker/plugin.json`（编辑）
  reason: `Version` 1.0.0 → **1.1.0**；`frontend` 加 `entry:"web/dist/index.js"`（`route` 保持 `/todo`、`views:["TodoView"]`、`menu` 单点声明）。

### F. 插件前端（新建 `web/`）

- file: `Plugins/TodoTracker/web/`（由 `pwsh .agents/skills/plugin-frontend-scaffold/scripts/scaffold-plugin-frontend.ps1 -Plugin TodoTracker` 生成骨架）
  reason: 铁律：不许手写 `web/`（`plugin-frontend-scaffold`「为什么不许手写 web/」两坑）。生成后必查 `pnpm-lock.yaml` + `pnpm-workspace.yaml`（`allowBuilds.esbuild: true` + `onlyBuiltDependencies:[esbuild]`）齐备（铁律 20，CI `scripts/release/build-frontend.ps1:39,41` 用 `--frozen-lockfile`）。
- file: `Plugins/TodoTracker/web/src/TodoView.vue`（改自模板 + 迁入宿主 TodoView）
- file: `Plugins/TodoTracker/web/src/{http.ts,types.ts,store.ts,confirm.ts,toast.ts,index.ts}`（照 `Plugins/QuickLinks/web/` 形状；`http.ts` 从 `localStorage['forge_api_token']` 取 Bearer —— 新鉴权必需）
- file: `Plugins/TodoTracker/web/src/components/{TaskListItem.vue,TaskDetailDrawer.vue,ProjectPicker.vue,ArtifactImportDialog.vue,DispatchPreview.vue,ExecutionTimeline.vue,ExecutionRecordForm.vue,StageStepper.vue}`（新增）
  reason: 界面拆分独立组件（AGENTS §4.2）；确认逻辑写成可单测纯编排函数（plugin-development §3.2，范例 `AIAgent/web/src/sessionArchive.ts`）。
- file: `Plugins/TodoTracker/web/src/*.spec.ts`（vitest 单测：confirm 编排、stage 标签映射、canonical 提示文案）
  reason: 「点取消不得发请求」这类语义只能靠纯函数单测锁死。

### G. 宿主前端清理（迁移必需，plugin-development §3.3）

- file: `ForgeSelf.Web/src/views/TodoView.vue` → 移入 `.trash/`（**禁止 rm**，AGENTS §4.1）
- file: `ForgeSelf.Web/src/components/todo/{TodoListItem.vue,TodoEditDialog.vue,TodoListItem.test.ts,TodoEditDialog.test.ts}` → `.trash/`（其单测语义迁入插件 `web/`）
- file: `ForgeSelf.Web/src/stores/todo.ts`、`ForgeSelf.Web/src/services/todoApi.ts`、`ForgeSelf.Web/src/types/todo.ts` → `.trash/`
  reason: 界面归插件（铁律 3）。实测宿主内 `/api/todos` 的唯一消费者就是 `todoApi.ts`（grep 实测），`Home` 插件走自己的 `http.ts`，删宿主份不影响它。
- file: `ForgeSelf.Web/src/router/index.ts`（删 `:22` import + `:98-100` 静态路由）
- file: `ForgeSelf.Web/src/router/dynamicPlugins.ts`（删 `:73-74` `case 'TodoView'`，改 `:10,:46` 陈旧注释）
- file: `ForgeSelf.Web/components.d.ts`（删 `:103-104` 两行，否则 `vue-tsc` 指向已删文件）
- file: `ForgeSelf.Web/src/data/features.ts`（`:305-318` 去掉 `signals.views:['TodoView']`，保留条目 + 注释「视图已迁移到插件」）
  reason: `ForgeSelf.Web/scripts/check-features.mjs:92-96,112-120` 幽灵页/孤儿门禁硬校验。
- file: `ForgeSelf.Web/src/router/__tests__/dynamicPlugins.test.ts`（`:62-82` 静态 `/todo` 冲突夹具前提变化、`:139-153` TodoView 回退断言 → 改 entry 型断言）
- file: `ForgeSelf.Web/e2e/todo.spec.ts`（页面 CRUD/过滤组迁出，仅保留首页待办面板组 `:262-364`；`:327` `toHaveURL(/\/todo$/)` 随迁）

### H. 后端测试

- file: `ForgeSelf.Api.Tests/Plugins/TodoTracker/ProjectPathCanonicalizerTests.cs`（新增，金样表 `[InlineData]` 逐条）
- file: `.../Plugins/TodoTracker/TaskExecutionServiceTests.cs`（新增：Seq 递增、append-only、批量计数）
- file: `.../Plugins/TodoTracker/ArtifactImportServiceTests.cs`（新增：真实 `docs/ai/pilot/2026-10-07-todo-agent-dispatch/` 夹具 + 越界/非 md/超限三红 + `Content` 12,000 字符不裁）
- file: `.../Plugins/TodoTracker/TodoStageMachineTests.cs`（新增：合法边全过、非法边 409 且列出可达集）
- file: `.../Plugins/TodoTracker/DispatchPayloadBuilderTests.cs`（新增：prompt/JSON 形状 + `missing` 点名）
- file: `.../Plugins/TodoTracker/TodoTrackerAuthTests.cs`（新增：反射断言每个 `api/todos*` 控制器带 `ApiKeyPolicy`，照 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpAdminAuthTests.cs`）
- file: `.../Plugins/TodoTracker/TodoTrackerWebAssetTests.cs`（新增：`plugin.json` entry/views[0] 与 `vite.config.ts` `entryFileNames` 对账，照 `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostScopeWebAssetTests.cs:130-162`）
- file: `.../Plugins/TodoTracker/TodoAgentDelegationTests.cs`（新增：接缝缺席 ⇒ Unavailable；`yolo` 被本层拒；**静态守卫**：`Plugins/TodoTracker/**/*.cs` 剥注释后不得出现 `api/agent-hub` 字面）
- file: `.../Plugins/TodoTracker/TodoTrackerTablesTests.cs`（新增：`EnsureCreated()` 返回 true 且新列/新表可读）
- file: `.../Plugins/AgentHub/AgentDelegationProviderTests.cs`（新增：Submit→`DelegationTask` 落库 + `taskKey` 回填 + `Find` 读回；照 `AgentHubDelegationRuntimeTests.cs` 的夹具形状）
- file: `.../Unit/TodoServiceTests.cs`、`.../Integration/TodosControllerTests.cs`（编辑：既有断言保持绿 + 新字段/兼容形状补条）
- file: `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`（**不改**：`:49` 已引用 TodoTracker）

### I. 文档与登记

- file: `docs/02-features/005-todo-tracker.md`（编辑：新能力/端点/状态机/路径规则/执行记录/版本 1.1.0）
- file: `docs/01-architecture/host-capability-seams.md`（编辑：§表格加 `IAgentDelegation` 一行 —— 提供方 AgentHub、消费方 TodoTracker、降级=503）
- file: `docs/07-decisions/not-taken-decisions.md`（编辑：不做自由文本回报解析、不接成本关联、不改宿主 `Project` 结构与合并脏行、不用插件前端直连 HTTP 委派）
- file: `.agents/skills/plugin-development/SKILL.md`（收口时回写本批新踩坑；若已有条目覆盖则不改）

## Implementation Steps

1. **环境与骨架**：设 `NO_PROXY`/`TEMP`（AGENTS §5.0）；`pwsh scripts/install-git-hooks.ps1`；跑 scaffold 生成 `Plugins/TodoTracker/web/`（先建骨架，后续只改组件）。
2. **数据层**：改 `Data/Model.xml`（Todo 18 列 + TaskExecution）→ `cd Plugins/TodoTracker/Data && xcode Model.xml` → `diff <(grep -oE 'BindColumn\("…"' 旧) <(新)` 对账无漂移 → 写 `TaskExecution.Biz.cs`/补 `Todo.Biz.cs` → 新增 `TodoTrackerTables.cs` 并在 `Apply` 调用。检查：`dotnet build Plugins/TodoTracker`（注意：单编插件**不算数**，判据见步骤 5）。
3. **契约与提供方**：`ForgeSelf.Abstractions/AgentDelegationContracts.cs` → `Plugins/AgentHub/Services/AgentDelegationProvider.cs` → `AgentHubPlugin.Apply` 注册 → `AgentDelegationProviderTests`。检查：`dotnet build ForgeSelf.Api` + `dotnet test --filter ~AgentHub`。
4. **插件服务与端点**：`ProjectPathCanonicalizer` → `TodoProjectService` → `ArtifactImportService` → `TaskExecutionService` → `TodoStage` → `DispatchPayloadBuilder` → `AgentTaskGateway` → `TodoService` 扩展 → 4 个新控制器 + `TodosController` 加鉴权 → 6 个工具函数。检查：`dotnet build ForgeSelf.Api` + `dotnet test --filter ~TodoTracker`。
5. **进宿主构建图核对**（铁律 12b）：`cd ForgeSelf.Api && dotnet build` 后核 `bin/Debug/net10.0-windows/Plugins/TodoTracker/TodoTracker.dll` 存在且与插件产物 md5 相同。
6. **插件前端**：`web/src` 组件按 FR-7 实现（列表/详情/项目/工件/下发/时间线）+ `http.ts` 带 Bearer + 版本徽标 + 确认编排纯函数 + vitest；`plugin.json` 加 `entry` 并升 1.1.0。检查：`cd Plugins/TodoTracker/web && pnpm install --frozen-lockfile && pnpm build`，再 `grep 'from "vue-router"' dist/index.js` 自检。
7. **宿主清理**：按 G 组逐文件移 `.trash/` + 改 5 个引用点（features.ts/dynamicPlugins/router/components.d/dynamicPlugins.test）。检查：`cd ForgeSelf.Web && pnpm run check && pnpm run test && node scripts/check-features.mjs`。
8. **e2e**：新建 `e2e/plugins/todo-tracker/todo-tracker.spec.ts`（迁入页面用例 + 新增下发/工件/执行记录/取消确认路径），裁减 `e2e/todo.spec.ts` 为首页面板组。检查（中档 e2e）：`pnpm exec playwright test --config=playwright.config.ts e2e/plugins/todo-tracker e2e/menu-route-consistency.spec.ts e2e/todo.spec.ts --output=../.pw-out-todo --reporter=list`。
9. **发布链同参数复跑**（AGENTS §5.6）：`cd ForgeSelf.Web && npx vue-tsc -b`（build 模式，把 `e2e/**` 编进来）。
10. **中档全量后端**：`dotnet test`（全量，不带 filter）与基线对账。
11. **Evidence/Review/文档**：补 05/06/07；同步 I 组文档；请示提交与发布（闸门2/3）。

## Test Plan

1. **纯函数优先**：`ProjectPathCanonicalizer`（金样表，含反例）、`DispatchPayloadBuilder`（形状 + missing）、`TodoStage.TryParse/CanTransit`（全边遍历）。这三处是"错一个字符即错判同项目/漏判非法流转"的高危点。
2. **服务层用真实库夹具**：`TaskExecutionService` 的 `Seq` 与 `recordCount` 直查 DB（铁律 11：每测试类独立随机目录，只建不删，铁律 10）。
3. **反例必须先证明前提真发生**：`ArtifactImportServiceTests` 断言"越界被拒"时，把路径直接喂给底层读取函数验证它**确实能读到**（阳性对照），否则守卫可能是空转（记忆：反例须证前提）。
4. **静态守卫先剥注释**：`TodoAgentDelegationTests` 的"无直连 HTTP"扫描必须 `StripComments` 后匹配（铁律：`IHostedService` 守卫曾因此自误，plugin-development §B）。
5. **MUTATION 探针**：鉴权守卫、无直连守卫、web 资产守卫三条，各自改坏一次证明会变红，再改回。
6. **契约只增不减**：`TodoServiceTests` 加一条断言 `TodoDto` 键集合 ⊇ 旧 8 键（Home 兼容）。
7. **e2e 零 mock 走真实前后端**：含"用 `/d/…` 写法关联项目 → 后端返回的 `projectRoot` 是归一 Windows 根"这条真实链路（截图 + 读图）。
8. **回归**：中档全量后端 + 定向 e2e（本插件 + menu-route-consistency + 首页面板）。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build                        # 宿主构建图（含插件）
cd Plugins/TodoTracker/web && pnpm install --frozen-lockfile && pnpm build
cd ForgeSelf.Web && npx vue-tsc -b                      # 发布链同参数（含 e2e/**）
```

### Unit Test

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TodoTracker|FullyQualifiedName~AgentHub"
cd ForgeSelf.Web && pnpm run check && pnpm run test
```

### Integration Test

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TodosController"   # 既有控制器集成测试保持绿
```
（本仓无独立集成测试工程，控制器测试即落在 `ForgeSelf.Api.Tests/Integration/`，依据 `Integration/TodosControllerTests.cs` 实际存在。）

### E2E

```bash
cd ForgeSelf.Web
export NO_PROXY=localhost,127.0.0.1,::1
export TEMP="$(pwd)/../.temp/tmp" TMP="$(pwd)/../.temp/tmp"
pnpm exec playwright test --config=playwright.config.ts \
  e2e/plugins/todo-tracker e2e/menu-route-consistency.spec.ts e2e/todo.spec.ts \
  --output=../.pw-out-todo --reporter=list
```
判定只读落盘日志正文（`X passed`），不读 exit code（e2e-testing 三条硬规矩 3）。未碰 `global-setup.ts`/config/fixtures ⇒ 不跑深档；如要报"全量绿"必须升深档并说明。

### Other Checks

```bash
cd Plugins/TodoTracker/Data && diff <(git show HEAD:Plugins/TodoTracker/Data/Entities/Todo.cs | grep -oE 'BindColumn\("[A-Za-z]+"' | sort) \
                                    <(grep -oE 'BindColumn\("[A-Za-z]+"' Data/Entities/Todo.cs | sort)   # 旧列无漂移（新列为预期增量）
grep -c 'from "vue-router"' Plugins/TodoTracker/web/dist/index.js            # ≥1：external 未漏声明
node ForgeSelf.Web/scripts/check-features.mjs                                 # 幽灵页/孤儿门禁
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-07-todo-agent-dispatch
```
插件门禁五步（plugin-development §四）：① 门禁 ② 插件层 e2e ③ 发布（打 tag / 本地目录更新源 + `-Sign`，须先请示提交）④ 隔离实例走查（读图 + 清测试数据）⑤ 运行实例只读复验（用户启用新版本后；先验 token，401 立即上报）。

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 19:20 | AgentHub 提供方的依赖注入方式 | 「eager 构造 `AgentDelegationProvider(runtime, registry)` 后 `ctx.Register`」 | `Apply` 期只有 `IServiceCollection`；`BuildServiceProvider()` 会另起一份 `PermissionBroker`/`AgentRegistry` 单例（接缝任务与审批面板不再是同一份队列，G2 失灵）。改为 `new AgentDelegationProvider(ctx)` **持容器、每次调用现取服务**（同 `AgentHubToolBase.GetService<T>()`）。已写入 `host-capability-seams.md` §7 踩坑 1 |
| 19:25 | 服务拆分 | 「TodoService 扩下发/委派方法」 | 拆出 `ITodoDispatchService` + `ITaskExecutionService` + `ITodoProjectService` + `IArtifactImportService`（否则单类 700+ 行且职责混杂）；`ITodoService` 只留 CRUD/查询/流转/关联/回填 |
| 19:40 | 状态码 424 的设想 | 曾考虑委派缺席用 424 | 按已批 02-spec 保持 **503**（规范优先于临时设想），并把判定从"原因文本嗅探"改成 `DelegateToAgentResultDto.SeamMissing` 标志位 |
| 20:05 | scaffold 脚本路径 | 「跑 `scaffold-plugin-frontend.ps1` 生成 web/」 | 脚本内模板/目标路径仍是旧的 `ForgeSelf.Api/Plugins/<X>/web`（插件目录早已搬到仓库根 `Plugins/`），一跑即 "Template web not found"。**就地修脚本 1 处路径**（不绕过技能手写 web/），并把坑记进技能文档 |
| 20:30 | 插件前端单测 | 「`web/src/*.spec.ts`（vitest：确认编排/stage 标签）」 | 模板 `web/` 的 package.json 无 vitest 配置，为跑 3 条单测新增测试依赖属越界（规范 §1.4 不新增依赖）。改由 **e2e 覆盖取消/确认双路径**（AC-16）+ 纯逻辑在后端 xUnit 侧锁死；偏差登记，不静默省掉 |
| 20:40 | `TodoProjection` / `TodoBackfill` / `FileChangeList` 三个新文件 | Plan 未列 | 实现中出现"投影规则被两处各写一份"（读时补 TaskKey）与"历史行 Priority=0 更新即抛"两个真问题，各抽一处收口；已补进 Expected Files 语义（04-task Allowed 覆盖同一目录，未扩大范围） |
| 20:55 | `e2e/todo.spec.ts` 后端地址 | 原 Plan 只说"裁掉页面用例组" | 该文件仍硬编码 `process.env.E2E_BACKEND_URL ?? 'http://localhost:7102'`（AGENTS §5.3 禁止新代码硬编码端口）。既然本批就要改它，顺手换成 `backendUrl()`（helpers/e2e-env 真源） |
| 21:30 | AC-14 的字面判据 `grep -c 'from "vue-router"' dist/index.js ≥ 1` | Plan 的验证命令假定插件前端会 import vue-router | **该判据对本插件不成立**：本插件前端不 import vue-router/pinia/element-plus（提示与确认走插件自带 `notify.ts`，布局用自带 CSS 变量，不挂 Element Plus 组件），产物里出现的裸导入只有 1 处 `from "vue"`。判据按语义改写作「**产物里出现的每一个共享依赖都必须是裸导入，不得被内联**」，实测成立；`TodoTrackerWebAssetTests` 对 `vite.config.ts` 声明 external 的守卫（vue/vue-router/pinia/element-plus/icons）**照旧全覆盖**，所以"漏声明 external 会被红"的能力没有降低（Verified：`grep -oE 'from "[^"]+"'` 计数 1） |
| 21:35 | AC-15 的字面判据「`check-features.mjs` 绿」 | Plan/Evidence 表把它写成"必绿" | 脚本在 **HEAD 就是红的**（`pluginsDir` 仍指 `ForgeSelf.Api/Plugins`，16 条插件信号假红 + 5 条他处历史遗留）。本批按「**不新增不一致**」判定并留痕：改名前 `grep TodoView` 在 `ForgeSelf.Web/src/` 命中 3 处、改名后 0 处；控制实验（临时把 `pluginsDir` 指仓库根，跑完还原）显示剩余 12 条**无一条与 todo 相关**。脚本自身的修法超出本批范围 ⇒ `TODO.md` P2 |
| 21:50 | Playwright 断言消息的挂载位置 | 用例里写了 `expect(page.url()).toMatch(/\/todo$/, '消息')` | `pnpm run check` 报 **TS2554 Expected 1 arguments, but got 2** ⇒ Playwright 的自定义消息只挂在 `expect(value, message)`；改为 `expect(page.url(), '…').toMatch(/\/todo$/)`。教训：新增 e2e 文件后**先跑 `pnpm run check`** 再跑 e2e，否则要等 10 分钟的 e2e 才发现一个编译期错误 |
| 22:05 | 插件前端新增"写请求串行队列"（Plan 未预见的机制） | Plan 只写「点即保存：只发变化的那一栏」 | 第一轮 e2e（B1）实测：连续失焦连发 4 个 PUT，**四个都落库**（宿主日志 `21:51:03.812/.828/.845/.868 更新待办，id=2`）但界面仍显示「还缺：验收判据、验证命令」——响应乱序回来，`applyUpdated` 把较早那次写的**整行旧快照**盖回新状态。修法：`web/src/http.ts` 把非 GET 请求串成一条队列（读不串）。这是界面层的真实缺陷修复，未扩大 Allowed 范围（仍在 `Plugins/TodoTracker/web/**`） |
| 22:10 | e2e 需要的前置缺件（非本批代码） | Plan 未列"先构建各插件 web 产物"与 SQLite provider | 全新 worktree 里 `Plugins/*/web/dist` **一个都没有**（gitignored），`Home`/`AgentHub` 等 10 个插件连 `node_modules` 都没有 ⇒ `menu-route-consistency ②` 报 `/agent-hub` 停在插件视图错误态、`todo.spec.ts` H2/H3/H5 的 `.todo-panel` 不存在。另 `global-setup.ts:250` 因缺 `System.Data.SQLite.dll` 直接中止（注释声称入库的 `build/runtime/Plugins` 实际不在仓库里）。处置：循环 `pnpm install && pnpm build` 全部 `Plugins/*/web`（日志 `.temp/tmp/plugin-webs-build.log`），并从本仓 `bin` 复制两件套到新建 `build/runtime/Plugins/`（这两个二进制目前未跟踪，是否入库需拍板）⇒ 两条都进 `TODO.md`，并回写 `e2e-testing` 技能与 AGENTS §5.0 |
