# Review

## 审查八问（逐项回答）

1. 实现是否真正满足 Intent？满足 Linux 上能编能跑 API 这一半。Windows 托盘菜单按用户要求未在本环境验收。
2. 实现是否符合 Spec？API 为 `net10.0`，托盘为独立 `net10.0-windows` 程序，管道协议与 `ShouldLaunch` 有测试。检查更新与服务菜单的点击未跑。
3. 是否超出了 Scope？插件复制改到小写 `plugins`、夹具 Link、`ServiceController` 测试跳过，都是这次目标框架改动在 Linux 上直接撞上的。没有改 VersionPrefix，没有改插件目标框架。
4. 是否修改了不应该修改的文件？没有。历史 pilot 里的 `net10.0-windows` 字样未改。
5. 测试是否覆盖 Acceptance Criteria？AC1–AC4 有 Verified 证据。AC5 只覆盖编译。
6. 是否存在明显回归风险？输出目录从 `net10.0-windows` 换成 `net10.0`。漏改的本机脚本会去旧目录找 dll。本次改了 `dev-stack.ps1` 与测试复制路径。全量测试仍有 90 条既有 Windows 失败。
7. 是否存在架构不一致？托盘不再引用 API 工程。共享库 `ForgeSelf.Host` 保留原命名空间，避免调用点大改。
8. Evidence 是否足以证明任务完成？足以证明 Linux 上的 API。不足以证明托盘菜单。该项已留在根待办，不在本环境宣称完成。

## Requirement Check

PASS（范围按用户收窄为 Linux 验证 + Windows 延后）

## Scope Check

PASS

## Test Check

PASS（Linux 相关用例）

## Architecture Check

PASS

## Risk

L3

## Findings

### Critical

无。

### Major

无。Windows 托盘点击未测，这是用户明确延后的范围，不是漏测。

### Minor

全量 `ForgeSelf.Api.Tests` 在 Linux 上 90 失败。抽查不是本次逻辑回归。不在本任务里改那些 Windows 黄金值。

## Final Decision

APPROVED

Linux 上的拆分与 API 启动可以提交。Windows 托盘菜单、服务模式与发布包留到 Windows 环境再验。
