---
feature_key: F026
feature_no: 026
status: implemented
last_updated: 2026-10-06
aliases: ["026-plugin-marketplace"]
---

# 026 · 插件体系与插件市场（Plugin System & Marketplace）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

OpenForgeSelf 采用**插件化架构**：功能以插件形式装载，每个插件含 `plugin.json` 清单 + 控制器/服务/实体。核心 `PluginManager` 负责发现、加载（独立 `PluginLoadContext`）、启用/禁用。前端提供插件市场浏览、详情、导入导出、脚手架、更新等完整管理界面。

## 代码落点

| 层 | 文件 |
|----|------|
| 核心框架 | `Plugins/PluginManager.cs`、`ExtensionPointManager.cs`、`PluginLoadContext.cs`、`PluginContext.cs`、`ServiceCollectionExtensions.cs`、`DefaultPermissionChecker.cs`、`README.md` |
| 已装载插件 | `Plugins/` 下：`ScriptRunner/`、`WorkflowEngine/`、`MemorySystem/`、`SystemMonitor/`、`FileTools/`、`DevTools/`、`TextTools/`、`QuickLinks/`、`AIAgent/`、`Scheduler/`、`TodoTracker/`（各含 `plugin.json`） |
| 前端视图 | `src/views/PluginStore.vue`（市场）、`PluginPage.vue`、`PluginDetail.vue`、`PluginImportExport.vue`、`PluginScaffolder.vue`、`PluginUpdates.vue` |
| 前端服务 | `src/services/pluginApi.ts` |

## 插件清单结构（`plugin.json`）

每个插件目录含 `plugin.json`，声明插件元数据（id/名称/入口/依赖）。`PluginManager` 启动时扫描并加载。

## 核心能力

- **动态加载**：通过 `PluginLoadContext` 隔离程序集，支持热装载。
- **启用/禁用**：各插件在 `api/<plugin>/` 下暴露能力；启用状态由框架维护。
- **市场**：`PluginStore` 浏览可用插件，`PluginDetail` 查看详情，`PluginImportExport` 导入导出，`PluginScaffolder` 脚手架生成新插件骨架，`PluginUpdates` 检查更新。

## 已知问题（见 TODO T032）

- 内嵌插件（随主程序集打包）无法经 `/api/plugin/{id}/enable` 启用——`EntryAssembly` 指向主程序集，`PluginManager` 找不到 dll，状态恒为 `isEnabled=false`，应识别内嵌插件跳过程序集加载。
- 部分插件（QuickLinks/Scheduler/WorkflowEngine/ScriptRunner）分页 `total=0`——`RetrieveTotalCount` 未开启（TodoTracker 已修）。

## 使用要点

- 新增插件：在 `Plugins/<Name>/` 下放 `plugin.json` + 实现，框架自动发现。
- 插件能力路由统一为 `api/<plugin>/`（如 `api/workflows`、`api/memory`），详见各功能文档（012–021）。
