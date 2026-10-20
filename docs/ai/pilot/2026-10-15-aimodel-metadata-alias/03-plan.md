# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**，禁止模糊描述。
> Task ID：PILOT-2026-10-15-aimodel-metadata-alias
> 前序：00/01/02 工件已产出（02-spec 的 AC 清单即为闸门1 确认对象）。

## Files To Change

### 后端

- file: `ForgeSelf.Api/Entities/AIModel.cs`
  reason: XCode 实体生成件（宿主无 `Data/Model.xml`，只能改生成件）。新增 `MaxOutput`(Int32)、`SupportVision`(Boolean)、`ThinkingLevels`(String 200)、`PublicAlias`(String 64) 四个字段的完整五件套：私有字段 + 属性（含 `DisplayName`/`Description`/`BindColumn`）+ `Copy()` + `[name]` 索引器 get/set + `_.`/`__` 字段名常量；并加唯一索引 `[BindIndex("IU_AIModel_PublicAlias", true, "PublicAlias")]`。

- file: `ForgeSelf.Api/Entities/Interfaces/IAIModelModel.cs`
  reason: 接口契约同步，否则 `IAIModelModel` 实现类编译不过。

- file: `ForgeSelf.Api/Entities/Models/AIModelModel.cs`
  reason: 模型类 + `Copy()` 同步（与上面的 Agent:— 同为 XCoder 4 处同步点之一）。

- file: `ForgeSelf.Api/Entities/AIModel.Biz.cs`
  reason: ① 新增 `FindByPublicAlias(String alias)`（**直查 DB**：`Find(_.PublicAlias == alias)`，不走 `Meta.Cache`，理由见 BR-02）；② 在 `Valid()` 内做 `PublicAlias` 的 trim→空转 null 归一化、字符集校验（`^[A-Za-z0-9._-]{1,64}$`）、**排除自身 Id** 的唯一性校验（冲突消息含占用方 `providerName:upstreamModelId`），使所有写入路径统一受控。

- file: `ForgeSelf.Api/Services/AIModelService.cs`
  reason: ① `AIModelDto` 新增 `maxOutput`/`supportVision`/`thinkingLevels`/`publicAlias`；`supportVision` 为派生值（`SupportVision || Capabilities 含 vision`，BR-07）；② 新增 `AIModelEditableFields` DTO 与 `UpdateEditable(long id, AIModelEditableFields fields)` 重载，**保留原四参重载**（内部转调）以免破坏既有调用与测试；③ `ToDto` 补字段映射与 `ThinkingLevels` 拆分（小写/去重/预设序，BR-06）；④ `UpdateEditable` 内 `publicAlias` 传 `''` 视为清空（置 null）、`null` 视为不改（BR-04）。**不改** `FetchForProviderAsync` 的更新分支（FR-05）。

- file: `ForgeSelf.Api/Controllers/AIModelController.cs`
  reason: ① `UpdateAIModelRequest` 扩展新字段；② 调用新的 `UpdateEditable(id, fields)`；③ 新增 `catch (ArgumentException ex) => BadRequest(new { success = false, message = ex.Message })`，保证别名冲突/非法字符返回 400 而非 500（FR-10）。

- file: `ForgeSelf.Api/Services/AI/AIProviderRegistry.cs`
  reason: 在 `GetProviderByModel` **方法最开头**（现有 `':'` 前缀分支之前）插入别名精确匹配分支：`AIModel.FindByPublicAlias(modelName)` 命中 → 取记录 `ProviderName`/`UpstreamModelId` → `GetProviderByName` + FR-013 启用检查 → 命中即返回，**不再 fallback 默认 provider**（BR-10）；别名未命中时完全走既有逻辑，行为不变。

- file: `ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs`
  reason: ① `ListModels`：对 registry 返回的 `ModelInfo`（已含 `ProviderName`）按 `AIModel.FindByProviderNameAndUpstreamModelId(ProviderName, Id)` 取 `PublicAlias`，非空则输出为 `id`（FR-07）；② `GetModelById(string model)`：先按别名反查，命中则回填为 upstream id 后再投影别名（FR-08）。

