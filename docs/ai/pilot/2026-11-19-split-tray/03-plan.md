# 03 Plan

## Implementation Steps

1. 新建 `ForgeSelf.Host`（`net10.0`）。迁入 `ForgeSetting`、`ForgeConfig`、加密三件、`ApiServerKeyService`、`ServiceManager` 及其接口。数据根解析收成 `HostDataRoot`，`DataLocationService` 改为调用它。
2. 新建 `ForgeSelf.Tray`。迁入 `TrayIconManager`。入口读 `--api-exe` 旁的 `appsettings.json`，用 `TryReadActiveKeyPlain` 取令牌。
3. `ForgeSelf.Api` 改为 `net10.0`，去掉 `UseWindowsForms` 和 H.NotifyIcon。`Program.cs` 删除同进程托盘。`TrayHostLauncher` 在 Windows 上启动托盘 exe。`WindowsService` 改为启动托盘 exe，不再把 API exe 配 `--tray`。
4. `ServicePipeServer` 改成一条双工管道，双向传递 `shutdown`。
5. 测试工程改为 `net10.0`。托盘单测移到 `ForgeSelf.Tray.Tests`（仍是 `net10.0-windows`）。`dev-stack.ps1` 输出目录改为 `net10.0`，并在 Windows 上加编托盘。`publish-host.ps1` 把 `ForgeSelf.Tray.exe` 拷进版本目录。
6. 回写 packaging §1.6 与 ADR-007 状态。

## Test Plan

- `ForgeSelf.Api.Tests` 中 `TraySplitTests`：启动条件、参数串、管道双向 shutdown。
- 数据根与安装根相关既有测试复跑。
- Linux 上启动 API 并请求 HTTP。
- 托盘工程只做编译。菜单点击不在本环境执行。

## Verification

- Build：`dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` 与 `dotnet build ForgeSelf.Tray/ForgeSelf.Tray.csproj`。
- Unit：`dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`。
- Integration：`dotnet run` 后 `curl` 监听端口。
- E2E：本批不跑 Playwright。托盘 UI 留到 Windows。
