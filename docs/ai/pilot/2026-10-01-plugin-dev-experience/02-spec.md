# Specification

> 阶段：Stage 2｜从真实 Repository Understanding 与 Intent 推导；不确定点显式记为 Unknown。
> Task ID：PILOT-plugin-dev-experience

## Functional Requirements

### FR-1 DevMode 总闸
- FR-1.1 新增静态判定 `DevMode`：环境变量 `FORGESELF_DEV_MODE=1`、CLI `--dev`、或 `ASPNETCORE_ENVIRONMENT=Development` 三者任一即启用；未设 = 关。
- FR-1.2 子开关（总闸开时默认开，显式 `=0` 可单独关）：`FORGESELF_DEV_SHADOWCOPY`（后端 shadow-copy 装载与 reload）、`FORGESELF_DEV_WEB_SRC`（前端直读源 dist）、`FORGESELF_DEV_DIAG`（`/api/dev/*` 端点）。
- FR-1.3 总闸关闭时：所有新代码路径不执行、`/api/dev/*` 返回 404、宿主行为与改动前逐字节一致。

### FR-2 后端同版本热重载（shadow-copy）
- FR-2.1 dev 下（`FORGESELF_DEV_SHADOWCOPY≠0`）装载插件时，把入口 DLL 连同 `*.deps.json` 及私有依赖复制到 `%TEMP%/forge-dev-shadow/<pluginId>/<contentHash>/`，ALC 从**副本**加载；源 DLL 不被锁。
- FR-2.2 副本内容排除宿主共享程序集（`^ForgeSelf\..*`、`^NewLife\..*`、`^(XCode|MX)\.`，dll 与 pdb 同名单），与 `scripts/publish-plugin.ps1:145-149` 排除名单一致。
- FR-2.3 入口程序集解析支持源码树布局：插件目录含 `.csproj` 而无入口 DLL 时，按 `bin/<Configuration>/net10.0/<EntryAssembly>` 解析（`FORGESELF_DEV_CONFIG` 可覆盖，默认 `Debug`）。
- FR-2.4 新增 dev 端点 `POST /api/dev/plugin/{id}/reload`：停用旧实例 → ForceCollect → 重新 shadow（内容变化自然产生新 hash 目录）→ 重载 → 启用；**不经过** `PluginVersionService.UpdatePlugin` 的版本比较。返回 `{pluginId, state, version, shadowPath, warnings[]}`。
- FR-2.5 新增 `POST /api/dev/plugin/reload-all`：对全部已发现插件依次 reload，返回逐插件结果数组。
- FR-2.6 reload 前后不修改 plugin.json、不产生 versions/ 目录、不调用裁剪。
- FR-2.7 已知边界如实返回：插件注册的宿主级 HostedService 不会因 reload 重启（`.agents/skills/plugin-development/SKILL.md:162-164`），warnings 中显式提示「如改动含 HostedService 请冷启动 dev 宿主」。

### FR-3 插件目录指向源码树（--plugins-dir）
- FR-3.1 新增 `--plugins-dir=<path>` CLI 参数与 `FORGESELF_PLUGINS_DIR` 环境变量，覆盖 `AppBuilder.cs:309` 的插件根目录解析（优先级：CLI > env > 默认 `{BaseDirectory}/plugins`），与 `StartupPortResolver.ResolveAndApply` 同样的「覆盖即用、未提供零副作用」语义。
- FR-3.2 dev 宿主指向源码 `Plugins/` 时：前端资源经既有扁平回退逻辑直接读 `<源码>/web/dist`（`PluginFrontendFileMiddleware.ResolveFrontendRoot` 的扁平分支天然覆盖，无需改逻辑）；`web/dist` 缺失时该插件前端 404（不报错不崩）。