- file: `ForgeSelf.Api.Tests/AIModelMetadataAliasTests.cs`（**新增**）
  reason: 覆盖 AC-03~AC-14（唯一性/字符集/更新语义/档位归一/fetch 不覆盖/`/v1/models` 与 `/v1/chat/completions` 别名路由/DB 新增列存在）。复用 `XCodeTestFixture` + `FakeHandler/HandlerHttpClientFactory` 现有桩（`AIModelFeatureTests.cs:22-87` 同款写法）。

- file: `ForgeSelf.Api.Tests/AIModelFeatureTests.cs`
  reason: 仅**回归验证**（跑过滤后的测试确认未被破坏）；不在其中大量新增用例，避免与既有 T011/T018/T024 语义混杂。

### 前端

- file: `ForgeSelf.Web/src/types/aiModel.ts`
  reason: `AIModel` 接口 +4 字段、`UpdateAIModelRequest` +4 字段（纯新增，不改既有字段）。

- file: `ForgeSelf.Web/src/services/aiModelsApi.ts`
  reason: `update()` 透传新字段（类型驱动，逻辑无需大改）。

- file: `ForgeSelf.Web/src/utils/modelAlias.ts`（**新增**）
  reason: 抽出纯函数 `buildAliasSuggestion(upstreamModelId, existingAliases?)`（清洗/截断 64/冲突追 `-2/-3`）与 `isValidAlias()`（与后端同正则），便于 vitest 直接覆盖（AC-17）。

- file: `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue`
  reason: 模型抽屉（`598-648` 行）新增「输出上限」「支持图片」「思考档位」「对外别名（含生成按钮）」四个控件；`modelForm`/`openModelEdit`/`saveModel` 同步；新增即时校验（`maxOutput` 越界禁用保存并可见原因）；别名清空传 `''`（与既有 `alias` 一致）；列表行有别名时显示徽标（`text-overflow: ellipsis`）。抽屉 380px 内滚动、footer 常驻。

- file: `ForgeSelf.Web/src/__tests__/modelAlias.test.ts`（**新增**）
  reason: 覆盖 AC-17 纯函数三类用例 + 正则边界。

- file: `ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts`
  reason: 追加/更新 AC-18、AC-19 用例（保存成功/失败、`maxOutput` 越界禁用）；若既有断言因新增控件失效，先跑再按实际修（不预改）。

### 明确**不改**的文件（避免范围膨胀）

| 文件 | 理由 |
| --- | --- |
| `ForgeSelf.Web/src/data/features.ts` | 本任务是既有设置面板的字段增强，**非新功能入口**，不应产生 SSOT phantom/orphan 变化（如需登记，另开任务） |
| `OpenAIChatController.cs` | 别名路由收口在 `AIProviderRegistry.GetProviderByModel`，此文件无需改动（避免动核心链路） |
| `AIModelService.FetchForProviderAsync` 更新分支 | FR-05 要求保持现状；AC-10 反向验证其未覆盖 |
| 任何插件目录 | 本任务为宿主能力，插件零改动 |

## Implementation Steps

1. **实体层**：按 XCoder 四件套同步 `AIModel.cs` / `IAIModelModel.cs` / `AIModelModel.cs`，并加唯一索引；`AIModel.Biz.cs` 加 `FindByPublicAlias` + `Valid()` 内校验与归一化。
2. **服务层**：`AIModelService` 扩展 DTO + 新 `UpdateEditable` 重载（保留旧重载）+ `ThinkingLevels` 归一 + 派生 `supportVision`。
3. **管理面 API**：`AIModelController` 扩展请求模型 + 调新重载 + `ArgumentException → 400`。
4. **对外 v1 面**：`AIProviderRegistry.GetProviderByModel` 加别名分支（FR-09/BR-10）；`ModelsController` 两个端点做别名投影（FR-07/FR-08）。
5. **后端测试**：新增 `AIModelMetadataAliasTests.cs`（含 DB 列存在性断言 AC-14），先跑 `dotnet build` 再跑过滤测试。
6. **前端**：types → api → `utils/modelAlias.ts` → 抽屉 UI → 单测；最后跑 `check` / `test` / `design:check`。

