# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：一切结论基于**真实仓库内容**，禁止凭常识推测。Task ID：2026-10-16-settings-network-proxy

## 项目结构

- 解决方案根：`ForgeSelf.sln`（工作区 `d:\src\my-proj\OpenForgeSelf\OpenForgeSelf`）
- 后端：`ForgeSelf.Api/`（ASP.NET Core，DI 装配单点 `AppBuilder.cs`）
- 前端：`ForgeSelf.Web/`（Vue 3 SPA，设置页 `src/views/SettingsView.vue` + `src/components/settings/*Panel.vue`）
- 后端测试：`ForgeSelf.Api.Tests/`（xUnit + Moq + FluentAssertions）
- e2e：`ForgeSelf.Web/e2e/`（Playwright，应用层 spec 平铺，如 `port-config.spec.ts`）
- 设计原型：`forgeself-design/pages/`（本任务无对应新设计稿，按既有设置面板形态对齐）

## 技术栈（本任务相关，均有文件依据）

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 后端 HTTP 出口 | `IHttpClientFactory`（`Microsoft.Extensions.Http`） | `ForgeSelf.Api/AppBuilder.cs:172` `AddHttpClient<IAIService, AIService>()` |
| OpenAI SDK 出口 | `OpenAI` / `Microsoft.Agents.AI.OpenAI 1.6.1`（`OpenAIClient` + `OpenAIClientOptions`） | `ForgeSelf.Api/ForgeSelf.Api.csproj:73`；`Controllers/UnifiedAI/AgentChatController.cs:74-76` |
| 本地配置存储 | NewLife `ForgeSetting : ForgeConfig<ForgeSetting>`（XML 落盘 `ForgeSetting.config`，不落库） | `ForgeSelf.Api/Models/ForgeSetting.cs:9-10`；`Controllers/SettingsController.cs:7` |
| 敏感值加密 | `ISecretEncryptionService.Encrypt/Decrypt`（AES，`AesSecretEncryptionService`） | `ForgeSelf.Abstractions/ISecretEncryptionService.cs:12-18`；`ForgeSelf.Api/Security/AesSecretEncryptionService.cs:50` |
| 前端 | Vue 3.5 + Element Plus（`--el-*` token）+ Tailwind；设置 API 封装 `settingsApi.ts` | `ForgeSelf.Web/src/services/settingsApi.ts`（GET/POST `/api/settings`） |

## 架构要点：后端「发出去的模型请求」的全部出口（实测盘点）

| # | 出口 | 创建方式 | 是否走 IHttpClientFactory |
| --- | --- | --- | --- |
| M1 | `AIService`（legacy 默认 AI 配置对话/流式） | typed client `AddHttpClient<IAIService, AIService>()`（AppBuilder.cs:172；AIService.cs:45-61） | ✅ |
| M2 | `OpenAICompatibleProvider`（chat/completions、/v1/models，按提供方路由） | `AIProviderRegistry.ReloadAsync` → `httpClientFactory.CreateClient()`（AIProviderRegistry.cs:50） | ✅ |
| M3 | `AIModelService`（拉取模型列表） | `_httpClientFactory.CreateClient()`（AIModelService.cs:71） | ✅ |
| M4 | `AIProviderService`（密钥校验等） | `_httpClientFactory.CreateClient()`（AIProviderService.cs:172） | ✅ |
| M5 | `AIProviderController` | 注入 `IHttpClientFactory`（AIProviderController.cs:27,35） | ✅ |
| M6 | `AgentChatController`（实验性 agent 对话，直连上游） | `new OpenAIClient(credential, new OpenAIClientOptions{Endpoint})`（AgentChatController.cs:74-76）——**不走 factory**，需单独接 | ❌ 需改 |
| M7 | `Plugins/AIAgent/AIAgentService` 自建 `_httpClient = new HttpClient()`（AIAgentService.cs:43） | 实测**除 Timeout 赋值外全文无使用**（grep `_httpClient.` 仅 1 处）；插件模型调用经宿主 IAIService 接缝（进程内，落 M1/M2） | ✅（间接） |

**结论**：M1~M5 收口于 `IHttpClientFactory` 默认 client ⇒ 在 DI 层 `ConfigureHttpClientDefaults(ConfigurePrimaryHttpMessageHandler)` 一处配置即可覆盖；M6 需给 `OpenAIClientOptions.Transport` 注入过代理的 transport。非模型请求出口（UpdateChecker/UpdateService 更新下载 `AppBuilder.cs:257,276`、WorkflowEngine HTTP 节点 `WorkflowExecutor.cs:541`、McpCenter 传输、Scheduler webhook）**不在本任务范围**（用户原话限定「模型请求」），记 TODO。

现状代理行为：factory 默认 handler 未显式设 Proxy ⇒ 跟随**系统代理**；本项目此前无代理设置入口（grep `WebProxy|Proxy` 无网络配置实现）。

## 设置页面板机制（前端真源）

- `SettingsView.vue:28-72`：`SettingsCategory` 联合类型 + `pageHeadMap` + `navItems`（图标来自 `@element-plus/icons-vue`，已核实包内存在 `Rank`/`Compass`/`Position`，**无 `Globe`**）+ 右侧 `<XxxPanel v-show=...>`（106-115 行）。
- 面板样例：`GeneralPanel.vue`（行式「左标题+右控件」+ `settingsApi.getSettings/updateSettings` 点即保存）；`ApiServerPanel.vue`（el-card 分区 + ElMessage 反馈）。
- API 层：`services/settingsApi.ts`（`/api/settings` GET/POST）+ `types/settings.ts`（`AppSettings` 非敏感字段 DTO）。
- 后端：`SettingsController.cs`——GET 匿名返回白名单字段、POST 需 `ApiKeyPolicy`，写 `ForgeSetting.Current` 后 `Save()`。
- 前端测试模式：`src/components/settings/__tests__/AboutPanel.test.ts`、`ApiKeysPanel.test.ts`（vitest 组件测试有先例）；设置类 e2e 先例 `e2e/port-config.spec.ts`（真实后端 + token 注入 + 测后恢复）。

## 验证入口（本仓库当前可用）

```bash
# 后端
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
# 前端（ForgeSelf.Web/ 下）
pnpm run check
pnpm run test
# e2e（真实前后端）
pnpm run test:e2e e2e/settings-network.spec.ts   # 新增
```

## 重要目录职责 / 既有规范

- 样式体系：Element Plus `--el-*` 变量 + Tailwind 布局原语，不定义独立色值（AGENTS.md §2.3）。
- 禁止显式 `import { ElXxx } from 'element-plus'`（类型导入除外），模板用 `<ElXxx>` 自动解析。
- 敏感字段落盘走 `ISecretEncryptionService`（先例：`ApiToken`、`AIProvider.ApiKey`，见 `SecretMigrationService.cs`）。
