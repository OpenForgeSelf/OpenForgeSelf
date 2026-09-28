# mini-task · 目录扫描提速（FileTools 1.1.1 → 1.1.2）

> **【2026-09-29 补注 · 双轨口径】** 本文件是**闸门1 批复原件**（T42 批复前落盘）。因 pre-commit 钩子强制本目录 00~07 八件齐全（无轻量裁剪豁免，与规范 §4 工具性冲突，记偏差 D-1），Intent/Spec/Plan/Task 已按标准模板拆出为 `01-intent.md`~`04-task.md`（**内容与本文件同源，非二次创作**；执行中新增偏差 D-2/D-3 只记拆出件与 03-plan）；Evidence/Review 按 §4 本就单独产出（05/06/07）。审阅闸门1 批复原貌读本文件，审阅执行与证据读 01~07。
>
> 阶段：Stage 1–4 合并（轻量裁剪）。产物规范：`docs/04-standards/ai-native-engineering-workflow.md` §4 ——「≤3 文件的缺陷修复/边界测试补充：Intent/Spec/Plan/Task 合并为单文件 `mini-task.md`（含五要素），**Evidence(05) 与 Review(06) 完成后仍必须单独产出**于本目录」。
> **闸门1：本文件经用户批准后才允许改代码。** 裁剪裁定（协调人 = 批次协调 Agent，须记录于工件）：核心改动 2 个代码文件（`FolderScanService.cs` + `FolderScanServiceTests.cs`），另有 2 个机械版本号文件（`plugin.json` / `web/package.json`），属性能修复、不碰 API 契约、不碰宿主 → 适用轻量；版本号文件为机械变更，不改变「≤3 文件」判定的实质。

## 输入溯源（回溯全部用户输入 · 逐回合原文）

完整原文存档：`.forgeself/memory/2026-09-27.md`、`.forgeself/memory/2026-09-28.md`。与本任务有约束关系的输入逐条如下（原文照录，只截取关键句）：

| 来源 | 原文 | 对本任务的约束 |
| --- | --- | --- |
| 输入1（2026-09-27） | 「做一个统计文件夹大小的插件」 | 本功能是它的性能补丁；功能边界不扩 |
| 输入2（2026-09-27） | 「文件工具应该前后端都迁移到独立文件夹，自身更新不依赖宿主，不重启宿主」 | 本补丁**只允许改 `Plugins/FileTools/` 内文件**；发版后必须侧载生效、宿主不重启 |
| 输入3（T8） | 「工作日记，项目文档记录规范，每一个回合记录…每个对话方的发言原文和序号(seqN)，如果有自己的发言也原样记录，不能只记结论或转述」 | 本表即按此执行；后续 Evidence/Review 同样逐回合记原文 |
| 输入12（T26） | 「参考一下实现，完善文件夹大小统计功能，取功能实现部分，ui用web做：…`MakeTree`（目录置 `-1` 后 `Task.Run` 异步统计）、`FolderSize`（递归求和 + `cache.Set(path, size, 30)`）…」＋拍板「只做回收站删除」「另起批次D」 | 只采纳与性能直接相关的两点：① 枚举自带 size（不额外 stat）② 并行统计；**懒展开 `-1` / 30 s 缓存 / 回收站删除一律排除在本补丁外**（归批次D） |
| 输入13（T27） | 「那当前文件夹大小统计功能到底能用吗，打一个插件包，我手动安装试试，或者你帮我打包放到宿主运行的插件目录下，我手动启用」 | 交付形态 = 插件侧载包；**启用动作由用户手动做**，agent 不替点「更新」 |
| 输入8（T20） | 「…如果没有token，根据插件技能流程，有解密脚本的（…放到访问连接上，或者设置localstorage，使得每次打开e2e就能自动鉴权）」 | 验证取 token 只走 `scripts/get-forge-token.cjs` 或 e2e `global-setup.ts` 注入，**禁止现写解密探针** |
| 输入14（T28） | 「把这三档写进 AGENTS.md §5 和 e2e-testing 技能？写了以后就按它执行，不再每次临场判断」 | 本任务门禁档位 = **快档**（不碰宿主源码/仓库脚本/共享 e2e 夹具），判定依据写入 Verification |
| 输入15（T29–33） | 「要走查http://localhost:51888/file-tools 吧？」「401了」「当前的key是如图所示」「把你的建议写进 plugin-publish-verify 的走查步骤」 | 交付后必走 `plugin-publish-verify`「运行实例只读复验」：token 预检（401 立即上报）→ 核 `.view-title` 徽标 = v1.1.2 → 主链路 + 中间态采样 → 截图读图；只读、不启停宿主 |
| 输入16（T34，本任务来源） | 「当前实现与XCoder码神工具的实现有何差别？为什么码神工具统计文件夹大小这么快，当前实现这么慢？」 | 根因已实测定位（附录 A），本补丁按此立项 |
| 输入17（T37–40） | 「一个清晰的方案应该包含什么」「你的格式是什么，有就说有」「给完了怎么不提在哪个文件夹让我审核」 | 本文件即按 `AGENTS.md` §3.1 六段格式与 §4 模板落盘；聊天只报路径 |

