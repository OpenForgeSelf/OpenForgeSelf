# 03 · Plan

## Files

- `ForgeSelf.Abstractions/TerminalOpenInbox.cs`：进程内收件箱与口令键判断。
- `ForgeSelf.Api/Services/Terminal/TerminalOpenParser.cs`：把协议 URL 和 `--open-terminal` 收成 query。
- `ForgeSelf.Api/Services/Terminal/TerminalLaunchBootstrap.cs`：主实例入箱、协议登记、打开页面；从实例转交。
- `ForgeSelf.Api/Services/Terminal/TerminalInstanceForwarder.cs`：向 `127.0.0.1` POST inbound，不轮换密钥。
- `ForgeSelf.Api/Program.cs`：互斥量被占用时转交，否则收起启动参数。
- `Plugins/SmartTerminal/Controllers/SmartTerminalController.cs`：GET/POST `/inbound`。
- `Plugins/SmartTerminal/Services/SmartTerminalToolFunctions.cs`：`CreateHostScope`；创建会话后入箱。
- `Plugins/AIAgent/Services/AIAgentService.cs`：默认作用域加上 `smart-terminal`。
- `Plugins/SmartTerminal/web/src/launchFromQuery.ts` 与 `SmartTerminalView.vue`：深链开标签。
- `ForgeSelf.Web/src/composables/useTerminalInbox.ts` 与 `App.vue`：主壳取走收件箱。

## Test Plan

解析器单测、插件 `launchFromQuery` 单测、宿主 `terminalOpen` 单测、dev-stack 上页面深链与第二个进程转交。

## Verification

`dotnet test` 过滤 `TerminalOpenParserTests` 与 `AIAgentToolScopeTests`。插件 `pnpm test` + `vue-tsc`。宿主 `vue-tsc`。dev-stack 只读走查以外的真实点击由浏览器完成。
