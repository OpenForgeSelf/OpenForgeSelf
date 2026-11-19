---
feature_key: F008
feature_no: 008
status: implemented
last_updated: 2026-11-19
aliases: ["008-tray-service-autoupdate"]
---

# 008 托盘常驻与自更新 — 功能需求与设计

> 功能编号：008
> 状态：已实现
> 关联：09-operations（部署运维）（历史 specs/008 已弃用）
> 最后更新：2026-11-19

## 1. 功能需求

### 1.1 背景
桌面应用需托盘常驻（后台运行、一键唤起），并支持检查更新、下载安装、记录更新操作。

### 1.2 目标
1. 系统托盘图标与菜单在独立程序 `ForgeSelf.Tray`；
2. API 在 Windows 上拉起托盘；检查更新走 `POST /api/update/check`；
3. 更新操作记录落库（`UpdateTrace` 实体）。

## 2. 设计

### 2.1 进程
API（`ForgeSelf.exe`，`net10.0`）只跑 Kestrel。托盘是并列程序 `ForgeSelf.Tray.exe`（`net10.0-windows`）。Windows 上且 `FORGESELF_NO_TRAY` 不是 `1` 时，API 拉起托盘。Linux 不拉起。

| 位置 | 职责 |
|------|------|
| `ForgeSelf.Tray/TrayIconManager.cs` | 托盘图标与菜单 |
| `ForgeSelf.Tray/Program.cs` | 读管道、按 `--role` 决定退出是否停 API |
| `ForgeSelf.Api/Services/TrayHostLauncher.cs` | 判断要不要拉起，并找到托盘 exe |
| `ForgeSelf.Api/Services/TrayProcessStarter.cs` | 拼启动参数、拉起托盘进程 |
| `ForgeSelf.Host` | 数据根、配置、令牌读取、服务安装 |

交互模式：托盘「退出」向管道写 `shutdown`，API 停止。服务模式：托盘退出只结束托盘；服务停止时 API 再写 `shutdown`。菜单「检查更新」调用已有 `POST /api/update/check`。服务安装的可执行文件是 API 入口，不是托盘 exe。

目录与发布落位以 `docs/04-standards/packaging-upgrade-backup.md` §1.6 为准。

### 2.2 实体
`Entities/UpdateTrace.cs`（更新操作记录：前/后版本、结果状态、耗时、错误信息等），实现 `IUpdateTraceModel`。

## 3. 使用指南
托盘右键：打开主界面、检查更新、安装/卸载/重启服务、关于、退出。检查更新走 API 的更新接口。更新过程仍在 `UpdateTrace` 留痕。

## 4. 注意事项
- 自更新涉及进程重启，需在 09-operations 的发布流程中验证（当前未做自动化 e2e）。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `UpdateTrace` 相关单测 | 实体落库、记录查询 |
