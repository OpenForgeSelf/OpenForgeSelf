# Intent

> 阶段：Stage 1｜Why → What → 到什么程度。

## 背景（Why）

本地开发（尤其自动化测试 `dotnet test`）**不应**影响或污染真实宿主的数据根 `~/.forgeself`（数据 / 日志 / 配置 / 数据库）。但现状是：`dotnet test` 既不设 `FORGESELF_DATA_ROOT`、也不设 `ASPNETCORE_ENVIRONMENT=Development`，测试进程内 `DataLocationService.ResolveHostDataDirectory()` 直接回落**真实宿主根**。凡 `WebApplicationFactory<Program>` 用例触发 `Program.cs` / `AppBuilder` 顶层代码，就会把日志、配置、数据库连接串全部指向真实宿主根。

用户输入4 直接质疑此现状并要求「优先解决」。

## Problem

- `Program.cs:36-41` / `AppBuilder.cs:87-107`：`Setting.LogPath = ~/.forgeself/log`、`XTrace.LogPath = ~/.forgeself/log`、`Setting.Save()`、`ConfigUnifier.UnifyAllConfigFiles(~/.forgeself/config)`
- `XCodeConfig.AddXCode(...)`：连接串指向 `~/.forgeself/ForgeSelf.db` 与 `~/.forgeself/plugins/*/*.db`
- `InitializeXCodeDatabase` 仅在 `env=="Testing"` 时早退，而 WAF 默认环境非 Testing → 存在在**真实库建表**的路径

## 目标（What）

让 `dotnet test` 在**无任何手工前缀**时，测试进程数据根自动落在**仓库内隔离目录**，真实 `~/.forgeself` 零写入；同时尊重 e2e / CI / 操作者的显式设置。

## 到什么程度（Expected Outcome / Success Criteria）

1. 不加任何前缀跑 `dotnet test`，测试进程解析出的数据根 ≠ `~/.forgeself`
2. 跑前/跑后 `~/.forgeself`（含 `log/`、`config/`、`*.db`）无新增写入（实证）
3. 守卫测试先红后绿
4. 显式 `FORGESELF_DATA_ROOT` / `ASPNETCORE_ENVIRONMENT=Development` 仍被尊重，既有数据根测试保持绿

## 边界（Constraints）

- 只改测试项目（`ForgeSelf.Api.Tests`）+ 1 处文档
- **不动** `DataLocationService` 生产回落语义（`DataLocationServiceTests` 断言默认路径语义，改动会连红）
- 不加新依赖；不碰 e2e 基建
- 隔离范围经用户拍板：**仅 `FORGESELF_DATA_ROOT`**，不重定向 `TEMP`/`TMP`