# 008 托盘常驻与自更新 — 功能需求与设计

> 功能编号：008
> 状态：已实现
> 关联：specs/008-tray-service-autoupdate/；09-operations（部署运维）
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
桌面应用需托盘常驻（后台运行、一键唤起），并支持检查更新、下载安装、记录更新操作。

### 1.2 目标
1. 系统托盘图标与菜单（`TrayIconManager`）；
2. 更新检查（`UpdateChecker`）与进程拉起（`TrayProcessStarter`）；
3. 更新操作记录落库（`UpdateTrace` 实体）。

## 2. 设计

### 2.1 关键组件（`ForgeSelf.Api/Services/`）
| 类 | 职责 |
|----|------|
| `TrayIconManager.cs` | 托盘图标/菜单/显隐 |
| `TrayProcessStarter.cs` | 拉起/管理后端进程 |
| `UpdateChecker.cs` | 检查更新（远端比对版本） |
| `Models/UpdateCheckResult.cs` `UpdateConfig.cs` `Models/Skills/UpdateSkillDto.cs` | 更新相关 DTO/配置 |

### 2.2 实体
`Entities/UpdateTrace.cs`（更新操作记录：前/后版本、结果状态、耗时、错误信息等），实现 `IUpdateTraceModel`。

## 3. 使用指南
托盘右键 → 检查更新 / 退出；更新过程在 `UpdateTrace` 留痕，可在管理面板查看。

## 4. 注意事项
- 自更新涉及进程重启，需在 09-operations 的发布流程中验证（当前未做自动化 e2e）。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `UpdateTrace` 相关单测 | 实体落库、记录查询 |