与闸门/发布相关的长期约束（`AGENTS.md` §0/§2.3，历次输入确立）：禁止 agent 停/启/杀用户运行中的宿主进程；插件侧载须用户同意；未过闸门2 不得 commit；发布只走技能脚本（tag→CI 或 `release-local.ps1`）；e2e 必带 `--output=<空目录>`；不新增依赖、不改构建目标；`.ps1` 含中文须 UTF-8 BOM（本任务不碰脚本，列出备查）。

## 1. Intent

### Problem
「目录大小排行」全树扫描过慢：走查实测 `C:\Users\Administrator\.qoder-cn\worktrees`（101,994 文件）耗时 **65,973 ms**（647 µs/文件）；同机 A/B 定位到两根因：① `Walk` 对每个文件做两次独立元数据调用（`File.GetAttributes` + `new FileInfo(e).Length`），而枚举记录本身就带 size；② 单线程遍历。

### Why
用户在 1.1.1 已可用后明确追问「为什么码神这么快、当前这么慢」（输入16）——性能是功能可用性的直接部分；且实测证明修复不花钱就能保住正确性（1.1.1 的任意时刻分区闭合，附录 A 变体 D）。

### Expected Outcome
同树扫描耗时降至原来的 **1/8 ~ 1/30**；对外契约、交互、中间态自洽性全部不变；以插件 1.1.2 侧载交付，宿主不重启。

### Constraints
- 只改 `Plugins/FileTools/` 内 2 个代码文件 + 2 个版本号文件；宿主零改动（输入2）。
- 不引入任何新依赖；不加缓存、不做懒展开（输入12 拍板：那些归批次D）。
- 保留 1.1.1 全部语义：reparse 跳过、权限/IO 容错、200k 目录 / 5M 文件上限、取消、**任意时刻分区闭合**。
- 并行度默认值待闸门1 拍板（见 §2 Unknown）。

### Success Criteria
1. 同树 `Plugins/`（51,931 文件 / 7,004 目录）扫描 **≤ 3 s**（当前实测 22 s）。
2. 线上锚点树（101,994 文件）**≤ 15 s**（当前 65,973 ms）。
3. `e2e/plugins/file-tools` **6/6 绿**，中间态断言不退化：每帧 `percentage ≤ 100` 且 `Σ(行) + otherRow + rootOwn == rootTotal`。
4. 取消 → `Cancelled` 且部分结果闭合；上限截断语义不变（均有测试断言）。

## 2. Spec

### Functional Requirements
- FR-1：目录遍历改为 `DirectoryInfo.EnumerateFiles()` / `.EnumerateDirectories()`，文件 size 与 `ReparsePoint` 判定取自枚举对象，**不得**再对每个文件单独 `File.GetAttributes` / `new FileInfo().Length`。
- FR-2：根的直接子目录并行统计（`Parallel.ForEach`，`MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8)`，闸门1 若改判则按拍板值）；根级文件直接计入。
- FR-3：共享状态读写全部保持在 `lock(job.Sync)` 内（含逐文件父链上卷），任意时刻分区闭合不变。
- FR-4：取消在分片内检查 `job.Cts`；取消后不再启动新分片；状态聚合：任一分片取消 → `Cancelled`（保留部分结果），根级意外 → `Failed`。

### Input
与现状一致：`FolderScanRequest { Directory, Top }`（端点契约不变）。

