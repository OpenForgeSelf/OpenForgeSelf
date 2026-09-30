# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**。禁止只写「修改 Service、增加测试」。
> Task ID：PILOT-050

## Files To Change

- file: `ForgeSelf.Api/StartupPortResolver.cs`（新增）
  reason: FR-1 后端启动端口覆盖解析器（env `FORGESELF_PORT` ＞ CLI `--server-port`，1024–65535 校验，覆盖并 `Save()` 落盘）。
- file: `ForgeSelf.Api/AppBuilder.cs`
  reason: 在 `ConfigUnifier.UnifyAllConfigFiles(configRoot)`（约第 94 行）之后接入 `StartupPortResolver.ResolveAndApply(args)`，使后续 TrayIconManager 注册(263)、StartTrayIcon(272)、端口绑定(524) 均读到覆盖值；`app.Urls.Add($"http://0.0.0.0:{port}")` 改为读 `ForgeSetting.Current.PortNumber`（已如此）。
- file: `ForgeSelf.Api.Tests/StartupPortResolverTests.cs`（新增）
  reason: AC-1 单测覆盖优先级/校验/无覆盖不落盘逻辑；挂 `[Collection("EnvVarIsolation")]` 隔离 env。
- file: `ForgeSelf.Web/e2e/helpers/free-port.ts`（新增）
  reason: FR-4 空闲端口探测 + claim 锁（`os.tmpdir()/forgeself-e2e-ports/<port>.lock`，`fs.openSync('wx')` + PID/cwd/mtime 老化校验），提供 `pickPortSync`/`pickPortAsync`。
- file: `ForgeSelf.Web/e2e/helpers/e2e-env.ts`（新增）
  reason: FR-4/FR-5 统一读取注入通道：`backendUrl()`、`frontendUrl()`、`e2eState()`（env → `current.json` PID 校验 → `state.json` → 默认 7102/7002）。
- file: `ForgeSelf.Web/e2e/helpers/real-auth.ts`
  reason: T3 去除时间戳正则（`/^\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}$/`，目录稳定化后必然失配），改为走 `e2e-env.ts`。
- file: `ForgeSelf.Web/e2e/global-setup.ts`
  reason: T4 e2eRoot 改为 `wt-<hash8>` 稳定派生；残留进程保护；清 `publish`/`data`；宿主 env 传 `FORGESELF_PORT` + `FORGESELF_DATA_ROOT`；SQLite DLL 无条件覆盖（修陈旧隐患）；写 `current.json` + `host.pid`；`E2E_CLEAN=1` 清空。
- file: `ForgeSelf.Web/vite.config.ts`
  reason: T5 `server.port` 动态 + 按需 `strictPort`；四个代理 target 改 router 运行时解析（避免 webServer 先于 globalSetup 启动导致前端端口未定）。
- file: `ForgeSelf.Web/playwright.config.ts`
  reason: T5 同步定前端端口、`baseURL`/`webServer.url` 动态、`webServer.env` 透传、`timeout` 120s、`FORGESELF_MCP_GATEWAY_PORT` 默认回落（多 worktree 抢端口）。
- file: `ForgeSelf.Web/e2e/*.spec.ts`（含 `token-init.spec.ts:55,73`、`profile.spec.ts:48`、注释等）
  reason: T6 清理 7102/7002 硬编码，改从 `e2e-env.ts` 读取，保留默认回落。
- file: `ForgeSelf.Web/e2e/port-config.spec.ts`
  reason: T7 改端口无关：`BACKEND_URL`→`backendUrl()`；`restorePort` 恢复运行前端口；断言改模板串；`BACKEND_CONFIG_PATH` 从 `current.json.dataDir` 派生；文件回退失败改 `throw`。
- file: `ForgeSelf.Web/playwright.e2e-published.config.ts`
  reason: T8 端口来源改 `E2E_BACKEND_URL ?? 7102`；修复 `publishDir = resolve(root,'../publish')` 越级 bug（本机目标不存在，应指向真实 publish 目录）。
- file: `AGENTS.md`（§2.3/§5.3/§5.6）、`docs/04-standards/agent-workflow.md`（B2/Part C）
  reason: FR-7/AC-8 回写新端口注入机制与门禁说明。

## Implementation Steps

