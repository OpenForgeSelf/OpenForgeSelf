# Evidence

> 阶段：Stage 7｜只记录实际发生的事。  
> Task ID：`2026-11-19-oar-agenthub-runtime`

## Changed Files（本批）

| 路径 | 动作 |
| --- | --- |
| `docs/06-research/007-oar-agenthub-runtime.md` | 新增 |
| `docs/01-architecture/agenthub-oar-runtime.md` | 新增 |
| `docs/07-decisions/007-oar-agenthub-runtime.md` | 新增 |
| `docs/07-decisions/005-maf-local-agent-reuse.md` | 增补 OAR 正交澄清 |
| `docs/07-decisions/not-taken-decisions.md` | 新增 062/063 |
| `docs/01-architecture/cordis-kernel.md` | `IAgentRuntime` 定案说明 |
| `docs/01-architecture/host-capability-seams.md` | §9 接缝登记 |
| `docs/02-features/032-agent-hub.md` | §6 演进 |
| `docs/README.md` | §一速查 + ADR 列表 + 032 行 |
| `docs/ai/pilot/2026-11-19-oar-agenthub-runtime/00`–`04`、`gate1-proposal`、`05`–`07` | 新增 |

## Validation

| 项 | Command / 动作 | Result | 等级 |
| --- | --- | --- | --- |
| 架构真源存在 | `test -f docs/01-architecture/agenthub-oar-runtime.md` | PASS | Verified |
| ADR 存在 | `test -f docs/07-decisions/007-oar-agenthub-runtime.md` | PASS | Verified |
| 调研存在 | `test -f docs/06-research/007-oar-agenthub-runtime.md` | PASS | Verified |
| gate1 方案存在 | `test -f …/gate1-proposal.md` | PASS | Verified |
| README 速查 | `rg agenthub-oar-runtime docs/README.md` | 命中 | Verified |
| 无业务代码 | `git diff --name-only` 仅 docs | PASS | Verified |
| Abstractions 仍无 IAgentRuntime 源码 | `rg IAgentRuntime ForgeSelf.Abstractions` | 0（符合本批 Forbidden） | Verified |
| 本机 Node | `node -v` | v22.23.4（&lt;24，P0 前置风险已写入 Spec） | Verified |

## Screenshots

N/A（无 UI）

## Known Limitations

- ADR-007 状态为 Proposed，待用户闸门1 确认后改为 Accepted。  
- 实现代码未开始；P0 需 Node≥24。

## Unresolved Issues

- 无（文档批范围内）。实现阶段依赖闸门1 拍板。