### Output
与现状一致：`ScanAccepted` / `ScanView`（字段、端点、返回结构全部不变）。

### Business Rules
- 不变：根分母 = 根整棵树；排行切片 = 直接子目录；`OtherRow` / `RootOwnRow` 补齐使 Σ 闭合；百分比 = 行/根总量，两位小数。
- 不变：不跟随符号链接/junction（`ReparsePoint` 跳过并计数）；权限/IO 失败逐条目容错并计数，不整体失败。

### Boundary Conditions
- 上限 `MaxDirectories=200,000` / `MaxFiles=5,000,000` 触顶即截断并标 `Truncated`——并行下允许「略微超阈值即停」，但断言仍须验证截断标记与闭合。
- 空目录、无子目录、根即盘符根：与单线程版行为一致。
- 树的直接子目录为 0 或 1 个时并行自动退化为单分片。

### Error Handling
- 分片内 `UnauthorizedAccessException` / `IOException`：沿用逐条目计数，不抛。
- 分片内 `OperationCanceledException`：聚合为 `Cancelled`。
- 其他异常：聚合为 `Failed`，`Error` 记根级消息并 `XTrace` 告警（沿用现状）。

### Compatibility
- API 契约零变化；1.1.0/1.1.1 生成的快照数据不受影响（扫描逻辑与持久化无关）。
- 前端零代码变化，仅 `web/package.json` 版本号跟随。

### Non-functional Requirements
- 性能：Success Criteria 1/2。
- 资源：峰值内存不得显著高于单线程版（仍是流式枚举 + 每目录一份 `DirStat`）；并行度上限 8。
- 正确性：并行/单线程对同一棵树的总字节、文件数、目录数、跳过数必须完全一致（测试断言）。

### Acceptance Criteria
- [ ] 同树 `Plugins/` 扫描 ≤ 3 s（计时断言，阈值策略见 §2 Unknown）
- [ ] `FolderScanServiceTests` 新增「并行结果与单线程结果逐字段一致」用例并绿
- [ ] 既有 `PartialViews_...PartitionInvariant...` 等中间态闭合用例不改断言且仍绿
- [ ] 新增「并行下取消 → Cancelled 且部分结果闭合」用例并绿
- [ ] `e2e/plugins/file-tools` 6/6 绿（含取消例的中间态断言）
- [ ] `plugin.json` / `web/package.json` = 1.1.2

### Unknown
- 计时断言阈值策略：拟用「固定夹具树（12,000 文件）扫描 ≤ 1,500 ms」（本机旧实现 ~3 s，给 2× 余量）；是否换成「相对基线比」待闸门1 拍板（默认：绝对阈值）。
- 并行度默认值：`min(ProcessorCount, 8)`（默认推荐）或 `min(ProcessorCount/2, 4)`（不抢前台）待闸门1 拍板。

## 3. Plan

### Files To Change
| file | reason |
| --- | --- |
| `Plugins/FileTools/Services/FolderScanService.cs` | FR-1/FR-2/FR-3/FR-4：`Walk` 枚举改造 + 根级子目录并行调度 + 取消聚合（约 :108-253） |
| `ForgeSelf.Api.Tests/Unit/FolderScanServiceTests.cs` | 新增并行一致性、并行取消、计时三条用例；既有用例不动 |
| `Plugins/FileTools/plugin.json` | `Version` 1.1.1 → 1.1.2（机械） |
| `Plugins/FileTools/web/package.json` | `version` 1.1.1 → 1.1.2（机械） |

### Implementation Steps
1. 将 `Walk` 的目录内枚举换成 `DirectoryInfo.EnumerateFiles()` / `.EnumerateDirectories()`；文件分支直接取 `FileInfo.Length`（枚举对象自带），目录分支用 `DirectoryInfo.Attributes` 判 `ReparsePoint`；容错/计数逻辑原样保留。
2. `RunAsync` 内：先处理根级文件，再把根的直接子目录列表交给 `Parallel.ForEach`（DOP 按拍板值）跑子树 `Walk`；共享计数（`touched`）改 `Interlocked`；取消检查照旧每 256 条目一次（分片内）。
3. 分片完成/取消/异常在 `RunAsync` 聚合状态（`Cancelled` 优先于 `Completed`）；`FinishedAt`/耗时记录不变。
4. 测试：新增 `ParallelScan_MatchesSequentialExactly`（同树跑两遍比对字节/文件/目录/跳过数）、`ParallelCancel_ReturnsCancelled_PartialClosed`、`LargeFixture_CompletesUnder1500ms`；版本号 1.1.2。
5. 跑 §Verification 全部命令；证据落 `05-evidence.md`；审查落 `06-review.md`（轻量级别仍必须单独产出）。

