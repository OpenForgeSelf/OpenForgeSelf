---
task_id: 2026-11-05-release-chain-hardening
feature: 100-INFRA-1
risk: { level: L2, triggers: ["scripts/release/release-local.ps1", "Plugins/*/web/pnpm-lock.yaml"], raised_by_agent: false }
gate1: { mode: auto, at: 2026-11-05T12:30, ref: "lead 晨批 task-13 + 方案 b 批准" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - scripts/release/release-local.ps1
  - Plugins/AgentHub/web/pnpm-lock.yaml
  - Plugins/AIAgent/web/pnpm-lock.yaml
  - Plugins/CostScope/web/pnpm-lock.yaml
  - Plugins/DesignSystem/web/pnpm-lock.yaml
  - Plugins/FileTools/web/pnpm-lock.yaml
  - Plugins/Home/web/pnpm-lock.yaml
  - Plugins/ImGateway/web/pnpm-lock.yaml
  - Plugins/McpCenter/web/pnpm-lock.yaml
  - Plugins/QuickLinks/web/pnpm-lock.yaml
  - Plugins/Sems/web/pnpm-lock.yaml
  - Plugins/TodoTracker/web/pnpm-lock.yaml
  - Plugins/ToolBridge/web/pnpm-lock.yaml
rollback: "git revert 单提交；水位/护栏/锚定/复算四件互相独立可部分回滚"
writeback: { feature_doc: none, reason: "发行链操作规范真源在 packaging-upgrade-backup.md，本批为脚本固化无结构事实变更" }
---
# 04 Task（task-13）

- **Allowed**：scripts/release/release-local.ps1；PluginsAgentHub,AIAgent,CostScope,DesignSystem,FileTools,Home,ImGateway,McpCenter,QuickLinks,Sems,ToolBridge,TodoTracker}/web/pnpm-lock.yaml
- **Forbidden**：updates/ 真源目录（lead 晨间约束）；wt-rel6；package.json（方案 b=lockfile-only）；package-release.ps1/publish-host.ps1（本批不必要不动）
- **验证命令**：
  - 水位：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/release-local.ps1 -MinFreeGB 9999`（期望立即 throw，exit 1）
  - 沙箱轮：`... -UpdateDir .temp\tmp\update-verify-t13`（期望 ALL DONE + UpdateDir SHA256 verification PASS）
  - 中档：cd ForgeSelf.Api.Tests && dotnet test（RepositoryScriptTests 扫 scripts/**）
- **risk**: medium —— 发行链主脚本变更；以沙箱轮+中档兜底；回归锚=2.4.22.2611051134 轮
- **expected_files**: scripts/release/release-local.ps1; 12 个 Plugins/*/web/pnpm-lock.yaml
- **feature**: 100-INFRA-1（发行可靠性）
- **rollback**: revert 单提交；水位/护栏/锚定/复算四件互相独立，可部分回滚
