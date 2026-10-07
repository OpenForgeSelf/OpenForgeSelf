# Agent Task

> 阶段：Stage 4｜把任务变成 Agent 可以直接执行的工作单元，零自我决策空间。
> 前序工件：`00-repository-understanding.md` / `01-intent.md` / `02-spec.md` / `03-plan.md` 齐备且**须经闸门1 用户确认后方可 Implement**。

## Task ID

PILOT-054（目录：`docs/ai/pilot/2026-10-07-todo-agent-dispatch/`）

## Objective

把 `Plugins/TodoTracker` 从「便签插件」改造成「可下发给 agent 的任务台账」：任务承载工作单元字段（目标/正文/允许禁止范围/验收判据/验证命令/优先级/阶段）、经插件内路径归一关联到唯一宿主项目、可用项目 `docs/ai/pilot/**` 真实工件组装正文、以 append-only 执行记录回写「做了什么/结果/改了哪些文件/验证/风险/遗留/证据/状态流转」，并可经新增 `IAgentDelegation` 能力接缝一键提交给 AgentHub 执行；界面迁入 `Plugins/TodoTracker/web/` 并从宿主 `src/` 清理干净；插件版本 1.1.0；所有 `api/todos*` 控制器带管理面鉴权；插件层 e2e 与门禁在真实宿主上跑绿。

## Scope

### Allowed

- `Plugins/TodoTracker/**`（`Data/Model.xml`、`xcode` 重生成件、`*.Biz.cs`、`Services/`、`Controllers/`、`Models/`、`ToolExtensions.cs`、`TodoTrackerPlugin.cs`、`plugin.json`、新建 `web/**`）—— 见 03-plan C/D/E/F 组
- `ForgeSelf.Abstractions/AgentDelegationContracts.cs`（**仅新增文件**，不改既有契约签名）
- `Plugins/AgentHub/Services/AgentDelegationProvider.cs` + `AgentHubPlugin.cs` 的 **Apply 内一行注册**（不改其控制器/状态机/DTO/运行时逻辑）
- `ForgeSelf.Api.Tests/Plugins/TodoTracker/**`、`ForgeSelf.Api.Tests/Plugins/AgentHub/AgentDelegationProviderTests.cs`、`ForgeSelf.Api.Tests/Unit/TodoServiceTests.cs`、`ForgeSelf.Api.Tests/Integration/TodosControllerTests.cs`
- `ForgeSelf.Web/**` 的迁移必需清理：删 `views/TodoView.vue`、`components/todo/**`、`stores/todo.ts`、`services/todoApi.ts`、`types/todo.ts`（**移入 `.trash/`**），改 `router/index.ts`、`router/dynamicPlugins.ts`、`components.d.ts`、`data/features.ts`、`router/__tests__/dynamicPlugins.test.ts`、`e2e/todo.spec.ts`；新增 `e2e/plugins/todo-tracker/todo-tracker.spec.ts`
- `docs/02-features/005-todo-tracker.md`、`docs/01-architecture/host-capability-seams.md`（加一行接缝登记）、`docs/07-decisions/not-taken-decisions.md`、`.forgeself/memory/2026-10-07.md`、`TODO.md`、本工件目录

### Forbidden

- **宿主库结构**：不得给 `ForgeSelf.Api/Entities/**` 任何实体加列/改索引（宿主侧无 Model.xml、重建不可行 —— plugin-development §E 实测）；不得改 `HostProjectRegistry`/`Project.FindByRoot` 的匹配语义（用户 2026-10-07 拍板：规范化只做在插件内）
- **插件间直连 HTTP**：`Plugins/TodoTracker/**` 不得出现 `api/agent-hub` 字面调用（委派一律经 `IAgentDelegation`）；不得为省事改走前端 fetch 另一插件 API
- **不改 `Plugins/Home/**`**（它对 `/api/todos` 的现有调用必须零改动仍工作）
- **不改 AgentHub 既有行为**：状态机、`DelegationRequest`/`DelegationTaskDto` 字段、控制器路由与鉴权现状
- **不改 e2e 基建**：`e2e/global-setup.ts`、`playwright.*.config.ts`、`e2e/fixtures/**`（一碰即需深档，超出本批）
- **不做**：自由文本回报解析、任务依赖图/自动重试编排、按任务算 token 成本、`Project` 脏行合并/数据迁移、插件 DLL 侧载到运行实例（须用户同意）
- **不做**：任何数据库/文件删除（铁律 10）；`temp/*.cjs` 一次性验证脚本（AGENTS 红线）；无关重构；`powershell`（5.1）跑脚本
- **不擅自**：git commit / 打 tag / 发布 / 停启宿主进程 —— 提交与发布只在闸门2/3 获批后执行

