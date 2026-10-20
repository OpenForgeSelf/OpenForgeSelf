# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：2026-10-15-aimodel-metadata-alias ｜ 日期：2026-10-15

## 项目结构

| 路径 | 说明 | 依据 |
| --- | --- | --- |
| `ForgeSelf.slnx` | 解决方案（含 Abstractions / Core / Api / Web / Bootstrapper / 各 Tests） | 根目录 |
| `ForgeSelf.Api/` | ASP.NET Core 10 宿主，含 `Entities/`、`Services/`、`Controllers/`、`Plugins/` | `ForgeSelf.Api/Entities/AIModel.cs` 等 |
| `ForgeSelf.Api.Tests/` | 后端 xUnit 测试（含 `AIModelFeatureTests.cs`、`XCodeTestFixture`） | `ForgeSelf.Api.Tests/AIModelFeatureTests.cs:22-28` |
| `ForgeSelf.Web/` | Vue 3.5 SPA（Vite 6 + TS + Element Plus + Tailwind + Pinia + pnpm） | `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue`、`playwright.config.ts` |
| `Plugins/` | 插件体系（各插件自带 `Data/Model.xml` + `plugin.json`） | `Plugins/*/Data/Model.xml` |
| `docs/04-standards/`、AGENTS.md | 强制工程/流程规范 | AGENTS.md §0、§11 |

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 ORM | NewLife.**XCode**（实体生成件 + `Entity<T>`；实体缓存 `Meta.Cache`） | `ForgeSelf.Api/Entities/AIModel.cs`（`[BindTable("AIModel", ConnName="ForgeSelf")]`、`public partial class AIModel : Entity<AIModel>`） |
| 后端 DB | SQLite（`ConnName="ForgeSelf"`） | `AIModel.cs:22` |
| 后端框架 | ASP.NET Core 10（`[ApiController]` + 手工 CG 注册） | `AppBuilder.cs:210`（`builder.Services.AddScoped<IAIModelService, AIModelService>()`） |
| 前端 | Vue 3.5 + TS + Element Plus + Tailwind + reactive/ref | `AiProvidersPanel.vue:158-195` |
| 前端单测 | vitest（`__tests__/*.test.ts`） | `ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts` |
| E2E | Playwright（配置在 Web 工程内） | `ForgeSelf.Web/playwright.config.ts` |
| 后端测试 | xUnit + FakeHandler 桩 + `XCodeTestFixture` 临时 SQLite | `AIModelFeatureTests.cs:22-70` |

## 架构特点

- **AIModel 实体的字段同步点有 4 处**（本次必须全部同步，否则编译不过）：
  1. `ForgeSelf.Api/Entities/AIModel.cs`（XCoder 生成件：字段 + `BindColumn` + `Copy` + `[name]` 索引器 + `_.__`/`__` 字段名常量）
  2. `ForgeSelf.Api/Entities/Interfaces/IAIModelModel.cs`（接口属性）
  3. `ForgeSelf.Api/Entities/Models/AIModelModel.cs`（模型类 + `Copy`）
  4. `ForgeSelf.Api/Entities/AIModel.Biz.cs`（业务/Valid/扩展查询，**按需改**）
- **宿主没有 `Data/Model.xml`**：全仓 `Model.xml` 均属插件（`Plugins/*/Data/Model.xml`），宿主实体无 XML SSOT → 新增字段只能直接改上述生成件（与项目记忆「改 Model.xml → xcode 生成」的插件路径不同，本任务为宿主实体，走**直接改生成件**路径，需 4 处同步）。
- **对外 `/v1` 端点数据源与 AIModel 表不是同一条链路**（关键）：
  - `Controllers/UnifiedAI/ModelsController.cs:33` → `_registry.GetAllModelsAsync()` → `AIProviderRegistry.GetAllModelsAsync` → 逐个 provider 实时 `GetModelsAsync`（`AIProviderRegistry.cs:158-174`），**只读上游实时清单，不查 AIModel 表、也不过滤 Enabled**。
  - 聊天路由 `OpenAIChatController.ResolveProviderAndModel`（`:366-390`）→ `registry.GetProviderByModel`（`AIProviderRegistry.cs:108-146`）→ 其中 `IsModelEnabled` 才查 AIModel 表（`AIProviderRegistry.cs:97-106`，FR-013 禁用不路由）。
  - ⇒ 别名若要在 `/v1/models` 生效，必须在 **ModelsController/Registry 层做 AIModel 表投影**；若要在 `/v1/chat/completions` 生效，必须在 **`GetProviderByModel` 之前/之中加别名反查**。
- **上游同步语义**：`AIModelService.FetchForProviderAsync` 更新分支只刷新 `Owner/LastSyncTime/UpdateTime`，注释明确「保留用户的 Enabled/Alias/Capabilities」（`:124-131`）⇒ 新增用户可编辑字段天然不被覆盖。

## 测试方式

