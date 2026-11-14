---
task_id: PILOT-081
feature: 004-provider-models-integration
risk: { level: L1, triggers: ["ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs", "ForgeSelf.Api/Entities/AIModel.Biz.cs"], raised_by_agent: false }
gate1: { mode: pre-approved, at: 2026-11-14T11:17, ref: "用户直接指令（输入原文见 01-intent 头注；审批策略已改为免批，按 AGENTS §8.5 自主迭代 + PILOT-080 先例『用户直接指令=闸门1 依据』）" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: user, at: null }
expected_files:
  - ForgeSelf.Api/Entities/AIModel.Biz.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs
  - ForgeSelf.Api.Tests/AIModelFeatureTests.cs
  - docs/02-features/004-provider-models-integration.md
rollback: "revert 单 commit（未提交前=还原工作区两文件）；无 DB 迁移、无配置变更"
writeback: { feature_doc: docs/02-features/004-provider-models-integration.md §2.6, reason: "/v1/models id 语义唯一真源" }
---

# Agent Task — v1/models 别名模型额外返回原模型

> 阶段：Stage 4｜任务日：2026-11-14

## Task ID

PILOT-081

## Objective

`GET /v1/models` 中配置了 `PublicAlias` 的模型同时返回别名条目与原名条目（`提供商:上游模型id`，别名在前），无别名模型输出与现状逐字节一致；全部既有别名/路由守卫测试保持绿。

## Scope

### Allowed

- `ForgeSelf.Api/Entities/AIModel.Biz.cs`：新增 `GetGatewayModelListIds`；`GetGatewayModelListId` 委托化（行为不变）。
- `ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs`：`ListModels` 展开逻辑。
- `ForgeSelf.Api.Tests/AIModelFeatureTests.cs`：新增 3 用例；如既有用例断言与「别名在前」次序冲突，仅可放宽/补充断言，不得删用例。
- `docs/02-features/004-provider-models-integration.md` §2.6 回写。
- `docs/ai/pilot/2026-11-14-models-alias-raw/`（本工件链）。

### Forbidden

- 禁止改 `AIProviderRegistry`（`ResolveUpstreamModel` / `GetProviderByModel` / `GetModelById` 路由与禁用语义）。
- 禁止改 `GET /v1/models/{model}` 单端点行为、鉴权策略、`Enabled` 过滤现状。
- 禁止 DB 结构变更 / 新增 NuGet 依赖 / 触碰前端与插件 / git 提交推送。
- 禁止动 `.forgeself/` 拒绝列表与门禁脚本。

## Acceptance Criteria

- [ ] AC-1 实体级：有别名 → `[别名, "OpenAI:gpt-4o"]`；无别名 → `["OpenAI:gpt-4o"]`
- [ ] AC-2 控制器级：别名模型 data 同时含别名 id 与原名 id，别名在前，`owned_by`/`created` 一致
- [ ] AC-3 控制器级：无别名模型仍单条原名（既有用例绿）
- [ ] AC-4 退化去重：providerName 空 + 别名=上游 id → 单条
- [ ] AC-5 PC-028 守卫回归全绿（G1/G2 + 禁用模型路由）
- [ ] AC-6 `dotnet build` 零错误；中档全量 `dotnet test` 对表基线零新增红
- [ ] AC-7 真源 §2.6 回写完成

## Expected Files

- `ForgeSelf.Api/Entities/AIModel.Biz.cs`
- `ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs`
- `ForgeSelf.Api.Tests/AIModelFeatureTests.cs`
- `docs/02-features/004-provider-models-integration.md`

## Verification Commands

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~AIModelFeatureTests"
cd ForgeSelf.Api.Tests && dotnet test
```
