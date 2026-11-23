# 03 Plan — update-restart-socket-10013

- **task**: 2026-11-21-update-restart-socket-10013
- **date**: 2026-11-21

## 步骤（可独立验证）

| # | 动作 | 文件 | 验证 |
| --- | --- | --- | --- |
| 1 | 新建更新代理工程（csproj/Program/Core/Runner） | `ForgeSelf.UpdateAgent/*` | `dotnet build ForgeSelf.slnx` 绿 |
| 2 | 出站自愈层 + AppBuilder 接线 | `ForgeSelf.Api/Services/Net/OutboundSocketResilienceHandler.cs`、`AppBuilder.cs` | 韧性/wiring 测试绿 |
| 3 | 宿主双轨选择链 | `ForgeSelf.Api/Services/StagedUpdateService.cs` | 读码 + E2E 选择链用例 |
| 4 | 测试两件 | `ForgeSelf.Api.Tests/Unit/UpdateAgentRunnerTests.cs`、`OutboundSocketResilienceHandlerTests.cs` | 过滤集 dotnet test 绿 |
| 5 | 打包链三脚本 + README | `scripts/release/publish-updateagent.ps1`（新）、`package-release.ps1`、`release-local.ps1`、`scripts/README.md` | 脚本语法 + 守卫 |
| 6 | PC-041 三处同源 | `UpdateAgentCore.cs`、`update-agent.ps1`、`publish-plugin.ps1` | RepositoryScriptTests 守卫 |
| 7 | 真源/台账回写 | `packaging-upgrade-backup.md`、`scripts/README.md`、index/PC-041/case | hygiene + Registry |
| 8 | csproj/slnx 登记 | `ForgeSelf.Api.csproj`（构建顺序引用）、`ForgeSelf.slnx` | 构建绿 |
| 9 | 全量后端测试对基线 | — | 基线失败名集合无新增 |

## 同类修复项（问题类闭环，属本任务范围）

- PC-041 同根因三处：`update-agent.ps1:175`、`publish-plugin.ps1:111-118`、新代码 `UpdateAgentCore.ToSortKey` 立法即修——非扩范围（problem-class-closure 同类扫查命中）。

## 偏差记录

- 中档全量首跑挂死（25 testhost 零 CPU）→ job_kill + 精确清批次进程 + `--blame-hang` 重跑收尾；
- 中档全量基线勘误：31 败 = 15 PWD + 6 WF + 3 Oar + 7 单发，DesignSystem 1 红属并行流（非本批）。
