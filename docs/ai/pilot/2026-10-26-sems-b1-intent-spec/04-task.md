---
task_id: 2026-10-26-sems-b1-intent-spec
feature: none（Sems 无 docs/02-features 档案；需求依据为 Plugins/Sems/docs/requirements.md）
risk: { level: L2, triggers: ["可能新增插件实体库（数据库结构变更，须闸门1授权）", "新增写操作端点与工具（管理面鉴权）"], raised_by_agent: true }
gate1: user
gate2: user
gate3: user
expected_files:
  - Plugins/Sems/Services/SpecService.cs
  - Plugins/Sems/Services/SpecFileStore.cs
  - Plugins/Sems/Controllers/SpecsController.cs
  - Plugins/Sems/SpecTools.cs
  - Plugins/Sems/SemsPlugin.cs
  - Plugins/Sems/plugin.json
  - Plugins/Sems/TODO.md
  - Plugins/Sems/docs/requirements.md
  - ForgeSelf.Api.Tests/Plugins/Sems/SpecServiceTests.cs
  - ForgeSelf.Api.Tests/Plugins/Sems/SpecCoverageTests.cs
  - ForgeSelf.Api.Tests/Plugins/Sems/SemsControllerTests.cs
  - ForgeSelf.Api.Tests/Plugins/Sems/SemsPluginRegistrationTests.cs
rollback: git revert <commit>
writeback: Plugins/Sems/docs/requirements.md（B1 的「Sems 现状」）、Plugins/Sems/TODO.md
---

# Task

> 阶段：Stage 4｜零自我决策空间。
> 前序工件：00、01、02、03 已齐备，但须经闸门 1 确认，且 03 须在 K1、K3 拍板后定稿。闸门 1 未通过前禁止执行本任务。

## Task ID

PILOT-B1（本任务目录：2026-10-26-sems-b1-intent-spec）

## Objective

在 Sems 插件中实现 B1 意图与规格层：规格的结构化记录、版本化、与代码变更的双向关联、验收标准的用例关联状态，达到 02-spec 的 AC1 到 AC6。

## Scope

Allowed：

- 03-plan.md「Files To Change」中，经闸门 1 定稿的文件。
- 补写 05、06、07 工件，更新日记与根 TODO.md。

Forbidden：

- 生产环境、数据库结构（除非闸门 1 明文授权，且仅限 Sems 插件自有库）、鉴权权限支付、新增大规模依赖。
- 无关重构；既有 13 个 sems_* 工具与既有端点的行为变更。
- 超出 03-plan.md 的文件；B7 溯源的实现；B3 沙箱；第 2 节已知风险（sems_add_command 与 sems_run_command）的修复，另立任务。
- 停、启、杀用户运行中的宿主进程；git 提交（闸门 2 通过后由用户授权）。

## Acceptance Criteria

- [x] AC1 给定一条规格，能列出全部关联变更；给定一个变更，能显示所依据的规格及版本。
- [x] AC2 给定含 N 条验收标准的规格，能显示每条标准是否已关联用例。
- [x] AC3 修改规格后旧版本仍可查看，已有关联变更仍指向其生成时的规格版本。
- [x] AC4 既有 13 个 sems_* 工具与既有端点行为不变，相关既有测试保持通过。
- [ ] AC5 新增写操作端点类级带 [Authorize("ApiKeyPolicy")]，匿名调用返回未授权。类级策略与「仅 GetCount 允许匿名」已有反射测试；运行时匿名 401 仍 Unknown。
- [x] AC6 构建通过，新增测试通过，判据看日志正文。

## Expected Files

见头部 expected_files。K1 选实体库时，另含 03-plan.md 所列的 Data/、XCodeConfig.cs、两个 csproj，须同步回写本头部。

## Verification Commands

与 03-plan.md Verification 一致，逐条须真实运行：

- `dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj`
- `dotnet test ForgeSelf.Api.Tests`（筛选范围在 K7 后写定）
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-26-sems-b1-intent-spec -EnforceHeader -EnforceAcCoverage`
- 起源码本地验收：`pwsh -NoProfile -File scripts/dev-stack.ps1`