## Test Plan

1. 后端 xUnit（`ForgeSelf.Api.Tests`）：
   - 唯一性：空 NULL 共存、大小写冲突、自己改自己不冲突、冲突消息含占用方。
   - 字符集：`:` `/` 空格 中文 超长（65）拒绝；合法 1 位通过。
   - 更新语义：`null` 不改、`''` 清空、档位归一（`["HIGH"," low ","low"] → "low,high"`）、空数组→null。
   - fetch 保持：重拉后 4 项不变。
   - `/v1/models` 投影、`/v1/chat/completions` 别名路由（FakeHandler 断言上游收到的 model = UpstreamModelId）、禁用模型不可路由。
   - **DB 结构**：断言 AIModel 表 4 列存在（经 `Meta.Session.Dal`/`TableItem` 读列信息），并断言既有记录条数与 `UpstreamModelId/Enabled` 未变（AC-14）。
2. 前端 vitest：`modelAlias` 纯函数 + `AiProvidersPanel` 保存成功/失败/越界禁用。
3. 门禁：`pnpm run check`、`pnpm run design:check`、`pnpm run check:features`。
4. 人工走查（Stage 7）：local dev-stack 起服务，设置面板实测「生成别名 → 保存 → `GET /v1/models` 显示别名」并截图。

## Verification

### Build

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
```

### Unit Test

```bash
# 后端：只跑 AIModel 相关（全量测试存在既有环境依赖失败项，非本任务引入）
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~AIModel"
```

```bash
# 前端
cd ForgeSelf.Web && pnpm run test -- run src/__tests__/modelAlias.test.ts src/__tests__/AiProvidersPanel.test.ts
```

### Integration Test

```bash
# N/A（理由）：本任务的新增集成点为「对外 /v1 端点的别名路由与列表投影」，
# 现有测试体系无 /v1 端点的 WebApplicationFactory 集成测试基础
# （AGENTS.md 记载：WAF 宿主下插件控制器未注册等基建缺口未修）。
# 替代：在 xUnit 中直接 new ModelsController/OpenAIChatController 并注入 FakeHandler 桩接的
# AIProviderRegistry 做**控制器级**验证（真实 HTTP 之上游不出网），结果记入 05-evidence。
```

### E2E

```bash
# N/A（理由）：本任务非插件任务（不涉及 Plugins/<X>/web），按 AGENTS.md §0 插件门禁的 e2e 要求不适用；
# 宿主侧 e2e 需 dev-stack 真实 LLM/上游，成本高且与本任务无新增交互路径收益。
# 替代：Stage 7 本地 dev-stack 起服务做人工走查 + 截图（见 Test Plan #4）。
```

### Other Checks

```bash
cd ForgeSelf.Web
pnpm run check            # vue-tsc + eslint
pnpm run design:check     # 样式 token / 设计合规（0 自定义 token）
pnpm run check:features   # features.ts SSOT 双向校验（确认无漂移）
```

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-15 | 宿主无 `Data/Model.xml`，无法走「改 xml → xcode 生成」路径（项目记忆中的流程仅适用于插件） | 按记忆假定存在 `Data/Model.xml` | 直接同步改实体四件套（`AIModel.cs`/接口/模型类/Biz），并在 00 工件记录该事实 |
| 2026-10-15 | `ModelInfo` 已含 `SupportsVision`/`MaxTokens`/`ProviderName` 字段（`ForgeSelf.Abstractions/ModelInfo.cs`），但 provider 拉取时未填前两者 | 原以为需扩展 `ModelInfo` 结构 | 不动 `ModelInfo` 定义，`SupportVision` 只在实体层与 DTO 层表达；`/v1/models` 投影用已填充的 `ProviderName` + `Id` |
