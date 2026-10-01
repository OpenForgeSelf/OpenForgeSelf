# Plan

> 阶段：Stage 3｜具体到真实文件路径。
> Task ID：PILOT-plugin-dev-experience

## Files To Change

### 新增（后端）

- file: `ForgeSelf.Api/Plugins/Dev/DevMode.cs`
  reason: dev 总闸与四个子开关的唯一判定点；`Initialize(string[] args)` 在 AppBuilder 最早调用，支持 env（FORGESELF_DEV_MODE 等）+ CLI（--dev）+ ASPNETCORE_ENVIRONMENT=Development 三通道，未触发全关。
- file: `ForgeSelf.Api/Plugins/Dev/PluginShadowCopy.cs`
  reason: shadow 核心逻辑：解析入口路径（versions/<current> → 扁平 → **源码 bin/<FORGESELF_DEV_CONFIG>/net10.0/**）→ 计算内容 hash → 复制入口 dll + `*.deps.json` + 私有依赖到 `%TEMP%/forge-dev-shadow/<id>/<hash>/`（沿用 publish-plugin.ps1:145-149 的宿主共享 DLL 排除名单）→ 返回 shadow 入口路径；含陈旧目录清理。
- file: `ForgeSelf.Api/Plugins/Dev/PluginErrorStore.cs`
  reason: 进程内 `ConcurrentDictionary<pluginId, PluginErrorRecord>`，记录 Message/ExceptionType/StackTrace/OccurredAt；Set/Clear/TryGet/GetAll。
- file: `ForgeSelf.Api/Plugins/Dev/PluginLogScope.cs`
  reason: `AsyncLocal<string?>` 作用域（Push/Dispose）+ `PluginTaggedLog : ILog` 装饰器——透传 Enable/Level，六方法（Debug/Info/Warn/Error/Fatal/Write，成员清单已从 NewLife.Core 11.17.2026.701 XML 文档核实）命中作用域时给 format 前缀 `[plugin:<id>] `。
- file: `ForgeSelf.Api/Controllers/DevController.cs`
  reason: dev-only 端点：`POST /api/dev/plugin/{id}/reload`、`POST /api/dev/plugin/reload-all`、`GET /api/dev/diagnostics`；类级 `[Authorize("ApiKeyPolicy")]` + action 首行 dev-gate（未启用返回 404）；devGate 以内部构造参数注入便于测试。

### 修改（后端）

- file: `ForgeSelf.Api/Plugins/PluginManager.cs`
  reason: ① `ResolvePluginInstance:404` 装载点插 dev 分支（DevMode.ShadowCopy 时 `assemblyPath = PluginShadowCopy.Prepare(metadata) ?? assemblyPath`）；② 五处 catch（:385/:447/:569/:628/:764）同步写 PluginErrorStore；③ 装载/Enable/Disable/Destroy 包 PluginLogScope；④ 新增 `ReloadPlugin(pluginId)`：停用→ForceCollect→重 shadow→Enable（含 HostedService warning）；⑤ 成功装载/启用时 Clear 错误记录。
- file: `ForgeSelf.Api/AppBuilder.cs`
  reason: ① `CreateWebApplication` 入口调 `DevMode.Initialize(args)`；② `:304-314` 插件根目录解析支持 `--plugins-dir=`/`FORGESELF_PLUGINS_DIR`（CLI > env > 默认，未提供零副作用）；③ `:87` 在 dev 下先装 `PluginTaggedLog` 装饰器并放开 Level=Debug（Production 保持 Info 不变）。
- file: `ForgeSelf.Api/Models/Plugins/PluginInfoDto.cs`
  reason: 新增可空 `Error` 字段（向前兼容）。
- file: `ForgeSelf.Api/Controllers/PluginController.cs`
  reason: 列表与 `detail/{id}` 映射时从 PluginErrorStore 填充 `Error`。
- file: `ForgeSelf.Api/Plugins/Services/PluginAwareControllerActivator.cs`
  reason: `Create` 命中插件控制器时包 `PluginLogScope.Push(pluginId)`（请求期日志 tag；通过 IPluginServiceRegistry 新增的 controllerType→pluginId 查询或注册表 Resolve 前查询实现）。

### 修改（前端）

- file: `ForgeSelf.Web/src/types/plugin.ts`
  reason: `PluginInfo` 增加可选 `error` 字段。
- file: `ForgeSelf.Web/src/views/PluginStore.vue`
  reason: Error 态卡片展示错误原因（el-tooltip/展开区），样式遵循 0 自定义 token 约定（--el-* + color-mix）。

### 新增（脚本）

- file: `scripts/dev-plugin.ps1`
  reason: 一条命令后端回路：`dotnet build Plugins/<X>` → 取 token（`node scripts/get-forge-token.cjs --config <dev数据根>/config/ForgeSetting.config`，可 -Token 覆盖）→ `POST /api/dev/plugin/<id>/reload`；支持 `-All`、`-HostUrl`、`-Configuration`。
- file: `scripts/dev-plugin-web.ps1`
  reason: 2B opt-in：在插件 `web/` 生成 `vite.config.dev.mjs`（import 插件自身 vite.config.ts 合并 + alias 五个共享包到宿主 shim 绝对 URL + cors）→ `vite serve --config vite.config.dev.mjs -p <Port>` → 写 `web/.dev-server.json`；Ctrl+C 清理。

### 修改（文档，6 处漂移）

- file: `docs/05-guides/plugin-hot-reload-limitations.md` — 修正「版本 API 全死」（Initialize 已接线）与 watcher 矛盾；补 dev reload 新路径。
- file: `ForgeSelf.Api/Plugins/README.md` — 更正 watcher 描述（:160-186）。
- file: `scripts/publish-plugin.ps1` — 头注释删 FileSystemWatcher 措辞（:9-12/32-35）。
- file: `docs/05-guides/plugin-frontend-development.md` — `/frontend/**` → `/web/dist/**` 等。
- file: `.agents/skills/plugin-development/SKILL.md` — 源码位置更正为仓库根 `Plugins/<X>`；登记 dev 快速回路。
- file: `ForgeSelf.Api/Properties/launchSettings.json` — https profile 端口 7002 → 7102。

### 新增（测试）

- file: `ForgeSelf.Api.Tests/Plugins/Dev/DevModeTests.cs` — env/CLI 解析矩阵（含未设置=关）。
- file: `ForgeSelf.Api.Tests/Plugins/Dev/PluginShadowCopyTests.cs` — 复制入口+deps、排除宿主共享、hash 目录稳定、bin 布局解析、陈旧清理。
- file: `ForgeSelf.Api.Tests/Plugins/Dev/PluginErrorStoreTests.cs` — Set/TryGet/Clear。
- file: `ForgeSelf.Api.Tests/Plugins/Dev/PluginTaggedLogTests.cs` — 作用域命中前缀、未命中零改动、成员透传。
- file: `ForgeSelf.Api.Tests/Controllers/DevControllerTests.cs` — devGate=false → 404 语义；devGate=true → reload 流程经内存 PluginManager 验证（复用既有 PluginManagerTests 的构造方式）。

## Implementation Steps

1. DevMode + PluginShadowCopy + PluginErrorStore + PluginLogScope（纯新增，先行编译）。
2. PluginManager 接线（shadow 分支、ErrorStore 五处、ReloadPlugin、作用域包裹）+ AppBuilder（Initialize/plugins-dir/装饰器）→ `dotnet build` 过。
3. DevController + PluginInfoDto.Error + PluginController 映射 → build。
4. 前端 types + PluginStore.vue。
5. 测试五个文件 → `dotnet test --filter Dev|ShadowCopy|ErrorStore|TaggedLog`。
6. 脚本 dev-plugin.ps1 / dev-plugin-web.ps1。
7. 文档 6 处 + launchSettings。
8. 全量验证（见 Verification）。

## Test Plan

1. 单测矩阵覆盖：DevMode 三通道判定与未设默认关；shadow 复制/排除/hash 稳定；ErrorStore 生命周期；TaggedLog 前缀与透传；DevController 404/重载编排。
2. 集成实跑（真 dev 宿主）：`FORGESELF_DEV_MODE=1 FORGESELF_PORT=<动态> FORGESELF_DATA_ROOT=<临时>` + `--plugins-dir <源码>/Plugins` 起宿主 → AC-3/4/5/6/8/9 逐条。
3. 基线对照：build 0 错误；test 失败名 ⊆ 基线 118。

## Verification

### Build

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug --nologo -v m
```

### Unit Test

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --logger "console;verbosity=detailed"
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~Dev|FullyQualifiedName~ShadowCopy|FullyQualifiedName~ErrorStore|FullyQualifiedName~TaggedLog"
```

### Integration Test

```bash
# dev 宿主实跑（本会话自起自停的隔离实例，非用户运行实例）
FORGESELF_DEV_MODE=1 FORGESELF_PORT=<动态> FORGESELF_DATA_ROOT=/tmp/forge-dev-verify FORGESELF_NO_TRAY=1 \
  dotnet ForgeSelf.Api/bin/Debug/net10.0-windows/ForgeSelf.dll --plugins-dir <repo>/Plugins
curl -s http://localhost:<动态>/api/dev/diagnostics -H "Authorization: Bearer <token>"
curl -s -X POST http://localhost:<动态>/api/dev/plugin/file-tools/reload -H "..."
```

### E2E

```bash
# N/A：本次为宿主 dev 能力，dev-off 行为与改动前一致（AC-3 保障）；e2e 套件不感知 dev 开关。
# 全量 e2e（≈38 分钟）留待与插件五步门禁一同执行的场景。
```

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run check && pnpm run test   # 对照基线：global-setup.ts:183 既有红不新增
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-01 | NewLife.Core 源码本地不可得 | 拟读 NewLife.Core 源码核对 ILog | 改用 NuGet 包 XML 文档（11.17.2026.701）提取成员：P Enable/Level + M Debug/Info/Warn/Error/Fatal/Write |
| 2026-10-01 | 前端 2A 的 mtime 指纹必要性 | 选型报告建议 mtime+size 指纹 | 核实 `ComputeWebVersion` 已是**内容**指纹（SHA256），dev 直读源 dist 后内容变化天然破缓存 → mtime 方案取消，仅补 no-store 兜底（FR-4.1 精简） |
