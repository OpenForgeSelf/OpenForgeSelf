# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`。
> Task ID：PILOT-2026-10-15-aimodel-metadata-alias
> 前序工件：00-repository-understanding.md、01-intent.md

## Functional Requirements

| FR | 需求 | 依据（真实文件） |
| --- | --- | --- |
| FR-01 | `AIModel` 实体新增字段：`MaxOutput`(Int32)、`SupportVision`(Boolean)、`ThinkingLevels`(String)、`PublicAlias`(String)，字段同步点 4 处 | `Entities/AIModel.cs`、`Entities/Interfaces/IAIModelModel.cs`、`Entities/Models/AIModelModel.cs`、`Entities/AIModel.Biz.cs` |
| FR-02 | 输入上下文沿用既有 `MaxContext`（Int32，0=未设置）；**不新增同义字段** | `AIModel.cs:82-88` |
| FR-03 | `AIModelDto` 输出新字段；管理面 `GET /api/ai-models` 返回之 | `Services/AIModelService.cs:280-296`、`Controllers/AIModelController.cs:26-39` |
| FR-04 | 管理面 `PUT /api/ai-models/{id}` 支持更新 `maxOutput`/`supportVision`/`thinkingLevels`/`publicAlias`（`null` = 不改；`''` = 清空） | `Controllers/AIModelController.cs:58-73`、`Services/AIModelService.cs:193-209` |
| FR-05 | 上游 `fetch-models` 重拉**不覆盖**上述 4 项用户设置（现有更新分支只刷新 Owner/时间） | `AIModelService.cs:122-131` |
| FR-06 | `PublicAlias` **全局唯一**（大小写不敏感），长度为 1~64，字符集 `[A-Za-z0-9._-]`，**禁止冒号**（避免与 `提供商:模型id` 路由语法冲突） | 新增规则；索引 `[BindIndex]` 于 `AIModel.cs` |
| FR-07 | `GET /v1/models` 的 `id`：**有别名输出别名**，无别名输出上游原始模型 id（现状保持不变） | `Controllers/UnifiedAI/ModelsController.cs:33-45`；`ModelInfo.ProviderName` 已由 provider 填充（`OpenAICompatibleProvider.cs:357`）⇒ 可按 (ProviderName, Id) 精确定位 AIModel 记录 |
| FR-08 | `GET /v1/models/{model}` 支持按别名查询单模型信息 | `ModelsController.cs:51-65`、`AIProviderRegistry.GetModelById`（`AIProviderRegistry.cs:176-194`） |
| FR-09 | `POST /v1/chat/completions` 的 `model` 支持别名；别名必须严格映射到其归属 provider + upstream id，**命中禁用的模型按 FR-013 返回不可路由**，`provider:model`、`model` 原名语义完全不变 | `Controllers/UnifiedAI/OpenAIChatController.cs:366-390`、`AIProviderRegistry.GetProviderByModel:108-146`、`IsModelEnabled:97-106` |
| FR-10 | 别名冲突/非法字符 → 明确 4xx 错误响应，指明冲突占用方 | 新增；Controller 增 `catch (ArgumentException)` → 400 |
| FR-11 | 设置面板「编辑模型」抽屉可编辑上述 4 项 + 别名，并提供「按上游 ID 生成」建议值 | `components/settings/AiProvidersPanel.vue:598-648` |
| FR-12 | 前端类型/API 封装同步 | `types/aiModel.ts`、`services/aiModelsApi.ts` |

## Input

- 管理面 `PUT /api/ai-models/{id}` body（`UpdateAIModelRequest`）：`alias?: string|null`、`capabilities?: string[]`、`maxContext?: number|null`（既有）+ 新增 `maxOutput?: number|null`、`supportVision?: boolean|null`、`thinkingLevels?: string[]|null`、`publicAlias?: string|null`。
- `POST /v1/chat/completions` body `model`：`别名` | `提供商:原始模型id` | `原始模型id`（后两者为既有语义，`OpenAIChatController.cs:71-77`）。
- `GET /v1/models/{model}` path：`别名` 或 `原始模型id`。

## Output

- `AIModelDto` 新增字段：`maxOutput: number`、`supportVision: boolean`、`thinkingLevels: string[]`、`publicAlias: string|null`。其中 `supportVision` 为**合并值**：`实体.SupportVision || Capabilities 含 "vision"`（迁移友好，纯派生不改库）。
- `GET /v1/models` data[]：`id`（别名优先）、`object`、`created`、`owned_by`（保持不变）。
- 错误：`{ success:false, message:"..." }`（管理面沿用既有形状）；`/v1` 面沿用 `{ error:{ message, type } }`。

## Business Rules

