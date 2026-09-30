# Plan

> 阶段：Stage 3｜具体到真实文件路径（file + reason），禁止模糊描述。
> Task ID：PILOT-sems-selfcontained-mcp-tools ｜ 级别：全量
> 前序：`00-repository-understanding.md`（含 file:line 依据）、`01-intent.md`、`02-spec.md`

## 环境与验证前置（先读，影响门禁可执行性）

- 本 worktree **无任何 `node_modules`**（实测 `ForgeSelf.Web/`、`Plugins/Sems/web/` 均无）。→ 前端门禁与插件 web 构建前须先 `pnpm install`（`pnpm 11.5.2` / `node v24.18.0` 可用）。
  - 若 `pnpm install` 因 registry 不可达失败：**如实把前端门禁记为 Unknown/BLOCKED**（规范 §1.10 禁止伪造），不得用「代码看起来对」代替。
  - `Plugins/Sems/web/node_modules` 缺失 + 技能 §3.2 沙箱拦截 → sems web 构建走**出树构建兜底**（复制 `src`+`vite.config.ts` 到 `ForgeSelf.Web/.plugin-build-sems/`，用宿主 vite 驱动，`publicDir:false` + 绝对 `--outDir`）。
  - sems web 的 vitest 同理：用宿主 `ForgeSelf.Web/node_modules/vitest` 出树跑（新增脚本保留在插件 `package.json` 供 CI/正常环境使用）。
- **并行会话避让**：`git worktree list` 实测存在 `7946e5`/`96a011`/`f570bb` 等兄弟 worktree 与主检出 `D:/src/my-proj/OpenForgeSelf/OpenForgeSelf`。只在本目录工作，**不读写其他 worktree、不复用它们的端口/副本**。
- 后端门禁基线（已 Verified）：`dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` → 0 错误 / 823 既有警告。

## Files To Change

### A. 宿主契约与实现（最小面，2 个文件）

- file: `ForgeSelf.Abstractions/IProjectRegistry.cs`
  reason: 新增 `Register(string root, string? source, out string? error)` 重载与 `bool Remove(int id, out string? error)`；**保留**原 2 参 `Register`（FR-A1/A3）。这是「sems 自己登记 + 项目可移除」在数据层的唯一缺口。
- file: `ForgeSelf.Api/Services/HostProjectRegistry.cs`
  reason: 实现上述两项；删除 `:68` 对 `Name` 的覆写（FR-A2 / BR2）；`Remove` 内级联 `RunCommand.Delete(...)`。不动表结构。

### B. sems 服务层（自洽落点，1 个文件）

- file: `Plugins/Sems/Services/ProjectService.cs`
  reason: `IProjectService` 扩为 FR-B1 的完整操作面（含 `Register/UpdateProject/RemoveProject/命令 CRUD/Browse`）；ctor 改 `(IContext, IRunnerService)`；`RemoveProject` 内实现 BR4 存活会话守卫；新增 `DirectoryEntry` DTO（放本文件，避免为 3 个属性新开文件）。sems 写路径的**唯一**落点（BR8）。
- file: `Plugins/Sems/SemsPlugin.cs`
  reason: DI 不变（`AddScoped<IProjectService>`/`AddSingleton<IRunnerService>` 已在 :20-22）；新增 13 次 `ToolExtensions.Add(...)`（FR-D1）。菜单扩展**保持不动**（见「不做」§5）。

### C. sems HTTP 面（3 改 1 删）

- file: `Plugins/Sems/Controllers/ProjectsController.cs`
  reason: 移除 `ctx.Get<IProjectRegistry>` 直连（:29），改注入 `IProjectService`；新增 `POST api/projects`、`DELETE api/projects/{id:int}`、`GET api/projects/browse`（FR-C1/C2/C3）；`{id}` 路由加 `:int` 约束避免与 `browse` 字面段歧义。
- file: `Plugins/Sems/Controllers/ProjectCommandsController.cs`
  reason: 同上改委托 `IProjectService`（FR-B3/C4），端点形状不变。
