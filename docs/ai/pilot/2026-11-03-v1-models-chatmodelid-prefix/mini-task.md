# mini-task：`GET /v1/models` 无别名时输出 `提供商:上游模型id`

> 日期：2026-11-03｜级别：轻量（宿主网关 ≤3 业务文件）
> 闸门1：用户「用 minitask 即可，实现该功能」= 显式授权，范围以本文件为界。
> **适用问题类**：无

## Intent

- **Problem**：`/v1/models` 的 `data[].id` 多为上游裸名（如 `gpt-4o`），LLM 客户端原样回填 `model` 时无法在多提供方场景稳定路由（须 `ChatModelId` 前缀）。
- **Expected Outcome**：无 `PublicAlias` 时列表与单查的 `id` = `ProviderName:UpstreamModelId`；有别名仍只输出别名（PILOT-068 FR-07 不变）。
- **Constraints**：不改 chat 路由语义；不碰 UI；不提交 git（除非用户另嘱）。

## Spec

- FR-1 `ListModels`：`id` = `AIModel.GetGatewayModelListId(providerName, upstreamId)`。
- FR-2 `GetModel`：同上投影；路径参数支持 `提供商:上游id` 查询（`GetModelById` 增强）。
- FR-3 兼容：仍支持按别名、按裸 upstream id 查单模型（既有行为）。
- **无界面交互**：纯 OpenAI 兼容 API 契约调整，无 UI。

## Plan

1. `AIModel.Biz.cs`：新增 `GetGatewayModelListId`。
2. `ModelsController.cs`：列表/单查统一投影。
3. `AIProviderRegistry.GetModelById`：解析 `provider:upstream`。
4. `AIModelFeatureTests`：无别名列表断言 + 前缀单查。
5. 更新 `docs/02-features/004-provider-models-integration.md` §2.6。

## Task

- **Allowed**：上述 3 业务文件 + 1 测试文件 + pilot 工件 + 004 文档 + TODO/日记。
- **Forbidden**：插件、前端、发布、启停宿主。
- **AC**：
  - [x] 无别名：`ListModels` → `id` 为 `OpenAI:gpt-4o`
  - [x] 有别名：仍为别名
  - [x] `GetModel("OpenAI:gpt-4o")` → `id` 同为前缀形式
  - [x] `dotnet test --filter AIModelFeatureTests` 绿（20/20）

## Verification

- `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AIModelFeatureTests"`（快档）
