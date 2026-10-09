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
| `ProviderType` | OpenAI / Anthropic / Custom |
| `Endpoint` | 接入地址 base URL |
| `ApiKey` | 访问密钥（**加密存储**，返回的密文原样不解密） |
| `SupportedModels` | 支持模型列表（逗号分隔） |
| `IsDefault` | 是否默认提供方 |
| `TimeoutSeconds` | 请求超时（秒） |
| `VisionModel` | 多模态视觉模型名（支持 `provider:model_id` 前缀，见多模态缓存文档） |
| `EnableMultimodal` | 是否启用多模态自动处理 |
| `VisionPromptTemplate` | 图片识别系统提示词模板 |

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
前端设置面板（`SettingsView` → AI 提供方）增删改查；"测试连接"调用 `POST api/ai-providers/{id}/test`（解密后发最小探测）；"拉取模型"调用 `POST api/ai-providers/{id}/fetch-models`（见 002/004）。

## 4. 注意事项
1. 运行实例无权限停止时，改 `gpustack.VisionModel` 等需**重启 exe** 才生效（端口由 `ForgeSetting.PortNumber` 决定）。
2. DB 位置：`ForgeSelf.Api/bin/Debug/net10.0-windows/Data/ForgeSelf.db`。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `AIProviderFeatureTests`（T018/T019/T021） | 加密落库、响应掩码、往返+掩码格式 |
| `AIProviderRegistry` 相关单测 | `GetProviderByModel` 前缀解析 |
