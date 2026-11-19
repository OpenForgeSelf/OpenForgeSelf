---
task_id: 2026-11-19-oar-agenthub-runtime
feature: 032
risk: { level: L2, triggers: ["docs/01-architecture/", "docs/07-decisions/", "ForgeSelf.Abstractions(planned)"], raised_by_agent: true }
gate1: { mode: user, at: null, ref: "gate1-proposal.md" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - docs/06-research/007-oar-agenthub-runtime.md
  - docs/01-architecture/agenthub-oar-runtime.md
  - docs/07-decisions/007-oar-agenthub-runtime.md
  - docs/07-decisions/not-taken-decisions.md
  - docs/01-architecture/cordis-kernel.md
  - docs/01-architecture/host-capability-seams.md
  - docs/02-features/032-agent-hub.md
  - docs/README.md
  - docs/ai/pilot/2026-11-19-oar-agenthub-runtime/
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "OAR 演进节" }
---

# Agent Task

> 阶段：Stage 4｜本工作单元 = **文档与最终决策落盘**；零业务代码。

## Task ID

`2026-11-19-oar-agenthub-runtime`

## Objective

把 OAR × AgentHub 改造方案梳理为与仓库事实一致的最终架构决策，更新真源文档，产出 AI-Native 闸门1 工件包，供用户批准后开启 P0 实现。

## Scope

### Allowed

- 仅 `docs/**` 下本 Plan 列出的文件与本 pilot 目录
- 工作日记 / 本地 `TODO.md`（gitignore，不入库亦可）

### Forbidden

- 修改任何 `.cs` / 插件前端业务源码 / 新增 RuntimeWorker 实现
- 安装或升级本机第三方 Agent
- 停/启/杀用户宿主
- 扩大范围到 Skills/MCP/Subagents 实现
- 修改 ADR-005 的「不另起本机 Runtime」结论（仅允许交叉引用澄清）

## Acceptance Criteria

- [ ] ADR-007、架构真源、调研 007、gate1-proposal、pilot 00–04 齐备
- [ ] README / cordis-kernel / host-capability-seams / 032 / not-taken 已回写
- [ ] 文档明确：OAR 内部化、Node Worker、Transport 注册表、SessionKey、IAgentRuntime、与 IAgentDelegation/IAgent/ADR-005 边界
- [ ] `git diff` 无业务代码
- [ ] 对外回复：路径 + 一句话摘要 + 待拍板项

## Expected Files

见 front-matter `expected_files`。

## Verification Commands

```bash
test -f docs/01-architecture/agenthub-oar-runtime.md
test -f docs/07-decisions/007-oar-agenthub-runtime.md
test -f docs/ai/pilot/2026-11-19-oar-agenthub-runtime/gate1-proposal.md
git diff --name-only main...HEAD | rg -v '^docs/' && echo 'UNEXPECTED_NON_DOCS' || echo 'DOCS_ONLY_OK'
```
