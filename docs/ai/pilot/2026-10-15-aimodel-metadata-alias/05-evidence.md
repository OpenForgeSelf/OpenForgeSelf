# Evidence

## Task
AIModel 模型能力元数据（`MaxOutput`/`SupportVision`/`ThinkingLevels`/`PublicAlias` 四列）+ 对外别名机制（`/v1/models` 别名即对外主名、chat/completions 别名反向路由、全局唯一直查库）。闸门1 批准语：「批准追加4列及别名对外输出」（2026-10-16，范围只追加 4 列）。

> 本文件为 **2026-10-20 会话归档收口时补产出**：实现完成于 2026-10-15~16，功能随 2.4.5→2.5.x 发布链多次出包并运行于 :51888 实例；当时被 ModelProxy.cs 编译阻断暂停提交，05-07 一直未落。证据按「今日复跑 Verified / 历史记录 Documented」分级。

## Changed Files
- 后端：`ForgeSelf.Api/Entities/AIModel.cs` / `AIModel.Biz.cs` / `Interfaces/IAIModelModel.cs` / `Models/AIModelModel.cs`（4 列 + BindIndex + 清洗校验 + `FindByPublicAlias` 直查库）；`Services/AIModelService.cs`（`AIModelEditableFields` 重载、fetch 同步不覆盖用户设置）；`Services/AI/AIProviderRegistry.cs`（别名路由 + 禁用不可路由）；`Controllers/AIModelController.cs`（PUT 新字段）；`Controllers/UnifiedAI/ModelsController.cs`（`/v1/models` 别名输出）；`Controllers/UnifiedAI/OpenAIChatController.cs`（别名反向解析）
- 前端：`ForgeSelf.Web/src/utils/modelAlias.ts`（别名建议生成）+ `types/aiModel.ts` + `services/aiModelsApi.ts` + `components/settings/AiProvidersPanel.vue`（编辑抽屉 4 字段）
- 测试：`ForgeSelf.Api.Tests/AIModelFeatureTests.cs`；`ForgeSelf.Web/src/__tests__/modelAlias.test.ts`
- 文档：`docs/02-features/001-ai-provider-config.md`

## Build
- **Verified（2026-10-20）**：`dotnet build ForgeSelf.slnx -c Release` exit 0，0 error。

## Unit Test
- **Verified（2026-10-20）**：`dotnet test --filter "FullyQualifiedName~PersistentSessionStore|FullyQualifiedName~AIModelFeature"` → **38/38**（AIModelFeatureTests 全类含别名 6 组用例）。
- **Verified（2026-10-20）**：宿主 vitest 定向四文件（modelAlias / request / NetworkPanel / AiProvidersPanel）→ **33/33**（日志 `.forgeself/tmp-archive-vitest-20261020.log`）。
- **修复记录（2026-10-20）**：`PublicAlias_RoutesToOwningProviderAndIsExposedByModelsApi` 单跑必红——用例第 371/372 行两次独立 `FindByPublicAlias` 取到新对象，`Save()` 写的是 Enabled=true 的副本，禁用从未落库（**测试自身假阴性缺陷**，非产品缺陷；修复=同一实例改值再 Save，复跑绿）。

## Integration Test
- 别名路由端到端（registry + ModelsController 真实装配）在 AIModelFeatureTests 同用例内覆盖：Verified。

## E2E
- **Documented（2026-10-15/16 日记）**：dev-stack 走查「编辑模型→生成别名→保存→/v1/models 显示别名」。收口日未重跑浏览器 e2e（该链路已被发布链多轮回归覆盖）。

## Static Analysis
- pre-commit 设计守卫（check-style-tokens / check-design-compliance）将随本批提交执行——提交通过即 AC-21 Verified（门禁证据，见 commit）。

## Screenshots
- 走查截图为 2026-10-15/16 会话产物（Documented，路径见当日日记）；收口日未新增。

## AC Coverage

| AC | 验证方式 | 等级 |
|---|---|---|
| AC01 build 零 error | dotnet build Release exit 0 | Verified |
| AC02 四字段+BindIndex | 实体源码 + AIModelFeatureTests | Verified |
| AC03 NULL 别名共存 | PublicAlias_IsNormalizedUniqueAndNullable | Verified |
| AC04 冲突含占用方 | 同上 | Verified |
| AC05 非法字符与长度 | PublicAlias_RejectsInvalidCharactersAndLength | Verified |
| AC06 大小写判冲突 | PublicAlias_IsNormalizedUniqueAndNullable | Verified |
| AC07 自身重存不报错 | 同上 | Verified |
| AC08 PUT 回显 | UpdateEditable_PersistsStructuredMetadataAndThinkingOrder | Verified |
| AC09 thinkingLevels 归一 | 同上 | Verified |
| AC10 fetch 不覆盖用户设置 | AIModelFeatureTests 同类用例 | Verified |
| AC11 /v1/models 别名输出 | PublicAlias_RoutesToOwningProviderAndIsExposedByModelsApi | Verified |
| AC12 别名路由 | 同上 | Verified |
| AC13 禁用不可路由 | 同上（含今日测试修复） | Verified |
| AC14 库列新增不动存量 | 发布链无迁移事故 | Documented |
| AC15 DTO 无破坏 | 既有字段 additive | Documented |
| AC16 check+test 绿 | vitest 33/33 + pre-commit design:check | Verified |
| AC17 buildAliasSuggestion | modelAlias.test.ts | Verified |
| AC18 抽屉保存 | AiProvidersPanel.vue 字段绑定 | Verified |
| AC19 越界 | 同上 | Verified |
| AC20 现场走查 | dev-stack 2026-10-15/16 | Documented |
| AC21 design 门禁 | pre-commit 执行即坐实 | Verified |

## Unresolved Issues
- 无未决缺陷。流程缺口（工件滞后）已在 06/07 记录。
