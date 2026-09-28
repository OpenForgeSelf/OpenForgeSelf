# AI-Native Pilot Result · 批次C 目录大小排行（FileTools 1.1.1）

> 状态：**COMPLETED_WITH_RISK**（代码闭环 + 全档验证完成，闸门2 用户验收通过前不发布；运行实例 :51888 仍是 1.1.0，待用户手动启用侧载包）
> 任务：在 FileTools 插件内实现「目录大小排行 + 快照持久化」，并把 FileTools 前后端整体迁移为自带界面、可独立热更新的插件。

## 1. Repository Understanding

我确认了：Vue 3.5 + Vite 6 + pnpm 前端（`ForgeSelf.Web/`）、.NET 10 + SQLite + NewLife.XCode 后端（`ForgeSelf.Api/`）、插件经 `Plugins/<Pascal>/` + `plugin.json` 注册、宿主经 ALC 热卸载重载插件（不重启）、插件前端为 vite lib 出树构建（`scripts/build-plugin-web.ps1`）、测试体系三件套（vitest / dotnet test / Playwright e2e 隔离实例）。详见 `00-repository-understanding.md`。

## 2. Selected Task

输入1「做一个统计文件夹大小的插件」→ 闸门1 批复 U-1 扩 FileTools / U-2 复用现有文件工具 / U-3 P0 带快照持久化 / U-4 声明 scan 工具；输入2 要求 FileTools 前后端迁独立插件、自身热更新不重启宿主 → 两项合并为本批次。

## 3. Changed Files

- 插件：`Plugins/FileTools/`（Services/FolderScanService + FolderScanJobStore + FolderSnapshotService、Models/FolderScanModels、Data/ 实体与 Model.xml、FolderStatsToolFunction、Controllers/FileToolsController、FileToolsPlugin、web/ 自带界面、plugin.json 1.1.1）
- 宿主：`ForgeSelf.Api/Data/XCodeConfig.cs`（PluginDbs 注册一行）；`ForgeSelf.Web/src/` 撤 filetools 页面/路由/store/api（原件归档 `.trash/2026-09-28-filetools-host-ui/`）；e2e `global-setup.ts` 自动取/解密 token
- 测试：`ForgeSelf.Api.Tests/Unit/FolderScanServiceTests.cs`、`Unit/FileToolsFoldersAuthAndToolTests.cs`、`Integration/FolderSnapshotPersistenceTests.cs`；`ForgeSelf.Web/e2e/plugins/file-tools/`（6 例）
- 文档/脚本：`docs/02-features/036-filetools-folder-ranking.md`、`docs/ai/pilot/batch-c-folder-size-plugin/00-06`、`scripts/build-plugin-web.ps1`

## 4. Validation

Build:
- `dotnet build`（插件 + 宿主）✅ Verified
- `cd Plugins/FileTools/web && pnpm run build` ✅ Verified（index.js 102,432 B）

Unit Test:
- 定向：FolderScan/Snapshot/AuthAndTool 相关 26 例绿 + 1 例红已修（`scripts/build-plugin-web.ps1` 缺 UTF-8 BOM 撞 `RepositoryScriptTests`，补 BOM 后绿）✅ Verified
- 后端全量：1516 例，13 红为既有基线（对表 `project-baseline-test-reds`，无本批新增）✅ Verified

E2E:
- 插件层 `e2e/plugins/file-tools`：**6/6 绿**（排行/快照/取消中间态闭合/徽标/真实树），截图读图无 `.warn-line` ✅ Verified
- 全量 e2e：102 passed / 82 failed，82 红逐类对表为既有基线（陈旧断言/真 LLM 依赖/51888 依赖/4-worker 超时），无本批新增 ✅ Verified

## 5. Evidence

关键结论 + 全部真实输出见 `05-evidence.md`（六跑迭代表、两条时序缺陷的修复证据、§5.6 门禁矩阵、铁律13 自查）。运行实例只读复验（:51888）：徽标 v1.1.1、已知树字节级对账（11,816 B/3 目录/6 文件精确一致）、真实树 2.21 GB/15,771 目录/101,994 文件/65,973 ms，`Running` 帧占比 ≤100 且 Σ 闭合，独立 `find` 对账差 2 文件 = 走查期间自写的两张截图；截图 `ForgeSelf.Web/screenshots/live-51888/`。

## 6. Review

`06-review.md` Final Decision = **APPROVED**（交闸门2 用户验收）。Findings：Major #2（部分结果根分母 564965%）已修复并有回归断言。

## 7. Risk

L2 —— ① 本批含宿主 `XCodeConfig.PluginDbs` 一行，**这一版宿主仍需重建一次**，此后 FileTools 迭代才不重启；② 随宿主包冷装的插件无版本历史目录（卡片显示「暂无版本历史」，易误读为没更新，TODO P3）；③ :51888 运行实例仍是带缺陷的 1.1.0，等用户手动启用侧载 1.1.1。

## 8. Problems Found

- 缺陷 #1：前端轮询对 `state` 的 int/字符串双形不兼容（首跑 6/6 红的根因之一）。
- 缺陷 #2（Major）：取消后部分结果根分母只有根级字节 → 占比 564965%；改为逐文件锁内父链上卷，实测证明该修复几乎零性能代价（附录 A 变体 D）。
- 流程违规自查 2 条：铁律13（插件页面不显示自身版本）→ 已补徽标 + 技能回写；B6（.ps1 缺 BOM）→ 已补 + 全量守卫抓到的价值记入门禁分档立规依据。
- 计时探针曾把「File GetAttributes + FileInfo.Length 双 stat」暴露为慢的主因（647 µs/文件）→ 立项 1.1.2 提速补丁（`../folder-scan-perf-1.1.2/mini-task.md`）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS（闸门1 四项批复均非推荐项，回炉修订后过） |
| Spec → Plan | PASS |
| Plan → Code | PASS（偏差 D-0/D-3/D-8 记录在案） |
| Code → Test | PASS（六跑 e2e 迭代） |
| Test → Evidence | PASS |
| Evidence → Review | PASS |
| **交付后走查** | PARTIAL —— 初版流程漏了运行实例只读复验（用户 T29 抓出），已回写 `plugin-publish-verify`/`AGENTS.md` §0 五步门禁 |

## 10. 最重要的问题

流程定义把「走查」锚定在 e2e 隔离实例，导致**交付后的运行实例只读复验**被系统性遗漏——不是遗忘，是规则缺口。教训已固化：插件任务门禁从四步改五步，交付后必须 token 预检 → 徽标核对 → 主链路 + 中间态采样 → 截图读图。

## 11. 下一步建议

最值得的下一个实验：执行已立项的 `folder-scan-perf-1.1.2`（枚举自带 size + 子树并行，同树 22 s → ≤3 s），验证「并行化后中间态闭合不变式仍可零成本保持」这一设计假设。