## Acceptance Criteria

> 与 02-spec.md §Acceptance Criteria 同源（AC-1…AC-20），此处按执行顺序勾选。

- [ ] AC-1 `Model.xml` 扩列 + `TaskExecution` 新表，`xcode` 重生成，`BindColumn` 对账无漂移，`.Biz.cs` 未被覆写
- [ ] AC-2 `Data/TodoTrackerTables.cs` 在场并被 `Apply` 调用；宿主产物 DLL 与插件产物 md5 相同
- [ ] AC-3 `Content` 12,000 字符写入读回等长；`TaskExecution.Seq` 1/2/3 递增且直查库
- [ ] AC-4 路径归一金样表 6 正例 + 反例全绿（`/d/p`、`D:\p`、`D:/p/`、`/mnt/d/p`、大小写、UNC、`~`、`.`/`..`、相对路径拒、超长拒）
- [ ] AC-5 同一目录不同写法 ⇒ 同一 `ProjectId` 且宿主项目行数不增
- [ ] AC-6 真实 pilot 目录导入组装成功；越界/非 md/穿越三例 400（含阳性对照）
- [ ] AC-7 下发预览含 prompt + AgentHub JSON；必填缺失 ⇒ 400 + `missing` 点名
- [ ] AC-8 一键执行经接缝回填 `AgentTaskKey`；接缝缺席 ⇒ 503；静态守卫证明无直连 HTTP
- [ ] AC-9 `permissionMode` 黑名单值在本层被拒
- [ ] AC-10 状态机合法边全通过、非法边 409 列出可达集；`Status` 与 `Stage` 一致
- [ ] AC-11 并发领取不返回同一任务
- [ ] AC-12 Home 形状 `POST {title,remark}` 仍 200；`TodoDto` 旧 8 键全在
- [ ] AC-13 鉴权反射守卫在场且经 MUTATION 探针证明会变红
- [ ] AC-14 插件前端产物与 `plugin.json`/`vite.config.ts` 三处对账；`--frozen-lockfile` 同参数构建通过
- [ ] AC-15 宿主 `src/**` 无 todo 页面残留；`pnpm run check`/`test`/`vue-tsc -b`/`check-features.mjs` 全绿
- [ ] AC-16 插件层 e2e（含 `/d/…` 关联项目真实链路 + 取消/确认双路径）跑绿
- [ ] AC-17 截图落 `screenshots/e2e/todo-tracker/` 且**已读图**逐项核对 Level 3 清单
- [ ] AC-18 中档全量 `dotnet test` 与基线对账：通过数 += 新增用例数，失败数不增
- [ ] AC-19 文档四处同步 + `plugin.json.Version=1.1.0`
- [ ] AC-20 五步闭环（门禁/e2e/发布/走查/运行实例只读复验）跑完，05-07 工件补齐

## Expected Files

