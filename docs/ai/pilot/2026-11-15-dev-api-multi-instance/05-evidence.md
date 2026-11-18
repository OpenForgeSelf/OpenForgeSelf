# Evidence

## Task

PILOT-2026-11-15-dev-api-multi-instance

## Changed Files

- `package.json`: 根 `dev` 调用 `scripts/dev-stack.ps1`，保留 `dev:web`。
- `ForgeSelf.Api.Tests/RepositoryScriptTests.cs`: 增加根整栈入口 G2 守卫。
- `scripts/dev-api.ps1`: 删除早期 API-only 方案新增的脚本。
- `AGENTS.md`、`docs/README.md`、PC-007、case、pilot 工件及工作日记：同步最终入口与证据。

## Build

- `dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --no-restore --nologo`: PASS (Verified), 28 projects、0 errors、458 warnings。
- `pnpm dev`: 通过现有 dev-stack 构建并启动宿主整栈；后端健康探测与前端 HTTP 响应均成功（见 Integration）。

## Unit Test

Command: `dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --no-build --filter FullyQualifiedName~RootDevCommand_MustUseFullStackLauncher --logger 'console;verbosity=minimal'`

Result: BLOCKED (Verified environment failure; assertion not executed). Testhost aborted at `ProcessManager.OpenProcess` with `Win32Exception (5)` while setting its parent-process exit callback. This is neither a passing test nor an assertion failure. The full backend suite is likewise still unverified.

## Integration Test

Command: `pnpm dev`

Result: PASS (Verified). The command invoked `scripts/dev-stack.ps1`; `.temp/dev-stack.json` recorded backend `http://localhost:7301` (PID 49076) and frontend `http://localhost:7399` (PID 28980). `GET /api/health` returned HTTP 200 and the frontend root returned HTTP 200. The recorded processes were `dotnet.exe` and `node.exe`, respectively.

## Acceptance Criteria Evidence

- AC1: PASS (Verified). Root `dev` invokes `scripts/dev-stack.ps1`; `dev:web` remains the frontend-only command; the obsolete `dev-api.ps1` is absent.
- AC2: BLOCKED (Partially Verified). The G2 test asserts the root route and checks that the launcher starts dotnet and node; the test project builds, but testhost aborts before executing the assertion.
- AC3: PASS (Verified). The real `pnpm dev` run recorded both service URLs/PIDs; backend health and frontend root each returned HTTP 200.
- AC4: PASS (Verified by diff). `scripts/dev-stack.ps1` was not modified; its existing stop and single-instance behavior remains unchanged.
- AC5: PARTIAL (Verified). PC-007 case/registry guard passes; G2 test execution remains blocked as described under AC2.
- AC6: BLOCKED. The required full backend test run has no verified result because the testhost process-access failure prevents reliable execution.

### Earlier API-only Experiment (Historical, Not Acceptance Evidence)

Before the user clarified the desired full-stack entry, two isolated API launcher instances were tested on ports 52332 and 61346. Both health checks and shutdown isolation succeeded. The launcher was subsequently removed; this experiment does not validate the final `pnpm dev` behavior.

## Static Analysis

- G2 test project build: PASS (Verified); test execution is blocked as noted above.
- `check-problem-classes.ps1 -PcId PC-007`: PASS，1 个 PC 的 signature/allowlist/guards 全部通过。
- `verify-pilot-artifacts.ps1 -TaskId 2026-11-15-dev-api-multi-instance`: PASS，00-07 工件及关键章节齐全。
- `check-commit-hygiene.ps1 -Staged`: PASS，0 staged files、0 lines、0 hits；拒绝列表缺失，按守卫提示跳过 L2。
- `git diff --check`: PASS，无输出。

## Screenshots

N/A. No UI was changed.

## Known Limitations

- G2 xUnit regression guard and full backend test suite remain unverified because testhost cannot open its parent process in this environment.
- `dev-stack.ps1` uses a fixed state file and single-instance lifecycle; a later `pnpm dev` may stop the instance recorded there. Do not treat it as a parallel-stack launcher.

## Unresolved Issues

- Re-run targeted and full backend tests in an environment where testhost can monitor its parent process. Tracked in `TODO.md` under the root `pnpm dev` full-stack task.