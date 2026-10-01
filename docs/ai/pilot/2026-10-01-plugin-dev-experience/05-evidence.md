# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：**Verified**（亲自跑过）/ **Inferred**（凭代码推断）/ **Unknown**（未验证）。
> Task ID：PILOT-plugin-dev-experience

## Changed Files

### 新增（12）

| 文件 | 内容 |
| --- | --- |
| `ForgeSelf.Api/Plugins/Dev/DevMode.cs` | dev 总闸：FORGESELF_DEV_MODE=1 / --dev 显式启用；4 子开关 + BuildConfiguration/TargetFramework + --plugins-dir 解析 |
| `ForgeSelf.Api/Plugins/Dev/PluginShadowCopy.cs` | 内容寻址 shadow 装载（%TEMP%/forge-dev-shadow/&lt;id&gt;/&lt;hash&gt;/），宿主共享 DLL 排除名单与 publish-plugin.ps1 一致；陈旧目录清扫 |
| `ForgeSelf.Api/Plugins/Dev/PluginErrorStore.cs` | 进程内错误仓库（Message/Type/StackTrace/At），dev 与 Production 均生效 |
| `ForgeSelf.Api/Plugins/Dev/PluginLogScope.cs` | AsyncLocal 插件日志作用域 + PluginTaggedLog ILog 装饰器（成员清单依据 NewLife.Core 11.17.2026.701 XML） |
| `ForgeSelf.Api/Controllers/DevController.cs` | POST /api/dev/plugin/{id}/reload、POST /api/dev/plugin/reload-all、GET /api/dev/diagnostics；ApiKeyPolicy + dev-gate 404 |
| `scripts/dev-plugin.ps1` | 一条命令后端回路：dotnet build → get-forge-token 解密 → POST reload（支持 -All/-HostUrl/-Token/-DataRoot） |
| `scripts/dev-plugin-web.ps1` | 生成式 vite dev 配置（vite.config.dev.mjs）+ .dev-server.json 标记 + Ctrl+C 清理；opt-in HMR |
| `ForgeSelf.Api.Tests/Plugins/Dev/DevModeTests.cs` | 9 用例：env/CLI 矩阵、子开关、plugins-dir 优先级 |
| `ForgeSelf.Api.Tests/Plugins/Dev/PluginShadowCopyTests.cs` | 6 用例：复制/排除/hash 稳定与变化/剪枝/无源返回 null |
| `ForgeSelf.Api.Tests/Plugins/Dev/PluginErrorStoreTests.cs` | 5 用例 |
| `ForgeSelf.Api.Tests/Plugins/Dev/PluginTaggedLogTests.cs` | 5 用例：前缀/零改动/嵌套/透传/Write |
| `ForgeSelf.Api.Tests/Plugins/Dev/DevControllerTests.cs` | 6 用例：dev-off 404×3、未知插件 404、诊断形状 |

### 修改（13）

| 文件 | 改动 |
| --- | --- |
| `ForgeSelf.Api/Plugins/PluginManager.cs` | ResolvePluginInstance 插 shadow 分支；五处 catch 写 PluginErrorStore；装载/注册/初始化/销毁包日志作用域；成功时 Clear；三类"入口类型不存在/未实现 IPlugin/实例创建失败"也入库完整错误 |
| `ForgeSelf.Api/AppBuilder.cs` | CreateWebApplication 入口 DevMode.Initialize；日志装饰器装配（dev-only）+ Level dev=Debug/prod=Info；插件根目录 --plugins-dir/FORGESELF_PLUGINS_DIR 覆盖；dev 启动时 shadow 清扫 |
| `ForgeSelf.Api/Controllers/PluginController.cs` | 列表/详情填充 Error；frontend-manifest 支持 FORGESELF_DEV_WEB_HMR 读 .dev-server.json 把 entry 指向 dev server |
| `ForgeSelf.Api/Models/Plugins/PluginInfoDto.cs` / `PluginDetailDto.cs` | 可空 Error 字段 |
| `ForgeSelf.Api/Plugins/Services/PluginAwareControllerActivator.cs` | 插件控制器 Create/Release 包 PluginLogScope（请求期日志带 [plugin:id]） |
| `ForgeSelf.Api/Services/IPluginServiceRegistry.cs` / `PluginServiceRegistry.cs` | 新增 GetOwnerPluginId(Type) |
| `ForgeSelf.Api/Plugins/Services/PluginFrontendFileMiddleware.cs` | dev WebSrc 下 Cache-Control: no-store（Production 不变） |
| `ForgeSelf.Api/ForgeSelf.Api.csproj` | InternalsVisibleTo → ForgeSelf.Api.Tests（DevController 测试构造） |
| `ForgeSelf.Web/src/types/plugin.ts` | PluginInfo.error?: string \| null |
| `ForgeSelf.Web/src/views/PluginStore.vue` | Error 态卡片展示失败原因（--el-* + color-mix，0 自定义 token） |
| `docs/05-guides/plugin-hot-reload-limitations.md` | 全文修订：watcher 已删、版本 API 已接线、dev reload 新通道、决策表按 dev/publish 宿主分列 |
| `ForgeSelf.Api/Plugins/README.md` | §9.2 触发方式表更正（watcher→dev reload）；端点前缀 api/plugins→api/plugin（单数）；补 dev 端点清单 |
| `docs/05-guides/plugin-frontend-development.md` / `scripts/publish-plugin.ps1` / `.agents/skills/plugin-development/SKILL.md` / `ForgeSelf.Api/Properties/launchSettings.json` | /web/dist 契约更正；头注释去 FileSystemWatcher；源码位置更正为仓库根 Plugins/ + 登记 dev 快速回路；https profile 7002→7102 |