- 改：`Plugins/TodoTracker/{Data/Model.xml,Data/Entities/Todo.cs,Data/Entities/Todo.Biz.cs,TodoTrackerPlugin.cs,ToolExtensions.cs,plugin.json,Services/ITodoService.cs,Services/TodoService.cs,Models/TodoDtos.cs,Controllers/TodosController.cs}`
- 新（后端）：`Plugins/TodoTracker/Data/{TodoTrackerTables.cs,Entities/TaskExecution.cs,Entities/TaskExecution.Biz.cs}`、`Plugins/TodoTracker/Services/{ProjectPathCanonicalizer,TodoProjectService,ITodoProjectService,ArtifactImportService,IArtifactImportService,TaskExecutionService,ITaskExecutionService,DispatchPayloadBuilder,TodoStage,AgentTaskGateway,IAgentTaskGateway}.cs`、`Plugins/TodoTracker/Controllers/{TodoProjectsController,TodoArtifactsController,TaskExecutionsController,TodoDispatchController}.cs`、`Plugins/TodoTracker/Models/{TaskExecutionDtos,ProjectDtos,DispatchDtos}.cs`
- 新（契约/提供方）：`ForgeSelf.Abstractions/AgentDelegationContracts.cs`、`Plugins/AgentHub/Services/AgentDelegationProvider.cs`；改：`Plugins/AgentHub/AgentHubPlugin.cs`
- 新（前端）：`Plugins/TodoTracker/web/**`（脚手架生成 + 组件）
- 改/删（宿主前端）：`ForgeSelf.Web/{src/router/index.ts,src/router/dynamicPlugins.ts,components.d.ts,src/data/features.ts,src/router/__tests__/dynamicPlugins.test.ts,e2e/todo.spec.ts}`；移入 `.trash/`：`ForgeSelf.Web/src/views/TodoView.vue`、`src/components/todo/*`、`src/stores/todo.ts`、`src/services/todoApi.ts`、`src/types/todo.ts`
- 新（测试）：`ForgeSelf.Api.Tests/Plugins/TodoTracker/{ProjectPathCanonicalizerTests,TaskExecutionServiceTests,ArtifactImportServiceTests,TodoStageMachineTests,DispatchPayloadBuilderTests,TodoTrackerAuthTests,TodoTrackerWebAssetTests,TodoAgentDelegationTests,TodoTrackerTablesTests}.cs`、`ForgeSelf.Api.Tests/Plugins/AgentHub/AgentDelegationProviderTests.cs`、`ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`
- 文档：`docs/02-features/005-todo-tracker.md`、`docs/01-architecture/host-capability-seams.md`、`docs/07-decisions/not-taken-decisions.md`、本目录 05/06/07

## Verification Commands

```bash
# 环境前置（AGENTS §5.0）
export NO_PROXY='localhost,127.0.0.1,::1'; export no_proxy="$NO_PROXY"
export TEMP="/c/Users/Administrator/.qoder-cn/worktrees/app/74f462/OpenForgeSelf/.temp/tmp"
export TMP="$TEMP"; mkdir -p "$TEMP"

# 1 实体：改 Model.xml 后必须重生成 + 对账（禁手改生成件）
cd Plugins/TodoTracker/Data && ~/.dotnet/tools/xcode Model.xml

# 2 后端：宿主构建图（单编插件不算数）
cd ForgeSelf.Api && dotnet build
pwsh -NoProfile -Command "Get-FileHash bin/Debug/net10.0-windows/Plugins/TodoTracker/TodoTracker.dll, '../Plugins/TodoTracker/bin/Debug/net10.0/TodoTracker.dll' | Format-Table Hash,Path"

# 3 定向测试（快档）
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TodoTracker|FullyQualifiedName~AgentHub" --nologo

# 4 中档全量（碰了 Abstractions + AgentHub ⇒ 必跑）
cd ForgeSelf.Api.Tests && dotnet test --nologo

# 5 插件前端 + 产物自检
cd ../Plugins/TodoTracker/web && pnpm install --frozen-lockfile && pnpm build
grep -c 'from "vue-router"' dist/index.js

# 6 宿主前端门禁
cd ../../ForgeSelf.Web && pnpm run check && pnpm run test && npx vue-tsc -b && node scripts/check-features.mjs

# 7 插件层 e2e（中档：本插件 + 菜单路由一致性 + 首页面板）
pnpm exec playwright test --config=playwright.config.ts \
  e2e/plugins/todo-tracker e2e/menu-route-consistency.spec.ts e2e/todo.spec.ts \
  --output=../.pw-out-todo --reporter=list        # 判定读落盘日志正文，不读 exit code

# 8 工件链门禁
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-07-todo-agent-dispatch
```
