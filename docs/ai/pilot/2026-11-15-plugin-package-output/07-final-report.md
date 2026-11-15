# AI-Native Pilot Result（最终汇报）— 插件包统一交付目录（2026-11-15）

> 任务来源：用户直接指令（输入25，即闸门1 依据）；轻量裁剪 = mini-task.md + 05 + 06 + 本件。

## 1. Repository Understanding

我确认了：`package-plugin.ps1` 输出默认 `artifacts/plugin-packages/`（仓内、各 worktree 各一份）；宿主更新源走 `release-local.ps1 -UpdateDir`（默认 `../updates/`）；worktree 的 `.git` 是文件（gitdir 指向主仓），是反解主仓根的可靠依据。

## 2. Selected Task

用户指令：插件打包输出统一到「项目根上级 `OpenForgeSelfPluginUpdates/`」，worktree 打包同样复制到主仓侧该目录，避免各插件/各工作树输出目录不一致；本批智能终端包随此落位交付。

## 3. Changed Files

`scripts/package-plugin.ps1`（+35 行：`Get-PluginPackageDeliveryDir` worktree 感知 + 投递步，失败仅 Warning）、`.agents/skills/plugin-publish-verify/SKILL.md`（新节）、`docs/04-standards/packaging-upgrade-backup.md`（§1.8 侧一行）、TODO/日记/pilot 工件。

## 4. Validation

Build: ✅ 重打包全链（dotnet publish + zip）成功。
Unit Test: ✅ 中档全量 `dotnet test ForgeSelf.Api.Tests` 15m04s：3767 通过 / 25 失败，逐族对表**全部命中已登记基线**（ProjectWorkspaceDefaultRootTests 15 / WorkflowPlanning 6 + ScriptRunnerDi 1 = 404 家族 / DesignAgentToolContract 1 = 负载 flake / MafApprovalLoop 1 = 中间态家族 / JsonNonAsciiEscapingGuard 1 = PC-027 守卫红变体，违例为 11-14 批 7fec7e1 引入、非本批文件）→ 归本批 0。**档位：中档**（scripts/** 触发，含 RepositoryScriptTests 全扫，无本批新红）。
E2E: 无 UI 改动，不适用；打包链端到端实跑即验收（见 §5）。

## 5. Evidence

端到端：`.temp/pkg-st-202b.log` — `Delivered: <主仓根上级>\OpenForgeSelfPluginUpdates\智能终端-2.0.2.2611151339.zip`（SHA256 D2138DA2…E91，包内 7 条目无宿主 DLL）；BOM 守护实测（edit 剥 BOM → 还原 True）；hygiene -Staged PASS（27 文件 915 行 0 命中）；全量日志 `.temp/be-full-25.log`。工件：`docs/ai/pilot/2026-11-15-plugin-package-output/`。

## 6. Review

06-review.md Final Decision = **APPROVED**（八问含隐藏耦合审查：gitdir 非标形态 → helper 返回 null 不误投）。

## 7. Risk

L1：交付目录在仓外（与 `../updates/` 同级层次），换机按目录名定位（无盘符入库）；L2：投递失败仅告警，若统一目录磁盘满需用户看 Warning——本地包不受影响。

## 8. Problems Found

PC-027 守卫红名单变体（11-14 批 7fec7e1 新增 2 处违例）→ TODO P2 挂账，异根因不属本批。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（.git 文件形态从仓内 worktree 实证） |
| Intent → Spec | PASS（用户直接指令） |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS（端到端 + 中档全量对表） |
| Test → Evidence | PASS（25 红逐族归属留档） |
| Evidence → Review | PASS |

### 9.5 本批自复盘（必填）

- **经验提取**：① 全量红对表时先查历史 pilot 的红清单（本仓已有多份「同名单红」留档，逐族命名对表比 stash 对照快一个量级）；② ps1 用 edit 后必核 BOM（本次实测复发，已当场还原）。
- **待改进**：无新增（上批三条已执行：断言先取实际值、写盘即跑 hygiene、动手前查真源）。

## 10. 问题类归档

无新 PC（流程/工具批）；对表发现的 PC-027 逃逸变体已记 TODO（异根因）。

## 11. 重构提案

R0（理由：改动即脚本化固化，一次性完成）；R1 候选已披露于 06（打包脚本族的仓库根解析模块化，不在本批）。

## 12. 悬空教训清单（必须为空）

无。
