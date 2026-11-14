# 05 Evidence — v1/models 别名模型额外返回原模型（PILOT-081）

> 阶段：Stage 7｜任务日：2026-11-14｜只记录实际发生的事情，标注来源等级。

## Task

PILOT-081（`docs/ai/pilot/2026-11-14-models-alias-raw/`）

## Changed Files

- `ForgeSelf.Api/Entities/AIModel.Biz.cs`：`GetGatewayModelListId` 委托化 + 新增 `GetGatewayModelListIds`（别名+原名集合，Ordinal 去重）
- `ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs`：`ListModels` 按集合展开条目（`SelectMany`）；`GetModel` 未改
- `ForgeSelf.Api.Tests/AIModelFeatureTests.cs`：新增 3 用例（实体级集合语义 / 控制器级两条目 / 退化去重）
- `docs/02-features/004-provider-models-integration.md`：§2.6 真源回写（2026-11-14 修订）

## Build

Command:

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~AIModelFeatureTests"   # 含 ForgeSelf.Api + Tests 增量构建
```

Result: PASS（来源等级：Verified）

```text
pilot081-green.log：EXIT=0，0 error（构建告警为存量 CS8618/xUnit1031，非本批引入）
```

## Unit Test

Command:

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~AIModelFeatureTests"
cd ForgeSelf.Api.Tests && dotnet test   # 中档全量（触碰宿主源码，AGENTS §5.6）
```

Result: PASS（快档）/ 见下（中档全量）（来源等级：Verified）

```text
TDD 红：pilot081-red.log —— error CS0117 "AIModel"未包含"GetGatewayModelListIds"的定义 ×3（AIModelFeatureTests.cs:427/429/483，即 3 条新用例）
TDD 绿：pilot081-green.log —— 已通过! - 失败: 0，通过: 25，已跳过: 0，总计: 25（22 既有 + 3 新增，含 PC-028 守卫与既有 ModelsApi 用例全绿）
中档全量：pilot081-fulltest2.log —— 失败! - 失败: 26，通过: 3752，已跳过: 0，总计: 3778，持续时间: 14 m 14 s（EXIT=1 系基线红；读数为日志正文）
```

### 中档全量对表（来源等级：Verified）

首跑（pwsh-6609ce0c13444bc1）在 ScriptRunnerDi 断言红之后挂死（testhost CPU 20s 采样 0 增量、日志 11:27:55 后 40 分钟无写入）→ 就地清殓本跑进程树 26 个后复跑（补 §5.0 NO_PROXY 前置）→ 复跑正常完成。**失败 26 条逐条对表**（对照 2026-11-13 全量注册 24 条 + TODO L16 已知两条）：

| 族 | 条数 | 归属 |
| --- | --- | --- |
| AIAgentTests.ProjectWorkspaceDefaultRootTests | 15 | 基线（TODO L173/L527，2026-11-13 L142 同列） |
| WorkflowPlanningIntegrationTests | 6 | 基线（2026-11-13 L142） |
| JsonNonAsciiEscapingGuardTests.生产代码_JSON写出路径 | 1 | 基线（TODO L16，smart-terminal 批在途文件） |
| ForgeConfigTests.ForgeSetting_配置文件归一到数据根config目录 | 1 | 基线（PC-014 残余尾巴，2026-11-06 基线注） |
| ScriptRunnerDiIntegrationTests.GetRuntimes | 1 | 基线（2026-11-13 L142） |
| ScriptRunnerTests.CancelAsync_ForRunningScript | 1 | 基线竞速 flake（TODO L174） |
| MafApprovalLoopTests.ConfiguredRule | 1 | 基线（2026-11-13 L142；TODO L28④ 并行构建瞬时态已知） |
| **归 PILOT-081 的新增红** | **0** | — |

总数 3778 = 2026-11-14 早间全量 3775 + 3（即本批 3 条新用例，全量中同样通过）；McpStdioWindowsShimTests 本轮未复现红（与 TODO L16②「归属待定维持」一致）。

## Integration Test

N/A（依据：本仓库该层验证由 XCodeTestFixture 临时 SQLite 单测承载，`AIModelFeatureTests.cs:21` 头注；无独立集成测试工程）

## E2E

N/A（依据：`e2e/api-gateway.spec.ts:63-71` 对 `/v1/models` 仅断言 200 + `object=="list"` + 数组结构，不锁 id 内容；本批未触碰 e2e 基建，按 AGENTS §5.6 不触发深档；未实际运行）

## Static Analysis

N/A（本批无前端/插件改动 ⇒ `pnpm run check` / design:check / 插件门禁不适用；后端无独立 lint 入口，构建告警零新增）

## Screenshots

N/A（无 UI 改动）

## AC 证据对照（gate: 02 每个 ACn 必须在 05 有对应证据；ACn 即 02 的 AC-1…AC-7）

| AC | 证据（Verified） |
| --- | --- |
| AC1 | `GetGatewayModelListIds_WithAlias_ReturnsAliasThenOriginal`：断言 `Should().Equal("client-model", "OpenAI:gpt-4o")` + 无别名 `["OpenAI:gpt-4o-mini"]`；pilot081-green.log 25/25 含本用例 |
| AC2 | `ModelsApi_WithPublicAlias_ListContainsAliasAndOriginalId`（FakeHandler 上游桩）：`data` 同时含别名与原名两条，别名在前，`owned_by`/`created` 与上游一致；pilot081-green.log |
| AC3 | 既有 `ModelsApi_WithoutPublicAlias_ExposesPrefixedChatModelId` 保持绿（pilot081-green.log 25/25；全量同绿） |
| AC4 | `GetGatewayModelListIds_AliasEqualsOriginal_Dedupes`（providerName=`" "` 空白 + PublicAlias=上游 id）→ 单条；pilot081-green.log |
| AC5 | PC-028 守卫两条（`ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId` / `GatewayModelEntries_CallSharedResolveUpstreamModel`）+ `GetProviderByChatModelId_DisabledModel_ReturnsNull` 快档与全量均绿（pilot081-green.log；pilot081-fulltest2.log 不在 26 红名单） |
| AC6 | 构建 0 error（pilot081-green.log EXIT=0）；中档全量 26 红逐条对表基线零条归本批（pilot081-fulltest2.log，见上「中档全量对表」） |
| AC7 | 真源回写完成：`docs/02-features/004-provider-models-integration.md` §2.6 含「2026-11-14 修订：别名模型额外返回原名」+ 两条目表格（git diff 已核） |

## Known Limitations

- `GET /v1/models/{model}` 单端点行为未随本需求变化（别名模型按原名查询返回体 id 仍为别名——既有行为，02-spec Known Boundary 登记）。
- 列表不按 `Enabled` 过滤（BR-09 沿袭 PILOT-068，本批不改）。
- 中档全量结果与基线对表结论见下节「全量对表」。

## Unresolved Issues

（无。26 条失败全部对表基线归零；挂起事件系环境前置缺失，教训已沉淀 agent-workflow b-engineering-daily §B2，见 Known Limitations 末条。）
