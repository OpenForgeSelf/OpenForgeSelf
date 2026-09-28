# Evidence（目录扫描提速 · FileTools 1.1.2）

> 阶段：Stage 7｜只记实际发生的事。来源等级：**Verified**（亲自跑过，拿到真实输出）/ Inferred / Unknown。task-id: `folder-scan-perf-1.1.2`｜2026-09-28 深夜 ~ 09-29 凌晨

## Changed Files

| file | 变更 |
| --- | --- |
| `Plugins/FileTools/Services/FolderScanService.cs` | 枚举换 `DirectoryInfo.EnumerateFileSystemInfos`（size/reparse 取自枚举记录，0 次额外 stat）；根级文件主线程累计 + 直接子目录 `Parallel.ForEach` 分片（DOP=min(核数,8)）；分片启动前查取消令牌；`RunAsync` 收尾 `!IsCancellationRequested` 才可标 Completed |
| `ForgeSelf.Api.Tests/Unit/FolderScanServiceTests.cs` | 新增 3 用例（并行多分区精确总量 / Running 后取消 / 12k 计时）；测试类加 `ITestOutputHelper`（计时值可采出） |
| `Plugins/FileTools/plugin.json` | Version 1.1.1 → 1.1.2 |
| `Plugins/FileTools/web/package.json` | version 1.1.1 → 1.1.2 |
| `docs/02-features/036-filetools-folder-ranking.md` | §3 算法描述同步（流式枚举自带 size + 并行分片） |
| `ForgeSelf.Web/e2e/plugins/file-tools/file-tools.spec.ts` | 取消用例扫描目标改 `FT_E2E_SLOW_DIR`（默认 `C:\Program Files`），删 12k 合成夹具（偏差 D-3） |
| `docs/ai/pilot/folder-scan-perf-1.1.2/*` | 00~07 + mini-task |

## Validation（命令 + 真实结果）

| 项 | 命令 | 结果 | 来源 |
| --- | --- | --- | --- |
| 插件后端构建 | `cd Plugins/FileTools && dotnet build` | **0 错误**（18 警告均为既有 XCode 生成实体 CS86xx） | Verified |
| 后端单测 | `dotnet test --filter "FullyQualifiedName~FolderScanServiceTests"` | **17/17 通过**（8m27s，夹具建树占大头）；含既有 12 用例不改断言回归绿 | Verified |
| 计时断言值 | 同上过滤 `ScanAndWait_12kFiles` + detailed logger | 「12k 夹具并行扫描实测 **32 ms**（阈值 1500 ms）」 | Verified |
| 插件前端构建 | `cd Plugins/FileTools/web && pnpm run build` | 绿（dist/index.js 102.43 kB / style.css 45.12 kB） | Verified |
| 宿主前端 check | `cd ForgeSelf.Web && pnpm run check` | **0 error**（81 warning 均为既有） | Verified |
| 宿主前端单测 | `pnpm run test` | **482/482 通过**（44 文件） | Verified |
| 插件层 e2e | `npx playwright test e2e/plugins/file-tools --output=.pw-out-perf` | **6/6 通过**（1.6m） | Verified |
| e2e 截图读图 | `screenshots/e2e/file-tools/folders-ranking.png` / `folders-cancelled.png` | ①徽标 v1.1.2、样本树 11.54 KB / 耗时 10 ms、三行合计 ≈100%、无告警行；②Program Files 扫描 3s Cancelled，已累计 31.20 GB / 76,393 文件、占比 ≤100、降序正常 | Verified |

档位声明：**快档**（不碰宿主源码/仓库脚本/共享 e2e 夹具，中/深档豁免理由见 03-plan §Verification）。

## 红灯 → 修复记录（真实缺陷，按 TDD 处置）

1. **并行取消被吞成 Completed**（首跑 16/17）：分片私有 `touched` 计数 + 夹具每分片仅 5 条目，永远撞不到 256 条目取消检查点；`RunAsync` 收尾只看 `State != Cancelled`。修复：分片 lambda 启动前查令牌 + 收尾 `!job.Cts.IsCancellationRequested` 才标 Completed。修复后 17/17。
2. **e2e 取消用例红**（5/6）：12k 夹具 32ms 扫完，「扫描中」窗口消失 → 偏差 D-3（改扫 `C:\Program Files`）。重跑 6/6。
3. **插曲**：一次 `dotnet test` 报「测试主机进程崩溃」（无用例执行即崩）；tasklist 查无残留 testhost，原命令重跑即恢复 → 按瞬时故障记录，不涉代码。

## Known Limitations

1. 运行实例（:51888）复验**未做**：前置条件是用户在设置页把插件更新到 1.1.2（发布通道未定：tag→CI 或本地目录更新源，待用户拍板）。启用后按 `plugin-publish-verify`「运行实例只读复验」五步走（token 预检 → 徽标 v1.1.2 → 主链路 + 中间态采样 → 截图 `screenshots/live-51888/`）。
2. e2e 取消用例依赖真实大盘（默认 `C:\Program Files`）：非 Windows 环境或目录极小的机器可用 `FT_E2E_SLOW_DIR` 覆盖；未设且目录过小则该用例可能红（窗口不足）——已在用例注释写明。
3. 计时阈值 1500 ms 是绝对阈值，慢盘/CI 机器存在环境敏感风险（当前 32 ms，余量 46×）。

## Unresolved Issues

- 发布通道待用户拍板（tag→CI 自动发布 / `release-local.ps1` 本地目录更新源）。
- 规范 §4 轻量裁剪与 pre-commit 钩子八件套强制的工具性冲突待裁定（D-1，本任务双轨落盘规避）。
