# Task（目录扫描提速 · FileTools 1.1.2）

> 阶段：Stage 4｜本文为 `mini-task.md` §4 的标准模板化拆出件（内容同源）。

## Task ID

PILOT-perf-1.1.2

## Objective

`Plugins/FileTools` 的目录扫描在同树实测 ≤ 3 s（原 22 s），对外契约与中间态自洽性不变，以 1.1.2 侧载交付、宿主不重启。

## Scope

### Allowed

- 修改：`Plugins/FileTools/Services/FolderScanService.cs`、`ForgeSelf.Api.Tests/Unit/FolderScanServiceTests.cs`、`Plugins/FileTools/plugin.json`、`Plugins/FileTools/web/package.json`、`docs/02-features/036-filetools-folder-ranking.md`（算法描述同步）。
- 新增测试：仅限 `FolderScanServiceTests.cs` 内。

### Forbidden

- 宿主任何文件（`ForgeSelf.Api/**`）、仓库脚本（`scripts/**`）、共享 e2e 基建（`global-setup`/`fixtures`/`playwright.*.config`）。
- 缓存、懒展开、删除能力（批次D 范围）；`FileStatsService` 既有方法；API 契约变更；新增 NuGet/npm 依赖；构建目标变更。
- 未过闸门2 前 push/tag；替用户在 :51888 点「更新」；停/启/杀任何宿主进程。

## Acceptance Criteria

- [ ] 同树 `Plugins/` 扫描 ≤ 3 s（单测计时断言，12k 夹具 ≤ 1500 ms）
- [ ] 并行多分区精确总量 + 分区不变式（新增用例绿）
- [ ] Running 后取消 → `Cancelled` 且部分结果闭合、占比 ≤100（新增用例绿）
- [ ] 既有中间态闭合用例不改断言仍绿
- [ ] `e2e/plugins/file-tools` 6/6 绿 + 截图读图无告警行
- [ ] 快档全部命令实际执行并记录真实输出（`05-evidence.md`）
- [ ] 版本号 1.1.2（两处）

## Expected Files

- `Plugins/FileTools/Services/FolderScanService.cs`
- `ForgeSelf.Api.Tests/Unit/FolderScanServiceTests.cs`
- `Plugins/FileTools/plugin.json`
- `Plugins/FileTools/web/package.json`
- `docs/ai/pilot/folder-scan-perf-1.1.2/05-evidence.md`（完成后）
- `docs/ai/pilot/folder-scan-perf-1.1.2/06-review.md`（完成后）

## Verification Commands

```bash
cd Plugins/FileTools && dotnet build
cd Plugins/FileTools/web && pnpm run build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~FolderScan"
cd ForgeSelf.Web && pnpm run check && pnpm run test
cd ForgeSelf.Web && npx playwright test e2e/plugins/file-tools --output=.pw-out-perf
```
