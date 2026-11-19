# ADR-007：Windows 托盘独立成程序，API 改为跨平台 Web API

- 状态：**Accepted**（2026-11-19 按本方案实施。Windows 上托盘菜单的点击留到 Windows 环境验收）
- 关联：[`04-standards/packaging-upgrade-backup.md`](../04-standards/packaging-upgrade-backup.md) §1.6（现行写「托盘不拆独立程序」，本 ADR 若接受则改这句）
- 背景：云代理是 Linux。宿主 `ForgeSelf.Api` 的 `TargetFramework` 是 `net10.0-windows` 且 `UseWindowsForms=true`，Linux 上 `dotnet build` 报 NETSDK1100。加上 `-p:EnableWindowsTargeting=true` 可以编过（SDK 10.0.115，0 error），`dotnet exec` 退出码 150，缺 `Microsoft.WindowsDesktop.App`。该框架没有 Linux 运行时。

## 现在实际在跑什么

云环境里起来的是前端 Vite，地址 `http://127.0.0.1:7002`。文本工具的 SHA-256 在浏览器里计算，没有连上 API。宿主进程没有跑起来。

产品进程今天是这样串的：

1. 根启动器 `ForgeSelf.Bootstrapper`（`win-x64` + WindowsDesktop）只负责拉起版本目录里的业务层 exe。
2. 业务层就是 `ForgeSelf.Api` 这一个 exe。控制台和 `--console` 在**同一进程**里先 `StartTrayIcon` 再 `app.Run()`（`ForgeSelf.Api/Program.cs`）。
3. 以 Windows 服务启动时，Kestrel 在服务进程里，`WindowsService.TryStartTrayProcess` 再用 `TrayProcessStarter` 拉起**同一个 exe** 的 `--tray`。命名管道只传 `shutdown`。服务模式下点「检查更新」目前只打日志，没有真正调用更新。
4. 托盘菜单在 `TrayIconManager`：打开主界面、检查更新、安装/卸载/重启服务、关于、退出。打开主界面用的令牌由本进程的 `ApiServerKeyService` 从配置读出。

## 目标

API 用 `net10.0`，Linux 上不设 `EnableWindowsTargeting` 就能 `dotnet build ForgeSelf.Api`，并且 `dotnet run --project ForgeSelf.Api -- --console` 能监听端口。Windows 上托盘仍是原来的菜单。成功判据：Linux 构建 0 error；`curl` 到该端口得到 HTTP 响应；Windows 上托盘 exe 能打开主界面。

## 改动

1. 新建 `ForgeSelf.Tray/ForgeSelf.Tray.csproj`（`net10.0-windows`、`UseWindowsForms`、H.NotifyIcon）。迁入 `ForgeSelf.Api/Services/TrayIconManager.cs` 和 `Program.cs` 里的 `RunTrayMode`。托盘不引用 API 工程。
2. `ForgeSelf.Api/ForgeSelf.Api.csproj` 与 `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj` 的目标框架改为 `net10.0`，去掉 `UseWindowsForms` 和 H.NotifyIcon。输出目录从 `bin/Debug/net10.0-windows` 改为 `bin/Debug/net10.0`。仓库里写死旧目录的脚本、测试、发布步骤一起改。
3. `Program.cs`、`AppBuilder.cs` 删掉同进程 `StartTrayIcon` 和 `TrayIconManager` 的 DI 注册。仅当 `OperatingSystem.IsWindows()` 且 `FORGESELF_NO_TRAY` 不是 `1` 时启动 `ForgeSelf.Tray`。Linux 不启动。`WindowsService.TryStartTrayProcess` 改为拉起托盘 exe，不再给 API exe 加 `--tray`。管道仍只传 `shutdown`。
4. `ServiceManager`、`ServiceConfig`，以及托盘读令牌用到的 `ApiServerKeyService` / `AesSecretEncryptionService` / `MachineKeyProvider`，抽到新的 `net10.0` 类库，API 和托盘都引用它。托盘「检查更新」改调已有的 `POST /api/update`（`UpdateController`，要带现有 API 密钥），不再在托盘进程里做更新编排。
5. 拍板后回写 `packaging-upgrade-backup.md` §1.6：版本目录里 API exe 与 `ForgeSelf.Tray.exe` 并列；托盘不再写「不拆独立程序」。根启动器仍只在 Windows 上编。

## 不改

- 插件加载、Kestrel、更新下载与解压、数据根，仍在 API。
- 根启动器继续是安装包入口，Linux 开发不编它。
- 不新造一套更新协议。服务模式下「检查更新」今天就是空日志，这次只接到已有 HTTP，不扩大更新功能。
- 不把 Windows 服务安装从产品里删掉。Linux 上没有托盘，也就没有这几项菜单。

## 验证

- Linux：`dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` 不传 `EnableWindowsTargeting`，0 error。`dotnet test ForgeSelf.Api.Tests`。`dotnet run --project ForgeSelf.Api -- --console` 后 `curl` 监听端口。
- 托盘工程只在 Windows 上编。本环境不声称托盘菜单已点过。Windows 验收：图标在、打开主界面带上令牌、交互模式下退出会停 API。

## 代价与风险

- 全仓 `net10.0-windows` 路径要改成 `net10.0`。漏改的脚本会去旧目录找 dll。
- 这和现行 §1.6「托盘不拆独立程序」相反。接受本 ADR 就改那一节，避免两份真源。
- 回滚：托盘源码移回 API，目标框架改回 `net10.0-windows`，恢复 `UseWindowsForms`。

## 退出行为（建议默认）

两种现状不要并成一种：

- 控制台 / 双击：托盘「退出」停掉 API（现在是同一进程，`StopAsync`）。
- Windows 服务：托盘「退出」只退出托盘进程，服务继续跑。服务停止时用管道通知托盘退出。

## 待拍板

1. 是否按本 ADR 拆托盘、把 API 改为 `net10.0`？默认：是。
2. 交互模式下托盘「退出」是否同时停 API？默认：是。服务模式下仍只关托盘。