### FR-4 前端缓存 dev 态
- FR-4.1 `FORGESELF_DEV_WEB_SRC` 开启时，`PluginFrontendFileMiddleware` 对插件 web 资源一律 `no-store`（内容指纹 `?v=` 机制保留，双重保险防浏览器陈旧缓存）。
- FR-4.2 Production 下缓存头行为不变（`?v=` → immutable 一年；无 `?v=` → no-cache）。

### FR-5 异常可见（所有宿主生效，含 Production）
- FR-5.1 新增 `PluginErrorStore`（进程内 `ConcurrentDictionary<pluginId, record{Message, ExceptionType, StackTrace, OccurredAt}>`）；`PluginManager` 五处 catch（`:385/447/569/628/764`）在保留现有日志行为的同时写入完整异常。
- FR-5.2 `PluginInfoDto` 增加可空 `Error` 字段（DTO 向前兼容），`GET /api/plugin` 列表与 `detail/{id}` 填充。
- FR-5.3 前端 `PluginStore.vue`：Error 态插件卡片展示错误原因（tooltip/展开区），不再只有置灰按钮。
- FR-5.4 成功 reload/enable 后清除对应错误记录。

### FR-6 dev 诊断端点
- FR-6.1 新增 `GET /api/dev/diagnostics`：返回 dev 开关状态、逐插件 `{id, state, version, entryPath, shadowPath, hasError, errorSummary}`、shadow 目录统计（目录数/总大小）、当日日志文件尾 N 行（默认 100）。

### FR-7 日志维度（3A + 3B）
- FR-7.1 3A：装饰 `XTrace.Log`（NewLife `ILog` 实现，100% 透传全部成员）；`AsyncLocal<string?>` 作用域在插件装载/Apply/卸载及**插件控制器请求期**注入当前 PluginId，命中时日志消息前缀 `[plugin:<id>] `。
- FR-7.2 3B：dev 下每插件独立 `TextFileLog` 落 `{数据根}/log/plugins/<id>/yyyy_MM_dd.log`；经插件 Context seed 的 `ILogService` 写入时同时落到全局日志（带 tag）与插件文件。
- FR-7.3 dev 总闸开启时 `XTrace.Log.Level` 放开为 `Debug`；Production 维持 `Info`。
- FR-7.4 177 处静态调用的请求期日志：控制器请求经 `PluginAwareControllerActivator` 作用域包裹可命中；**非请求线程**（插件自起后台任务）无法命中，作为已知限制记录。

### FR-8 真 HMR dev server（2B，opt-in）
- FR-8.1 新增 `scripts/dev-plugin-web.ps1 -Plugin <X> [-Port n]`：在插件 `web/` 目录用**生成式 vite 配置**（临时文件，`-c` 注入，不改动 9 个插件自身 `vite.config.ts`）启动 `vite serve`；生成配置含：与生产构建一致的 external 声明 + `resolve.alias` 把 `vue/vue-router/pinia/element-plus/@element-plus/icons-vue` 别名到宿主 shim 绝对 URL（`http://<宿主前端>/<shared/*.js>`），保住单 Vue 实例与 import map 语义。
- FR-8.2 dev server 启动后把 `{pluginId, port, url}` 写入插件 `web/.dev-server.json`；宿主在 `FORGESELF_DEV_WEB_HMR=1` 时，`frontend-manifest` 中该插件的 entry 改为 dev server URL（缺文件则维持静态产物路径）。
- FR-8.3 停止脚本（Ctrl+C）时清理 `.dev-server.json`。

