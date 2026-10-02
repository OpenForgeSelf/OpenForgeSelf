# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。

## Task

`2026-10-02-test-data-root-isolation`（轻量档；输入4）

## Changed Files

- `ForgeSelf.Api.Tests/TestDataRootIsolation.cs`（新增）
- `ForgeSelf.Api.Tests/TestDataRootIsolationGuardTests.cs`（新增）
- `docs/04-standards/agent-workflow.md`（§B12 第 830 行 + Part C 变更记录）
- `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/_ProbeToCssNoDb.cs`（删除，M2 临时探针）
- `docs/ai/pilot/2026-10-02-test-data-root-isolation/mini-task.md`（闸门/checkbox 状态更新）
- 删除 `ForgeSelf.Api.Tests/bin/**/Config/`、`ForgeSelf.Api/bin/**/Config/` 污染残留（未入库产物）

## Build

Command:

```bash
dotnet build ForgeSelf.Api.Tests
```

Result: PASS（来源等级：Verified）——随 `dotnet test` 隐式构建通过，0 error（详见 Test 段）。

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TestDataRootIsolationGuardTests"
```

Result: PASS（来源等级：Verified）

```text
已通过! - 失败: 0，通过: 4，已跳过: 0，总计: 4，持续时间: 13 ms - ForgeSelf.Api.Tests.dll (net10.0)
```

4 条守卫（数据根 ≠ 真实宿主根 / 自动隔离落在仓库内 / 日志路径不落宿主根 / 配置文件不落宿主根）全绿。

## 根因证据链（Verified）

1. **污染链实证**：测试详细日志出现 `尝试写日志文件失败：Access to the path 'C:\Users\Administrator\.forgeself\log\2026_10_02.log' is denied.`——证明 `dotnet test` 触及真实宿主路径（沙箱/宿主句柄拦截）。
2. **残留配置定位**：程序目录 `ForgeSelf.Api.Tests/bin/Debug/net10.0-windows/Config/Core.config` 实存，内容为宿主机 `Core.config`（`<LogPath>C:\Users\Administrator\.forgeself\log</LogPath>`）；同目录另有残留 `XCode.config`（含连接串）。
3. **决定性判别实验**：删除 `bin/Config` 残留后重跑，`Setting.LogPath` 恢复默认值 `Log`（不再指宿主）→ 证实**残留文件是绕过隔离的直接来源**。
4. **顺序缺陷实证**：`Program.cs:38` / `AppBuilder.cs:89` 的 `NewLife.Setting.Current.Save()` 早于 `ConfigUnifier` 重定向执行 → 在**程序目录**写出固化宿主 `LogPath` 的 Core.config（宿主侧关联缺陷，本轮未修，已记 TODO）。

## 修复验证（Verified）

| 验证项 | 方法 | 结果 |
|--------|------|------|
| 数据根自动隔离 | 无手工前缀跑 `dotnet test` | 隔离根 = `.temp\dotnet-test\20261002-120925-73848`（仓库内，`.temp/` 已 gitignore） |
| 日志路径归位 | 读隔离 `Core.config` | `<LogPath>D:\...\.temp\dotnet-test\20261002-120925-73848\log</LogPath>` ✓ |
| 配置文件名归位 | 隔离目录清单 | `config\{Core,XCode,Agent}.config` 均落在隔离根 ✓ |
| 程序目录零残留 | 检查两处 bin `Config/` | `ForgeSelf.Api.Tests\bin\...\Config` 与 `ForgeSelf.Api\bin\...\Config` **均为空**（不再生成） |
| 宿主日志零写入 | 测试输出 | 修复后测试输出**不再出现** `~/.forgeself` 写失败 |

## Integration Test

Result: N/A（本次为测试基建隔离修复，无接口/业务逻辑变更；WAF 集成用例的隔离链路已由守卫测试覆盖）。

## E2E

Result: N/A（依据：未改动 `e2e/**`；e2e 已显式设 `FORGESELF_DATA_ROOT`，本机制对其短路，链路不变）。用户批准范围为「仅 FORGESELF_DATA_ROOT」，未触发深档。

## Static Analysis

Result: N/A（未改动前端；`pnpm run check` 不适用）。

## Screenshots

N/A

## 回归对表

Command:

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DataLocation|FullyQualifiedName~ForgeConfig|FullyQualifiedName~AppBuilder|FullyQualifiedName~ConfigUnifier"
```

- 修复后：`失败: 6，通过: 21，总计: 27`
- 修复前同过滤集基线：`失败: 6，通过: 17，总计: 23`
- **失败集完全一致**（无新增红）：`AppBuilderWebRootTests` 3 例（`Path.GetTempPath()` → `C:\Users\Administrator\AppData\Local\Temp` 访问被拒）+ `ConfigUnifierTests` 2 例 + `ConfigUnifierQaAdversarialTests` 1 例，均系本 worktree 沙箱/临时目录环境基线红，与本次改动无关。通过数 +4 = 新增 4 守卫。
- `ScriptRunnerDiIntegrationTests`：修复前后均 `失败: 1`（`GetRuntimes_ShouldResolvePluginControllerThroughCordisContext`），基线红，非本次引入。

## Known Limitations

- 隔离目录按 `yyyyMMdd-HHmmss-<pid>` 命名，每次 `dotnet test` 生成一个新目录（`.temp/` 已 gitignore，不污染工作区、不入库）；长期累积需依赖 `.temp/` 清理，未做自动清理。
- 宿主侧顺序缺陷（`Program.cs:38` / `AppBuilder.cs:89` 的 `Save()` 早于 `ConfigUnifier`）未修——生产 publish 程序目录仍可能残留 Core.config。已记入 `TODO.md`，本轮范围外（用户批准范围=仅 `FORGESELF_DATA_ROOT` 隔离）。

## Unresolved Issues

无（本次任务范围内）。宿主侧顺序缺陷为**已知关联缺陷**，非本任务范围，另立 TODO 跟踪。