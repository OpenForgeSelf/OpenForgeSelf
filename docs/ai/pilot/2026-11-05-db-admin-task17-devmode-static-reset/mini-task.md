# mini-task — task-17 DevModeTests 静态态复位（PluginFrontendFileMiddlewareTests flake 消除）

## Intent（为什么/做什么/到什么程度）
今夜全量对表暴露 PluginFrontendFileMiddlewareTests ×2 偶发 flake（no-store vs cache 头断言），qa 收尾亦留同类残余 ×2。根因定性：`DevMode.Enabled` 是 private set 静态（唯一写手 `Initialize`），DevModeTests.Dispose 只清环境变量、不复位静态 → 并行测试类读到残留 `Enabled=true` → `WebSrcEnabled = Enabled && FlagEnabled(env null→true) = true` → 中间件走 dev 分支恒 no-store → 缓存头断言翻转。目标：静态复位协议闭环 + 守卫固化，消除类序依赖。

## Spec（关键行为）
1. DevModeTests.Dispose 末尾执行复位协议：清总闸 env（FORGESELF_DEV_ENV）+ `DevMode.Initialize([])` 幂等归 false。
2. PluginFrontendFileMiddlewareTests 实现 IAsyncLifetime，每用例前 `DevMode.Initialize(null)` 防御复位（类内自洽，不依赖他人清理纪律）。
3. 守卫测试 `ResetProtocol_ClearEnvPlusInitialize_IsDeterministicOff` 固化复位协议：置 1 → Initialize → true；清 env → Initialize → Enabled/WebSrc/ShadowCopy 全 false。

## Plan（真实文件）
- `ForgeSelf.Api.Tests/Plugins/Dev/DevModeTests.cs`：Dispose 追加复位协议（+4 行）+ 守卫测试（+23 行）。
- `ForgeSelf.Api.Tests/Plugins/PluginFrontendFileMiddlewareTests.cs`：类声明加 `: IAsyncLifetime` + InitializeAsync 复位 + using。

## Interaction Design（交互设计）
**无界面交互**：本任务为纯测试基建修复（xUnit 测试静态态复位），不涉及任何用户可见 UI/插件界面/REST 端点行为变化；验收经由测试断言（缓存头 no-store/immutable/no-cache 的确定性）而非界面走查。

## Task（Allowed/Forbidden + 验证命令）
- Allowed：上述两测试文件；Forbidden：产品代码 DevMode.cs（Initialize 幂等语义已足够，无需新 API）、其它测试类。
- 验证：`dotnet test --filter 'FullyQualifiedName~PluginFrontendFileMiddlewareTests|FullyQualifiedName~DevModeTests'`（预期 22/22）+ `--filter 'FullyQualifiedName~ConfigUnifierTests|FullyQualifiedName~ForgeSetting'`（qa 收尾残余对表，预期 5/5）+ 中档全量对表（与 qa 错峰）。