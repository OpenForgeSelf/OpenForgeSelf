# 02 Spec — v1/models 别名模型额外返回原模型（PILOT-081）

> 阶段：Stage 2｜任务日：2026-11-14｜Task ID：PILOT-081
> 依据：00 工件实读源码 + 01 Intent。不确定点已显式列 Unknown。

## Functional Requirements

| # | 需求 | 依据（现状实现点） |
| --- | --- | --- |
| FR-1 | `GET /v1/models` 对每个聚合模型条目计算**对外 id 集合**：有 `PublicAlias` 时 = [别名, `提供商:上游模型id`]（别名在前）；无别名时 = [`提供商:上游模型id`]（与现状一致） | `ModelsController.ListModels`（`ModelsController.cs:34-45`）现调用 `AIModel.GetGatewayModelListId`（`AIModel.Biz.cs:135-147`）只取单值 |
| FR-2 | id 集合按 Ordinal 去重（可达退化路径仅一处：providerName **空白**（IsNullOrWhiteSpace）时不加前缀且库行可命中，别名可等于原名；providerName 严格为空时查库短路必无别名，结构上不可能撞） | `PublicAlias` 正则禁冒号（`AIModel.Biz.cs:66`），带前缀原名恒不与别名相等 |
| FR-3 | 每条输出的条目形状不变：`{ id, object: "model", created, owned_by }`，同一模型的别名条目与原名条目共享相同 `created`/`owned_by` | `ModelsController.cs:38-44` 既有匿名对象形状 |
| FR-4 | 新增实体级方法产出该 id 集合；既有单值方法 `GetGatewayModelListId` 行为不变（改由集合方法首项实现，保证两法不漂移），`GET /v1/models/{model}` 单端点继续用单值语义 | `AIModel.Biz.cs:135-147`；`ModelsController.cs:61` |

## Input

- `GET /v1/models`，`Authorization: Bearer <ApiKey>`（既有 `ApiKeyPolicy`）。
- 内部输入：`GetAllModelsAsync` 聚合的 `ModelInfo{ Id=上游模型id, ProviderName, Owner, Created }`（`OpenAICompatibleProvider.cs:230-238`）。

## Output

- `200 {"object":"list","data":[{id,object,created,owned_by},...]}`。
- 别名模型示例（`OpenAI:gpt-4o` 配别名 `client-model`）：`data` 同时含 `id=="client-model"` 与 `id=="OpenAI:gpt-4o"` 两条，别名条目在前。

## Business Rules

| # | 规则 |
| --- | --- |
| BR-1 | 别名条目在前、原名条目在后（保持既有「有别名时首条=别名」的观察语义，兼容既有测试 `PublicAlias_RoutesToOwningProviderAndIsExposedByModelsApi` 的 `data[0]` 断言）。 |
| BR-2 | 无别名模型输出与改动前逐字节一致（单条、`提供商:上游模型id`）。 |
| BR-3 | 沿袭 BR-09（PILOT-068）：列表不按 `Enabled` 过滤，本批不改。 |
| BR-4 | 去重仅按**同一模型**的 id 集合内部进行；跨模型/跨 provider 不做全局去重（保持既有聚合行为，避免范围扩张）。 |
| BR-5 | 不改 `GET /v1/models/{model}`：按别名或原名查询仍可用，返回体 id 维持既有单值语义（别名模型查原名返回别名 id 为**既有行为**，本批不动、记入 Known Boundary）。 |

## Boundary Conditions

| 场景 | 行为 |
| --- | --- |
| 模型无 AIModel 库记录（上游裸列表，未 fetch 登记） | 查不到别名 ⇒ 单条原名（现状不变） |
| providerName 为空 | 集合=[上游裸 id]（与 `GetGatewayModelListId` 现退化行为一致） |
| upstreamModelId 为空 | 集合=[空串]（与现状一致，Degenerate） |
| 别名字符串与原名相同（仅 providerName 空时可能） | 去重为一条 |
| 上游列表同模型重复出现 | 各自投影，不做全局去重（BR-4） |

