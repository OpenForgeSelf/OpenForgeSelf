---
feature_key: F001
feature_no: 001
status: implemented
last_updated: 2026-10-06
aliases: ["001-ai-provider-config"]
---

# 001 AI Provider 配置（数据库化）— 功能需求与设计

> 功能编号：001
> 状态：已实现
> 关联：002 模型列表；003 API 服务器；004 集成；100 密钥加密（历史 specs/001 已弃用）
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
早期 AI Provider 配置散落在 `appsettings.json` 文件，改配置需改文件、重启、且明文密钥有泄露风险。改为数据库化持久化，支持界面增删改查与运行时热加载。

### 1.2 目标
1. AI Provider 配置落库（`AIProvider` 实体），支持 CRUD；
2. 密钥加密存储（见 `100-secret-encryption.md`），接口仅返回掩码；
3. 管理接口加鉴权（`[Authorize("ApiKeyPolicy")]`）；
4. 默认 provider 切换（`IsDefault`），`AIProviderRegistry` 运行期从 DB 重载注册表。

### 1.3 非目标
- 不负责上游模型列表拉取（见 002/004）；
- 不做密钥自动轮换（见 100 已知约束）。

## 2. 设计

### 2.1 实体关键字段（`Entities/AIProvider.cs`）
| 字段 | 含义 |
|------|------|
| `Name` | 提供方显示名（唯一，Master） |
| `ProviderType` | OpenAI / OpenAIResponses / Anthropic / Custom（PILOT-074 增 `OpenAIResponses`；输入9 后全类型聊天出站统一 openai-dotnet SDK，Anthropic 走官方 OpenAI 兼容层——出站分派见 004 §2.4） |
| `Endpoint` | 接入地址 base URL |
| `ApiKey` | 访问密钥（**加密存储**，返回的密文原样不解密） |
| `SupportedModels` | 支持模型列表（逗号分隔） |
| `IsDefault` | 是否默认提供方 |
| `TimeoutSeconds` | 请求超时（秒） |
| `VisionModel` | 库列保留。2026-11-12 起 API 与设置页不再读写，运行时不调用视觉模型 |
| `EnableMultimodal` | 库列保留。同上，不再作为请求开关 |
| `VisionPromptTemplate` | 库列保留。同上。更新提供方时仓储不覆盖这三列 |

### 2.2 加密边界（铁律）
- 仓储层仅**写时加密**；读返回**密文原样**，不解密、不克隆。
- 明文由调用方显式 `DecryptApiKey` 获取（测试连接、上游转发时才解密）。
- 列表/详情响应只出掩码 `ApiKeyMasked`；编辑 ApiKey 留空 = 保留原值。
- 默认切换用 `SetDefault(id,isDefault)`，只改标志，避免密文实体回传 `Update` 被二次加密。

### 2.3 实现位置
| 文件 | 职责 |
|------|------|
| `Entities/AIProvider*.cs` | 实体 + 业务扩展 |
| `Controllers/AIProviderController.cs` | REST（`api/ai-providers`）+ `[Authorize("ApiKeyPolicy")]` |
| `Services/AI/AIProviderRegistry.cs` | 注册表：`GetProviderByModel` 支持 `provider:` 前缀解析 |
| `AppBuilder.cs` | 启动 `InitializeXCodeDatabase` → `EnsureSeeded` → `ReloadAsync` 从 DB 重载 |

## 3. 使用指南
前端设置面板（`SettingsView` → AI 提供方）增删改查；提供方新增/编辑使用 Element Plus `el-dialog append-to-body`，在设置容器外以视口弹窗展示，长表单在弹窗内容区滚动；背景图模式沿用 `bg-image-mode.css` 的弹窗透明度主题规则。"测试连接"调用 `POST api/ai-providers/{id}/test`（解密后发最小探测）；"拉取模型"调用 `POST api/ai-providers/{id}/fetch-models`（见 002/004）。

**测试连接的 URL 归一化（2026-10-18 修复后语义，真源）**：探测地址不是裸 `Endpoint`，而是经 `OpenAICompatibleProvider.GetChatEndpointByProviderType` 推导出**完整 chat 接口**后 POST（与真实聊天链路同一套规则）：已含 chat 路径（`…/v1/chat/completions`）→ 原样；Anthropic → 补 `/messages`；裸基地址（`…/v1` 或无路径）→ 补 `/v1/chat/completions`；自定义路径（`…/gpus/v5`）→ 只加后缀。守卫单测 `ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionUrlTests.cs`。历史缺陷：直接 POST 裸 `/v1` 必 404，致「聊天能用、测试必失败」（pilot `2026-10-16-settings-proxy-mode-bypass` 05-evidence 补充证据节）。

**选定模型 · 真实对话测试（2026-10-18 输入3 拍板，真源）**：
- **UI 动线**：点「测试连接」先弹「选择模型」弹窗（列出该提供方**已登记**模型，预选第一个启用项）→ 确定才发请求，取消不发；**无已登记模型 → 按钮禁用**（用户拍板「没有模型禁止测试」，tooltip 提示先点「获取模型」）。
- **契约**：`POST api/ai-providers/{id}/test` body `{modelId}`；**省略 body = 旧仅探连通行为（向后兼容）**；modelId 非空 = 用该模型 `UpstreamModelId` 发真实 chat（content 精确为「请回复且仅回复ok」，**max_tokens=128** ——思考型模型先输出 reasoning 再输出 content，给 8 会在思考阶段被 length 截断致 content 永远 null，gpustack deepseek-v4-flash 实测），**成功判据 = HTTP 200 且回复非空白即成功，不校验回复内容（2026-11-14 输入1 放宽，PC-034：模型可拒绝「仅回复 ok」类指令但仍完全可用；成功消息展示回复前 100 字）**；**空回复仍判失败（PC-013）**；模型不属于该提供方/已删除 → 明确拒绝。
- **响应文本提取**：PILOT-074 起统一走 SDK 适配器（`OpenAISdkAdapter`，failed/空输出按 PC-013 语义判失败/抛异常）；服务侧取 `UnifiedChatResponse.Choices[0].Content` 为回复，取不到（空/纯空白）→ 失败消息「响应解析失败（未取到模型回复）」。（历史：`ExtractContent` 裸 HTTP 回退链已随 SDK 迁移移除，2026-11-14 勘误。）
- **守卫**：后端 `ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionReplyGuardTests.cs`（回复非空白矩阵，PC-034 G1；原 `AIProviderTestConnectionOkGuardTests`「含 ok 判据」已随 2026-11-14 放宽删除）+ `ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionUrlTests.cs`（URL 归一化）；e2e `ForgeSelf.Web/e2e/ai-providers.spec.ts`（无模型禁用 / mock 上游全链路 / 上游不可达失败）；`e2e/helpers/mock-upstream.ts` 可选 `chatContent` 参数（默认 'hi'）。实测铁证：dev-stack 源码宿主对 gpustack `dp-model` 返回 `{success:true, message:"连接成功 · 模型回复「ok」"}`（存证 `.forgeself/tmp-providertest-s9.json`，2026-10-18）。

## 4. 注意事项
1. 运行实例无权限停止时，改提供方端点等运行期配置需**重启 exe** 才生效（端口由 `ForgeSetting.PortNumber` 决定）。视觉模型配置已退出 API。
2. DB 位置：`ForgeSelf.Api/bin/Debug/net10.0-windows/Data/ForgeSelf.db`。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `AIProviderFeatureTests`（T018/T019/T021） | 加密落库、响应掩码、往返+掩码格式 |
| `AIProviderRegistry` 相关单测 | `GetProviderByModel` 前缀解析 |