| # | 规则 |
| --- | --- |
| BR-01 | `PublicAlias` 落库前 trim；**空串归一化为 `null`**（唯一索引允许多 NULL，空串会导致第二条记录插入冲突） |
| BR-02 | 唯一性判定：`OrdinalIgnoreCase`；校验时**排除自身 Id**；必须**直查 DB**（`Find(_.PublicAlias == alias)`），**不得依赖 `Meta.Cache`**（AsyncLocal 缓存可能假阳性/脏读） |
| BR-03 | 别名不得与既有路由语法冲突：正则 `^[A-Za-z0-9._-]{1,64}$` |
| BR-04 | 更新语义：`null` = 不改；`''` = 清空；`PublicAlias` 清空即置 `null` |
| BR-05 | `MaxOutput` 取值范围 `0 ~ 2,000,000`（0 = 不限/未设置）；`MaxContext` 沿用 `0 ~ 1,000,000`（前端既有 `max="1000000"`） |
| BR-06 | `ThinkingLevels` 落库：小写、trim、去重、按预设顺序稳定排序（`none < low < medium < high`，未知档位按原序追加末尾），逗号分隔存储 |
| BR-07 | `SupportVision` 仅由用户设定；**不自动同步 `Capabilities`**（避免静默改写用户数据）；派生读出现在 DTO |
| BR-08 | fetch models 更新分支不触碰 4 个新字段（FR-05） |
| BR-09 | `/v1/models` **保持现状不过滤** `Enabled=false`（别名投影与启停过滤解耦，见 Unknown #2） |
| BR-10 | 别名路由命中后**不再 fallback 到默认 provider**（现状 `GetProviderByModel` 末尾会兜底默认 provider，别名必须精确；禁用则 FR-013 判空） |

## Boundary Conditions

| 场景 | 行为 |
| --- | --- |
| 同一 provider 存在两条未设别名的模型 | 允许（`PublicAlias` 均为 NULL，不冲突） |
| 别名大小写不同（如 `GPT4o` vs `gpt4o`） | 视为冲突，拒绝后者 |
| 别名含冒号 / 斜杠 / 空格 / 中文 | 拒绝（400，含允许字符集说明） |
| 别名长度 0（空串） | 视为「清空」而非非法 |
| 别名长度 > 64 | 拒绝 |
| 两条记录同时 UPDATE 成同名（并发） | 由唯一索引兜底 → 捕获 DB 异常转 400；单测覆盖顺序场景即可（**并发压测不在本期范围**） |
| `ThinkingLevels` 空数组 | 视为清空（存 `null`） |
| `MaxOutput = 0` | 未设置/不限，不在任何地方做强制截断 |
| 上游模型 id 本身就是合法字符 | 别名仍可设（用户自选），无别名时 `/v1/models` 输出原名 |
| `provider:model` 形式且 provider 名恰好等于某别名 | 路由优先按既有 `':'` 前缀语法处理（含冒号的一定是 `provider:model`，别名禁冒号，天然无歧义） |

## Error Handling

| 触发 | 响应 | 位置 |
| --- | --- | --- |
| 别名非法字符 / 超长 | HTTP 400 `{success:false,message:"对外别名只允许字母、数字、点、下划线和连字符，长度 1~64"}` | `AIModelController.UpdateEditable` 增 `catch (ArgumentException)` |
| 别名重复 | HTTP 400 `{success:false,message:"对外别名「X」已被模型「<providerName>:<upstreamModelId>」占用"}` | 同上（Service 抛 `ArgumentException`） |
| 模型不存在 | HTTP 404（既有行为不变） | `AIModelController.cs:65` |
| 其它异常 | HTTP 500 + 日志（既有行为不变） | `AIModelController.cs:67-72` |
| `/v1/chat/completions` 别名对应模型被禁用 | 返回 No provider found（`异常的 maBadRequest`，既有 FR-013 语义） | `OpenAIChatController.cs:73-74` |
| `/v1/models/{model}` 查不到 | 404 `{error:{message:"Model 'X' not found"}}`（既有形状） | `ModelsController.cs:55-56` |

## Compatibility

- **向后兼容**：新列可空、默认 0/false/null ⇒ 既有数据无需回填语义；`/v1/models` 无别名时 id 完全不变；`/v1/chat/completions` 原有 3 种 model 写法行为不变；`AIModelDto` 只**新增**字段，不删改既有字段。
- **DB 结构**：仅追加列（`ADD COLUMN` 语义），不改列类型、不删列、不建表。SQLite 下由 XCode 反向工程完成；**是否自动发生待 Stage 6 实测**（00 工件 Unknown #1），若未自动则补显式补列步骤并回报用户。
- **前端**：`types/aiModel.ts`、`aiModelsApi.ts` 只加字段；抽屉表单新增控件，不动既有控件语义（唯一例外：既有 `alias` 清空传 `null` 导致清不掉，改为传 trim 后空串，见 BR-04 一致性说明）。