## Error Handling

- 无新增错误路径：列表聚合异常仍由 `GetAllModelsAsync` 逐 provider 捕获跳过（`AIProviderRegistry.cs:209-218`）；鉴权 401 形状不变。

## Compatibility

- **Additive**：多返回条目，不删不改性状；无别名模型零变化。
- 既有消费方兼容：`data[0]` 语义不变（别名模型首条仍为别名）；e2e `api-gateway.spec.ts` 仅断言结构，不锁条数。
- 聊天路由兼容：别名与原名两个 id 均可回填 `model`（`ResolveUpstreamModel` 双路已支持，`AIProviderRegistry.cs:117-142`）。
- 文档：`docs/02-features/004-provider-models-integration.md` §2.6 与 `docs/16-reference/api.md`（如涉及行）需回写（§2.5 唯一真源流程）。

## Non-functional Requirements

- 别名查点查走既有 `FindByProviderNameAndUpstreamModelId`（小表实体缓存，`AIModel.Biz.cs:160-169`）；每模型一次点查，与现状同量级（不引入全表扫描）。

## Failure Modes（v1.2.0 · 命中 PC 逐条预填）

| 命中 PC | 本任务如何满足 |
| --- | --- |
| PC-028（别名出站必须换上游 id） | 不涉及出站：只改列表投影；`ResolveUpstreamModel` 零改动；G1/G2 守卫复跑（中档全量） |
| PC-014（测试数据根污染） | 新测试全部落在既有 `[Collection("XCode")]` 形态；判责用隔离复跑 |
| 其余 PC | 未命中（无新 JSON options、无 usage/tool_choice/对话入口改动） |

## Interaction Design（无界面交互声明）

本任务为宿主 HTTP 端点行为调整，**无前端界面改动**。交互面 = OpenAI 兼容 HTTP API：

- 操作：`GET /v1/models`，头 `Authorization: Bearer <API Key>`。
- 入参示例：无 query 参数。
- 正常返回：`{"object":"list","data":[{"id":"client-model",...},{"id":"OpenAI:gpt-4o",...}]}`（别名模型两条；无别名模型一条）。
- 错误提示：无 key/错 key → 401（既有形状，不变）；不新增错误分支。

## Acceptance Criteria

- [ ] AC-1（xUnit·实体级）`AIModel` 新方法：有别名返回 `[别名, "OpenAI:gpt-4o"]`（次序与内容）；无别名返回单条 `["OpenAI:gpt-4o"]`。
- [ ] AC-2（xUnit·控制器级）别名模型列表：`data` 同时含别名 id 与原名 id，别名在前；两条 `owned_by`/`created` 一致。
- [ ] AC-3（xUnit·控制器级）无别名模型列表仍为单条原名（既有用例保持绿）。
- [ ] AC-4（xUnit）退化去重：providerName 空白 + 别名=上游 id（库行可命中）→ 集合去重为单条。
- [ ] AC-5（回归）`ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId`、`GatewayModelEntries_CallSharedResolveUpstreamModel`、`GetProviderByChatModelId_DisabledModel_ReturnsNull` 全绿（PC-028 守卫不回退）。
- [ ] AC-6 `dotnet build` 零错误；中档全量 `dotnet test` 红条目逐条对表基线，零条归本批。
- [ ] AC-7 真源回写：`004-provider-models-integration.md` §2.6 更新为「别名+原名都返回」并注明日期。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 是否存在外部客户端依赖「别名模型只出现一条」 | 极低（additive 变化，多一条原名条目） | 保守假设无；改动随发布链由用户决定升级，回滚点=revert 单 commit |
| `GET /v1/models/{model}` 查原名时返回 id=别名（既有行为）是否也应改 | 单端点语义，非本需求 | 记 Known Boundary 不改；如需另立任务 |
