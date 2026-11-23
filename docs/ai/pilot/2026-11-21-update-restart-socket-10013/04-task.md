---
task: 2026-11-21-update-restart-socket-10013
feature: 008
risk: { level: L2, triggers: ["ForgeSelf.Api/AppBuilder.cs", "ForgeSelf.Api/Services/StagedUpdateService.cs", "scripts/release/package-release.ps1"], raised_by_agent: false }
gate1: { mode: user, at: 2026-11-21T22:00, ref: "输入15" }
gate2: { mode: user, at: 2026-11-22T21:20, ref: "Review=APPROVED" }
gate3: { mode: user, at: 2026-11-22T21:32, ref: "输入17「先提交且仅提交本次修改」" }
expected_files:
  - ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj
  - ForgeSelf.UpdateAgent/Program.cs
  - ForgeSelf.UpdateAgent/UpdateAgentCore.cs
  - ForgeSelf.UpdateAgent/UpdateAgentRunner.cs
  - ForgeSelf.Api/Services/Net/OutboundSocketResilienceHandler.cs
  - ForgeSelf.Api/AppBuilder.cs
  - ForgeSelf.Api/Services/StagedUpdateService.cs
  - ForgeSelf.Api/ForgeSelf.Api.csproj
  - ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
  - ForgeSelf.Api.Tests/RepositoryScriptTests.cs
  - ForgeSelf.Api.Tests/Unit/UpdateAgentRunnerTests.cs
  - ForgeSelf.Api.Tests/Unit/OutboundSocketResilienceHandlerTests.cs
  - ForgeSelf.slnx
  - scripts/release/publish-updateagent.ps1
  - scripts/release/package-release.ps1
  - scripts/release/release-local.ps1
  - scripts/update-agent.ps1
  - scripts/publish-plugin.ps1
  - scripts/README.md
  - docs/04-standards/packaging-upgrade-backup.md
  - docs/11-troubleshooting/index.md
  - docs/11-troubleshooting/cases/README.md
  - docs/11-troubleshooting/cases/2026-11-21-update-agent-version-sort-overflow.md
  - docs/11-troubleshooting/problem-classes/README.md
  - docs/11-troubleshooting/problem-classes/PC-041-update-agent-version-sort-overflow.md
rollback: "git revert <commit>；exe 轨缺位时宿主自动回退 ps1（行为级回退）"
writeback: { feature_doc: 008-tray-service-autoupdate.md, reason: "宿主更新链路功能文档随批核对，本批以 packaging 真源为结构事实归属" }
---

# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。

## Allowed

- 上述 expected_files 清单内文件的创建/修改；
- 测试过滤集与全量对基线执行；
- 日志/台账/真源回写（清单内）。

## Forbidden

- 禁止停/启/杀用户运行实例（`<安装根>` :51888，只读走查允许）；
- 禁止改 ForgeSelf.Api.csproj 版本行；
- 禁止删除/停发 `update-agent.ps1`（老宿主只认 ps1）；
- 禁止提交含绝对路径/个人标识的内容（hygiene）。

## 验证命令

```bash
dotnet build ForgeSelf.slnx
dotnet test ForgeSelf.Api.Tests --filter "UpdateAgent|OutboundSocket|RepositoryScript"
# 中档全量（碰宿主源码/仓库脚本，§5.6 必跑）：
dotnet test ForgeSelf.Api.Tests
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 -Staged
```
