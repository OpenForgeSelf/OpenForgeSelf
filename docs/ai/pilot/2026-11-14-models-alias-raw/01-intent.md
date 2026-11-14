# 01 Intent — v1/models 别名模型额外返回原模型（PILOT-081）

> 阶段：Stage 1｜任务日：2026-11-14｜Task ID：PILOT-081
> 输入原文：「需求调整：v1/models 获取模型接口，模型设置了别名，增加返回原本应该返回的模型，即设置了别名的额外返回，而不是仅返回别名，原本的也要有」

## Problem

`GET /v1/models` 的 `data[].id` 在模型配置了 `PublicAlias` 时**只返回别名**（`AIModel.GetGatewayModelListId`，`AIModel.Biz.cs:143-144`），原本的对外 id（`提供商:上游模型id`）被别名遮蔽，客户端从列表里看不到该模型的原名形态。

## Why

- 客户端可能需要用原名（`provider:model` 前缀路由形态）指定模型，列表只给别名时不可发现。
- 别名 + 原名两种路由形态本就都被聊天路由支持（`ResolveUpstreamModel` 双路解析），列表只展示其一属于投影不完整，非安全约束。
- 用户明确要求「设置了别名的额外返回……原本的也要有」。

## Expected Outcome

配置了 `PublicAlias` 的模型在 `GET /v1/models` 列表中**同时出现两条**：别名条目 + 原名条目（`提供商:上游模型id`）；未配置别名的模型行为完全不变（单条，带前缀原名）。两个 id 均可直接回填 `model` 发起对话。

## Constraints

- 不修改鉴权（`[Authorize("ApiKeyPolicy")]` 保持）。
- 不修改数据库结构（零迁移、零新列）。
- 不改 `ResolveUpstreamModel` / `GetProviderByModel` 路由规则与出站别名替换不变量（PC-028）。
- 不改 `GET /v1/models/{model}` 单模型端点行为（本需求只针对列表；单端点按别名或原名查询本就都可用）。
- 不新增依赖；不做无关重构；不提交 git（闸门3 留给用户）。
- 保持既有条目字段形状：`id`/`object`/`created`/`owned_by`（OpenAI 规范形态）。

## Success Criteria

1. 后端 xUnit 真实通过：别名模型在列表中同时含别名 id 与原名 id（次序：别名在前，保持既有消费方取首项语义）；无别名模型仍单条原名。
2. 既有用例 `ModelsApi_WithoutPublicAlias_ExposesPrefixedChatModelId`、`PublicAlias_RoutesToOwningProviderAndIsExposedByModelsApi`、`ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId`、`GatewayModelEntries_CallSharedResolveUpstreamModel` 全绿（路由行为无回归）。
3. `dotnet build` 零错误；中档全量 `dotnet test` 红条目逐条对表基线、零条归本批。
4. 真源 `docs/02-features/004-provider-models-integration.md` §2.6 已回写新语义。
