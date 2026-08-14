# 015 · 系统监控（System Monitor）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

实时系统资源监控，覆盖 CPU、内存、磁盘（含 IO）、网络（含连接）、进程五大维度，支持历史曲线查询与实时推送（SignalR Hub）。监控数据通过环形缓冲（`CircularBuffer`）保活近期窗口。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/SystemMonitor/Controllers/SystemMonitorController.cs`（`[Route("api/monitor")]`） |
| 实时通道 | `Plugins/SystemMonitor/Hubs/MonitorHub.cs`（SignalR） |
| 服务 | `Plugins/SystemMonitor/Services/`：`CpuMonitorService`/`MemoryMonitorService`/`DiskMonitorService`/`NetworkMonitorService`/`ProcessMonitorService`（对应接口 `ICpuMonitorService` 等） |
| 模型 | `Plugins/SystemMonitor/Models/`：`CpuMonitorModels`/`MemoryMonitorModels`/`DiskMonitorModels`/`NetworkMonitorModels`/`ProcessMonitorModels`/`MonitorHistoryModels` |
| 工具 | `Plugins/SystemMonitor/Services/CircularBuffer.cs` |
| 前端视图 | `src/views/SystemMonitorView.vue` |
| 前端服务 | `src/services/systemMonitorApi.ts` |

## 核心 API（`api/monitor`）

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/cpu`、`/cpu/history` | CPU 实时/历史 |
| GET | `/memory`、`/memory/history` | 内存实时/历史 |
| GET | `/disks`、`/disks/{drive}/io`、`/disks/{drive}/history` | 磁盘与 IO |
| GET | `/network`、`/network/connections`、`/network/history` | 网络 |
| GET | `/processes`、`/processes/{pid}` | 进程列表/详情 |
| DELETE | `/processes/{pid}` | 结束进程 |
| GET | `/overview` | 总览快照 |

## 使用要点

- 历史数据由 `CircularBuffer` 在内存维护，非长期持久化。
- 实时刷新走 `MonitorHub`（SignalR），前端订阅推送。
