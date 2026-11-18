# Repository Understanding

> 阶段：Stage 0（开发前理解）｜Task ID：2026-11-15-dev-api-multi-instance

## 项目结构

仓库根 `package.json` 提供 pnpm 命令；`ForgeSelf.Api/` 是 ASP.NET Core 宿主；`ForgeSelf.Api.Tests/` 承载后端与仓库脚本守卫；`scripts/` 存放本地开发启动脚本；故障类索引为 `docs/11-troubleshooting/problem-classes/README.md`。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| API 宿主 | .NET 10 / ASP.NET Core | `ForgeSelf.Api/ForgeSelf.Api.csproj`、`ForgeSelf.slnx` |
| 命令入口 | pnpm 10 + PowerShell 7 | 根 `package.json`、`AGENTS.md` §2.3 |
| 测试 | xUnit / FluentAssertions | `ForgeSelf.Api.Tests/`、`ForgeSelf.Api.Tests/RepositoryScriptTests.cs` |

## 本任务相关事实

- 当前工作区根 `package.json` 曾新增直接运行宿主的 `dev:api` 命令；用户反馈本地启动时提示已有实例，随后明确要求把隔离脚本接入 `pnpm run dev` 并移除 `dev:api`。
- `ForgeSelf.Api/Program.cs` 默认使用全局 Mutex；设置 `FORGESelf_INSTANCE_ID` 或 `--instance-id=<id>` 后才使用独立 Mutex 名。
- `ForgeSelf.Api/AppBuilder.cs` 将 `FORGESELF_PORT` / `--server-port` 解析为监听端口，并写入当前实例配置。
- `ForgeSelf.Api/DataLocationService.cs` 支持 `FORGESELF_DATA_ROOT` 覆盖数据目录；未设置时，开发运行可能共享默认目录。
- `ForgeSelf.Api/Properties/launchSettings.json` 的 `https` profile 固定包含 `7102`，新测试入口须避免隐式采用固定 launch profile。
- `scripts/dev-stack.ps1` 是前后端整栈入口，使用固定 `.temp/dev-stack.json`、实例 ID `dev-stack` 与共享数据根，且再次启动会停止上一个整栈实例；这与最终确认的根 `pnpm dev` 全栈需求一致。
- 现有 PC-007 已将“本地启动只走脚本入口”定为不变量，但原根 `dev` 前端别名被改成 `dev:api` 直跑宿主，沒有入口级 G2 守卫；最终入口分工为根 `dev` 启动隔离 API、`dev:web` 保留前端命令。

## 测试方式

| 验证面 | 正规入口 |
| --- | --- |
| API 构建 | `dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug --nologo` |
| 入口与启动脚本守卫 | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~RepositoryScriptTests"` |
| 问题类台账 | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~ProblemClassRegistry"` 与 `pwsh scripts/check-problem-classes.ps1` |
| 多实例行为 | 两次启动 `pnpm run dev`，验证两个不同端口的 `/api/health` 均可达，再分别 Ctrl+C 退出 |

## 候选低风险任务与选择

早期方案曾尝试让根命令启动隔离 API-only 实例；用户随后明确要求 `pnpm dev` 启动前后端，并指出应复用既有 `dev-stack.ps1`。最终任务因此收敛为根 `dev` 路由到整栈脚本、保留 `dev:web`、删除临时 API-only 启动器；不改宿主默认 Mutex、数据库结构或整栈生命周期，并为根入口增加回归守卫。