- file: `Plugins/Sems/Controllers/RunsController.cs`
  reason: 仅补文档注释（其实现已委托 `IRunnerService`，即服务层，符合 BR8）；**行为不变**。
- file: `Plugins/Sems/Controllers/RunnerController.cs`
  reason: **删除**（FR-C5，冗余端点）。按 AGENTS.md §4.1「禁止永久删除」→ 用 `git rm` 前先移动到 `.trash/Plugins/Sems/Controllers/RunnerController.cs`（`.trash/` 目录本 worktree 不存在，需创建）。

### D. sems 工具层（新增 1 个文件）

- file: `Plugins/Sems/ToolExtensions.cs`
  reason: 13 个 `IToolFunctionExtension` 实现类（FR-D 全表）。形态照抄 `Plugins/TodoTracker/ToolExtensions.cs`：`Id`/`Name`/`PluginId` get-only，`Description`/`ParametersJsonSchema`，`ExecuteAsync(string)` 内 `JsonDocument.Parse` 取参、`serviceProvider.CreateScope()` 解析 `IProjectService`/`IRunnerService`、`{success,data|error}` 封套、全异常捕获、`RecordUsageAsync` 计用量。
  - ⚠ 关键约束（来自 00 号工件③）：`ctx.Get<IToolRegistry>()` 在 `Apply` 期为 null，但本组工具不需要 registry（registry 反向调用我们），仅需 `IServiceProvider`；参数 schema 必须含 `type` + `required`，否则被静默丢弃。

### E. sems 前端（5 改 2 增 + 配置）

- file: `Plugins/Sems/web/src/SemsView.vue`
  reason: 加「添加项目」按钮 + 弹层挂载、空态分级（FR-E4，替换 `:9`/`:35` 话术）、版本徽标（FR-E5）、滚动子项 `flex-shrink:0`（FR-E6）、确认后刷新装配复用现有 `reloadProjects`。
- file: `Plugins/Sems/web/src/DirectoryPickerDialog.vue`（**新增**）
  reason: 目录浏览 + 手工输入绝对路径 → `POST api/projects`（FR-E1，数据源 FR-C3）。样式随现有原生 CSS + `--el-*` 体系。
- file: `Plugins/Sems/web/src/ProjectCard.vue`
  reason: 卡片加「移除」按钮 → 经 `confirmOps` 二次确认（FR-E2）。
- file: `Plugins/Sems/web/src/confirmOps.ts`（**新增**）
  reason: 纯编排、UI 动作由调用方注入（参照 `Plugins/AIAgent/web/src/sessionArchive.ts`），锁死「取消 → 零请求」（FR-E3）。
- file: `Plugins/Sems/web/src/CommandList.vue` / `RunPanel.vue`
  reason: 把 `window.confirm`/`window.alert`（`CommandList.vue:150`、`RunPanel.vue:109,143,148,155,163,173`）迁到 `confirmOps`，统一交互与可测性（FR-E3）。
- file: `Plugins/Sems/web/src/types.ts`
  reason: 加 `RegisterProjectReq` / `RegisterResp` / `BrowseResp` / `DirectoryEntry` 类型（新契约，TS 不允许 `any`，AGENTS.md §4.2）。
- file: `Plugins/Sems/web/package.json`
  reason: 加 `"test": "vitest run"` + vitest devDependency（正常环境可跑；沙箱内走出树，见「环境前置」）。
- file: `Plugins/Sems/web/vite.config.ts`
  reason: 注释更新（现有注释写「界面一律用原生 HTML，不用 `<ElXxx>`」，但 `element-plus` 已在 external；本次引入 `ElMessageBox` 用法需说明依据），必要时加 vitest 配置节。

### F. 测试（4 文件）

- file: `ForgeSelf.Api.Tests/Services/HostProjectRegistryTests.cs`
  reason: 追加 3 参 Register / Name 不被覆写 / Remove 级联 + 不触碰磁盘 / 不存在 id（AC1/AC3/AC4）。
