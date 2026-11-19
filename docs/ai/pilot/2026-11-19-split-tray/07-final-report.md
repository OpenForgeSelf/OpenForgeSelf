# AI-Native Pilot Result（最终汇报）

## 1. Repository Understanding

宿主原先是 `net10.0-windows`，托盘与 Kestrel 在同一个 exe。本次把托盘拆出去，API 改为 `net10.0`。

## 2. Selected Task

split-tray：API 保持纯 Web API，Windows 托盘独立程序。本环境只验证 Linux 能做的部分。

## 3. Changed Files

见 `05-evidence.md` 的 Changed Files。

## 4. Validation

Build: `dotnet build` API、Tray、Tray.Tests 均为 0 error。Verified。

Unit Test: 过滤集 10 passed / 1 skipped。全量 3662 passed / 90 failed / 10 skipped。Verified。90 条是既有 Windows 假设。

E2E: 未跑。`dotnet run -- --console` 后 `GET /api/health` 返回 200。Verified。托盘菜单未点击。

## 5. Evidence

工件 `docs/ai/pilot/2026-11-19-split-tray/05-evidence.md`。AC1–AC4 Verified，AC5 仅编译 Verified。

## 6. Review

`06-review.md` 的 Final Decision 为 APPROVED。Windows 菜单不在本次验收范围。

## 7. Risk

L3。回滚是把托盘源码移回 API，目标框架改回 `net10.0-windows`，并恢复 UseWindowsForms 与 H.NotifyIcon。

## 8. Problems Found

Linux 上插件复制原先只替换反斜杠，并且目录名是 `Plugins`，运行时扫描的是 `plugins`。已改为两种斜杠都替换、目录用小写。`ServiceController` 在 Linux 上构造即抛 `PlatformNotSupportedException`，对应测试改为非 Windows 跳过。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

### 9.5 本批自复盘（必填 · 2026-10-31 用户复盘要求立）

- **经验提取（保留打法，2-4 条）**：先让 API 在 Linux 上听端口，再谈托盘。目标框架一改，路径大小写和反斜杠会同时露出来，复制规则要按运行时扫描的目录名写。
- **待改进（1-3 条）**：全量测试在 Linux 上有 90 条既有红，没有逐条改断言。Windows 环境跑全量时再对表。

## 10. 问题类归档

无。本次是已批准的架构拆分，不是缺陷修复。Linux 上暴露的路径大小写已在本改动里修掉，没有单独开问题类。

## 11. 重构提案

R0。拆分本身就是本任务，不再另提重构。

## 12. 悬空教训清单（必须为空）

空。Windows 托盘验收已写入根待办「Windows 环境验收托盘菜单」。
