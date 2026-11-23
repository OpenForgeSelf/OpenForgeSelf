---
task_id: 2026-11-23-sems-stop-feedback
feature: none                      # 插件缺陷修复，不对应 docs/02-features 编号；需求真源在 Plugins/Sems/docs/01-requirements.md（F09）
risk: { level: L2, triggers: ["Plugins/Sems/Services/RunnerService.cs", "Plugins/Sems/web/src/workbench/workbenchStore.ts", "ForgeSelf.Api.Tests/Plugins/Sems/"], raised_by_agent: false }
gate1: { mode: auto-granted, at: 2026-11-23, ref: "§8.5 自动迭代授权 + 用户输入1 直接指令（含验收标准）+ 本会话审批策略 never-ask；决策留痕见 07 与工作日记" }
gate2: { mode: merged-to-final-report, at: null, decision: null }
gate3: { mode: not-executed, at: null, ref: "发布动作（git 提交/发布）须用户批准，本批不执行" }
expected_files:
  - Plugins/Sems/Services/RunnerService.cs
  - Plugins/Sems/Controllers/ProjectCommandsController.cs
  - Plugins/Sems/Controllers/RunsController.cs
  - Plugins/Sems/ToolExtensions.cs
  - Plugins/Sems/web/src/workbench/workbenchStore.ts
  - Plugins/Sems/web/src/workbench/stop-feedback.spec.ts
  - Plugins/Sems/web/src/workbench/views/ProjectTasksView.vue
  - Plugins/Sems/web/src/RunPanel.vue
  - Plugins/Sems/web/src/confirmOps.ts
  - Plugins/Sems/web/src/confirmOps.spec.ts
  - Plugins/Sems/web/src/types.ts
  - Plugins/Sems/web/src/workbench/workbench.css
  - ForgeSelf.Api.Tests/Plugins/Sems/*.cs
  - Plugins/Sems/docs/01-requirements.md
  - Plugins/Sems/docs/02-test-plan.md
  - docs/11-troubleshooting/cases/2026-11-23-sems-stop-no-feedback.md
  - docs/11-troubleshooting/problem-classes/PC-024-frontend-poll-error-handling.md
  - docs/ai/pilot/2026-11-23-sems-stop-feedback/00..07
# workbench.css 闸门1 追加（2026-11-23 走查）：toast-host absolute 锚文档底 → toast 首屏外；02-spec 行60/68 承诺 toast 为任务详情反馈通道，修为 sticky（与 statusbar 同机制）。决策留痕见 05 §5 与工作日记
rollback: "worktree 干净基线 bb59154；git checkout -- <file> 逐文件回退，无 DB 迁移"
writeback: { feature_doc: none, reason: "插件需求真源在 Plugins/Sems/docs/01-requirements.md，本轮增补 F09" }
---

# Agent Task

> 阶段：Stage 4 — Task｜规范：`docs/04-standards/ai-native-engineering-workflow.md`

## Task ID

> Task ID：PILOT-087

## 头部元数据

见文件顶部 YAML 头部块（门禁机械校验源）。

## Allowed

- 按上表文件清单修改 sems 插件后端/前端与其测试、需求/测试计划文档、PC-024 与 case 归档。
- 后端 `Stop/StopExternal` 返回类型由 `bool` 升级为 `StopResult`（插件内部契约，调用方仅本插件 controllers/tools/测试）。
- 新增 vitest spec 文件（复用 regression-fixes harness 模式）。

## Forbidden

- ❌ 修改任何 `sems_*` 工具名、注册表、权限声明；改变工具既有成功/失败语义（F25）。
- ❌ 修改 e2e global-setup / 其他插件 e2e / 宿主内核 / 鉴权 / DB schema / 依赖版本。
- ❌ 把「已不在运行」实现为 error 级反馈或静默吞掉。
- ❌ 发布动作（git commit/push、release、更新运行实例）；触碰用户 :51888 宿主进程。
- ❌ 手写一次性 temp/*.cjs 验证脚本。

## 验证命令（§5.6 快档 + 插件六步）

```bash
# 后端（sems 过滤集）
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~Plugins.Sems"
# 插件前端
cd Plugins/Sems/web && pnpm install && pnpm run check && pnpm run test && pnpm run build
# 设计守卫 + 提交卫生（工作区级）
pnpm run design:check
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1
# 插件冒烟 e2e（定向）
cd ForgeSelf.Web && pnpm run e2e -- e2e/plugins/sems/sems.spec.ts
# 问题类守卫
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
```

判定：全绿（基线外无新增红）→ 05/06/07 补齐 → 汇报。
