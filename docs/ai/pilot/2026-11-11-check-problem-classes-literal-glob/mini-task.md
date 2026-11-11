# Mini-Task — check-problem-classes 字面路径 glob 全仓遍历卡顿

> Task ID：`2026-11-11-check-problem-classes-literal-glob`｜日期：2026-11-11｜闸门1 依据：§8.5(1) 自动迭代（守卫/门禁维护自主完成）+ 本次「分批提交」任务（输入2）执行中暴露，完成后随批汇报

## 1 Intent（为什么 / 做什么 / 到什么程度）

- **为什么**：输入2 分批提交前的 `check-problem-classes.ps1` 全量扫描在 PC-023 卡死 25 分钟+（log 停在 `[scan] PC-022 完成`，进程 CPU 近零）；pre-commit hook 对 `docs/11-troubleshooting/` 提交也要跑同一全检 → 每次相关提交都会卡同样时长，阻塞本批提交链。
- **做什么**：`Get-FilesByGlob` 对**无通配符的字面路径**短路（`Test-Path` 直接取文件），不再落入 `Get-ChildItem -LiteralPath $repoRoot -Recurse -File` 全仓遍历（该遍历枚举 .git/node_modules/versions 全树，仅 where 过滤后丢弃）。
- **程度**：PC-023 单类扫描从 10min+（未完成）降到秒级；全检整体恢复分钟级；全部既有签名判定语义不变。

## 2 Spec（判据）

1. 字面路径 glob（如 `Plugins/AIAgent/Services/BuiltInAgentExecutionService.cs`，无 `*`/`{`）→ 直接 `Get-Item` 单文件，命中数与原语义一致（存在→1 文件、不存在→0 文件并报「glob 命中 0 文件」）。
2. 通配符 glob 行为完全不变（锚点优化、excludeRe、正则构造均不动）。
3. `check-problem-classes.ps1` 全量跑通且 OK（28+ PC 全绿）。

## 3 方案

`scripts/check-problem-classes.ps1` `Get-FilesByGlob` 函数头部加字面路径短路分支（约 8 行），复用既有 `$repoRoot`/分隔符归一。无其他文件改动。

## 4 改动清单

1. `scripts/check-problem-classes.ps1`：Get-FilesByGlob 字面短路。
2. 本目录 05/06/07（lite 收口）。

## 5 无界面交互

无界面（纯脚本守卫）。交互面 = CLI：`pwsh scripts/check-problem-classes.ps1 [-PcId PC-xxx] [-Baseline]`，行为不变、耗时大降；失败输出格式不变（`FAIL（N 项）` 列表）。

## 6 验证命令

```
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1        # 全量，预期 OK
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1 -PcId PC-023   # 单类，预期秒级完成
```

## 7 风险与回滚

风险：短路分支误伤带 `{a,b}` 或 `**` 的 glob → 判据 `[ * { ]` 二选一检测覆盖两类通配符；PC-026/PC-025/PC-027 等通配类全量复跑验证。回滚：`git checkout scripts/check-problem-classes.ps1`。
