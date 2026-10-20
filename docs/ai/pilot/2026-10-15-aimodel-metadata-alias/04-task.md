---
task_id: 2026-10-15-aimodel-metadata-alias
feature: none                      # 既有设置面板能力增强，非新功能入口，不登记 features.ts（已在 03-plan 说明）
risk: { level: L2, triggers: ["SQLite 表结构新增列（ForgeSelf.Api/Entities/AIModel.cs）", "对外 /v1 契约输出变化（ModelsController id 语义）"], raised_by_agent: false }
gate1: { mode: user, at: "2026-10-16", ref: "用户回复：批准追加4列及别名对外输出" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Api/Entities/AIModel.cs
  - ForgeSelf.Api/Entities/Interfaces/IAIModelModel.cs
  - ForgeSelf.Api/Entities/Models/AIModelModel.cs
  - ForgeSelf.Api/Entities/AIModel.Biz.cs
  - ForgeSelf.Api/Services/AIModelService.cs
  - ForgeSelf.Api/Controllers/AIModelController.cs
  - ForgeSelf.Api/Services/AI/AIProviderRegistry.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs
  - ForgeSelf.Api.Tests/AIModelMetadataAliasTests.cs
  - ForgeSelf.Web/src/types/aiModel.ts
  - ForgeSelf.Web/src/services/aiModelsApi.ts
  - ForgeSelf.Web/src/utils/modelAlias.ts
  - ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue
  - ForgeSelf.Web/src/__tests__/modelAlias.test.ts
  - ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts
rollback: "git checkout -- ForgeSelf.Api/Entities ForgeSelf.Api/Services ForgeSelf.Api/Controllers ForgeSelf.Web/src && rm -f ForgeSelf.Api.Tests/AIModelMetadataAliasTests.cs ForgeSelf.Web/src/utils/modelAlias.ts ForgeSelf.Web/src/__tests__/modelAlias.test.ts；数据库：新增列为空/默认值，无需回滚数据（禁止删列）"
writeback: { feature_doc: none, reason: "字段级增强，已由 docs/ai/pilot 工件承载；如需功能文档登记另起 TODO" }
---

# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备。闸门1 已于 2026-10-16 批准：追加 4 列及别名对外输出。

## Task ID

PILOT-2026-10-15-aimodel-metadata-alias

## Objective

`AIModel` 增加「输出上限 / 图片支持 / 思考档位」三项结构化能力元数据与一个**全局唯一的对外别名**，使 `GET /v1/models` 优先输出别名、`POST /v1/chat/completions` 支持按别名路由（原名语义不变），并在系统设置面板可编辑；以上行为有后端 xUnit + 前端 vitest 真实通过的证据。

## Scope

### Allowed

- 03-plan.md「Files To Change」列出的 **15 个文件**（含 3 个新增文件）。
- 追加/更新测试用例：`AIModelMetadataAliasTests.cs`（新增）、`modelAlias.test.ts`（新增）、`AiProvidersPanel.test.ts`（按需修既有失效断言）。
- 因新增字段导致的必要编译修复（如同文件内 `Copy()`/字段名常量同步）。

### Forbidden

- ❌ 修改生产环境 / 运行中的宿主实例（不启停、不发布）。
- ❌ **删除或改名任何既有数据库列**；只允许追加列（碰请 lifted 到闸门1）。
- ❌ 修改鉴权/权限/安全核心（`ApiKeyPolicy`、加密服务、插件子容器）。
- ❌ 引入新依赖（npm / NuGet 均禁止）。
- ❌ 触碰 `Plugins/**`、`ForgeSelf.Web/src/data/features.ts`、`OpenAIChatController.cs`、`AIModelService.FetchForProviderAsync` 更新分支。
- ❌ 做「自动透传 reasoning_effort」「发图前硬校验 supportVision」「按能力自动路由」「`/v1/models` 过滤禁用模型」等延伸功能（已登记 Unknown/TODO）。
- ❌ git commit / push（须用户明确授权）。

## Acceptance Criteria

> 逐条来自 02-spec.md「Acceptance Criteria」，实现后逐条打勾并给出证据。

**后端**
- [ ] AC-01 `dotnet build ForgeSelf.Api` 零 error
- [ ] AC-02 实体新增 4 字段 + `[BindIndex("IU_AIModel_PublicAlias", true, "PublicAlias")]`
- [ ] AC-03 两条记录 `PublicAlias` 均为 NULL 时不冲突
- [ ] AC-04 同名别名冲突 → `ArgumentException`，消息含占用方 `providerName:upstreamModelId`
- [ ] AC-05 别名含 `:` / `/` / 空格 / 中文、长度 65 → 拒绝；长度 1 合法 → 通过
- [ ] AC-06 大小写不同判冲突
- [ ] AC-07 自身重复保存同一别名不报错
- [ ] AC-08 `null` 不改、`''` 清空，`maxOutput/supportVision/thinkingLevels/publicAlias` DTO 回显一致
- [ ] AC-09 `thinkingLevels` 空数组→null；`["HIGH"," low ","low"]→"low,high"`
- [ ] AC-10 fetch models 重拉后 4 项新字段不变
- [ ] AC-11 `GET /v1/models` 有别名输出别名、无别名输出原名
- [ ] AC-12 `POST /v1/chat/completions` 别名 → 正确 provider + 上游收到 UpstreamModelId；原名回归通过
- [ ] AC-13 别名对应模型 `Enabled=false` → 不可路由
- [ ] AC-14 实测新增 4 列存在，既有记录行数与 `UpstreamModelId/Enabled` 无变化
- [ ] AC-15 `AIModelDto` 只新增字段，无删除/重命名

**前端**
- [ ] AC-16 `pnpm run check` 零错误、`pnpm run test` 相关用例全绿
- [ ] AC-17 `buildAliasSuggestion('Qwen/Qwen2.5-72B') === 'qwen-qwen2.5-72b'`，含去首尾 `-`、合并连续 `-`、>64 截断
- [ ] AC-18 保存成功（抽屉关闭 + 列表就地更新 + payload 含新字段）/ 失败（抽屉不关 + 错误消息上屏）
- [ ] AC-19 `maxOutput` 越界 → 保存不可用且可见原因
- [ ] AC-20 本地 dev-stack 走查：生成别名 → 保存 → `GET /v1/models` 显示别名（截图留存）
- [ ] AC-21 抽屉仅用 `--el-*` token，无私有色值（`pnpm run design:check` 通过）

## Expected Files

见 front-matter `expected_files`（15 项）。

## Verification Commands

```bash
# 1) 后端构建
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj

# 2) 后端单测（仅 AIModel 相关；全量含既有环境依赖失败项，非本任务引入）
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~AIModel"

# 3) 前端类型 + lint
cd ForgeSelf.Web && pnpm run check

# 4) 前端单测
pnpm run test -- run src/__tests__/modelAlias.test.ts src/__tests__/AiProvidersPanel.test.ts

# 5) 前端 SSOT / 设计门禁
pnpm run design:check
pnpm run check:features

# 6) 人工走查（Stage 7）
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1
# 走查后停止：pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1 -Stop
```

## 停止条件（Escalation）

出现下列任一情况，**立即停止实现并回报用户**，不得自行尝试 workaround：

1. XCode 未自动为新字段加列（U-1 验证失败）→ 属 DB 结构变更，须用户二次裁决。
2. 唯一索引在 SQLite 上对多个 NULL 判冲突 → 说明 `[BindIndex]` unique + nullable 组合在本项目不可用，须改设计（如改应用层唯一 + 非唯一索引）。
3. `/v1/models` 投影依赖的 `ModelInfo.ProviderName` 在任一 provider 路径为空（除已知 OpenAI/Anthropic 两条外还有新路径）。
4. 需要触碰 Forbidden 清单中的任何文件才能通过测试。

