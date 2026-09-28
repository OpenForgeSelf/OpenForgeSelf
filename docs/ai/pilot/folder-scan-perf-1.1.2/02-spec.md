# Spec（目录扫描提速 · FileTools 1.1.2）

> 阶段：Stage 2｜本文为 `mini-task.md` §2 的标准模板化拆出件（内容同源）。不确定点显式标注，无自造接口。

## Functional Requirements

- **FR-1**：目录遍历改为 `DirectoryInfo.EnumerateFileSystemInfos()` 系（流式枚举），文件 size 与 `ReparsePoint` 判定取自枚举对象，**不得**再对每个文件单独 `File.GetAttributes` / `new FileInfo().Length`。
- **FR-2**：根本级文件由主线程直接累计；根的直接子目录各为一个独立分区，`Parallel.ForEach` 并行遍历，`MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8)`（闸门1 拍板值）；每分区顺序 DFS。
- **FR-3**：共享状态读写全部保持在 `lock(job.Sync)` 内（含逐文件父链上卷），任意时刻分区闭合不变。
- **FR-4**：取消在分片内按既有粒度检查 `job.Cts`；取消后不再启动新分片；状态聚合：任一分片取消 → `Cancelled`（保留部分结果），根级意外 → `Failed`。

## Input

与现状一致：`FolderScanRequest { Directory, Top }`（端点契约不变）。`Top=0 → 50`、`Top>500 → 500` 夹紧不变。

## Output

与现状一致：`ScanAccepted` / `ScanView`（字段、端点、返回结构全部不变）；`truncated`/`capNote`/`inaccessibleCount`/`skippedReparseCount` 语义不变。

## Business Rules

- 不变：根分母 = 根整棵树；排行切片 = 直接子目录；`OtherRow` / `RootOwnRow` 补齐使 Σ 闭合；百分比 = 行/根总量两位小数。
- 不变：不跟随符号链接/junction/挂载点（`ReparsePoint` 跳过并计数）；权限/IO 失败逐条目容错并计数，不整体失败。
- 不变：内存任务表 ≤ 20 个，按完成时间淘汰、在跑的不淘汰。

## Boundary Conditions

- 上限 `MaxDirectories=200,000` / `MaxFiles=5,000,000` 触顶即截断并标 `Truncated`；并行下允许「略微超阈值即停」，但截断标记与分区闭合仍须可断言。
- 空目录、无子目录、根即盘符根：与单线程版行为一致。
- 树的直接子目录为 0 或 1 个时并行自动退化为单分片（仍走 `Parallel.ForEach`，语义一致）。

## Error Handling

- 分片内 `UnauthorizedAccessException` / `IOException`：沿用逐条目计数，不抛。
- 分片内取消（`job.Cts`）：聚合为 `Cancelled`（`Cancelled` 优先于 `Completed`）。
- 其他异常：聚合为 `Failed`，`Error` 记消息并 `XTrace` 告警（沿用现状）。
- 根路径非法：`NormalizeRoot` 行为不变（不存在/是文件 → `DirectoryNotFoundException`；空白 → `ArgumentException`）。

## Compatibility

- API 契约零变化；1.1.0/1.1.1 生成的快照数据不受影响（扫描逻辑与持久化无关）。
- 前端零代码变化，仅 `web/package.json` 版本号跟随。

## Non-functional Requirements

- 性能：Success Criteria 1/2（Intent）。
- 资源：峰值内存不得显著高于单线程版（仍是流式枚举 + 每目录一份 `DirStat`）；并行度上限 8。
- 正确性：并行/单线程对同一棵树的总字节、文件数、目录数、跳过数必须完全一致（测试断言）。

## Acceptance Criteria

- [ ] `FolderScanServiceTests` 新增「并行多分区精确总量 + 不变式」用例并绿
- [ ] 新增「Running 后取消 → Cancelled 且部分结果闭合、占比 ≤100」用例并绿
- [ ] 新增「12,000 文件夹具预热后扫描 ≤ 1500 ms」计时用例并绿
- [ ] 既有 `PartialViews_...PartitionInvariant...` 等中间态闭合用例不改断言且仍绿
- [ ] `e2e/plugins/file-tools` 6/6 绿（含取消例的中间态断言）
- [ ] `plugin.json` / `web/package.json` = 1.1.2

## Unknown

- 无新增。原两条 Unknown（并行度默认值、计时阈值策略）已于闸门1（T42 默认确认）拍板，见 Intent §Constraints。
