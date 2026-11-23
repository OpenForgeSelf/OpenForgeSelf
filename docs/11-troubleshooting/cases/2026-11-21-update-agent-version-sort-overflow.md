---
id: 2026-11-21-update-agent-version-sort-overflow
date: 2026-11-21
pc: PC-041
severity: Major
status: closed
---

# Case 2026-11-21-update-agent-version-sort-overflow — 自更新版本保留步 System.Version 溢出误报失败

- **date**: 2026-11-21
- **pc**: [PC-041](../../problem-classes/PC-041-update-agent-version-sort-overflow.md)
- **severity**: Major
- **evidence**: Verified（实机日志 + 修复后复跑）

## 现象

宿主自更新（ps1 轨）走到「版本保留」步骤抛：

```
无法将值"2.4.22.2611131611"转换为类型"System.Version"。错误:"值对于 Int32 太大或太小。"
```

实机日志：`%LOCALAPPDATA%\ForgeSelf\Logs\agent-20261121203854.log`。此时新版本已落
`versions/<newVer>/`、current 已切换——清理被中断，更新整体被误报失败。

## 定位（读码 + 日志对表）

1. 发行串第 4 段 = 10 位时间码 `yyMMddHHmm`（2026-10-02 输入9/输入10 起），`2611131611` > `int.MaxValue`；
2. `update-agent.ps1` 版本保留步用 `Sort-Object { [version]$_.Name }` 选「上一版」，分量解析即抛；
3. 时间越晚（时间码越大）越必炸——自 2026-10-02 时间码方案起 7 周后所有实例命中，属**定时炸弹**型类缺陷。

## 修复（canonical fix，三处同源）

各段 `PadLeft(12,'0')` 补零定宽 + 字符串 ordinal 比较（等长补零数字串的字符串序 = 数值序，无溢出）：

- `scripts/update-agent.ps1:175`（本 case 首发现处）
- `scripts/publish-plugin.ps1:111-118`（同类扫查命中，同型隐患）
- `ForgeSelf.UpdateAgent/UpdateAgentCore.cs` `ToSortKey`（本批新增原生代理，立法即按本模式）

## 验证

- 修复后全流程更新在模拟布局复跑通过（版本保留 = current + 上一版，其余清理）；
- 守卫：`RepositoryScriptTests` 三处同源断言（删除任一 `PadLeft(12, '0')` 即红）+ `UpdateAgentCore` 排序单测（含 10 位时间码边界）；
- 全量后端测试基线对表无新增红（3956 总/3924 过/32 基线败，失败名集合与晨跑基线一致）。

## 悬空教训

- 无悬空。同类扫查结论与守卫落位见 PC-041 §6。
