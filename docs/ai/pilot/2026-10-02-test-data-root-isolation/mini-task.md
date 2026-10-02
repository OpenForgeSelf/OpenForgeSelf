# mini-task：dotnet test 数据根自动隔离，杜绝本地开发污染真实宿主根 ~/.forgeself（输入4 · 2026-10-02）

> 级别：轻量（2 个测试文件 + 1 处文档同步；含 1 个临时探针清理）。Intent/Spec/Plan/Task 合并本文件；Evidence/Review 单独产出。
> 闸门1：**已批准**（2026-10-02，用户经 AskUserQuestion 拍板「批准，开工实施」；隔离范围「仅 FORGESELF_DATA_ROOT」，不重定向 TEMP/TMP）。

## Intent（为什么 / 做什么 / 到什么程度）
- **Problem**：`dotnet test`（`ForgeSelf.Api.Tests`）默认既不设 `FORGESELF_DATA_ROOT`、也不设 `ASPNETCORE_ENVIRONMENT=Development`，于是测试进程内 `DataLocationService.ResolveHostDataDirectory()` 回落到**真实宿主数据根** `%UserProfile%/.forgeself`。凡 `WebApplicationFactory<Program>` 用例触发 `Program.cs` / `AppBuilder` 顶层代码，就会把日志、配置、数据库连接串全部指向真实宿主根：
  - `Program.cs:36-41` / `AppBuilder.cs:87-107`：`NewLife.Setting.Current.LogPath = ~/.forgeself/log`、`XTrace.LogPath = ~/.forgeself/log`、`Setting.Save()`、`ConfigUnifier.UnifyAllConfigFiles(~/.forgeself/config)`；
  - `XCodeConfig.AddXCode(...)`：连接串指向 `~/.forgeself/ForgeSelf.db` 与 `~/.forgeself/plugins/*/*.db`；
  - `InitializeXCodeDatabase` 仅在 `env=="Testing"` 时早退，而 WAF 默认环境非 Testing → 存在在**真实库建表**的路径。
- **Why**：本地开发（尤其自动化测试）必须与真实宿主数据根**物理隔离**；隔离不能依赖操作者手工命令行前缀（`agent-workflow.md` §B12#830 的「跑前手工赋值」），实测**易忘且无强制**——本会话就忘设过。
- **Expected Outcome**：`dotnet test` 在**无任何手工前缀**时，测试进程数据根自动落在**仓库内隔离目录**（`.temp/dotnet-test/<时间戳>-<pid>`，`.temp/` 已 gitignore）；真实 `~/.forgeself` 零写入。e2e（`FORGESELF_DATA_ROOT`）/CI/操作者显式指定仍最优先。
- **Constraints**：只改测试项目（`ForgeSelf.Api.Tests`）+ 1 处文档；**不动 `DataLocationService` 生产回落语义**（`DataLocationServiceTests` 断言默认路径语义，改动会连红）；不加新依赖；不碰 e2e 基建。
- **Success Criteria**：
  1. 不加任何前缀跑 `dotnet test`，测试进程解析出的数据根 ≠ `~/.forgeself`；
  2. 跑前/跑后 `~/.forgeself`（含 `log/`、`config/`、`*.db`）文件 mtime **无变化**（实证）；
  3. 守卫测试先红后绿（停用 ModuleInitializer 见红 → 恢复见绿）；
  4. 显式 `FORGESELF_DATA_ROOT` / `ASPNETCORE_ENVIRONMENT=Development` 仍被尊重，既有 `DataLocationServiceTests`/`DataLocationOverrideTests`/`ForgeConfigTests` 保持绿。

## Spec（规格）
- **Functional Requirements**：
  1. 新增测试侧 `[ModuleInitializer]`：在测试程序集加载时（任何用例执行前）执行，仅在 `FORGESELF_DATA_ROOT` **未设置或空白**时，把它设为本进程专属的仓库内隔离目录。
  2. 已显式设置 `FORGESELF_DATA_ROOT`（e2e/CI/操作者前缀）→ **不覆盖**，保持现有隔离链路不变。
  3. 隔离目录 = `<仓库根>/.temp/dotnet-test/<yyyyMMdd-HHmmss>-<进程号>`（仓库根 = 从 `AppContext.BaseDirectory` 向上找到含 `ForgeSelf.slnx` 的目录）；进程号避免并行/多 worktree 撞目录。
  4. 新增守卫测试：断言「测试进程解析出的数据根 ≠ 真实 `~/.forgeself`」且「落在仓库内 `.temp/dotnet-test/` 下」；挂 `EnvVarIsolation` 集合（与改环境变量的测试串行）。
  5. 文档：`docs/04-standards/agent-workflow.md` §B12#830 由「手工前缀必须设 `FORGESELF_DATA_ROOT`」改为「已自动隔离，手工前缀仅在需要指定位置/覆盖时使用」。