### Test Plan
- 单测：上述三条新用例 + 既有 `FolderScanServiceTests` 全部回归。
- e2e：`e2e/plugins/file-tools` 现有 6 例（排行/快照/取消/中间态闭合/徽标/真实树）定向跑，截图读图。

### Verification
#### Build
```bash
cd Plugins/FileTools && dotnet build
cd Plugins/FileTools/web && pnpm run build
```
#### Unit Test
```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~FolderScan"
```
#### Integration Test
N/A —— 无 API 契约变化（端点/字段/鉴权全部不变），依据 §2 Compatibility。
#### E2E
```bash
cd ForgeSelf.Web && npx playwright test e2e/plugins/file-tools --output=.pw-out-perf
```
判定：读落盘输出 6/6 绿 + 截图 `ForgeSelf.Web/screenshots/e2e/file-tools/` 读图无 `.warn-line`。
#### Other Checks
- 前端门禁：`cd ForgeSelf.Web && pnpm run check && pnpm run test`（宿主前端本批零改动，仍按 §5.1 必跑项执行）。
- 档位声明（输入14）：本任务 = **快档**。不跑中档理由：不碰 `ForgeSelf.Api/**`、不碰 `scripts/**`、不碰共享测试夹具；不跑深档理由：不碰 `e2e/global-setup.ts` / `playwright.*.config.ts` / `e2e/fixtures/**`。
- 交付后（启用 1.1.2 后）：`plugin-publish-verify`「运行实例只读复验」（token 预检 → 徽标 v1.1.2 → 主链路 + 中间态采样 → 截图 `screenshots/live-51888/` 读图），只读边界不变。

### Plan 偏差记录
暂无。执行中发现与本 Plan 不符处，先记录于此并修正 Plan，不得直接绕过。

## 4. Task

### Task ID
PILOT-perf-1.1.2

### Objective
`Plugins/FileTools` 的目录扫描在同树实测 ≤ 3 s（原 22 s），对外契约与中间态自洽性不变，以 1.1.2 侧载交付、宿主不重启。

### Scope
#### Allowed
- 修改：`FolderScanService.cs`、`FolderScanServiceTests.cs`、`Plugins/FileTools/plugin.json`、`Plugins/FileTools/web/package.json`。
- 新增测试：仅限 `FolderScanServiceTests.cs` 内。
#### Forbidden
- 宿主任何文件（`ForgeSelf.Api/**`）、仓库脚本（`scripts/**`）、共享 e2e 基建（`global-setup`/`fixtures`/`playwright.*.config`）。
- 缓存、懒展开、删除能力（批次D 范围）；`FileStatsService` 既有方法；API 契约变更；新增 NuGet/npm 依赖；构建目标变更。
- 未过闸门2 前 commit/push/tag；替用户在 :51888 点「更新」；停/启/杀任何宿主进程。

### Acceptance Criteria
- [ ] 同树 `Plugins/` 扫描 ≤ 3 s（单测计时断言）
- [ ] 并行结果与单线程结果逐字段一致（字节/文件/目录/跳过数）
- [ ] 既有中间态闭合用例不改断言仍绿；新增并行取消用例绿
- [ ] `e2e/plugins/file-tools` 6/6 绿 + 截图读图无 `.warn-line`
- [ ] 快档四条命令全部实际执行并记录真实输出（`05-evidence.md`）
- [ ] 版本号 1.1.2（两处）

### Expected Files
- `Plugins/FileTools/Services/FolderScanService.cs`
- `ForgeSelf.Api.Tests/Unit/FolderScanServiceTests.cs`
- `Plugins/FileTools/plugin.json`
- `Plugins/FileTools/web/package.json`
- `docs/ai/pilot/folder-scan-perf-1.1.2/05-evidence.md`（完成后）
- `docs/ai/pilot/folder-scan-perf-1.1.2/06-review.md`（完成后）

