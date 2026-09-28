# Plan（目录扫描提速 · FileTools 1.1.2）

> 阶段：Stage 3｜本文为 `mini-task.md` §3 的标准模板化拆出件（内容同源）。

## Files To Change

| file | reason |
| --- | --- |
| `Plugins/FileTools/Services/FolderScanService.cs` | FR-1/FR-2/FR-3/FR-4：枚举改造（size 取自枚举记录）+ 根级子目录并行分片 + 取消聚合 |
| `ForgeSelf.Api.Tests/Unit/FolderScanServiceTests.cs` | 新增并行多分区、并行取消、计时三条用例；既有用例不动 |
| `Plugins/FileTools/plugin.json` | `Version` 1.1.1 → 1.1.2（机械） |
| `Plugins/FileTools/web/package.json` | `version` 1.1.1 → 1.1.2（机械） |
| `docs/02-features/036-filetools-folder-ranking.md` | 算法描述同步（§3：流式枚举自带 size + 并行分片） |

## Implementation Steps

1. `FolderScanService`：目录内枚举换成 `DirectoryInfo.EnumerateFileSystemInfos()`；文件分支直接取 `FileInfo.Length`（枚举对象自带，0 次额外 stat），目录分支用 `DirectoryInfo.Attributes` 判 `ReparsePoint`；容错/计数逻辑原样保留。
2. 根级处理：根本级文件由主线程在锁内直接累计；根的直接子目录收集成列表后 `Parallel.ForEach`（DOP = `min(ProcessorCount, MaxParallelism=8)`）跑各分区顺序 DFS `Walk(job, subDir, 1)`；共享状态全部在 `lock(job.Sync)` 内。
3. 状态聚合：`RunAsync` 聚合分片终态（`Cancelled` 优先于 `Completed`；OCE → `Cancelled`；其他异常 → `Failed` + `XTrace` 告警）；`FinishedAt` 记录不变。
4. 测试：新增 ①并行多分区精确总量+不变式 ②Running 后取消 ③12k 夹具计时 ≤1500ms；版本号 1.1.2 两处；功能文档算法描述同步。
5. 跑 §Verification 全部命令；证据落 `05-evidence.md`；审查落 `06-review.md`。

## Test Plan

- 单测：上述三条新用例 + 既有 `FolderScanServiceTests` 全部回归（`--filter "FullyQualifiedName~FolderScan"`）。
- e2e：`e2e/plugins/file-tools` 现有 6 例（排行/快照/取消/中间态闭合/徽标/真实树）定向跑，截图读图。

## Verification

### Build
```bash
cd Plugins/FileTools && dotnet build
cd Plugins/FileTools/web && pnpm run build
```
### Unit Test
```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~FolderScan"
```
### Integration Test
N/A —— 无 API 契约变化（端点/字段/鉴权全部不变），依据 02-spec §Compatibility。

### E2E
```bash
cd ForgeSelf.Web && npx playwright test e2e/plugins/file-tools --output=.pw-out-perf
```
判定：读落盘输出 6/6 绿 + 截图 `ForgeSelf.Web/screenshots/e2e/file-tools/` 读图无告警行。

### Other Checks
- 前端门禁：`cd ForgeSelf.Web && pnpm run check && pnpm run test`（宿主前端本批零改动，仍按 AGENTS.md §5.1 必跑项执行）。
- 档位声明（输入14）：本任务 = **快档**。不跑中档理由：不碰 `ForgeSelf.Api/**`、不碰 `scripts/**`、不碰共享测试夹具；不跑深档理由：不碰 `e2e/global-setup.ts` / `playwright.*.config.ts` / `e2e/fixtures/**`。
- 交付后（用户在 ：51888 启用 1.1.2 后）：`plugin-publish-verify`「运行实例只读复验」（token 预检 401 立即上报 → 核 `.view-title` 徽标 v1.1.2 → 主链路 + 中间态采样 → 截图 `screenshots/live-51888/` 读图），只读边界不变。

## Plan 偏差记录

| # | 偏差 | 处理 |
| --- | --- | --- |
| D-1 | pre-commit 钩子强制 `docs/ai/pilot/<task>/` 00~07 八件齐全，**无轻量裁剪豁免**，与规范 §4「mini-task 裁剪（01~04 合并）」工具性冲突 | 双轨落盘：`mini-task.md` 为闸门1 批复原件，01~04 由其标准模板化拆出（内容同源，非二次创作）；规范层面冲突记入 06-review / TODO |
| D-2 | 测试用例命名从 Plan 草案的 `ParallelScan_MatchesSequentialExactly`（跑两遍比对）调整为 `ScanAndWait_ParallelPartitions_...`（单遍精确期望值断言） | 等价更强：夹具字节全部已知，直接断言精确值即可证明并行无丢字节；省一遍扫描 |
| D-3 | e2e 取消用例首跑红：12k 合成夹具在新引擎下 32ms 扫完（单测实测），首轮轮询即 Completed，取消键全程置灰——「扫描中」窗口不存在；且本机小文件首建 ~25ms/个，扩夹具到能撑秒级窗口（>100 万条目）不可行 | 探针实测候选真实大盘后（worktrees 14.5s / WinSxS 108.9s / Program Files 28.6s），用例扫描目标改为 `FT_E2E_SLOW_DIR`（默认 `C:\Program Files`，每台 Windows 都有、只读元数据），删除合成夹具函数；测试结构（两条弹窗路径 + 部分闭合断言 + 截图）不变。真实文件系统，零 mock 不变 |