| 层 | 命令 | 依据 |
| --- | --- | --- |
| 后端单测 | `dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`（走 `XCodeTestFixture` 临时 SQLite + FakeHandler，无需真实网络） | `AIModelFeatureTests.cs:18-28` |
| 后端构建 | `dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` | AGENTS.md §5 |
| 前端单测 | `cd ForgeSelf.Web && pnpm run test`（vitest） | AGENTS.md §5.3 |
| 前端类型/Lint | `cd ForgeSelf.Web && pnpm run check` | AGENTS.md §5.3 |
| E2E | `ForgeSelf.Web/playwright.config.ts`（`e2e/plugins/<id>` 为插件层用例目录约定） | AGENTS.md §0 插件门禁② |

## 构建命令

```bash
# 后端
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj

# 前端
cd ForgeSelf.Web && pnpm run check && pnpm run test
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Api/Entities/` | XCode 实体（生成件 + Interface/Models + Biz） |
| `ForgeSelf.Api/Services/` | 业务服务（含 `AIModelService`、`AI/AIProviderRegistry`） |
| `ForgeSelf.Api/Controllers/` | 管理面 REST（`/api/ai-models`、`/api/ai-providers`） |
| `ForgeSelf.Api/Controllers/UnifiedAI/` | OpenAI 兼容面（`/v1/models`、`/v1/chat/completions`，`[Authorize("ApiKeyPolicy")]`） |
| `ForgeSelf.Web/src/components/settings/` | 设置面板（AI 提供方 + 模型编辑抽屉） |
| `ForgeSelf.Web/src/services/` | API 封装（`aiModelsApi.ts`） |
| `ForgeSelf.Web/src/types/` | 前端类型（`aiModel.ts`） |

## 代码组织方式

- 后端：EF-free，纯 XCode 实体 + 手工 Services + Controller 直返匿名对象 `{ success, data }`；DTO 定义在 Service 文件尾部 `#region DTO`（`AIModelService.cs:258-296`）。
- 前端：抽屉表单用 `reactive` 表单对象 + `aiModelsApi.update` 提交；面板本地状态 `providerModels` 手工合并返回值（`AiProvidersPanel.vue:184-187`）。
- 注释一律中文；Controller 不吞凭证（`AIModelController.cs:8-11` 注释明确）。

## 现有工程规范（对本任务有约束力）

| 规范 | 条目 | 约束 |
| --- | --- | --- |
| AGENTS.md §0 | 预飞铁律 | 日记 + TODO + 读规范 + AI-Native 九阶段 + 三闸门 |
| AGENTS.md §2.3 | 前端 0 自定义 token | 只用 `--el-*` + `color-mix()`，界面不得引入私有色值 |
| 规范 §1.1 | 三闸门 | 闸门1（Intent/Spec/Plan/Task 后用户批准）→ Implement |
| 规范 §1 硬约束 | 「不修改数据库结构（涉及迁移即高风险，须升级审批）」 | **本任务新增实体字段会触发 SQLite 表结构变化 → 必须在 Intent/Spec 显式申报，由用户在闸门1 裁决** |
| 规范 §2 Stage2 | UI 功能必写 Interaction Design 节 | 02-spec 必须有该节 |
| 项目记忆 | XCode 测试隔离 | 插件必须自行建表、唯一性校验**直查 DB 不信 `Meta.Cache`**（`Meta.Session` 是 AsyncLocal） |

## 候选低风险任务

本次为单一任务：`AIModel` 模型能力元数据（输出上限 / 图片支持 / 思考档位）+ 对外唯一别名。范围限定宿主后端 + 设置面板前端，**不涉插件、不涉生产环境、不涉鉴权体系改造**。

## 选择该任务的原因

- 用户显式提出（唯一来源），且已拍板「别名即对外主名 + 全局唯一」。
- 现状具备低风险落地面：已有 `AIModel` 实体、已有管理抽屉 UI、已有 xUnit 夹具；对外 `/v1` 链路只有 2 个文件需投影/反查。
- 唯一高风险点明确且可控：SQLite 表结构新增列（见 §硬约束），已在前置沟通中提示等待闸门1 裁决。

## Unknown（Stage 0 记录）

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| XCode 是否对新字段自动 ALTER TABLE ADD COLUMN（宿主未见显式 `Migration` 设置，全仓 grep `Migration` 仅命中 `SecretMigrationService` 与 `ForgeSetting.SecretMigrationVersion`） | 决定新增字段能否自动落眸、既有数据是否保留 | 保守假设：**依赖 XCode 反向工程默认行为**；Stage 6 用临时/副本库实际验证（新增列是否出现 + 老数据不丢）。若实测不自动，Plan 补显式建/扩表步骤并回报用户（属 DB 结构变更，须用户二次确认） |
| `/v1/models` 是否应过滤 `Enabled=false` 的模型（当前**不过滤**） | 别名列表是否会暴露被禁用的模型 | 保守假设：**保持现状不过滤**（最小改动），仅做 id 重命名；如需过滤列为后续 TODO |
| 思考档位上游透传是否需要翻译（OpenAI `reasoning_effort` / Claude budget / Gemini `thinking_level`） | 是否需要在 provider 层适配 | 保守假设：**本期只做「声明 + 存储 + 展示」，不做自动透传**；透传留给后续 TODO |
