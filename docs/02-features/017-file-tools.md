---
feature_key: F017
feature_no: 017
status: implemented
last_updated: 2026-10-06
aliases: ["017-file-tools"]
---

# 017 · 文件工具（File Tools）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

文件批处理能力：批量重命名（预览+执行）、清理（空文件夹/重复文件）、归档压缩与解压、目录统计（大小/大文件/类型分布）。所有操作支持预览，避免误删。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/FileTools/Controllers/FileToolsController.cs`（`[Route("api/filetools")]`） |
| 服务 | `Plugins/FileTools/Services/`：`RenameService`(IRenameService)、`CleanupService`(ICleanupService)、`ArchiveService`(IArchiveService)、`FileStatsService`(IFileStatsService)、`FileSizeFormatter.cs` |
| 模型 | `Plugins/FileTools/Models/`：`RenameModels`、`CleanupModels`、`ArchiveModels`、`FileStatsModels` |
| 前端视图 | `src/views/FileToolsView.vue` |
| 前端服务 | `src/services/fileToolsApi.ts` |

## 核心 API（`api/filetools`）

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `rename/preview`、`rename/execute` | 批量重命名预览/执行 |
| POST | `cleanup/preview`、`cleanup/execute` | 清理预览/执行 |
| POST | `cleanup/empty-folders` | 空文件夹扫描 |
| POST | `cleanup/duplicates` | 重复文件扫描 |
| POST | `archive/compress`、`archive/extract` | 压缩/解压 |
| GET | `archive/info` | 压缩包信息 |
| POST | `stats/directory`、`stats/large-files`、`stats/types` | 目录统计 |

## 使用要点

- 重命名/清理均先 `preview` 后 `execute`，前端展示影响清单再确认。
- 属本地文件系统操作，路径由前端传入，后端执行。
