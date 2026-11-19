---
risk: { level: L3, note: 宿主进程边界与输出目录 }
expected_files:
  - ForgeSelf.Api
  - ForgeSelf.Host
  - ForgeSelf.Tray
  - ForgeSelf.Tray.Tests
  - ForgeSelf.Api.Tests
  - ForgeSelf.slnx
  - scripts/dev-stack.ps1
  - scripts/release/publish-host.ps1
feature: "008"
rollback: 把托盘源码移回 API，目标框架改回 net10.0-windows，并恢复 UseWindowsForms 与 H.NotifyIcon
---

# 04 Task

- Task ID: split-tray
- Objective: API 成为跨平台 Web API，Windows 托盘独立成程序。

## Allowed

- `ForgeSelf.Api`、`ForgeSelf.Host`、`ForgeSelf.Tray`、`ForgeSelf.Tray.Tests`、`ForgeSelf.Api.Tests` 里与托盘/目标框架/管道直接相关的文件。
- `scripts/dev-stack.ps1`、`scripts/release/publish-host.ps1`。
- 打包真源 §1.6、ADR-007、上手指南里的目标框架事实。

## Forbidden

- 改 `VersionPrefix`。
- 改插件目标框架。
- 停掉或重启用户已在跑的宿主。
- 在本环境宣称托盘菜单已经点过。

## Acceptance Criteria

- [ ] AC1 API 在 Linux 上不带 `EnableWindowsTargeting` 构建通过
- [ ] AC2 API 进程对监听端口返回 HTTP
- [ ] AC3 管道双向 shutdown 单测通过
- [ ] AC4 非 Windows 不拉起托盘
- [ ] AC5 托盘工程能编过，运行留到 Windows

## Expected Files

见头部 `expected_files`。

## Verification Commands

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
dotnet build ForgeSelf.Tray/ForgeSelf.Tray.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter FullyQualifiedName~TraySplitTests
dotnet run --project ForgeSelf.Api -- --console
```
