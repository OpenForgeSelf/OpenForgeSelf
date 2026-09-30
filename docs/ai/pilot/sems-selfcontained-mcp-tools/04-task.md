# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：`00-repository-understanding.md` / `01-intent.md` / `02-spec.md` / `03-plan.md` 齐备，**待闸门1 用户确认**。
> Task ID：PILOT-sems-selfcontained-mcp-tools

## Objective

sems 插件（`Plugins/Sems/`，v1.0.3 → **1.1.0**）在**不依赖 AIAgent 目录选择动作**的前提下，可在自身面板完成「添加/编辑/移除项目 + 命令 CRUD + 进程启停/检测」；其 13 个 `sems_*` 能力工具注册进宿主 `IToolRegistry`，可经 McpCenter 的 `universal_tool`/`list_tools` 被外部 MCP 客户端发现并真实调用；上述行为全部由后端 xUnit + vitest + Playwright e2e（零 mock）断言，且宿主契约向后兼容（AIAgent 零改动）。

## Scope

### Allowed（即 03-plan「Files To Change」全集，不得扩）

| 组 | 文件 |
| --- | --- |
| A 宿主契约/实现（2） | `ForgeSelf.Abstractions/IProjectRegistry.cs`、`ForgeSelf.Api/Services/HostProjectRegistry.cs` |
| B sems 服务层（2） | `Plugins/Sems/Services/ProjectService.cs`、`Plugins/Sems/SemsPlugin.cs` |
| C sems HTTP（3 改 1 删） | `Plugins/Sems/Controllers/ProjectsController.cs`、`ProjectCommandsController.cs`、`RunsController.cs`（仅注释）、`RunnerController.cs`（移入 `.trash/`） |
| D sems 工具（1 增） | `Plugins/Sems/ToolExtensions.cs` |
| E sems 前端（5 改 2 增 + 配置） | `web/src/SemsView.vue`、`ProjectCard.vue`、`CommandList.vue`、`RunPanel.vue`、`types.ts`、**新** `DirectoryPickerDialog.vue`、**新** `confirmOps.ts`、`web/package.json`、`web/vite.config.ts` |
| F 测试（5 改/增） | `ForgeSelf.Api.Tests/Services/HostProjectRegistryTests.cs`、**新** `ForgeSelf.Api.Tests/Plugins/Sems/ProjectServiceTests.cs`、`SemsControllerTests.cs`、`SemsToolExtensionTests.cs`、`ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`、**新** `web/src/confirmOps.spec.ts` |
| G 文档/清单 | `Plugins/Sems/plugin.json`、`docs/02-features/028-project-workspace.md`、`.forgeself/memory/2026-09-28.md`、`TODO.md`、`docs/ai/pilot/sems-selfcontained-mcp-tools/{05,06}*.md`、`docs/07-decisions/not-taken-decisions.md` |

### Forbidden

- **数据库结构**：不加表、不加列、不写迁移、不跑 `xcode Model.xml`（规范 §1.2；本任务无需）。
- **删除数据/目录**：任何代码与测试不得 `File.Delete`/`Directory.Delete`/`rm` 任何数据目录或数据库文件；e2e 临时目录**只创建不删除**（技能铁律 10）。
- **用户运行中的宿主进程**：禁止 `Stop-Process`/kill/重启 `:51888` 实例或 `D:\src\tools\ForgeSelf`（AGENTS.md §0 门禁 / 技能铁律 16）。宿主升级只由页面「自动更新」完成。
- **其他插件**：`Plugins/AIAgent/**`（含其 `browse-directories` 缺陷）、`Plugins/McpCenter/**`、其余 16 个插件源码一律不改。
- **宿主前端**：`ForgeSelf.Web/src/**` 不改（本任务无宿主界面诉求；只跑门禁证明未回归）。
- **契约破坏性变更**：不得改 `IProjectRegistry` 既有 2 参 `Register` 签名；不得改 `ProjectInfo`/`RunCommandInfo` DTO 字段；不得改既有 HTTP 端点的请求/响应形状（FR-C4）。
- **依赖**：不新增 NuGet 包；不新增非 vitest 系的前端依赖（vitest 已在宿主 devDependencies 中，版本 `^3.2.4`）。
- **范围外重构**：不动 sems 的 route/menu（03-plan「不做」#3）；不引入 L2 事件；不做运行会话持久化；不把 `RunnerService` 升格为宿主接缝；不顺手改 `ForgeSelf.Abstractions/SemsShared.cs` 的 `[Obsolete]` 遗留。
- **其他 worktree**：不读写 `7946e5`/`96a011`/`f570bb`/主检出目录，不复用其端口。
- **验证方式**：禁止 `temp/*.cjs` 一次性脚本充当验证（AGENTS.md 红线）；结论必须落在 T1~T14 可重复入口上。

## Acceptance Criteria

> 与 02-spec「Acceptance Criteria」同一条编号，逐条可测；**闸门1 用户批准的就是这张表**。