- **Input / Output / Boundary**：输入=测试进程启动时的环境变量；输出=被设置的 `FORGESELF_DATA_ROOT`。无网络、无外部依赖。
- **Error Handling / Compatibility**：隔离目录创建失败不阻断测试（兜底继续，仅告警）；e2e 已设 `FORGESELF_DATA_ROOT` → 本机制短路，二者不冲突。
- **Acceptance Criteria**：见 Intent·Success Criteria 1-4。

## Plan（具体到文件）
1. **新增** `ForgeSelf.Api.Tests/TestDataRootIsolation.cs`：`internal static class` + `[ModuleInitializer] internal static void Initialize()`；含 `FindRepoRoot()`（复刻 `DesignSystemAuthTests` 的 `ForgeSelf.slnx` 向上查找写法）。
2. **新增** `ForgeSelf.Api.Tests/TestDataRootIsolationGuardTests.cs`：`[Collection("EnvVarIsolation")]` 的守卫测试（红/绿判据）。
3. **修改** `docs/04-standards/agent-workflow.md` §B12 第 830 行：表述由「跑前手工赋值 `FORGESELF_DATA_ROOT`」改为「测试进程已由 `[ModuleInitializer]` 自动隔离，手工仅用于覆盖」；§Part C 变更记录补一行。
4. **删除** `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/_ProbeToCssNoDb.cs`：M2 临时探针（M2 已暂缓，按红线「用完即删、不进版本控制」清理）。
5. **不碰**：`ForgeSelf.Api/DataLocationService.cs`（生产语义冻结）、`ForgeSelf.Web/e2e/global-setup.ts`（e2e 隔离已就绪）、`ForgeSelf.Core.Tests` / `ForgeSelf.Abstractions.Tests`（不引用 `Program`/`DataLocationService`）。

## Task（工作单元）
- **Objective**：让 `dotnet test` 在无手工前缀时自动把数据根隔离到仓库内目录，真实 `~/.forgeself` 零写入。
- **Scope Allowed**：上述 2 新增 + 1 修改 + 1 删除；跑后端过滤集/守卫测试验证；更新日记/TODO/工件。
- **Scope Forbidden**：改 `DataLocationService` 生产回落逻辑；停/启/杀用户运行中的宿主进程；未授权 `git commit/push/tag`；把探针脚本作为验证手段长期留存。
- **Acceptance Criteria（checkbox）**：
  - [x] `TestDataRootIsolation.cs` 落地（ModuleInitializer，尊重已设值）
  - [x] 守卫测试先红后绿（TDD：停用机制见红 → 恢复见绿）——注：实现期以「残留 Core.config 存在 → 写宿主日志失败」复现红灯，机制落地后转绿；4 守卫最终 4/4 绿
  - [x] 无手工前缀跑过滤集，`~/.forgeself` mtime 跑前跑后无变化（测试进程不再触达宿主路径；bin 两处 `Config/` 清零且不再生成）
  - [x] `DataLocationServiceTests` / `DataLocationOverrideTests` / `ForgeConfigTests` 保持绿（回归过滤集 6 红与本 worktree 环境基线**完全一致**，无新增）
  - [x] `agent-workflow.md` §B12 + 变更记录同步
  - [x] `_ProbeToCssNoDb.cs` 已删
  - [x] 05-evidence / 06-review 产出
- **Verification Commands**（本 worktree 沙箱需 `dangerouslyDisableSandbox: true`）：
  - 守卫测试（红/绿）：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TestDataRootIsolationGuardTests"`
  - 数据根相关回归：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DataLocation|FullyQualifiedName~ForgeConfig"`
  - 污染实证：跑前/跑后 `Get-ChildItem "$env:USERPROFILE\.forgeself" -Recurse | Select FullName,LastWriteTime` 对比