- file: `ForgeSelf.Api.Tests/Plugins/Sems/ProjectServiceTests.cs`（**新增**）
  reason: FR-B1 全成员 + 校验分支 + 接缝 null 降级 + BR4 存活守卫（AC5）。夹具须按技能铁律 10/11：随机隔离目录、只创建不删除、唯一性校验直查 DB。
- file: `ForgeSelf.Api.Tests/Plugins/Sems/SemsControllerTests.cs`（**新增**）
  reason: 新端点状态码矩阵（200/400/404/409/503）+ 反射 Theory 断言 `Plugins/Sems/Controllers/` 每个控制器类带 `[Authorize("ApiKeyPolicy")]`（AC6/AC7；仿 `McpAdminAuthTests.cs:23-28`）。
- file: `ForgeSelf.Api.Tests/Plugins/Sems/SemsToolExtensionTests.cs`（**新增**）
  reason: 13 工具齐备/无重名丢失、schema 合法、`required` 被拒、Description 关键字、端到端往返（AC9~AC12）。
- file: `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`
  reason: 扩为 UI 全链 + MCP 链 + 截图（AC13/AC14，FR-F3）。测试目录只创建不删除（铁律 10）。
- file: `Plugins/Sems/web/src/confirmOps.spec.ts`（**新增**）
  reason: vitest 锁「取消 → 零请求」（FR-F2）。

### G. 文档与记录（4 文件）

- file: `Plugins/Sems/plugin.json` — `Version` 1.0.3 → **1.1.0**（AC15）。
- file: `docs/02-features/028-project-workspace.md` — 同步新端点/新工具/契约变化/`RunnerController` 已删/已知问题 #1 闭环（AC20；技能 §四-5「验证通过后更新插件文档」）。
- file: `.forgeself/memory/2026-09-28.md` — 随做随记（输入已完成勾选、决策、验证结果）。
- file: `TODO.md` — 进行中条目 + 附带发现（AIAgent `browse-directories` 无后端实现）。
- file: `docs/ai/pilot/sems-selfcontained-mcp-tools/05-evidence.md`、`06-review.md`、`07-final-report`（Stage 7/8 产出）。

## Implementation Steps

1. **契约先行（A）**：`IProjectRegistry` 加重载 + `Remove` → `HostProjectRegistry` 实现（含去掉 :68 的 Name 覆写）→ `dotnet build` 立即验证编译面（此时 sems 未动，AIAgent 走 2 参路径）。
2. **后端测试红灯（F 之契约部分）**：先给 `HostProjectRegistryTests` 补 AC1/AC3/AC4 用例，确认对第 1 步的行为断言成立（TDD：若第 1 步实现有漏即在此暴露）。
3. **服务层收口（B）**：扩 `IProjectService` + `ProjectService`（含 `Browse`、BR4 守卫），新增 `ProjectServiceTests`。
4. **控制器改委托 + 新增端点（C）**：`ProjectsController`（POST/DELETE/browse）+ `ProjectCommandsController` 委托服务；`SemsControllerTests`（状态码矩阵 + 鉴权反射）。
5. **收敛冗余**：`RunnerController.cs` 移入 `.trash/` → `dotnet build` + grep 全仓 `api/runner` 零引用留证（AC8）。
6. **工具层（D）**：写 `ToolExtensions.cs` 13 个类 → `SemsPlugin.Apply` 注册 → `SemsToolExtensionTests`（含 schema/required/往返）。这一步之后能力已经「经 MCP 中心可达」（宿主 registry 链路无需改码）。
7. **版本与清单**：`plugin.json` → 1.1.0。
8. **前端（E）**：`types.ts` → `confirmOps.ts`（+ spec）→ `DirectoryPickerDialog.vue` → `SemsView.vue`（添加/空态/徽标/flex-shrink）→ `ProjectCard.vue`（移除）→ `CommandList/RunPanel` 迁确认；随后端门禁一起跑 `dotnet build`+`dotnet test`，前端跑 `pnpm install` + 出树构建 + 出树 vitest。
9. **e2e（F）**：扩 `sems.spec.ts`（UI 链 + MCP 链），Level 3 截图读图；顺带实跑 `menu-route-consistency.spec.ts`、`mcp-center.spec.ts` 证明未回归（AC18）。
10. **发布 + 走查**：按技能 §四-3/4 与 plugin-publish-verify（打 tag `v<宿主版本>` 交用户批准 → CI；或 `release-local.ps1 -UpdateDir` + 设置页本地目录更新源）→ 浏览器走查（e2e 隔离实例，用户视角点一遍 + 清测试数据）。**禁止停/启/杀用户宿主**。
11. **文档 + 沉淀**：更新 `028-project-workspace.md`、Evidence、Review、日记、TODO；把可复用规律回写技能/agent-workflow.md（如「sems 工具经宿主 registry 自动可达，插件无需改 McpCenter」）。