### FR-9 文档漂移修正（6 处）
- FR-9.1 `docs/05-guides/plugin-hot-reload-limitations.md`：修正「版本 API 全死」（`PluginVersionService.Initialize` 已由 `AppBuilder.cs:362` 接线）与「watcher 自动重载」矛盾陈述，补 dev 热重载新路径。
- FR-9.2 `ForgeSelf.Api/Plugins/README.md`：删除/更正 watcher 描述（`:160-186`）。
- FR-9.3 `scripts/publish-plugin.ps1` 头注释：删除 `FileSystemWatcher` 措辞（`:9-12/32-35`）。
- FR-9.4 `docs/05-guides/plugin-frontend-development.md`：`/frontend/**` → `/web/dist/**` 契约、entry 路径、watcher 陈述。
- FR-9.5 `.agents/skills/plugin-development/SKILL.md`：源码位置 `ForgeSelf.Api/Plugins\<X>` → 仓库根 `Plugins/<X>`。
- FR-9.6 `ForgeSelf.Api/Properties/launchSettings.json`：https profile 端口 `7002`（前端端口误植）→ `7102`。

## Input

- 环境变量：`FORGESELF_DEV_MODE`、`FORGESELF_DEV_SHADOWCOPY`、`FORGESELF_DEV_WEB_SRC`、`FORGESELF_DEV_DIAG`、`FORGESELF_DEV_WEB_HMR`、`FORGESELF_DEV_CONFIG`、`FORGESELF_PLUGINS_DIR`。
- CLI：`--dev`、`--plugins-dir=<path>`。
- 脚本参数：`dev-plugin.ps1 -Plugin <PascalCase>|-All [-Configuration Debug] [-HostUrl url] [-Token xxx]`；`dev-plugin-web.ps1 -Plugin <X> [-Port n] [-HostFrontend url]`。

## Output

- HTTP：`POST /api/dev/plugin/{id}/reload`、`POST /api/dev/plugin/reload-all`、`GET /api/dev/diagnostics`（统一 `ApiResponse<T>` 壳）。
- DTO 扩展：`PluginInfoDto.Error?: string`。
- 文件系统：`%TEMP%/forge-dev-shadow/<id>/<hash>/`；`{数据根}/log/plugins/<id>/*.log`；插件 `web/.dev-server.json`。
- 新脚本：`scripts/dev-plugin.ps1`、`scripts/dev-plugin-web.ps1`。

## Business Rules

- BR-1 dev 能力一律显式 opt-in；默认关闭时零行为变化（含日志级别、缓存头、端点存在性）。
- BR-2 shadow 副本与 reload 全程 in-process，不重启宿主进程、不触碰 `versions/` 与 `current` 指针。
- BR-3 `PluginVersionService.UpdatePlugin` 严格递增语义**不改**（生产回滚安全前提），dev 需求由平行 reload 端点满足。
- BR-4 新端点复用 `[Authorize("ApiKeyPolicy")]`；Production 下即便持有 token 也返回 404（dev-gate 在 action 首行）。
- BR-5 版本化布局优先级不变：非 dev 场景 `versions/<current>/` 仍优先；dev 场景 shadow/源码目录优先。

## Boundary Conditions

- 源码插件目录无入口 DLL（只有 csproj）→ 按 FR-2.3 从 `bin/<cfg>/net10.0/` 解析；仍找不到 → 装载失败并进 PluginErrorStore。
- reload 目标插件不存在/未发现 → 404 语义（ApiResponse.Error 404）。
- reload 时旧 ALC 无法卸载（持有者未释放）→ ForceCollect 后仍锁则**报错返回**（不静默假成功），提示冷启动。
- `%TEMP%` 不可写 → shadow 禁用并 Warn，回落直接加载（行为等同改动前）。
- `web/.dev-server.json` 存在但 dev server 已死 → 前端加载失败时回落静态产物 entry（前端 catch 已有降级）。

## Error Handling

- 装载失败：写入 PluginErrorStore（Message/Type/StackTrace/At），状态 `PluginState.Error`，API/前端可见（FR-5）。
- reload 失败：HTTP 500 + ApiResponse.Error，含具体失败原因；插件保持旧实例运行（先停用后失败的场景如实返回状态）。
- diagnostics 日志尾读取失败（文件锁/不存在）→ 返回空数组 + warning 字段。

