# 05-evidence — 插件包统一交付目录（输入25，2026-11-15）

## 实测记录（全部 Verified）

| 项 | 命令/动作 | 结果 |
| --- | --- | --- |
| 脚本实现 | `scripts/package-plugin.ps1` 新增 `Get-PluginPackageDeliveryDir` + 投递步 | edit 工具剥 BOM 实测复发（BOM before False）→ pwsh `UTF8Encoding($true)` 还原（after True）；`.temp` 无日志，字节级核验 |
| 端到端投递（worktree 场景） | 重跑 `package-plugin.ps1 -Plugin SmartTerminal -Configuration Release` | 日志 `.temp/pkg-st-202b.log`：`Package OK: …\artifacts\plugin-packages\智能终端-2.0.2.2611151339.zip` + **`Delivered: <主仓根上级>\OpenForgeSelfPluginUpdates\智能终端-2.0.2.2611151339.zip`**（SHA256 D2138DA2…E91）——worktree 打包落主仓侧统一目录 ✓ |
| 包内容核验 | ZipFile 解包枚举 | 7 条目（plugin.json 版本戳 2.0.2.2611151339 / SmartTerminal.dll+deps+pdb / web/dist×2 / Data\permission-rules.default.json），无宿主共享 DLL（同 `.temp/pkg-st-202.log` 首跑核验 + 脚本内置 hygiene 校验） |
| 回写 | 技能 `plugin-publish-verify/SKILL.md` 新节 + `packaging-upgrade-backup.md` §1.8 侧一行 | 均已落盘（无盘符字面量，靠目录名定位） |
| 中档全量（scripts/** 触发） | `dotnet test ForgeSelf.Api.Tests` 15m04s | **失败 25 / 通过 3767 / 总计 3792**（`.temp/be-full-25.log`）；逐族对表：ProjectWorkspaceDefaultRootTests 15（TODO L527 既有）+ WorkflowPlanningIntegrationTests 6 + ScriptRunnerDiIntegrationTests 1（404 家族，TODO 登记）+ DesignAgentToolContractTests 1（负载 flake 登记）+ MafApprovalLoopTests 1（并行构建中间态家族登记）+ JsonNonAsciiEscapingGuardTests 1（**PC-027 守卫红变体**：违例从 AnthropicProtocolBridge:151 变为 11-14 批 7fec7e1 的 TerminalInstanceForwarder.cs:23 + SmartTerminalToolFunctions.cs:11——非本批文件，已记 TODO）→ **归本批 = 0** |
| 提交卫生 | `check-commit-hygiene.ps1 -Staged` | PASS（files=27 lines=915 hits=0，`.temp/hygiene-25.log`） |

## 决策记录

- 决策：投递目录 = **主仓根上级**（worktree 反解主仓根后取其上级）→ 背景：用户要求「避免各插件/各工作树输出目录不一致」+「工作树复制到工作区上级」；理由：若取 worktree 自身上级，投递会散落 `.agents/worktrees/` 各处，违背立规初衷；准则：统一取件点 > 便利性。
- 决策：投递失败只 Warning 不失败 → 背景：打包产物已存在本地；理由：交付目录为仓外目录，可能被清理/占用，不应阻断打包主链；准则：可回滚、少耦合（回滚点=本地 `artifacts/plugin-packages/`）。
- 不做事决策：不改 `release-local.ps1` 宿主更新源语义（`../updates/`）——插件交付目录与宿主更新源是两条线，混同会破坏设置页既有更新源路径（NOT-to-do 已记 07 §11）。
