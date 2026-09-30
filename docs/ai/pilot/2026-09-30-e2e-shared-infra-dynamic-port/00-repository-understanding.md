# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。

## 项目结构

- 后端：`ForgeSelf.Api/`（ASP.NET Core，.NET 10，SQLite + NewLife.XCode，唯一 ORM）；`ForgeSelf.Api.Tests/`（xUnit + Moq + FluentAssertions + Coverlet）。
- 前端：`ForgeSelf.Web/`（Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia，pnpm）。
- e2e 基建：`ForgeSelf.Web/e2e/`（Playwright，global-setup.ts / playwright.config.ts / playwright.e2e-published.config.ts / helpers/ / 各 `.spec.ts`）。
- 插件：`Plugins/`（每个插件含 `plugin.json` + 独立程序集）。
- 文档：`docs/`（04-standards 工程规范、18-templates 模板、ai/pilot 工件链）。
- 规格/工件：`docs/ai/pilot/`（AI-Native 闭环工件，替代已 gitignore 的 `specs/`）。
- 构建：`build.ps1`、`publish/`。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | .NET 10 / ASP.NET Core / SQLite / NewLife.XCode | `ForgeSelf.Api/ForgeSelf.Api.csproj`、`ForgeSelf.Api/Data/ForgeConfig.cs` |
| 后端测试 | xUnit 2.9.2 / Moq 4.20.72 / FluentAssertions 7.0.0 / Coverlet | `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj` |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia | `ForgeSelf.Web/package.json` |
| 前端测试/e2e | Playwright（e2e/ 内 .spec.ts，webServer 托管 dev server） | `ForgeSelf.Web/playwright.config.ts`、`e2e/global-setup.ts` |
| 配置体系 | NewLife `Config<T>`（派生 `ForgeConfig<T>`）落盘到 `{数据根}/config/<Name>.config` | `ForgeSelf.Api/Data/ForgeConfig.cs`、`Models/ForgeSetting.cs` |

## 架构特点

- 数据根解析：`DataLocationService.ResolveHostDataDirectory()`（B9-4）：优先环境变量 `FORGESELF_DATA_ROOT`；否则 Development→`{BaseDirectory}/data`，否则→`%USERPROFILE%/.forgeself`。（`ForgeSelf.Api/DataLocationService.cs`）
- 配置落盘统一：`ConfigUnifier.UnifyAllConfigFiles({数据根}/config)` 在 `Program.cs`（先于一切 NewLife 访问）与 `AppBuilder.cs:94` 各调用一次（幂等）。
- 端口真源：`ForgeSetting.Current.PortNumber`（默认 7102，`Models/ForgeSetting.cs:25`）。`AppBuilder.cs:523-525` 无条件 `app.Urls.Add($"http://0.0.0.0:{port}")`，且 `IServerAddressesFeature.Addresses` 已被填充 → **`ASPNETCORE_URLS` 环境变量被忽略**（实测确认，推翻任务书原假设）。
- 托盘/重启：`Program.cs` 中 `--port` 仅托盘辅助进程（`RunTrayMode`）独占；`ApplicationRestartService`（Services/）重启进程用 `UseShellExecute=true` 且不传环境变量 → 端口覆盖**必须落盘 config** 才能保持重启后一致。
- 单实例互斥：全局 Mutex，实例标识 `FORGESelf_INSTANCE_ID` env 或 `--instance-id=`（Program.cs:59-64），支持同机多独立实例（e2e 宿主与 dev 宿主并存）。
- 前端代理：`ForgeSelf.Web/vite.config.ts` 四个代理 target = `process.env.VITE_APP_BASE_API ?? 'http://localhost:7102'`；`server.port=7002`（无 strictPort）。
- e2e 注入通道：global-setup.ts 写 `state.json` 由 `e2e/helpers/real-auth.ts` 跨进程读取 token；端口目前硬编码 7102/7002。

## 测试方式

- 后端单测：`dotnet test ForgeSelf.Api.Tests`（xUnit）。环境变量隔离测试挂 `[Collection("EnvVarIsolation")]`（DisableParallelization），见 `ForgeSelf.Api.Tests/EnvVarIsolationCollection.cs`。
- 前端：`./pnpm run check`（类型+lint）、`./pnpm run test`（vitest）。
- e2e：`ForgeSelf.Web/e2e/` Playwright；`globalSetup` 负责 publish 宿主 + 启进程 + 写 state；worker 在 globalSetup 之后 fork，env 继承主进程 `process.env`（已从 `node_modules/.pnpm/playwright@1.61.1` runner 源码核实：顺序 `...globalSetupTasks → createLoadTask → createRunTestsTasks`，webServer 先于 globalSetup 启动）。

## 构建命令

```bash
# 后端
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj

# 前端
cd ForgeSelf.Web && pnpm run check && pnpm run test

# e2e（深档，4 worker；实际依赖 Windows 宿主，平台限制如实上报）
cd ForgeSelf.Web && npx playwright test --config=playwright.config.ts --workers=4
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Api/` | 后端宿主（启动/端口/配置/插件装配/SignalR/WebSocket） |
| `ForgeSelf.Web/e2e/` | e2e 测试与共享基建（global-setup、config、helpers、spec） |
| `docs/04-standards/` | 工程规范（agent-workflow、ai-native-engineering-workflow 等） |
| `docs/18-templates/ai-pilot/` | AI-Native 闭环工件模板 |
| `docs/ai/pilot/<task-id>/` | 本任务工件链落点 |

## 代码组织方式

- 后端按职责分 `Models/`、`Services/`、`Data/`、`Plugins/`；`AppBuilder.cs` 是 WebApplication 装配唯一入口（控制台/服务模式共用）。
- 前端单页应用，代理集中 `vite.config.ts`，e2e 端口/入口集中在 `playwright.config.ts` + `e2e/global-setup.ts`。

## 现有工程规范

- AGENTS.md §0：预飞铁律 + PILOT 工件链门禁（提交前 hook 校验 `docs/ai/pilot/<task-id>/` 00-07 八件工件含关键节齐全，缺件提交被拒）。
- AGENTS.md §5.3/§5.6：测试体系与门禁分档——改 `e2e/global-setup.ts`/`playwright.*.config.ts`/`ForgeSelf.Api/**` 触发「中档」(全量 `dotnet test`) + 「深档」(全量 e2e 4 worker)。
- `docs/04-standards/ai-native-engineering-workflow.md` v1.1.0（强制）：开发类任务唯一流程依据，九阶段 + 三道闸门。
- `e2e-testing` 技能：e2e 为项目唯一集成测试体系，禁止用一次性散落脚本代替；`E2E_SKIP_GLOBAL_SETUP=1` 可跳过全局装配。

## 候选低风险任务

- （本任务本身）e2e 共享基建改造：支持多 worktree 并行、动态端口、稳定目录，消除 Windows 防火墙弹窗；单 worktree 行为与基线不变。改动集中在 e2e 基建 + 一处后端启动端口覆盖，不触生产数据/DB 结构/鉴权核心，属低风险架构微调。

## 选择该任务的原因

- 用户以任务书形式直接下达（@long-text），并追加授权改后端端口机制（新增 `--server-port` + `FORGESELF_PORT` 环境变量优先）。
- 痛点明确：e2e 随机时间戳目录 + 固定端口 7102/7002 导致多 worktree 并行冲突、每次运行触发 Windows 防火墙授权弹窗，阻断自动化。
- 影响面可控、可测：端口覆盖走 `ForgeSetting` 落盘（已有机制），e2e 改造保留默认回落，单 worktree 行为不变。
