# Plan

> 阶段：Stage 3 ｜ Task ID：2026-10-16-settings-network-proxy
> 具体到真实文件（`file:` + `reason:`），基于 00 的实测盘点，无发明接口。

## Implementation Steps

### S1 后端 · 配置与代理决策（先行，可独立编译验证）

- `file: ForgeSelf.Api/Models/ForgeSetting.cs` — reason: 新增 5 个「网络」类目属性（FR-1），NewLife ForgeConfig 自动落盘，无迁移。
- `file: ForgeSelf.Api/Services/Net/ModelProxy.cs`（新建）— reason: FR-2 `IWebProxy` 实现；`GetUri`/`GetCredential`/`IsBypassed` 实时读 `ForgeSetting.Current`；构造注入 `ISecretEncryptionService`（解密密码，失败降级无密码+Warn，BC-2），注册为 Singleton，测试可直接 `new ModelProxy(stubCrypto)`，无需 IServiceProvider 延迟解析。
- `file: ForgeSelf.Api/AppBuilder.cs` — reason: ① `AddSingleton<ModelProxy>`；② `ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler { Proxy = sp.GetRequiredService<ModelProxy>(), BypassProxyOnLocal = true }))` 覆盖 M1~M5（`Microsoft.Extensions.Http` 已随 ASP.NET Core 内置，无新依赖）。

### S2 后端 · 出口 M6 接线

- `file: ForgeSelf.Api/Controllers/UnifiedAI/AgentChatController.cs` — reason: 构造函数增注入 `IHttpClientFactory`；`OpenAIClientOptions { Endpoint, Transport = new HttpClientPipelineTransport(httpClientFactory.CreateClient()) }`（`System.ClientModel.Primitives`，包已引用）。

### S3 后端 · 设置 API

- `file: ForgeSelf.Api/Controllers/SettingsController.cs` — reason: FR-4/BR-5——`AppSettingsDto`/`UpdateSettingsRequest` 增网络字段（密码请求字段为明文入参、密文出参不存在）；GET 回 `proxyPasswordSet`；POST 增校验（开启时 host 正则 `^[A-Za-z0-9._-]+$`、port 1-65535 → 400）与 `ISecretEncryptionService` 注入（构造参数，现有 `AddControllers` 自动解析，测试用 stub——先例 `ApiKeysControllerTests.cs:81`）。语义：null=不改、`""`（仅 proxyPassword）=清空。

### S4 前端 · 类型与 API

- `file: ForgeSelf.Web/src/types/settings.ts` — reason: `AppSettings` 增 `proxyEnabled/proxyHost/proxyPort/proxyUsername/proxyPasswordSet`；请求侧允许 `proxyPassword?: string`。
- `file: ForgeSelf.Web/src/services/settingsApi.ts` — reason: 类型跟进（无结构改动，POST 载荷透传可选字段）。

### S5 前端 · 网络设置面板

- `file: ForgeSelf.Web/src/components/settings/NetworkPanel.vue`（新建）— reason: FR-5 交互规格全表实现；行式布局复制 GeneralPanel 结构（CRAP-Repetition），控件 el-switch/el-input/el-input-number/el-button；表单校验与错误文案按 AC-UX-2；onMounted ≤1 次 GET。
- `file: ForgeSelf.Web/src/views/SettingsView.vue` — reason: 增 `network` 分类（type 联合、pageHeadMap、navItems〔图标 `Rank`，位置在 `api-server` 后〕、`<NetworkPanel v-show>`）。

### S6 测试

- `file: ForgeSelf.Api.Tests/Unit/ModelProxyTests.cs`（新建）— reason: AC-1 五种状态（关/开+非回环/回环/用户名 Credential/解密失败降级）。直接 `new ModelProxy(stubCrypto)` + 操纵 `ForgeSetting.Current`（测试收尾恢复，参考 `StartupPortResolver` 相关测试对 ForgeSetting 的用法）。
- `file: ForgeSelf.Api.Tests/Controllers/SettingsControllerNetworkTests.cs`（新建）— reason: AC-1 契约（读写/400/不回传密码/落盘密文）。控制器直 new（先例：`ApiKeysControllerTests`）。
- `file: ForgeSelf.Api.Tests/Unit/HttpClientProxyWiringTests.cs`（新建）— reason: AC-2 组合根冒烟：以 AppBuilder 精简方式构建服务（或复用既有测试宿主 fixture，若无则 `new ServiceCollection()+AddHttpClient+复刻 ConfigureHttpClientDefaults 行`——**实现时优先从真实 `AppBuilder` 抽取该行为独立静态方法 `AppBuilder.ConfigureModelProxyHandler(IServiceCollection)` 供生产与测试同调**，避免复刻漂移），反射取默认 client 的 `Proxy` 属性判型 `ModelProxy`。
- `file: ForgeSelf.Web/src/components/settings/__tests__/NetworkPanel.test.ts`（新建）— reason: AC-3（显隐/校验拦截/载荷；mount + stub settingsApi，先例 `ApiKeysPanel.test.ts`）。
- `file: ForgeSelf.Web/e2e/settings-network.spec.ts`（新建）— reason: AC-4（模式复制 `port-config.spec.ts`：token 注入、真实后端、`GET/POST /api/settings` 读回原值测后恢复）；截图两态存 `ForgeSelf.Web/screenshots/`。

### S7 现场验证（AC-5）

- `pwsh -NoProfile -File scripts/dev-stack.ps1` 起源码前后端 → `pwsh` 起本地 TCP/HTTP 探针 listener（`.temp/probe-proxy.ps1` 一次性脚本，用完即删不进版本库，**结论以 e2e/单测为准，探针只作探索证据**）→ 设置页开代理 → 触发 `/v1/models` 刷新 → 探针记录 + 面板截图 → 关代理复验直连 → 清测试数据（恢复原配置）。

### S8 文档回写

- `docs/02-features/`（查 README §一定位设置域真源，若无专档则在相应功能文档加「网络设置/代理」节）— reason：§2.5 唯一真源，事实落档。
- `TODO.md` 登记衍生待办：SOCKS5 评估、IPv6 字面量、代理连通检测端点、非模型出口（更新下载/webhook/MCP）是否纳入代理、AIAgentService 休眠 HttpClient 清理。

## Test Plan

| 层 | 命令 | 判定 |
| --- | --- | --- |
| 后端门禁 | `dotnet build` + `dotnet test`（ForgeSelf.Api.Tests） | 全绿（新增 ≥3 文件用例并入） |
| 前端门禁 | `cd ForgeSelf.Web; pnpm run check; pnpm run test` | check 0 error；test 全绿（既有 2 红若仍在须为 AppearancePanel 基线问题，如与本轮无关在 05 标注来源） |
| e2e | `pnpm run test:e2e e2e/settings-network.spec.ts` | 通过 + 截图读图 |
| 现场 | dev-stack + 探针（S7） | 开=探针收到请求、关=直连成功 |

## Verification（映射）

Build→S1-S5；Unit→S6 前三项后端 + NetworkPanel 单测；Integration/契约→SettingsControllerNetworkTests；E2E→settings-network.spec.ts；Other→AC-5 现场探针。

## 回滚点

实施前 git 干净基线（当前工作区若含他人未提交改动，仅 `git add` 本任务文件分批提交）；单步失败即 `git diff` 审查 + 文件级还原。风险最高为 S1 的 `ConfigureHttpClientDefaults`（影响所有 factory client）——回滚即移除该注册行，行为回到现状。
