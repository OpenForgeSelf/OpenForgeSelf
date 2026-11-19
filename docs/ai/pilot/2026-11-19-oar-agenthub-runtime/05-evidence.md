# Evidence

> 阶段：Stage 7｜只记录实际发生的事。  
> Task ID：`2026-11-19-oar-agenthub-runtime`

## Changed Files（本批）

| 路径 | 动作 |
| --- | --- |
| `docs/06-research/007-oar-agenthub-runtime.md` | 新增 |
| `docs/01-architecture/agenthub-oar-runtime.md` | 新增（后续回写进度） |
| `docs/07-decisions/007-oar-agenthub-runtime.md` | 新增 → **Accepted** |
| `docs/07-decisions/005-maf-local-agent-reuse.md` | 增补 OAR 正交澄清 |
| `docs/07-decisions/not-taken-decisions.md` | 新增 062/063 |
| `docs/01-architecture/cordis-kernel.md` | `IAgentRuntime` 定案说明 |
| `docs/01-architecture/host-capability-seams.md` | §9 接缝登记 |
| `docs/02-features/032-agent-hub.md` | §6 演进（含 P0 进度） |
| `docs/README.md` | §一速查 + ADR 列表 + 032 行 |
| `docs/ai/pilot/2026-11-19-oar-agenthub-runtime/00`–`04`、`gate1-proposal`、`05`–`07`、`concurrency-plan.md` | 新增/回写 |

## Validation

| 项 | Command / 动作 | Result | 等级 |
| --- | --- | --- | --- |
| 架构真源存在 | `test -f docs/01-architecture/agenthub-oar-runtime.md` | PASS | Verified |
| ADR 存在 | `test -f docs/07-decisions/007-oar-agenthub-runtime.md` | PASS | Verified |
| 调研存在 | `test -f docs/06-research/007-oar-agenthub-runtime.md` | PASS | Verified |
| gate1 方案存在 | `test -f …/gate1-proposal.md` | PASS | Verified |
| README 速查 | `rg agenthub-oar-runtime docs/README.md` | 命中 | Verified |
| 无业务代码（文档批） | `git diff --name-only` 仅 docs（文档批当时） | PASS | Verified |
| 闸门1 | 用户 seq6 + 续作「方案落实了…并发实现」 | **批准** | Verified |
| P0 跟进 | commit `fc87d58`；smoke 12/12；pilot `oar-runtime-worker-p0` | PASS | Verified |

## Acceptance Criteria Coverage

| AC | 证据 | 等级 |
| --- | --- | --- |
| AC1 | ADR-007 / `agenthub-oar-runtime.md` / 调研 007 / pilot 00–04 / `gate1-proposal.md` 均已落盘且互相链接 | Verified |
| AC2 | `docs/README.md` §一 新增「外部 Coding Agent / OAR / IAgentRuntime」速查行 | Verified |
| AC3 | 调研 007 §2 + ADR-007 + ADR-005 澄清：与 `IAgent` / `IAgentDelegation` / ADR-005 边界写清 | Verified |
| AC4 | `not-taken-decisions.md` 062/063 | Verified |
| AC5 | 文档批无 `.cs` 变更 | Verified |
| AC-R1 | P0 pilot Evidence：smoke 12/12 | Verified |

## Screenshots

N/A（无 UI）

## Known Limitations

- 实现按 P1–P4 分批；本 design pilot 不宣称业务代码完成。  
- Node≥24 仍为 Worker 前置（Cloud 默认可能为 22；P0 用 `/tmp/node24` 验证）。

## Unresolved Issues

- 无（文档批范围内）。实现见各阶段 pilot。
