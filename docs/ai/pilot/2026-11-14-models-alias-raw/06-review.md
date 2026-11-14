# 06 Review — v1/models 别名模型额外返回原模型（PILOT-081）

> 阶段：Stage 8｜审查日：2026-11-14｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 满足。Intent 要求「设置别名的模型在 `/v1/models` 列表中额外返回原名条目，别名与原名都有」。`AIModel.GetGatewayModelListIds` 返回 `[别名, 提供商:上游模型id]`（别名在前，Ordinal 去重），`ModelsController.ListModels` 用 `SelectMany` 展开——Verified：控制器级用例 `ModelsApi_WithPublicAlias_ListContainsAliasAndOriginalId` 断言两条 id 同时存在且顺序正确，实体级用例断言 `Equal("client-model", "OpenAI:gpt-4o")`。
2. **实现是否符合 Spec？** 符合。FR-1..4 全落地（别名+原名两条、无别名单条、共享单值语义、退化去重）；BR-1..5 无违背（响应结构不变、owned_by/created 同源、路由可达、PC-028 出站替换不回归）；AC-1..7 逐一有测试或 Verified 读数。已知边界（`GET /v1/models/{model}` 单端点单值语义、Enabled 不过滤）按 Spec 声明保留。
3. **是否超出了 Scope？** 未超出。改动面 = 2 个生产文件 + 1 个测试文件 + 1 篇真源文档 + 本目录工件；`GetModel`、`ResolveUpstreamModel`、e2e 基建均未触碰。同类扫查仅命中文档与测试面，无额外代码。
4. **是否修改了不应该修改的文件？** 无。工作区其余改动（DbAdmin / commit-hygiene 批）属并行会话，本批未触碰；本批 4 文件 diff 逐行复核（含一处编辑工具丢失 BOM 的编码回归，已当场还原 BOM，diff 恢复最小面）。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖。AC-1（别名两条）→ 控制器用例；AC-2（无别名单条）→ 既有 `ModelsApi_WithoutPublicAlias_ExposesPrefixedChatModelId` + 新用例 no-alias 断言；AC-3（顺序别名在前）→ `Equal("client-model", "OpenAI:gpt-4o")`；AC-4（退化去重）→ `GetGatewayModelListIds_AliasEqualsOriginal_Dedupes`；AC-5（快档红→绿）→ pilot081-red/green.log；AC-6（构建 0 错）→ 测试构建含宿主工程 EXIT=0；AC-7（中档对表）→ 26 失败逐条归基线、0 新增红。
6. **是否存在明显回归风险？** 低。PC-028 守卫两条 + 既有 2 条 ModelsApi 用例在快档与全量均绿；别名行为改动点单一（`GetGatewayModelListId` 委托保单值语义，调用面 grep 复核无第三处消费）；最坏回滚点 = 还原该 3 文件（git diff 可回溯）。
7. **是否存在架构不一致？** 无。沿用「列表 id 语义单点在 AIModel 实体层、控制器只做投影」的既有分层；新增集合方法与既有 `ChatModelId`/`ResolveUpstreamModel` 语义对齐（文档 004 §2.6 已回写真源）。
8. **Evidence 是否足以证明任务完成？** 足以。构建/快档/中档全量三级 Verified 读数齐全（25/25、3778 例 26 红全归基线），证据链含红→绿 TDD 与对表明细；挂起事件如实记录并归因环境前置（教训已沉淀 B2）。

## Requirement Check

PASS（别名模型列表两条目 = 用户原话「设置了别名的额外返回，而不是仅返回别名，原本的也要有」逐字落实）

## Scope Check

PASS

## Test Check

PASS（AC 全覆盖；中档对表 0 新增红）

## Architecture Check

PASS

## Risk

L1（公共 API 响应内容新增条目——属用户明确要求的行为变更；消费方按 OpenAI 兼容协议按 id 取用，多一条 id 无破坏面）

## Findings

### Critical

（无）

### Major

（无）

### Minor

- 编辑工具重写 `AIModel.Biz.cs` 时丢失 UTF-8 BOM（diff 出现首行伪变更），审查时已还原；提醒后续 edit 生产文件后核对编码（非本批功能缺陷，编码纪律已属既有规则）。

## Final Decision

APPROVED