- [ ] AC1 `POST api/projects{root}` → 200 且新记录 `Source=="manual"`；同 root 重复调用不新增行。
- [ ] AC2 `IProjectRegistry` 有 3 参 `Register`，2 参版保留；`git diff --stat` 不含 `Plugins/AIAgent/`。
- [ ] AC3 改名后再登记同 Root → Name 保持用户值（后端测试断言）。
- [ ] AC4 `DELETE api/projects/{id}` → 项目行 + 其命令行消失；磁盘目录 `Directory.Exists` 仍 true；id 不存在 → 404。
- [ ] AC5 有存活 Launched 会话的项目删除 → 409 且数据零变更。
- [ ] AC6 `GET api/projects/browse`：无参列盘符；有参列子目录 + `parent`；不存在路径 → 400；无 token → 401。
- [ ] AC7 `Plugins/Sems/Controllers/` 全部控制器类带类级 `[Authorize("ApiKeyPolicy")]`（反射 Theory）。
- [ ] AC8 `RunnerController` 已移入 `.trash/`；全仓 grep `api/runner` 零引用（证据写入 05-evidence）。
- [ ] AC9 `IToolRegistry.GetAllTools()` 含 13 个 `sems_*`（名字集合精确断言，无重名静默丢失）。
- [ ] AC10 13 个工具的 `ParametersJsonSchema` 合法且 `required` 缺参被 `ValidateParameters` 拒绝（参数化 13 例）。
- [ ] AC11 13 个工具 `Description` 含「id 来源 list 工具」+ 破坏性提示关键字。
- [ ] AC12 工具端到端往返：登记 → 列 → 加命令 → 启动拿 pid → 停止 → 删除项目。
- [ ] AC13 sems UI 全链 e2e 绿（添加 → 刷新持久 → 命令 CRUD → 启动/停止 → 移除；取消路径零请求）。
- [ ] AC14 MCP 链 e2e 绿：`tools/list`=`universal_tool`；`list_tools{keyword:"sems"}` 含 13 个；`sems_list_projects` total 与 `GET api/projects` 一致。
- [ ] AC15 `plugin.json` Version=1.1.0 且面板标题旁显示 `v1.1.0` 徽标。
- [ ] AC16 空态四态文案齐备，「请前往 AI Agent 页」话术已从 `SemsView.vue` 移除。
- [ ] AC17 门禁：`dotnet build` 0 error / `dotnet test` 0 fail / sems web 构建产物仅 `index.js`+`style.css` 且裸导入自检通过 / `pnpm run check`+`pnpm run test` 绿。
- [ ] AC18 `menu-route-consistency.spec.ts` + `mcp-center.spec.ts` 实跑通过。
- [ ] AC19 维护闭环四步证据齐备（门禁 / 插件 e2e / 发布产物可下载且 SHA256 一致 / 走查截图 + 清测试数据）。
- [ ] AC20 `028-project-workspace.md` 同步；`05-evidence.md` + `06-review.md` 齐备且 Review = **APPROVED**。

## Expected Files

见「Scope / Allowed」表（A~G 共 24 个文件 + 7 个新增文件）。任何超出该表的文件改动 = 越界，须先回炉修正 Plan。

## Verification Commands

```bash
# T1 后端编译
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -v q --nologo

# T2 契约行为 / T3 sems 服务层与工具 / T4 控制器与鉴权 / T6 全量
cd ForgeSelf.Api.Tests
dotnet test --filter "HostProjectRegistry"
dotnet test --filter "Sems"
dotnet test --filter "SemsController"
dotnet test

# T5 冗余端点零引用
grep -rn "api/runner" --include=*.cs --include=*.ts --include=*.vue . | grep -v node_modules

# T7 插件前端（出树构建兜底，技能 §3.2）+ 裸导入自检
cd ForgeSelf.Web && pnpm install
node node_modules/vite/bin/vite.js build --config .plugin-build-sems/vite.wrapper.config.ts \
  --outDir <abs>/Plugins/Sems/web/dist --emptyOutDir
grep -c 'from "vue"' Plugins/Sems/web/dist/index.js      # 须 >0（裸导入，证明未内联副本）

# T8 插件前端单测（出树 vitest）
node node_modules/vitest/vitest.mjs run --root <abs>/Plugins/Sems/web

# T9 宿主前端门禁
cd ForgeSelf.Web && pnpm run check && pnpm run test

# T10/T11 插件层 e2e（UI + MCP 链）
cd ForgeSelf.Web
bash node_modules/.bin/playwright test --config=playwright.config.ts e2e/plugins/sems/sems.spec.ts

# T12 回归对账
bash node_modules/.bin/playwright test --config=playwright.config.ts \
  e2e/menu-route-consistency.spec.ts e2e/plugins/mcp-center/mcp-center.spec.ts

# T13 走查：e2e 隔离实例 + 截图读图 + 清测试数据（不动用户宿主）
# T14 发布：经用户批准后打 tag → CI；或 scripts/release/release-local.ps1 -UpdateDir <目录> + 设置页本地目录更新源
```