## Compatibility

- Production（无 dev 开关）：端点 404、日志级别 Info、缓存头、装载路径、`UpdatePlugin` 语义全部不变。
- DTO `Error` 为可空新增字段，旧客户端向前兼容。
- 插件 `web/vite.config.ts`、`package.json` **零改动**（生成式配置）。

## Non-functional Requirements

- reload 端到端 ≤ 5s（不含 dotnet build 时间）。
- 装饰器对每条日志的额外开销 ≤ 微秒级（字符串前缀拼接，无 IO）。
- 新代码零外部依赖；遵循项目中文注释惯例。

## Acceptance Criteria

- [ ] AC-1 build：`dotnet build ForgeSelf.Api -c Debug` 0 错误（基线 0）。
- [ ] AC-2 test：`dotnet test ForgeSelf.Api.Tests`（verbose）失败数 ≤ 基线 118 且失败名不新增。
- [ ] AC-3 dev off：不设 `FORGESELF_DEV_MODE` 启动，`GET /api/dev/diagnostics` = 404；`GET /api/plugin` 无 `Error` 字段值（null）。
- [ ] AC-4 reload：dev 宿主 + 源码 plugins 目录，不改 plugin.json 版本，`POST /api/dev/plugin/<id>/reload` 连续 ≥3 次成功，响应含 state=Running。
- [ ] AC-5 reload 生效性：改插件代码（加一条可观测输出）→ `dotnet build` 该插件 → reload → 新输出出现；**同版本号**。
- [ ] AC-6 shadow 无泄漏：≥3 次 reload 后 `forge-dev-shadow/<id>/` 仅 1 个 hash 目录。
- [ ] AC-7 异常可见：EntryType 拼错的假插件装载失败后，`GET /api/plugin` 该条目 `Error` 含消息+类型；PluginStore 卡片展示原因。
- [ ] AC-8 日志维度：dev 宿主日志含 `[plugin:<id>]` 前缀行；`log/plugins/<id>/` 有独立文件。
- [ ] AC-9 前端 dev 直读：dev 宿主 `GET /plugins/<id>/web/dist/index.js` 返回源码 dist 内容，修改 dist 内容后（watch 重建）无 bump 版本即拉到新内容（no-store）。
- [ ] AC-10 前端门禁：`pnpm run check` 红条目不新增（基线 global-setup.ts:183 既有红）；`pnpm run test` 不劣于基线。
- [ ] AC-11 文档：6 处漂移修正完成，grep 无「watcher 自动重载」矛盾残留。
- [ ] AC-12 2B opt-in：dev-plugin-web.ps1 能启动 vite serve 且转换产物中 vue 导入指向宿主 shim URL（模块级验证；浏览器级 HMR 验证标记为 Unknown 时须在 Evidence 显式声明）。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| NewLife `ILog` 接口完整成员清单（装饰器需 100% 透传） | 装饰器漏成员 → 该级别日志丢失 | 以本地 NewLife.Core 源码为准逐成员核对（用户记忆：XCode/NewLife 源码可本地读取），实现后用反射测试断言成员齐全 |
| Vite dev 对「alias → 绝对 URL」的处理是否稳定透传 | 2B HMR 可用性 | 模块级实测（curl 转换产物）；不稳则 2B 降级为「dev server + import map 失效已知限制」，Evidence 标注 Unknown |
| `PluginAwareControllerActivator` 是否可无损包裹请求作用域 | FR-7.1 请求期 tag 覆盖面 | 实现前读该类源码；不可行则接受已知限制（生命周期 tag 覆盖），Evidence 如实记录 |
| e2e 隔离实例与本 dev 改动的兼容性（global-setup 起宿主不带 dev 开关） | e2e 回归 | AC-2 覆盖测试层；e2e 不在本次门禁（宿主行为 dev-off 与改动前一致由 AC-3 保障），Review 记录该决策 |