### Verification Commands
```bash
cd Plugins/FileTools && dotnet build
cd Plugins/FileTools/web && pnpm run build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~FolderScan"
cd ForgeSelf.Web && pnpm run check && pnpm run test
cd ForgeSelf.Web && npx playwright test e2e/plugins/file-tools --output=.pw-out-perf
```

---

## 附录 A · 实测数据（Verified，2026-09-28，本任务立项依据）

同一棵树 `Plugins/`（51,931 文件 / 7,004 目录 / maxDepth 12 / 1,193 reparse），8 核，两个 pass；**7 个变体跑出的 files/bytes/links 完全一致**（探针自洽性证明）。

| 变体 | 说明 | pass1 / pass2 (ms) | 相对当前 |
|---|---|---|---|
| B1 | **当前实现**：`EnumerateFileSystemEntries` + 每条目 `File.GetAttributes` + `new FileInfo(e).Length`，单线程 | 22,721 / 22,049 | 1× |
| B2 | 只去掉多余的 `File.GetAttributes`（1 stat/条目） | 12,503 / 15,503 | ~1.6× |
| D | **B1 + 1.1.1 的锁内父链上卷** | 21,410 / 25,433 | ≈1×（噪声内） |
| A | 码神风格：`DirectoryInfo.GetFiles()/GetDirectories()` 批量数组 | 3,142 / 2,239 | ~8× |
| A2 | **流式** `DirectoryInfo.EnumerateFiles()/EnumerateDirectories()` | 2,178 / 3,134 | ~8× |
| C | B1 + 并行 DOP=8 | 8,457 / 12,256 | ~2–2.8× |
| E | **A2 + 并行 DOP=8** | **671 / 2,182** | **~10–30×** |

结论：① 主因是 per-file 两次元数据调用（枚举记录自带 size，A2 证明流式同样快）；② 单线程是第二因；③ **1.1.1 的父链上卷几乎不花钱**（D ≈ B1）→ 提速不需要牺牲任意时刻分区闭合。线上锚点吻合：101,994 文件 / 65,973 ms = 647 µs/文件，正是 B1 形态。

## 附录 B · 与码神/XCoder 参考实现的对照（输入12，仅采纳与性能相关项）

| 维度 | 码神 / XCoder | 当前实现 | 本补丁 |
|---|---|---|---|
| 枚举 | `GetFiles()` 批量，Length 自带 | `EnumerateFileSystemEntries` 只给路径 | ✅ 流式 `EnumerateFiles`（保留「不物化整棵树」性质） |
| 每文件元数据 | 0 次 | 2 次 | ✅ 降为 0 次 |
| 并行 | 每节点 `Task.Run` | 单线程 | ✅ 子树并行 DOP≤8 |
| 懒展开（未展开显示 -1） | 有 | 无 | ❌ 批次D |
| `MemoryCache` 30 s | 有 | 无 | ❌ 批次D（Top-N 排行必须全树，缓存替代不了） |
| 回收站删除 | `item.Delete()`（永久删，不合规） | 无 | ❌ 批次D（届时自写 `SHFileOperation` 送回收站） |
| 中间态分区闭合 | 无此要求 | 有（1.1.1） | 保留（实测不花钱） |
| 取消/上限/reparse/权限 | 无 | 有 | 保留 |

## 附录 C · 探针合规交代
A/B 计时用 `pwsh + Add-Type` 内联 C#，脚本放**仓库外** `%TEMP%\probe-folderscan.ps1`，**跑完已删**（`ls` 确认不存在）；不进版本控制、不作为验证结论（`AGENTS.md` §5.3 红线）。正式验证以 §Verification 各条命令的真实落盘输出为准。

## 附录 D · 历史工件纠偏记录
2026-09-28 曾产出 `docs/ai/pilot/batch-c-folder-size-plugin/08-mini-task-perf-1.1.2.md`：① 命名自创（规范 §4 固定 `mini-task.md`）② 错放批次C 目录（规范：每任务一目录）③ 章节结构未按模板 → **已作废留档**（文件顶部标注作废原因并指向本文件；删除动作被权限分类器拦下，留档也符合项目「被推翻的判断原文留存」惯例），以本文件为准。该次违规已写入 `AGENTS.md` §3.1 与用户记忆。
