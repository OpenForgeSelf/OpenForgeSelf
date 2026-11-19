# Evidence

> Task ID：`2026-11-19-oar-runtime-worker-p0`

## Commands Run

| # | 命令 | 结果 | 等级 |
| --- | --- | --- | --- |
| 1 | `node -v`（PATH 含 Node 24.11.0） | v24.11.0 | Verified |
| 2 | `cd Plugins/AgentHub/RuntimeWorker && pnpm install` | `@botiverse/oar@0.109.0` | Verified |
| 3 | `pnpm run smoke` / `node --import tsx src/smoke.ts` | `ok: true`，passed 12/12 | Verified |
| 4 | `pnpm run build` | `tsc` 成功 | Verified |
| 5 | stdio：`worker.hello` + `runtime.list` | hello 含 protocolVersion=1；list 14 项含 scripted/claude/codex/opencode | Verified |

## AC 对照

| AC | 结果 | 证据 |
| --- | --- | --- |
| AC1 smoke 12/12 | ✅ | `/opt/cursor/artifacts/oar-p0-smoke.json` |
| AC2 build | ✅ | `/opt/cursor/artifacts/oar-p0-build.log` |
| AC3 stdio list | ✅ | hello + list 实测 |
| AC4 无 .cs | ✅ | `git diff --name-only` 无 `*.cs`（本批） |

## 探针 12 步摘要（Verified）

1 worker.hello → 2 runtime.list → 3 installation(scripted) → 4 auth(not_supported) → 5 models(not_supported) → 6 session.open → 7 prompt+events → 8 records → 9 resume(generation≥2) → 10 control cancel → 11 dispose → 12 vendor installation read-only（claude/codex/opencode = not_found）

## 风险 / Unknown

- 本环境无 Claude/Codex/OpenCode 二进制；vendor auth/models 未实测（仅 installation not_found）
- CI 默认 Node 可能仍为 22；Worker 构建需单独 Node≥24 工具链（P4 打包项）
