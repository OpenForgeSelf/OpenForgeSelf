# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。

## Task ID

PILOT-050

## Objective

让 e2e 共享基建支持多 worktree 并行 + 动态端口 + 稳定目录，消除 Windows 防火墙弹窗，且单 worktree 现有 e2e 行为与测试基线（后端 1516/13 红、e2e 102 passed/82 failed）保持不变。

## Scope

### Allowed

- 新增 `ForgeSelf.Api/StartupPortResolver.cs` 与 `ForgeSelf.Api.Tests/StartupPortResolverTests.cs`；在 `ForgeSelf.Api/AppBuilder.cs` 接入（ConfigUnifier 之后）。
- 新增/改造 `ForgeSelf.Web/e2e/` 下的 `helpers/free-port.ts`、`helpers/e2e-env.ts`、`helpers/real-auth.ts`、`global-setup.ts`、`vite.config.ts`、`playwright.config.ts`、`playwright.e2e-published.config.ts`、`port-config.spec.ts` 与清理 7102/7002 硬编码的 `.spec.ts`。
- 回写 AGENTS.md（§2.3/§5.3/§5.6）与 `docs/04-standards/agent-workflow.md`（B2/Part C）。

### Forbidden

- 不改后端 C# 业务逻辑 / 数据模型 / 鉴权权限 / DB 结构（仅新增启动端口覆盖能力）。
- 不引入任何新依赖。
- 不改 e2e 业务断言逻辑（仅改基建/端口注入/硬编码清理）。
- 不破坏默认 7102/7002 回落与单 worktree 行为。
- 不自行伪造/美化验证结果；平台限制（e2e 依赖 Windows 宿主）如实上报。

## Acceptance Criteria

- [ ] AC-1 `StartupPortResolver` 单测全绿（`dotnet test --filter StartupPortResolver`）。
- [ ] AC-2 设 `FORGESELF_PORT` 启动的宿主监听该端口（e2e 实跑验证）。
- [ ] AC-3 e2e 运行目录稳定派生（同 worktree 同目录），不再用时间戳。
- [ ] AC-4 e2e 前后端端口动态贯通、多 worktree 并行不冲突。
- [ ] AC-5 e2e 侧 7102/7002 硬编码清理完成，默认值回落保留。
- [ ] AC-6 `port-config.spec.ts` 端口无关；`playwright.e2e-published.config.ts` 端口来源与 `publishDir` 修复。
- [ ] AC-7 门禁：前端 `check`/`test` 绿；后端中档全量 `dotnet test` 基线持平；深档全量 e2e 4 worker 基线持平（不因本任务新增红/失败）。
- [ ] AC-8 文档回写完成。

## Expected Files

- `ForgeSelf.Api/StartupPortResolver.cs`（新增）
- `ForgeSelf.Api/AppBuilder.cs`（接入）
- `ForgeSelf.Api.Tests/StartupPortResolverTests.cs`（新增）
- `ForgeSelf.Web/e2e/helpers/free-port.ts`（新增）
- `ForgeSelf.Web/e2e/helpers/e2e-env.ts`（新增）
- `ForgeSelf.Web/e2e/helpers/real-auth.ts`（改）
- `ForgeSelf.Web/e2e/global-setup.ts`（改）
- `ForgeSelf.Web/vite.config.ts`（改）
- `ForgeSelf.Web/playwright.config.ts`（改）
- `ForgeSelf.Web/e2e/port-config.spec.ts`（改）
- `ForgeSelf.Web/playwright.e2e-published.config.ts`（改）
- 相关 `.spec.ts` 硬编码清理
- `AGENTS.md`、`docs/04-standards/agent-workflow.md`（回写）

## Verification Commands

```bash
# 后端单测（AC-1）
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter StartupPortResolver

# 后端全量（中档门禁，AC-7）
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj

# 前端类型/lint + vitest（AC-7）
cd ForgeSelf.Web && pnpm run check && pnpm run test

# 深档 e2e 全量（AC-7，依赖 Windows 宿主）
cd ForgeSelf.Web && npx playwright test --config=playwright.config.ts --workers=4
```
