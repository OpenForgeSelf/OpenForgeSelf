# Plan

> 阶段：Stage 3｜Task ID：2026-11-15-dev-api-multi-instance

## Files To Change

- file: `package.json` — 根 `dev` 路由到隔离启动脚本，删除 `dev:api`，将原前端命令保留为 `dev:web`；保留其他本地改动。
- file: `scripts/dev-api.ps1` — 删除不再需要的 API-only 启动器，根默认入口复用整栈脚本。
- file: `ForgeSelf.Api.Tests/RepositoryScriptTests.cs` — 调整 G2 入口守卫，锁定根命令路由到 `dev-stack.ps1` 且脚本负责启动 API 与前端。
- file: `AGENTS.md` — 将根 `pnpm dev` 全栈、`pnpm run dev:web` 单独前端的适用范围说清。
- file: `docs/README.md` — 同步入口索引，说明根 `pnpm dev` 全栈与 `dev:web` 前端-only。
- file: `docs/11-troubleshooting/problem-classes/PC-007-manual-dev-launch-silent-trap.md` — 增补本次入口缺口、G2 守卫和 lens/canonical fix。
- file: `docs/11-troubleshooting/cases/2026-11-16-dev-api-multi-instance.md` — 记录入口需求调整、扫查、逃逸分析和修复证据。
- file: `docs/11-troubleshooting/problem-classes/README.md` — 更新 PC-007 守卫与 case 索引。
- file: `docs/11-troubleshooting/cases/README.md` — 登记本次 case。
- file: `docs/11-troubleshooting/index.md` — 更新问题类速查入口。
- file: `docs/ai/pilot/2026-11-15-dev-api-multi-instance/00-repository-understanding.md` — 已完成。
- file: `docs/ai/pilot/2026-11-15-dev-api-multi-instance/01-intent.md` — 已完成。
- file: `docs/ai/pilot/2026-11-15-dev-api-multi-instance/02-spec.md` — 已完成。
- file: `docs/ai/pilot/2026-11-15-dev-api-multi-instance/03-plan.md` — 本文件。
- file: `docs/ai/pilot/2026-11-15-dev-api-multi-instance/04-task.md` — 本工作单。
- file: `docs/ai/pilot/2026-11-15-dev-api-multi-instance/05-evidence.md` — 验证后补齐。
- file: `docs/ai/pilot/2026-11-15-dev-api-multi-instance/06-review.md` — 验证后补齐。

## Implementation Steps

1. 将 `package.json` 根 `dev` 路由到已有 `scripts/dev-stack.ps1`，保留 `dev:web`，移除 API-only 脚本。
2. 调整 `RepositoryScriptTests`，验证根入口使用整栈脚本且脚本含后端与前端启动链。
3. 更新 AGENTS、文档索引、PC-007 与 case，反映单一全栈默认入口及脚本现有单实例语义。
4. 执行静态/构建/测试验证，并在确认不会中止既有 dev-stack 实例后实跑 `pnpm dev` 检查前后端探活。

## Test Plan

1. G2 回归守卫：断言根 package `dev` 精确调用 `scripts/dev-stack.ps1`，并检查既有脚本包含宿主后端和前端启动步骤；定向测试在 testhost 环境恢复后运行。
2. 行为冒烟：运行 `pnpm dev`，检查 `.temp/dev-stack.json` 的 backend/frontend URL 与 PID；请求 backend `/api/health` 并读取前端首页；仅在启动前确认没有用户已有的 `.temp/dev-stack.json` 实例，结束时只停止本次实例。
3. 同类扫查：核对 package scripts 与 `scripts/dev-stack.ps1`、`scripts/dev-plugin-web.ps1` 的职责边界，更新 case §4。

## Verification

### Build

```powershell
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug --nologo
```

### Unit Test

```powershell
$env:TEMP = $env:TMP = '<repo>/.temp/tmp'
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~RepositoryScriptTests|FullyQualifiedName~ProblemClassRegistry"
```

### Integration Test

运行一次 `pnpm dev`，读取状态文件并验证后端 `/api/health` 与前端 URL；停止前确认状态文件属于本次启动，使用既有 `pwsh scripts/dev-stack.ps1 -Stop` 收尾。

### E2E

N/A：本批只调整本地开发命令入口，不改 UI/API 业务契约或 e2e 基建；整栈行为由真实启动及双端探活验证。

### Other Checks

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
dotnet test ForgeSelf.Api.Tests --nologo
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 -Staged
```

全量测试前按 AGENTS §5.0 将 `TEMP`/`TMP` 指向仓库 `.temp/tmp`，并设置本机 `NO_PROXY`；按中档对比最近基线红。

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 用户调整入口 | 原先将根 `dev` 接 API-only 多实例启动器 | 用户现明确希望 `pnpm dev` 启动前后端，根命令改为直接调用既有 `dev-stack.ps1`；删除临时 API-only 启动器并同步守卫和文档。 |
| 入口职责 | 之前新增 API-only 脚本解决并发 API 需求 | 当前最终要求为全栈单入口，复用 `dev-stack.ps1` 的单实例状态生命周期；先前双 API 并行实测不等于本次全栈验收。 |
| 验证环境 | 预期可由定向测试建立红绿证据 | testhost 在用例前 `OpenProcess` 被拒（Win32Exception 5）；语法与 BOM 检查已通过，单测仍待验证。 |
