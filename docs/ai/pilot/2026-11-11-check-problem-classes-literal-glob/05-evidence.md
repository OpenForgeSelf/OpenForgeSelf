# Evidence — check-problem-classes 字面路径 glob 短路（lite）

> 收口：2026-11-11｜分级：Verified / Inferred / Unknown

## Validation（实际执行）

| 项 | 命令 | 结果 | 级别 |
| --- | --- | --- | --- |
| 修复前基线（卡死取证） | `pwsh scripts/check-problem-classes.ps1 *> log`（17:45:53 起） | log 17:47 即停在 `[scan] PC-022 完成`，此后 25 分钟+ 无新增、进程 CPU 近零（87.83s→104.61s / 13min）——PC-023 处理中阻塞 | ✅ Verified（`.temp/tmp/check-problem-classes-final.log`） |
| 归因 | PC-023 S2 glob = 字面路径（无 `*`/`{`）→ `Get-FilesByGlob` 的锚点优化条件 `idx -gt 0` 不成立 → `Get-ChildItem -LiteralPath $repoRoot -Recurse -File` 全仓遍历（.git/node_modules/versions 全枚举后 where 丢弃） | 代码路径唯一，排除法成立（PC-001~022 均 ~90s 内完成，其 glob 皆锚定） | ✅ Verified（脚本 L99-L115 与 PC-023 front-matter） |
| 修复后单类 | `pwsh scripts/check-problem-classes.ps1 -PcId PC-023` | 秒级完成（见 07 记录的实测耗时） | ✅ Verified |
| 修复后全量 | `pwsh scripts/check-problem-classes.ps1`（28 个 PC） | 分钟级跑完，结果见 07 | ✅ Verified |
| 通配类无回归 | PC-025（themes/*.css）/ PC-026（Plugins/**/web/src/**/*.vue）/ PC-027（test 签名）随全量复跑 | 语义不变，判定结果与修复前可对比部分一致 | ✅ Verified |

## Unknown

- 全仓遍历若换机器/换盘可能更慢或偶发完成——不影响本修复的正确性（短路后该路径不再触发）。
- `RepositoryScriptTests`（scripts/** 守卫）覆盖：随中档全量 `dotnet test` 验证（与本批一起执行）。
