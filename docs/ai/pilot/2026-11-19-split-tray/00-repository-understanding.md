# 00 Repository Understanding

- 宿主 `ForgeSelf.Api` 原来是 `net10.0-windows` + `UseWindowsForms`，Kestrel 与托盘在同一个 exe。
- `Program.cs` 的 `--console` 和无参数交互模式调用 `StartTrayIcon`。Windows 服务模式用 `TrayProcessStarter` 把同一个 exe 配 `--tray` 拉到用户会话，管道只传 `shutdown`。
- 加密、`ForgeSetting`、`ServiceManager` 都在 API 工程里。托盘打开主界面要读同一份配置里的令牌。
- 测试工程同样是 `net10.0-windows`。输出目录被 `scripts/dev-stack.ps1` 写成 `bin/Debug/net10.0-windows`。
- 打包真源 `docs/04-standards/packaging-upgrade-backup.md` §1.6 原来写「托盘不拆独立程序」。本次按 ADR-007 改掉这句。
- 适用问题类：无。这是进程边界调整，不是已登记缺陷的复发。
