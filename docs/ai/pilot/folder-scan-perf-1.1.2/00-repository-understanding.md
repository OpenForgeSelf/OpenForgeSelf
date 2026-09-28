# Repository Understanding（目录扫描提速 · FileTools 1.1.2）

> 阶段：Stage 0｜规范：`docs/04-standards/ai-native-engineering-workflow.md` §2｜task-id: `folder-scan-perf-1.1.2`｜落稿：2026-09-28
> 本任务仓库基线与批次C 相同，**不重复**批次C《`docs/ai/pilot/batch-c-folder-size-plugin/00-repository-understanding.md`》已核实的内容（插件发现/加载、前端远程加载、StageAllPlugins、鉴权、端口等），本文件只记**与本任务直接相关**的增量现状。

## 本任务涉及的代码现状（全部经当次读文件核实）

1. **扫描实现（1.1.1 版，本任务要改的文件）**：`Plugins/FileTools/Services/FolderScanService.cs` —— `RunAsync` 调 `Walk(job)` 单线程递归（显式栈）；目录内枚举用 `Directory.EnumerateFileSystemEntries(dir)`（只给路径），每个条目再 `File.GetAttributes(e)` + `new FileInfo(e).Length` 两次元数据调用；共享状态（`ScanJob.Dirs` 字典 + 各 `DirStat`）全部在 `lock(job.Sync)` 内逐文件沿父链上卷。取消按 `CancelCheckGranularity`（256 条目）粒度检查 `job.Cts`。
2. **任务表**：`Plugins/FileTools/Services/FolderScanJobStore.cs` —— `ScanJob { Dirs: Dictionary<string,DirStat>（Ordinal，非并发，必须持锁访问）, Sync, Cts, State, TotalFiles, ... }`；`DirStat { Depth, Parent, Direct, Total, Files, ChildDirs }`；`GetOrAdd(path, depth, parent)`；`MaxJobs=20` 按完成时间淘汰。
3. **既有测试**：`ForgeSelf.Api.Tests/Unit/FolderScanServiceTests.cs`（410 行，12 用例）——夹具模式：`NewRoot()`（Temp 下建目录，铁律10 只建不删）、`WriteFile(dir,name,bytes)`、期望值按真实写入字节计算、中间态闭合轮询断言、`Pct()` 复刻 internal Percent。
4. **版本号落点**：`Plugins/FileTools/plugin.json:4`（`"Version": "1.1.1"`）+ `Plugins/FileTools/web/package.json:3`（`"version": "1.1.1"`）。
5. **worktree 状态**：本会话在 detached-HEAD worktree（基点 78d065c），批次C 1.1.1 已以 `c4b0577` 提交；`scripts/verify-pilot-artifacts.ps1` 已从 main 检出（pre-commit 钩子依赖）。

## 性能现状（本任务的立项证据，探针实测）

- 走查实测锚点：`C:\Users\Administrator\.qoder-cn\worktrees`（101,994 文件）扫描 **65,973 ms** ≈ 647 µs/文件。
- 同机 A/B（同棵 `Plugins/` 树 51,931 文件 / 7,004 目录，两 pass）：当前实现 B1 = 22.0~22.7 s；仅去掉多余 `GetAttributes`（B2）≈ 12.5~15.5 s；流式枚举自带 size（A2）≈ 2.2~3.1 s；A2 + 并行 DOP=8（E）≈ **0.67~2.2 s**。7 个变体 files/bytes/links 全一致。详见 mini-task 附录 A。

## 流程现状

- pre-commit 钩子强制 `docs/ai/pilot/<task>/` 八件齐全（00~07）才允许提交，**无轻量裁剪豁免** → 与规范 §4「mini-task 裁剪」存在工具性冲突，按偏差处理：本目录以 `mini-task.md`（闸门1 批复原件）+ 拆分后的 00~07 链双轨落盘，内容同源。
- 门禁档位（输入14）：本任务 = **快档**（不碰宿主源码/仓库脚本/共享 e2e 夹具）。

## 候选低风险任务及选择理由

用户输入16（T34）直接点名性能差距「为什么码神工具统计文件夹大小这么快，当前实现这么慢？」→ 任务自上而下指定，无需从候选清单挑选；且根因已 A/B 实测定位（附录 A），修复面收敛在单个 Service 文件内，属低风险。
