# 02 Spec

## Functional Requirements

- API 目标框架为 `net10.0`，工程里不再引用 `System.Windows.Forms` 或 H.NotifyIcon。
- 新建 `ForgeSelf.Tray`（`net10.0-windows`）。菜单仍是：打开主界面、检查更新、安装/卸载/重启服务、关于、退出。
- 仅当进程在 Windows 上且 `FORGESELF_NO_TRAY` 不是 `1` 时，API 拉起托盘 exe。
- 交互模式：托盘「退出」向管道写 `shutdown`，API 停止。服务模式：托盘「退出」只结束托盘进程；服务停止时向托盘写 `shutdown`。
- 托盘「检查更新」调用已有 `POST /api/update/check`，带 Bearer 令牌。
- 服务安装用的可执行文件是 API 入口，不是托盘 exe。
- 托盘读令牌失败时不重写主密钥。

## Input

- 环境变量 `FORGESELF_NO_TRAY`、`FORGESELF_TRAY_EXE`。
- 托盘参数：`--pipe-name`、`--port`、`--role`、`--api-exe`。

## Output

- Linux：只有 Kestrel。
- Windows 发布目录：`ForgeSelf.exe` 与 `ForgeSelf.Tray.exe` 并列。

## Business Rules

- 令牌与数据根的解析规则 API 与托盘共用 `HostDataRoot`。
- 旧参数 `--tray` 传给 API 时不再进入托盘模式，改为启动 Web 宿主。

## Boundary Conditions

- 找不到托盘 exe：API 照常听端口，日志记一笔。
- 非 Windows：不查找、不启动托盘。
- `FORGESELF_NO_TRAY=1`：不启动托盘（e2e 仍可用）。

## Error Handling

- 托盘进程启动失败不拖垮 API。
- 管道没连上时，`SendShutdown` 只记日志。
- 检查更新 HTTP 失败时，托盘气泡显示失败，不抛到进程外。

## Compatibility

- 插件目标框架不变。
- 根启动器仍只在 Windows 上编。
- 输出目录从 `net10.0-windows` 改为 `net10.0`。`dev-stack.ps1` 与仍被执行的文档路径一并改。历史 pilot 记录不改写。

## Non-functional Requirements

- 不新增 NuGet 依赖的大版本。H.NotifyIcon 只留在托盘工程，版本仍是 2.4.1。

## Acceptance Criteria

- AC1 Linux `dotnet build ForgeSelf.Api` 0 error，且不需要 `EnableWindowsTargeting`。
- AC2 `dotnet run --project ForgeSelf.Api -- --console` 后，本机端口返回 HTTP 响应。
- AC3 管道单测在 Linux 上通过：服务端写出的 `shutdown` 被客户端读到，客户端写出的 `shutdown` 触发回调。
- AC4 `ShouldLaunch(false, …)` 为 false；`ShouldLaunch(true, "1")` 为 false。
- AC5 托盘工程能在 Linux 上借助 csproj 里的 `EnableWindowsTargeting` 编过。托盘菜单不在本环境点击。

## Failure Modes

无已知问题类。新增风险是输出目录改名后，漏改的脚本会去旧目录找 dll。本次改了 `dev-stack.ps1` 和测试工程里的复制路径。

## Interaction Design

托盘菜单的可见行为与拆分前相同：点「打开主界面」用本机令牌打开 `http://localhost:{port}/#token=`；点「检查更新」出气泡；点「退出」在交互模式停 API，在服务模式只关托盘。本环境不走查这个窗口。无新页面。