## 验证结果

| # | 验证项 | 命令 | 结果 | 等级 |
| --- | --- | --- | --- | --- |
| V1 | 改动前构建基线 | `dotnet build ForgeSelf.Api -c Debug` | **0 错误 / 4 警告**，57s（evidence/baseline-build.log） | Verified |
| V2 | 改动前测试基线 | `dotnet test ForgeSelf.Api.Tests` | 1953 总 / 1835 过 / **118 既有失败**，16m3s（evidence/baseline-test-failures.txt） | Verified |
| V3 | 改动后构建 | 同 V1 | **0 错误**，与基线持平（AC-1） | Verified |
| V4 | 新增定向测试 | `dotnet test --filter DevModeTests\|PluginShadowCopyTests\|...` | **31/31 通过**（AC-2 部分） | Verified |
| V5 | 前端 check | `pnpm run check` | **0 错误**（81 warnings 全部既有 style 类），exit 0（AC-10） | Verified |
| V6 | 前端 vitest | `pnpm run test` | **53 文件 / 568 用例全部通过**，61s（AC-10） | Verified |
| V7 | dev 宿主实跑 AC-4 | dev 宿主(7301, FORGESELF_DEV_MODE=1, --plugins-dir 源码 Plugins) × `POST /api/dev/plugin/sample/reload` ×3 | 3 次全 success，state=Running，**version 1.0.0→1.0.0 未变**，shadowPath 均返回（AC-4） | Verified |
| V8 | dev 实跑 AC-5 | 改 SamplePlugin（加标记 Debug 日志）→ `dotnet build` → reload | shadow 哈希 `b32077938af4`→`f1e0e6fde3c5`（新二进制被装载），版本号未变；诊断日志尾出现标记行（AC-5） | Verified |
| V9 | dev 实跑 AC-8 | 同 V8 日志 | `12:44:56.763 16 Y P [plugin:sample] DEV-VERIFY-MARKER-RELOADED-v2` —— 装载期作用域前缀生效 | Verified |
| V10 | dev 实跑 AC-6 | `GET /api/dev/diagnostics` + %TEMP% 目录列举 | 18 插件各 1 个当前 shadow 目录；sample 旧 hash 目录已剪枝；残留 1 个被锁陈旧目录（旧 ALC 句柄未及释放，设计为跳过+下次启动清扫），CleanupUnlocked 全量清扫已实测 | Verified |
| V11 | dev 实跑 AC-7 | 预置坏插件 DevBrokenTest（EntryType 指向不存在类型）→ `GET /api/plugin/dev-broken-test` | `state=Error`，`error="插件入口类型不存在: …NoSuchType（检查 plugin.json 的 EntryType…）（System.InvalidOperationException）"`；diagnostics 同样可见；验证后目录已删除 | Verified |
| V12 | dev 实跑 AC-9 | `GET /plugins/ai-agent/web/dist/index.js` | 200，Content-Type: text/javascript，**Cache-Control: no-store**（dev WebSrc 生效） | Verified |
| V13 | 单测面 AC-3 | DevControllerTests（devGate=false） | diagnostics/reload/reload-all 均 404 语义 | Verified |
| V14 | 全量测试干净轮（基线对照） | `TMP/TEMP 重定向到 temp/test-tmp` 后 `dotnet test ForgeSelf.Api.Tests`（dev 实例已停） | **27 失败 / 1959 通过 / 1986 总 / 9m5s**。与基线 118 名单（剥离时长归一后）对照：真正新增仅 2 条——① DevControllerTests 一条（DevMode 进程级静态与 DevModeTests env 矩阵在 xUnit 并行下竞态）→ 修复（移除对全局静态值的断言）后连跑 3×49/49 稳定；② RepositoryScriptTests BOM（本任务新增 2 个含中文 ps1 脚本缺 BOM，实为**本任务回归**）→ 补 UTF-8 BOM 修复，复验通过。其余 25 条全在基线名单内；基线另有 91 条环境性失败（系统 Temp 拒访）在 TMP 重定向后消失。**当前仓库无本任务引入的失败**（AC-2） | Verified |
| V15 | dev-off 实机 404 | 独立起非 dev 宿主（:7302，同构建，无 FORGESELF_DEV_MODE）→ `GET /api/dev/diagnostics`、`POST /api/dev/plugin/sample/reload` | 无 token = **401**（ApiKeyPolicy 先拦，铁律 17 语义）；带 token = **404**（dev-gate 生效）；reload 同 404。宿主验证后即停（AC-3 实机面） | Verified |
| V14a | 首次全量（方法错误轮，留证） | `dotnet test`（dev 验证实例未停，默认 TEMP） | 535 失败 / 2m29s——运行实例 ConfigUnifier `.tmp` 争抢（TODO.md:86 已录坑的实锤复现），非代码回归 | Verified |
| V16 | 2B HMR 浏览器级 | 浏览器操作 dev server 页面 | **Unknown**——模块级机制已实现并经配置生成/启动脚本验证，浏览器端 alias→宿主 shim 的跨源 import 行为需人工 HMR 走查确认（AC-12 如实标注） | Unknown |
| V17 | per-plugin 分文件日志 | dev 宿主 `{数据根}/log/plugins/<id>/` | **Unknown**——3B 通道未在本次实施（见 Known Limitations #3，范围裁决） | Unknown |

