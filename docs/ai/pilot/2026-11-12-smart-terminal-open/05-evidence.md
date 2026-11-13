# 05 · Evidence

## Changed Files

见 03。本地 LLM 代理未提交改动未回滚。

## Validation

- AC4 / 解析器：`dotnet test --filter TerminalOpenParserTests|AIAgentToolScopeTests` → 通过 9，失败 0。Verified。
- AC5 / AC1 解析：`Plugins/SmartTerminal/web` `pnpm test` → 25 通过，含 `launchFromQuery` 12 条；`vue-tsc` 退出码 0。Verified。
- 宿主：`ForgeSelf.Web` `vitest run src/utils/terminalOpen.test.ts` → 2 通过；`vue-tsc --noEmit` 退出码 0。Verified。
- AC3：dev-stack `POST /api/smart-terminal/inbound` 带 `password=` → HTTP 400。同端点合法 query → 202。`GET /meta` → 200。Verified。
- AC1：浏览器打开 `http://localhost:7399/smart-terminal?st=1&type=local&shell=pwsh&title=url-open&step=echo%20url-ok`。页面出现标签 `url-open`，输出 `url-ok`，退出码 0，地址栏去掉启动参数。Verified。
- AC2：对已运行的 dev-stack 实例再执行 `dotnet ForgeSelf.dll --console "forgeself://terminal?type=local&shell=pwsh&title=from-fourth"`，退出码 0。页面新增标签 `from-fourth`，地址栏保持 `/smart-terminal`，没有把 query 留在栏上。此前 `from-second` 的 `echo second-ok` 退出码 0。Verified。
- 插件 `pnpm run build` 产出 `web/dist/index.js`。Verified。

## Known Limitations

- dev-stack 状态栏为「AI 离线」，没有做一次真实模型对话。助手作用域与工具 DI 由单测和源码守卫证明，不是一轮线上对话。
- 拉下来的 `SmartTerminalV2Tests` 在本机有 6 条失败，都是去启动 `/bin/bash`。本次未改外壳选择，记入 TODO，不算本批回归。
- 协议登记在 `dotnet` 开发宿主和 `FORGESELF_NO_TRAY=1` 下会跳过。安装后的 `ForgeSelf.exe` 交互启动才会写 HKCU。这一步没有在本机注册表上复验。

## Unresolved Issues

Windows 上 v2 单测默认外壳为 `/bin/bash`。见 TODO。