## Test Plan

| # | 检查 | 命令 / 断言 | 覆盖 AC |
| --- | --- | --- | --- |
| T1 | 后端编译 | `dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj`（0 error） | AC17 |
| T2 | 契约行为 | `dotnet test --filter HostProjectRegistry` | AC1/AC2/AC3/AC4 |
| T3 | sems 服务层 | `dotnet test --filter Sems` | AC5/AC9~AC12 |
| T4 | 鉴权面 | `dotnet test --filter SemsController`（反射 Theory）+ 无 token 真发 HTTP 得 401 | AC6/AC7 |
| T5 | 冗余端点消失 | `grep -rn "api/runner" --include=*`（源码零引用）+ 路由回归 e2e | AC8 |
| T6 | 全量后端 | `dotnet test`（不得删测试换绿） | AC17 |
| T7 | 插件前端构建 | 出树 `vite build` → 产物只有 `index.js`+`style.css`；`grep 'from "vue-router"'` 证明裸导入（技能铁律 4 自检） | AC17 |
| T8 | 前端单测 | 出树 `vitest run`（`confirmOps.spec.ts`） | FR-F2 |
| T9 | 宿主前端门禁 | `pnpm run check && pnpm run test` | AC17 |
| T10 | 插件 e2e（UI） | `bash node_modules/.bin/playwright test e2e/plugins/sems/sems.spec.ts` | AC13/AC16 |
| T11 | 插件 e2e（MCP 链） | 同上文件内网关用例（`tools/list` + `list_tools` keyword=sems + 真调 `sems_list_projects`） | AC14 |
| T12 | 回归对账 | `playwright test e2e/menu-route-consistency.spec.ts e2e/plugins/mcp-center/mcp-center.spec.ts` | AC18 |
| T13 | 走查 | 浏览器点一遍 + 截图读图核对（铁律 8/13 + §3.4 验证清单）+ 清测试数据 | AC15/AC16/AC19 |
| T14 | 发布 | tag → CI Release 资产可下载 + `SHA256SUMS.txt` 一致（或本地更新源页面能检出新版） | AC19 |

## Verification