## Known Limitations

1. **AsyncLocal 作用域不覆盖插件自起后台线程**：插件后台任务脱离请求/装载调用链，其静态 `XTrace.Log` 输出无 `[plugin:]` 前缀（设计已知，Spec FR-7.4）；请求期经 PluginAwareControllerActivator 作用域覆盖。
2. **全量测试轮次对运行实例敏感**（TODO.md:86 已录）：第一轮全量测试与 dev 验证实例并行跑出 496 失败（ConfigUnifier `.tmp` 争抢 + 数据根/句柄冲突），属验证方法问题非代码回归；停实例后的干净轮 V14 为准。**V14 相对基线的差异详见下方偏差分析**。
3. **3B per-plugin 分文件日志未实施**：实施中发现 `PluginTaggedLog`（3A）+ 请求期作用域已覆盖日志定位主诉求；3B 需重构 `LogService` seed 链路并处理 177 处静态调用仍写主文件的覆盖不全问题（选型报告已预判），按最小切片原则裁剪出本次范围，管线已预留（PluginLogScope 可扩展为按插件路由 sink）。
4. **2B HMR 的双 Vue 风险仍在**：alias 漏配或插件用到 shim 未导出的组件时可能静默失效；脚本头注释与文档均已警示，默认关闭。
5. **shadow 陈旧目录可能短暂残留**：旧 ALC 卸载后句柄释放有延迟，剪枝时仍被锁的目录跳过（不阻塞装载），下次 dev 启动 `CleanupUnlocked` 清扫；实测每插件当前目录恒为 1。

## Unresolved Issues

- 2B HMR 浏览器级验证（V16）待人工走查；如 alias 方案在真实浏览器行为不符，回退路径 = 关闭 FORGESELF_DEV_WEB_HMR 用 2A watch 回路（零成本）。
- 前端 PluginState 枚举注释（types/plugin.ts：Error=9）与实测 state=10（坏插件）存在既有偏差（PluginState.cs 实有 11 值），影响面=前端徽标文案映射，属既有问题，已记观察不在本次范围。

## Plan 偏差记录（实施期）

1. `ASPNETCORE_ENVIRONMENT=Development` 不再作为 dev 总闸通道——e2e 隔离实例固定注入该变量，纳入会使 e2e 宿主行为漂移，违背 AC-3 兼容性承诺（02-spec FR-1.1 修订）。
2. 前端 2A 的 mtime 指纹方案取消——核实 `ComputeWebVersion` 已是内容 SHA256 指纹，dev 直读源 dist 后内容变化天然破缓存；仅补 no-store 兜底。
3. 3B per-plugin 分文件日志裁剪出本次范围（Known Limitations #3）。
