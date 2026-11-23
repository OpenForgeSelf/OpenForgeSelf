---
id: PC-041
title: 版本目录名第 4 段为 10 位时间码，[version] 分量解析抛 OverflowException
tags: [update-agent, version-sort, powershell, overflow, pc041]
invariant: 对版本目录名/发行串的比较一律补零定宽（PadLeft(12,'0')）+ ordinal 字符串序；禁止 [version]/System.Version 分量解析
status: guarded
first_seen: 2026-11-21
recurrences: []
cases: [2026-11-21-update-agent-version-sort-overflow]
signature: []
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Api.Tests/RepositoryScriptTests.cs", status: active, note: "三处同源 PadLeft(12,'0') 断言 + UpdateAgentCore 排序单测"}
lens:
  - 环境定时炸弹：时间码随时间单调增大，2026-10-02 时间码方案起约 7 周后必炸（越晚必炸）。
---

# PC-041 — 版本目录名第 4 段为 10 位时间码，[version] 分量解析抛 OverflowException

- **date**: 2026-11-21
- **severity**: Major
- **category**: 更新链路 / 版本比较

## 1. 症状

宿主自更新「版本保留」步骤抛「无法将值 "2.4.22.2611131611" 转换为 System.Version」（实机
`agent-20261121203854.log` 实录）。此时新版本已落 `versions/<newVer>/`、current 已切换——
只有清理被中断，且更新被误报为失败。

## 2. 根因

2026-10-02 起发行串 = `<major>.<minor>.<patch>.<yyMMddHHmm>`，第 4 段是 **10 位时间码**
（如 `2611211942` ≈ 2.6e9 > `int.MaxValue` ≈ 2.1e9）。凡对版本目录名做 `[version]` /
`System.Version` 分量解析的代码，一碰到时间码段就抛 OverflowException：

| 位置 | 用途 | 修复前 |
| --- | --- | --- |
| `scripts/update-agent.ps1` 版本保留步 | Sort-Object `[version]` 选上一版 | 抛（实机日志实锤） |
| `scripts/publish-plugin.ps1` | `ForEach-Object { [version]$_.Name }` 选最高已发布版本 | 同型隐患 |
| 原生代理（本批新增 `UpdateAgentCore`） | 版本排序/保留 | 立法即按本 PC 修复 |

## 3. Canonical fix（补零定宽 + ordinal）

各段 `PadLeft(12,'0')` 补零定宽后做字符串 ordinal 比较：等长补零数字串的字符串序 = 数值序，
任意段长无溢出；段数不一致时短串在前（与 `System.Version` 语义一致）。

- C#：`ForgeSelf.UpdateAgent/UpdateAgentCore.cs` `ToSortKey`（`Split('.') → PadLeft(12,'0') → join('|')`）
- PowerShell：`update-agent.ps1:175` 与 `publish-plugin.ps1:111-118` 同源（`-join '.'` / `-join '|'` 等价）

## 4. 守卫（G1 active）

- `ForgeSelf.Api.Tests/RepositoryScriptTests.cs`：仓库级守卫断言三处同源均含 `PadLeft(12, '0')`，删除任一处即红；
- `UpdateAgentRunnerTests`：`ToSortKey`/`CompareVersionNames`/`SortNewestFirst` 对 10 位时间码、段数不一致、null/空串的排序语义全覆盖。

## 5. 案例列表

- [2026-11-21-update-agent-version-sort-overflow](../cases/2026-11-21-update-agent-version-sort-overflow.md)（首例，2026-11-21）

## 6. 同类扫查结论（2026-11-21）

全仓 grep `[version]` / `System.Version` 对**版本目录名/发行串**的解析：上述三处即全集
（`new-version.ps1` 只打印不比较；`HostInstallRoot` 不解析版本；插件裁决用字符串，
畸形版本视为相等的语义已在 §1.7 注明）。逃逸风险集中在脚本侧：新增 PowerShell/C# 代码
对版本目录名排序时必须复用本 PC 的补零定宽模式，禁止引入 `[version]`。