## Non-functional Requirements

| 项 | 要求 |
| --- | --- |
| 安全 | 管理面接口仍不返回任何凭证；`/v1` 端点的 `[Authorize("ApiKeyPolicy")]` 不变 |
| 性能 | `/v1/models`、`/v1/chat/completions` 的别名查询走**主键式点查**（`(ProviderName, UpstreamModelId)` / `PublicAlias`），不得全表 `FindAll()` 后在内存过滤；缓存读取可用但**唯一性写校验必须直查 DB**（BR-02） |
| 可读性 | 注释中文；DTO 注释说明合并语义（BR-07） |
| 前端样式 | 0 自定义 token：只用 `--el-*` + `color-mix()`（AGENTS.md §2.3） |
| 可测性 | 别名生成/校验的纯函数集中于前端可被 vitest 直接调用；服务端规则由 xUnit 覆盖 |

## Interaction Design（交互设计 · 必写节）

> 涉及用户可见 UI ⇒ 本节为闸门1 必审项；自查依据 `.agents/skills/ui-ux-design`（CRAP 四原则 + 配色 + 字体）。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 打开模型抽屉 | 点模型行右侧编辑图标 | 出现「编辑模型」抽屉，含 上游模型ID（禁用）/ ChatModelId（禁用）/ 别名 / 能力标签 / 输入上下文 / **输出上限** / **支持图片** / **思考档位** / **对外别名** | 查看态 → 编辑态（同一抽屉） | 抽屉滑入 | — | 抽屉 `size=380px`，内容超一屏时**内部滚动**，footer（取消/保存）常驻可见 |
| 生成对外别名 | 点「生成」小按钮（别名输入框后置） | 输入框填入由 UpstreamModelId 清洗出的建议值；若已存在同名则追 `-2/-3` | 无输入 → 有建议值 | 输入即时变化，无 toast | UpstreamModelId 为空时不生成并禁用按钮 | 建议值仍需服务端校验，**前端校验不替代后端** |
| 支持图片开关 | 点击 el-switch | 开=宣称该模型可接收图片；关=不宣称 | 开/关 二态 | 开关即时反馈 | 默认关（未设置） | 当 `capabilities` 含 `vision` 而开关为关时，开关下方显示辅助说明文字「能力标签含 vision」 |
| 思考档位多选 | el-select 多选 + allow-create | 选中项即落户；可输入自定义档位 | 未设置 / 已设置（1~N 项） | chips 即时增删 | 空态 placeholder「未设置，如 low/medium/high」 | 上限 10 项，超出提示；重复项自动去重 |
| 输出上限输入 | el-input-number | 填 ≥0 整数 | 0=不限 | 即时 | 默认 0 | 超 `2,000,000` 时输入框标红并禁用保存（前端提示，不需点保存才报错） |
| 保存 | 点「保存」 | 调 `PUT /api/ai-models/{id}` → 成功：抽屉关闭 + `ElMessage.success('模型已更新')` + 列表该行就地更新（不整页刷新） | 空闲 → saving(loading) → 成功/失败 | 按钮 loading | — | 失败：抽屉**不关闭** + `ElMessage.error(服务端 message)`（如别名被占用原文上屏） |
| 取消 | 点「取消」/遮罩 | 抽屉关闭，放弃改动 | — | — | — | 不触发保存请求 |
| 模型行展示 | 列表渲染 | 有别名时行内显示别名徽标；有思考档位时显示档位 tag | — | — | 无别名不显示徽标（不占位） | 长别名 `text-overflow: ellipsis`，不破版 |

### 交互验收标准（并入 Acceptance Criteria，逐条可测）

- 「生成」按钮：给定 `Qwen/Qwen2.5-72B` → 期望 `qwen-qwen2.5-72b`（纯函数单测，含去首尾 `-`、合并连续 `-`、截断 64 三条用例）。
- 保存失败：mock `PUT` 返回 400 → 断言抽屉未关闭、错误消息出现在 `ElMessage`、列表未更新。
- 保存成功：mock 200 → 断言抽屉关闭、`request` 携带 `publicAlias`/`maxOutput`/`supportVision`/`thinkingLevels` 字段、列表行显示新别名徽标。
- 即时校验：`maxOutput` 输入 2000001 → 断言保存按钮 disabled（可见原因，非无声禁用）。
- 禁用/不可用态必须可见原因：switch 关且 capabilities 含 vision 时显示辅助说明。

### 走查符合性（DoD 绑定）

