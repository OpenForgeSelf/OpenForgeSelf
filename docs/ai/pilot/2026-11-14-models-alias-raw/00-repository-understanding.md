# 00 Repository Understanding — v1/models 别名模型额外返回原模型（PILOT-081）

> 阶段：Stage 0 · Repository Understanding｜任务日：2026-11-14｜Task ID：PILOT-081
> 原则：所有条目来自真实仓库内容（本次全部实读源码文件确认，标注 文件:行）。

## 项目结构

- `ForgeSelf.Api/`（.NET 10 宿主 API）＋ `ForgeSelf.Api.Tests/`（xUnit）＋ `ForgeSelf.Web/`（Vue 3 SPA）＋ `Plugins/<X>/`（插件）。
- 本任务触碰面：宿主 `ForgeSelf.Api` 的 UnifiedAI 网关层与 AIModel 实体扩展。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | .NET 10 + ASP.NET Core + NewLife.XCode | `ForgeSelf.Api/ForgeSelf.Api.csproj`；`ForgeSelf.Api/Entities/AIModel.cs`（XCode 实体） |
| 测试 | xUnit + FluentAssertions，XCodeTestFixture 临时 SQLite | `ForgeSelf.Api.Tests/AIModelFeatureTests.cs:23-29`（`[Collection("XCode")]`） |
| 网关 | OpenAI 兼容面 `/v1/*`，`[Authorize("ApiKeyPolicy")]` | `ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs:16-18` |

## 架构特点（与本任务直接相关）

- `GET /v1/models`（`ModelsController.ListModels`）聚合各 provider 的 `ModelInfo`，逐条把对外 `id` 交给 `AIModel.GetGatewayModelListId(ProviderName, Id)`（`ModelsController.cs:34-45`）。
- `GetGatewayModelListId`（`ForgeSelf.Api/Entities/AIModel.Biz.cs:135-147`）：查 `AIModel` 行（按 ProviderName+UpstreamModelId），**有 `PublicAlias` 则只返回别名，否则返回 `提供商:上游模型id`** —— 这就是「别名遮蔽原名」的现状实现点。
- `PublicAlias` 全局唯一（唯一索引 `IU_AIModel_PublicAlias`，`AIModel.cs:22`）、字符集 `^[A-Za-z0-9._-]{1,64}$`（`AIModel.Biz.cs:66`，禁冒号 ⇒ 与带前缀原名 `provider:model` 恒不冲突，但 providerName 为空的退化路径理论上可相等，需去重）。
- 聊天路由双 id 均可用：别名经 `AIProviderRegistry.ResolveUpstreamModel` 反查（`AIProviderRegistry.cs:117-142`），带前缀 `provider:model` 走拆分路由（`AIProviderRegistry.cs:131-139`）⇒ 把原名也放进列表不会造成不可路由的 id。
- 列表数据源 = 上游 `/models` 实时拉取（`OpenAICompatibleProvider.GetModelsAsync`，`OpenAICompatibleProvider.cs:169-239`），非 AIModel 表全量投影；`ModelInfo.ProviderName`/`Id`（上游模型名）是投影键。
- 单模型端点 `GET /v1/models/{model}`（`ModelsController.cs:52-66`）与 `AIProviderRegistry.GetModelById`（`AIProviderRegistry.cs:222-278`）已支持按别名或 `provider:上游id` 查询，不受本任务影响。

## 测试方式

- 后端：`dotnet build`（ForgeSelf.Api）+ `dotnet test`（ForgeSelf.Api.Tests）。本域既有用例：`AIModelFeatureTests.cs`（`ModelsApi_WithoutPublicAlias_ExposesPrefixedChatModelId`:339、`PublicAlias_RoutesToOwningProviderAndIsExposedByModelsApi`:370、`ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId`:413）。
- 前端 e2e `api-gateway.spec.ts:63-71` 只断言 `/v1/models` 返回 200 + `object=="list"` + 数组结构，**不锁 id 内容**，本改动不破坏。
- PC-028 守卫（`GatewayModelEntries_CallSharedResolveUpstreamModel`:459、`ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId`）锁的是出站别名替换，与本任务（列表输出形态）互补不冲突。

## 构建命令

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~AIModelFeatureTests"
cd ForgeSelf.Api.Tests && dotnet test   # 中档：触碰宿主源码 → 全量
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Api/Controllers/UnifiedAI/` | OpenAI 兼容入站面（models / chat / responses / anthropic / agent） |
| `ForgeSelf.Api/Entities/AIModel*.cs` | XCode 实体 + 业务方法（别名清洗/唯一性/投影 id） |
| `ForgeSelf.Api/Services/AI/` | 提供方注册表、路由解析、上游拉取 |
| `docs/02-features/004-provider-models-integration.md` | §2.6 = `/v1/models` id 语义真源（改后须回写） |

## 代码组织方式

Controllers（薄）→ Registry/Service（路由与聚合）→ Entities（XCode + Biz 扩展方法）。别名投影 id 的唯一实现点在 `AIModel.Biz.cs`，控制器无第二份实现（PC-028 收口结果）。

## 现有工程规范

- AGENTS.md §5.6 门禁分档：触碰 `ForgeSelf.Api/**` → 中档全量 `dotnet test`，对表基线红。
- AGENTS.md §2.5：改完必须回写真源 `docs/02-features/004-provider-models-integration.md` §2.6。
- 提交卫生：不写绝对地址/人名（本批文档注意）。

## 适用问题类（problem-classes 检索）

- **PC-028（gateway/public-alias）**：命中区域（`/v1/models` 别名语义）。本任务不改出站替换不变量（`ResolveUpstreamModel` 不动）；lens 自查：①不新增对话入口 ②不改发给上游的 model 字符串 ③别名禁用语义不变（列表不过滤 Enabled 的现状保持，BR-09 沿袭 PILOT-068）。守卫 G1/G2 在中档全量复跑。
- **PC-014（测试数据根污染族）**：本批新测试落在 `[Collection("XCode")]`，复用现有 fixture 形态，不新建共享数据根；隔离复跑作为判责手段。
- 其余（PC-027 JSON 转义 / PC-031 usage / PC-033 tool_choice）：未命中——不新增 JsonSerializerOptions、不动 usage/tool_choice 路径。

## 候选低风险任务与选择原因

用户直接指令的需求调整（v1/models 别名模型额外返回原名）。低风险依据：纯只读列表的输出形态变化（additive，多一条条目）；无 DB 迁移（不新增列）；无鉴权/权限改动；不改路由与出站；改动面 ≤3 生产/测试文件 + 文档回写。