1. **T1 后端端口覆盖**：新建 `StartupPortResolver.cs`（`ResolveFromSources` 纯解析 env＞CLI、`ResolveAndApply` 覆盖+落盘、内部 `TryParsePort` 校验 1024–65535）；`AppBuilder.cs` 在 ConfigUnifier 后接入；新建 `StartupPortResolverTests.cs`；跑 `dotnet test --filter StartupPortResolver`。
2. **T2 e2e 辅助**：新增 `free-port.ts`（探测+claim 锁）、`e2e-env.ts`（注入通道+state 解析）。
3. **T3** 改 `real-auth.ts` 走 `e2e-env.ts`，去除时间戳正则。
4. **T4** 改 `global-setup.ts`：稳定目录 `wt-<hash8>`、残留保护、清 publish/data、宿主 env 注入、`current.json`/`host.pid`、SQLite 覆盖、`E2E_CLEAN`。
5. **T5** 改 `vite.config.ts` + `playwright.config.ts` 端口动态与 env 透传。
6. **T6** grep 全量清理 e2e 7102/7002 硬编码（保留默认回落）。
7. **T7** `port-config.spec.ts` 端口无关改造。
8. **T8** `playwright.e2e-published.config.ts` 端口来源 + `publishDir` 修复。
9. **T9 门禁**：前端 `pnpm run check`/`test`；后端「中档」全量 `dotnet test`（基线 1516/13 红，对表持平）；「深档」全量 e2e 4 worker（基线 102 passed/82 failed，对表持平）；平台限制如实上报。
10. **T10** 回写 AGENTS.md/agent-workflow.md。

## Test Plan

1. 后端单测：`dotnet test ForgeSelf.Api.Tests --filter StartupPortResolver` —— 覆盖 env 优先、CLI 两种形式、非法/越界/空白忽略、无覆盖不落盘。
2. 前端：`cd ForgeSelf.Web && pnpm run check && pnpm run test` —— 类型/lint + vitest 绿。
3. 中档（后端全量）：`dotnet test ForgeSelf.Api.Tests` —— 基线 1516/13 红，本任务不应新增红。
4. 深档（e2e 全量）：`cd ForgeSelf.Web && npx playwright test --workers=4` —— 基线 102 passed/82 failed，本任务不应新增失败（平台限制：依赖 Windows 宿主，非 Windows 无法跑，如实上报）。
5. 专项验证：设 `FORGESELF_PORT` 启动宿主，确认监听该端口（e2e global-setup 实跑验证）。

## Verification

### Build

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
cd ForgeSelf.Web && pnpm run build
```

### Unit Test

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter StartupPortResolver
```

### Integration Test

```bash
# 后端全量（中档门禁）：基线 1516/13 红，对表持平
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
```

### E2E

```bash
# 深档门禁（依赖 Windows 宿主，4 worker）：基线 102 passed / 82 failed，对表持平
cd ForgeSelf.Web && npx playwright test --config=playwright.config.ts --workers=4
```

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run check   # 类型 + lint
cd ForgeSelf.Web && pnpm run test    # vitest
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-09-30 | 任务书原 `ASPNETCORE_URLS` 覆盖方案实测不成立（`AppBuilder.cs:525` 无条件 `app.Urls.Add` + Addresses 非空） | 走 ASPNETCORE_URLS | 改新增后端 `--server-port` + `FORGESELF_PORT` env 优先（已获用户授权改后端 C#） |
| 2026-09-30 | 任务书统计「7102×37 / 7002×11」与实测不符 | 按 37/11 清理 | 实测 e2e/ 下 7102×50（非注释 17）、7002×9（非注释 2），按真实计数清理（代码级 11 处全改，注释保留描述默认回落） |
| 2026-09-30 | 后端端口选口时机：原计划 globalSetup 异步 pick + vite 代理 router 运行时解析 | globalSetup 异步定后端口 | 改为 playwright.config 求值期同步认领（前后端都同步定，含后端）；webServer.env 在 spawn 前即可定死 E2E_BACKEND_URL，无需 router hack；globalSetup 保留 pickFreePort 兜底 |
| 2026-09-30 | real-auth.ts CONFIG_PATH 模块加载期求值在无运行态时得到空串（--list 即炸） | 常量模块期求值 | 改 configPath() 函数惰性求值 + 空路径显式报错 |
| 2026-09-30 | StartupPortResolver 单测原走进程级 env（EnvVarIsolation 集合内改 env），与外部集合并行存在窗口期外泄风险 | 测试改进程 env | 解析器加 env 查找函数注入重载，测试零进程 env 变更、天然并行安全（实证：全量失败 70→49） |
| 2026-09-30 | 深档 e2e 首轮：宿主监听就绪后仍被托盘创建失败打崩（H.NotifyIcon TryCreate failed → STA 线程 Unhandled exception → 进程退出） | Plan 未覆盖托盘子系统 | 宿主 Program.cs StartTrayIcon 加 `FORGESELF_NO_TRAY=1` 守卫早退（无人值守场景托盘无意义），e2e env 注入该变量——超出原 Plan 文件清单的必要最小后端改动 |
