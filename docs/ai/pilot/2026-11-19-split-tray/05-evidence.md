# Evidence

> 只记录这次在 Linux 上实际跑过的结果。Windows 托盘菜单、服务安装、发布包未在本环境执行。

## Task

split-tray

## Changed Files

- 新建 `ForgeSelf.Host`（net10.0）、`ForgeSelf.Tray`（net10.0-windows）、`ForgeSelf.Tray.Tests`。
- API 目标框架改为 `net10.0`，去掉 UseWindowsForms 与 H.NotifyIcon。
- 托盘源码与服务控制、配置、令牌读取移出 API。交互模式经命名管道 `shutdown` 停 API。
- `dev-stack.ps1` 的输出目录改为 `bin/Debug/net10.0`；`publish-host.ps1` 增加复制 `ForgeSelf.Tray.exe` 的步骤（该脚本未在本环境执行）。
- 插件复制目标改为小写 `plugins`，并同时替换 `\` 与 `/`。测试夹具 `mock-mcp-server.js` 用 Link 落到 `plugins/`。
- `ServiceControllerWrapper` 的构造测试在非 Windows 上跳过。

## Build

Command:

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj --nologo
dotnet build ForgeSelf.Tray/ForgeSelf.Tray.csproj --nologo
dotnet build ForgeSelf.Tray.Tests/ForgeSelf.Tray.Tests.csproj --nologo
```

Result: PASS（来源等级：Verified）

API 工程未设置 `EnableWindowsTargeting`。三次构建均为 0 error。托盘工程靠自身 csproj 的 `EnableWindowsTargeting` 在 Linux 上编过。AC1、AC5 的编译部分成立。托盘窗口未启动。

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --nologo --filter "FullyQualifiedName~TraySplitTests|FullyQualifiedName~ServiceControllerWrapper_ConstructAndDispose|FullyQualifiedName~McpToolInvokeTests.Invoke_RealMockServer|FullyQualifiedName~McpStdioFramingTests.Auto_Framing_Ndjson"
```

Result: PASS（来源等级：Verified）

```text
Passed!  - Failed:     0, Passed:    10, Skipped:     1, Total:    11, Duration: 5 s
```

跳过的 1 条是 `ServiceControllerWrapper_ConstructAndDispose_DoesNotThrow`（非 Windows）。`TraySplitTests` 含管道双向 `shutdown` 与 `ShouldLaunch`。AC3、AC4 成立。

全量（插件目录改为小写之后重跑）：

```text
Failed!  - Failed:    90, Passed:  3662, Skipped:    10, Total:  3762, Duration: 3 m 51 s
```

来源等级：Verified。90 条失败抽查后归为既有的 Windows 假设：盘符路径、robocopy、GBK 码页、反斜杠拼接、`npx.exe`。不是本次管道或 Kestrel 启动引入的红。托盘测试工程只做了 build，没有 `dotnet test`（来源等级：Unknown，留到 Windows）。

## Runtime

Command: `dotnet run --project ForgeSelf.Api --no-build -- --console`，数据根指向临时目录，`FORGESELF_NO_TRAY=1`。

Result: PASS（来源等级：Verified）。时间 2026-11-19 15:08（Asia/Shanghai）。

```text
监听端口: 7102
Now listening on: http://0.0.0.0:7102
跳过托盘进程（非 Windows 或 FORGESELF_NO_TRAY=1）
```

`GET /api/health`：

```text
HTTP/1.1 200 OK
{"status":"healthy","timestamp":"2026-11-19T07:08:13.8291386+00:00"}
```

日志同时记下 23 个插件从 `bin/Debug/net10.0/plugins` 被发现。AC2 成立。

## AC 对照

- AC1 Verified：API `dotnet build` 0 error，工程未开 `EnableWindowsTargeting`。
- AC2 Verified：7102 返回 HTTP 200。
- AC3 Verified：`TraySplitTests` 管道用例在上述 10 条通过里。
- AC4 Verified：`ShouldLaunch` 用例在同一通过集里。
- AC5 Verified 编译；菜单点击 Unknown。

## 未验证

- 托盘菜单点击、`CreateProcessAsUser`、服务安装 binPath、`publish-host.ps1` 产出的 zip。来源等级：Unknown。按本次要求留到 Windows。