### Build

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -v q --nologo
cd ForgeSelf.Web && pnpm install && pnpm run check
```

### Unit Test

```bash
cd ForgeSelf.Api.Tests && dotnet test                       # 全量
cd ForgeSelf.Api.Tests && dotnet test --filter "Sems|HostProjectRegistry"
cd ForgeSelf.Web && pnpm run test                           # 宿主单测（未改宿主前端，须仍绿）
```

### Integration Test

N/A —— 本仓库无独立集成测试工程；后端「集成」由 `ForgeSelf.Api.Tests`（真实 XCode/SQLite 临时目录，见铁律 10/11）+ Playwright e2e（真起宿主、零 mock）两层覆盖，正规入口即上面两节与 E2E 节。

### E2E

```bash
cd ForgeSelf.Web
bash node_modules/.bin/playwright test --config=playwright.config.ts e2e/plugins/sems/sems.spec.ts
bash node_modules/.bin/playwright test --config=playwright.config.ts e2e/menu-route-consistency.spec.ts e2e/plugins/mcp-center/mcp-center.spec.ts
```

### Other Checks（插件门禁四步，技能 §四）

1. 插件 web 出树构建 + 裸导入自检（T7）；
2. 管理面鉴权反射断言 + 真发无 token 请求（T4）；
3. 工具注册面：`dotnet test --filter SemsTool` 断言 `GetAllTools()` 名字集合含 13 个 `sems_*`（AC9）；
4. 发布 + 走查（T13/T14），**全程不碰用户宿主进程**。

## 「不做」决策（NOT-to-do，同步 `docs/07-decisions/not-taken-decisions.md`）

| # | 不做 | 理由 | 触发再做的条件 |
| --- | --- | --- | --- |
| 1 | 不给 sems 建自有数据库 / 不迁 `Project`、`RunCommand` 进 `Plugins/Sems/Data/` | 架构裁决 `host-capability-seams.md` §4.1 明确选宿主 seed；且规范 §1.2 禁改库结构。sems 自持接缝会让 AIAgent 在 sems 卸载时数据空洞（正是当年否掉方案 A 的理由） | 出现「sems 独有、宿主不该知道的」表时 |
| 2 | 不新增「执行任意脚本」的 MCP 工具 | 会把 sems 变成远程 RCE 面；现有能力已能覆盖（先 `sems_add_command` 登记、再 `sems_run_command`） | 明确的安全评审 + 用户拍板 |
| 3 | 不改 sems 的 route/menu | `PluginController.cs:296-301` 已按 pluginId 去重，sems 的 `IMenuExtension` 不会双发；删掉它反而丢 `Order=150` 的排序位置。全仓 16 个插件仍用 `IMenuExtension`，单独动 sems 属无关重构 | 项目级「菜单真源」批次统一处理 |
| 4 | 不修 AIAgent 的 `browse-directories` 404 | 跨插件、非本任务诉求 | 记 `TODO.md`，用户排期 |
| 5 | 不引入 L2 事件（登记后推送刷新） | `host-capability-seams.md` §5「不做事件优先」+ 偏差#5 事件跨插件尚不可用 | 出现必须实时的消费方 |
| 6 | 不做运行会话持久化（宿主重启保留） | 既有 NFR 设计接受项（`028-project-workspace.md:99-101`） | 用户明确提出 |
| 7 | 不把 `RunnerService` 升格为宿主 `IProcessRunner` | 唯一消费方仍是 sems；升格属预测性抽象 | 第二个消费方出现 |

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-09-28（Plan 阶段自查） | `Sems.csproj:10` 注释提到 `StageSemsPlugin` | 曾假定存在插件 staging target | 全仓 grep 无该 target；实际由 `ForgeSelf.Api.csproj:26` Content glob + `ProjectReference`（:77）承担 → 发布链路无需改动（已写入 02-spec Unknown 结论） |
| 2026-09-28（Implement 步骤 3） | `IProjectService.Browse` 错误传递方式 | FR-B1 表里写「非法/无权限 → 抛异常，控制器转 400」 | 改为与服务层其它操作一致的 `SemsResult<T>` 元返回（含 `StatusCode` 映射）。理由：控制器 catch 任意异常会把非业务错误也变 400，且工具面无法复用同一异常语义；`SemsResult<T>` 让 HTTP 与 MCP 工具共用一份状态语义（BR8 一处真相）。AC6 断言不变 |
| 2026-09-28（Implement 步骤 3，**缺陷**） | `GET api/projects` 的 `commands` 恒为空 | Plan 假定「列表含每项目命令概要」（`028-project-workspace.md:49` 文档口径） | 实测 `HostProjectRegistry.ToInfo` 不填 `Commands`（仅 `Get(id)` 填），`RunnerService.cs:200` 注释亦承认。后果是既有前端三处静默失效：统计「运行命令」恒 0、`commandUrls` 空 → 运行面板快捷访问图标永不出现、**「启动全部」遍历空列表＝什么都不做**。修正：`ProjectService.GetProjects()` 逐项补 `registry.GetCommands(id)`（服务层唯一落点，HTTP/工具共用），并加回归测试。属「完善并测试 sems」范围内的缺陷修复，非扩范围 |
