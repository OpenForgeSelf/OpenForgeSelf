# 03 Plan — v1/models 别名模型额外返回原模型（PILOT-081）

> 阶段：Stage 3｜任务日：2026-11-14｜Task ID：PILOT-081

## Files To Change

- file: `ForgeSelf.Api/Entities/AIModel.Biz.cs`
  reason: 新增 `GetGatewayModelListIds(providerName, upstreamModelId)`（别名+原名集合，去重）；既有 `GetGatewayModelListId` 改为委托集合方法首项（单值语义不变，防双实现漂移）。
- file: `ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs`
  reason: `ListModels` 改用 `SelectMany` + 集合方法，按 id 集合展开条目；`GetModel` 调用点不变（仍单值）。
- file: `ForgeSelf.Api.Tests/AIModelFeatureTests.cs`
  reason: 新增 AC-1/AC-2/AC-4 用例；AC-3/AC-5 由既有用例承载（必要时强化断言）。
- file: `docs/02-features/004-provider-models-integration.md`
  reason: §2.6 真源回写（AGENTS §2.5 强制），标注 2026-11-14 语义。

## Implementation Steps

1. **TDD 红**：先在 `AIModelFeatureTests.cs` 新增三个用例（实体级集合语义 / 控制器级两条目 / 退化去重），运行过滤集确认新用例红（编译失败即为红的第一形态：方法不存在——按 TDD 以「新增测试先行」记录红证据）。
2. `AIModel.Biz.cs`：实现 `GetGatewayModelListIds`：
   - `original = upstream 空 ? (upstream ?? "") : provider 空 ? upstream : $"{provider}:{upstream}"`（与既有单值方法退化分支逐字对齐）；
   - 查 `FindByProviderNameAndUpstreamModelId`，命中且 `PublicAlias` 非空 → 先加别名；再加 original；按 Ordinal 去重。
   - `GetGatewayModelListId` 改为 `GetGatewayModelListIds(...)[0]`，XML 注释补充 2026-11-14 语义说明。
3. `ModelsController.ListModels`：`allModels.SelectMany(m => ids.Select(id => new { id, @object="model", created=m.Created, owned_by=m.Owner }))`。
4. **TDD 绿**：过滤集复跑全绿。
5. 回写真源 `004-provider-models-integration.md` §2.6。

## Test Plan

1. 新增 `GetGatewayModelListIds_WithAlias_ReturnsAliasThenOriginal`（AC-1，实体级，XCode fixture）。
2. 新增 `ModelsApi_WithPublicAlias_ListContainsAliasAndOriginalId`（AC-2，控制器级，FakeHandler 桩上游，同既有写法 `AIModelFeatureTests.cs:354-367`）。
3. 新增 `GetGatewayModelListIds_AliasEqualsOriginal_Dedupes`（AC-4，providerName 空 + 别名=上游 id；不走库插入，直接调静态方法——注意该路径查库必空，无需 Insert）。
4. 既有回归：`ModelsApi_WithoutPublicAlias_ExposesPrefixedChatModelId`、`PublicAlias_RoutesToOwningProviderAndIsExposedByModelsApi`（其 `data[0]` 断言在别名次序下继续成立）、`ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId`、`GatewayModelEntries_CallSharedResolveUpstreamModel`（PC-028 G1/G2）。
5. 中档全量 `dotnet test`（触碰宿主源码触发，AGENTS §5.6）对表基线红。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build
```

### Unit Test

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~AIModelFeatureTests"
cd ForgeSelf.Api.Tests && dotnet test   # 中档全量
```

### Integration Test

```bash
# N/A：本仓库该层验证由 XCodeTestFixture 临时 SQLite 单测承载（AIModelFeatureTests 头注 21 行）
```

### E2E

```bash
# N/A：e2e/api-gateway.spec.ts 仅断言 /v1/models 结构（63-71 行），不锁 id 内容；
# 本批未触碰 e2e 基建（global-setup/config/fixtures），按 §5.6 不触发深档。
```

### Other Checks

- 提交卫生（若最终提交）：`pwsh scripts/check-commit-hygiene.ps1 -Staged`（本批不提交，留给闸门3）。
- 无前端/插件改动 ⇒ `pnpm run check` / 插件六步不适用。

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-11-14 11:4x | AC-4 去重测试的可达形态 | 「providerName 为空 + 别名等于上游 id」——实测发现 providerName 严格为空时 `FindByProviderNameAndUpstreamModelId` 短路返回 null，永远无别名可加，该形态不可达 | 改用 providerName=空白（`" "`，IsNullOrWhiteSpace：不加前缀但查库可命中）构造可达退化场景，实现仍为 `Distinct(Ordinal)` 一行，02-spec FR-2/AC-4 已同步修正 |
