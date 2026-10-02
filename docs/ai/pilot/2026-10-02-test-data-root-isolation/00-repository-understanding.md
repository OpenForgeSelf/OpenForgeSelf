# Repository Understanding

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。

## 项目结构

OpenForgeSelf（铸己匣）——宿主 + 插件架构。本任务涉及测试基建与数据根解析：

- `ForgeSelf.Api/Program.cs`：入口，含 testhost 短路分支（第 15-21 行）与顶层数据根解析（第 28-41 行）
- `ForgeSelf.Api/DataLocationService.cs`：数据根解析（`FORGESELF_DATA_ROOT` → Development `{BaseDirectory}/data` → 生产 `%UserProfile%/.forgeself`）
- `ForgeSelf.Api/AppBuilder.cs`：WAF 路径下构建宿主，含 `Setting.LogPath` 设定（第 87-89 行）与 `ConfigUnifier`（第 107 行）
- `ForgeSelf.Api/ConfigUnifier.cs`：统一重定向已知 `Config<T>` 的 `FileName` 到数据根 config
- `ForgeSelf.Api/Data/ForgeConfig.cs`：`ForgeConfig<TConfig> : Config<TConfig>`，静态构造把派生配置指到 `{数据根}/config`
- `ForgeSelf.Api.Tests/`：xUnit 测试项目
- `docs/04-standards/agent-workflow.md`：§B12 工程踩坑真源

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 后端 | ASP.NET Core（.NET 10）+ NewLife.XCode + NewLife.Config | ForgeSelf.Api/ForgeSelf.Api.csproj |
| 测试 | xUnit + WebApplicationFactory | ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj |
| 前端 | Vue 3 SPA（本任务不涉及） | ForgeSelf.Web/package.json |

## 架构特点

宿主 + 插件；数据根由 `DataLocationService` 统一解析，所有落盘（config/log/db/plugins）都应相对该根。生产根 = `%UserProfile%/.forgeself`。NewLife 框架 `Config<T>` 默认把配置文件放 `Config\{Name}.config`（相对程序目录），故需 `ConfigUnifier` 统一重定向。

## 测试方式

- 后端：`dotnet build` + `dotnet test`（本 worktree 沙箱需 `dangerouslyDisableSandbox: true`，因系统 Temp 与 `~/.forgeself` 在沙箱不可写）
- 测试进程默认不设 `ASPNETCORE_ENVIRONMENT` → WAF 环境非 Development/Testing，走生产回落路径 → 潜在污染真实宿主根

## 构建命令

```powershell
dotnet build ForgeSelf.Api
dotnet test ForgeSelf.Api.Tests --filter "<filter>"
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| ForgeSelf.Api/ | 宿主后端（数据根解析、配置统一、插件加载） |
| ForgeSelf.Api.Tests/ | 后端测试（本任务改动区） |
| docs/ai/pilot/2026-10-02-test-data-root-isolation/ | 本任务工件目录 |

## 代码组织方式

数据根解析集中在 `DataLocationService`（双解析重载：静态版 + 实例版，语义须一致）；配置重定向集中在 `ConfigUnifier`。测试项目此前**无任何**进程级环境隔离入口，隔离完全依赖操作者手工命令行前缀。

## 现有工程规范

- `docs/04-standards/agent-workflow.md` §B12 第 830 行：要求跑测前手工前缀赋值 `TEMP`/`TMP` + `FORGESELF_DATA_ROOT=<仓库外或 .tmp 独立目录>`——**易忘且无强制**，本会话就曾忘记设置。
- AGENTS.md §0：开发类任务走 AI-Native 闭环，工件链 00-07 门禁。

## 候选低风险任务

测试侧新增进程级自动隔离，使 `dotnet test` 无手工前缀时数据根自动落在仓库内隔离目录，杜绝污染真实宿主根。低风险：仅改测试项目，不动生产回落语义。

## 选择该任务的原因

用户输入4 明确要求「优先解决」本地开发污染宿主数据/日志这一重大问题。实证测试进程曾尝试写 `C:\Users\Administrator\.forgeself\log\2026_10_02.log`（被拦），且程序目录残留固化了宿主 LogPath 的 Core.config——架构缺陷确凿，凡含 WAF 的测试族必然触达真实宿主根。