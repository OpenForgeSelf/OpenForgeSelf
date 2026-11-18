---
task_id: 2026-11-15-dev-api-multi-instance
feature: none（本地开发启动入口缺陷，不对应用户功能文档）
risk: { level: L1, triggers: ["package.json", "scripts/dev-api.ps1", "ForgeSelf.Api.Tests/RepositoryScriptTests.cs"], raised_by_agent: false }
gate1: { mode: approved, at: "2026-11-16", ref: "用户明确要求根 pnpm dev 启动前后端并复用 dev-stack" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - package.json
  - scripts/dev-api.ps1（删除）
  - ForgeSelf.Api.Tests/RepositoryScriptTests.cs
  - AGENTS.md
  - docs/11-troubleshooting/problem-classes/PC-007-manual-dev-launch-silent-trap.md
  - docs/11-troubleshooting/cases/2026-11-16-dev-api-multi-instance.md
  - docs/11-troubleshooting/problem-classes/README.md
  - docs/11-troubleshooting/cases/README.md
  - docs/11-troubleshooting/index.md
rollback: "只回退本任务新增的脚本/守卫/文档与 dev:api 接线；保留 package.json 其他已有改动和 .temp 实例数据。"
writeback: { feature_doc: not_applicable, reason: "本地启动脚本与测试基建，不是用户功能" }
---

# Agent Task

## Task ID

2026-11-15-dev-api-multi-instance

## Objective

让根 `pnpm dev` 启动宿主后端和前端开发服务器，复用现有 `scripts/dev-stack.ps1`；保留 `dev:web` 单独启动前端的能力，并由自动化守卫固定入口。

## Scope

### Allowed

- 严格按 `03-plan.md` 文件清单修改根 `dev` 接线、移除临时 API-only 启动脚本、扩展仓库脚本测试。
- 更新 PC-007、对应 case、三处索引及 `AGENTS.md` 本地启动入口说明。
- 将本次验证日志与结果记录到 05/06 工件和当天日记。

### Forbidden

- 不改变 `Program.cs` 默认单实例 Mutex 行为，不改 `dev-stack.ps1` 的固定状态/停止语义。
- 不访问、停止、重启或修改生产/用户运行实例；不改变 `:51888`。
- 不改数据库结构、API 业务逻辑、鉴权、安全策略、发布配置或前端。
- 不覆盖 `package.json` 中本任务以外的既有改动，不删除 `.temp` 数据。
- 不得提交或发布。

## Acceptance Criteria

- [x] AC-1 根 `package.json` 的 `dev` 调用 `scripts/dev-stack.ps1`；`dev:web` 保留；`dev-api.ps1` 不再保留。
- [ ] AC-2 G2 守卫验证根命令指向现有 `dev-stack.ps1`，且该脚本同时启动 API 和前端；测试项目构建通过，执行被 testhost 权限异常阻塞。
- [x] AC-3 真实运行 `pnpm dev` 后后端健康端点和前端页面均可访问；状态文件记录两者地址和 PID。
- [x] AC-4 `dev-stack.ps1 -Stop` 与其现有单实例行为未改动。
- [x] AC-5 PC-007 问题类归档检查通过；G2 测试被 testhost 权限异常阻塞并已如实记录。
- [ ] AC-6 解决方案构建、相关测试与 scripts 改动触发的中档全量后端测试全部可执行并有真实结果与基线对照；当前测试被 testhost 权限异常阻塞。

## Expected Files

参照 `03-plan.md` 的完整文件清单；不得新增计划外业务文件。

## Verification Commands

```powershell
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug --nologo
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~RepositoryScriptTests|FullyQualifiedName~ProblemClassRegistry"
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
dotnet test ForgeSelf.Api.Tests --nologo
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 -Staged
```

行为验收：确认没有既有 `.temp/dev-stack.json` 实例后运行 `pnpm dev`，检查后端 `/api/health` 与前端首页；只停止本次启动实例。

**闸门1 状态：已批准（按用户明确要求根 `pnpm dev` 启动前后端）。**