- Stage 7 走查按 `ui-ux-design` 的「走查 UI 符合性清单」逐项核对：上述「触发 → 结果」与实现一致；别名徽标/档位 tag 用 `--el-*` token（含 `color-mix()` 派生色），不出现私有色值；抽屉 380px 下控件对齐（label 与上控件间距一致）、footer 常驻；长文本别名不破版。
- 本期 UI 验证手段：**vitest 覆盖行为 + 本地起 dev-stack 截图读图**（`pwsh scripts/dev-stack.ps1`），不新增 e2e 全量用例（理由：本任务非插件任务，宿主 e2e 成本高收益低；若用户要求再补）。

## Acceptance Criteria

> 闸门1 用户确认的就是本清单（逐条可测）。

**后端**
- [ ] AC-01 `dotnet build ForgeSelf.Api` 零 error。
- [ ] AC-02 实体新增 4 字段；存在 `[BindIndex("IU_AIModel_PublicAlias", true, "PublicAlias")]`。
- [ ] AC-03 xUnit：两条记录 `PublicAlias` 均为 NULL 时**不冲突**（可共存）。
- [ ] AC-04 xUnit：两条记录设同名别名 → `ArgumentException`，消息含占用方 `providerName:upstreamModelId`。
- [ ] AC-05 xUnit：别名含 `:` `/` `空格` `中文`、长度 65 → 均被拒；长度 1 且合法 → 通过。
- [ ] AC-06 xUnit：别名大小写不同 → 判冲突。
- [ ] AC-07 xUnit：自身重复保存同一别名（更新自己）→ 不报错。
- [ ] AC-08 xUnit：`PUT` 更新 `maxOutput/supportVision/thinkingLevels/publicAlias` 后 DTO 回显一致；`null` 表示不改、`''` 表示清空。
- [ ] AC-09 xUnit：`thinkingLevels` 空数组→存 null；`["HIGH"," low ","low"]`→存 `low,high`（小写去重、预设序）。
- [ ] AC-10 xUnit：fetch models 重拉后 4 项新字段值不变（FR-05）。
- [ ] AC-11 xUnit：`GET /v1/models` 有别名输出别名、无别名输出原名（FakeHandler 桩接上游）。
- [ ] AC-12 xUnit：`/v1/chat/completions` 传别名 → 路由到正确 provider 且上游收到的是 UpstreamModelId；原名行为回归通过。
- [ ] AC-13 xUnit：别名指向的模型 `Enabled=false` → 不可路由（FR-013 语义）。
- [ ] AC-14 数据库实测：临时/副本库上新增列出现，且既有记录行数与 `UpstreamModelId/Enabled` 等既有值**无变化**。
- [ ] AC-15 `AIModelDto` 只新增字段，无删除/重命名既有字段（契约无破坏）。

**前端**
- [ ] AC-16 `pnpm run check` 零错误、`pnpm run test` 相关用例全绿。
- [ ] AC-17 vitest：`buildAliasSuggestion('Qwen/Qwen2.5-72B') === 'qwen-qwen2.5-72b'`；含去首尾 `-`、合并连续 `-`、>64 截断三类用例。
- [ ] AC-18 vitest：保存成功路径（抽屉关闭 + 列表就地更新 + payload 含新字段）；失败路径（抽屉不关 + 错误消息上屏）。
- [ ] AC-19 vitest：`maxOutput` 越界 → 保存不可用且可见原因。
- [ ] AC-20 本地 dev-stack 起服务，走查「编辑模型」抽屉：生成别名 → 保存 → `GET /v1/models` 显示别名（截图留存）。
- [ ] AC-21 抽屉仅用 `--el-*` token，无私有色值（`scripts/check-design-compliance.mjs` 或既有 design 门禁通过）。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| U-1 XCode 是否自动 ALTER TABLE ADD COLUMN（宿主无显式 Migration 设置） | 决定是否需要补显式扩表步骤（属 DB 结构变更） | **Stage 6 实测**（临时库）；若不自动 → 停止并回报用户，由用户裁决后再执行（不擅自写迁移脚本） |
| U-2 `/v1/models` 是否应过滤 `Enabled=false` 的模型 | 别名列表是否会暴露禁用模型 | 保守选择：**保持现状不过滤**（BR-09）；如需过滤，登记 TODO 后续处理 |
| U-3 各家思考档位是否需要 provider 侧翻译再透传（OpenAI `reasoning_effort` / Claude budget / Gemini `thinking_level`） | 本期是否做自动透传 | 保守选择：**本期只做声明+存储+展示，不自动透传**；登记 TODO |
| U-4 `SupportVision` 是否需要在发图前做硬校验（拦截不支持模型的图片请求） | 是否要做请求级拦截 | 保守选择：**本期不拦截**（只声明）；登记 TODO，避免改变现有多模态处理行为（`OpenAIChatController.cs:84-88`） |
| U-5 既有 `GetProviderByModel` 末尾对未知模型兜底默认 provider | 别名必须精确路由（BR-10） | 本期在别名分支内绕过兜底；**不改动既有兜底行为** |
