---
feature_key: F025
feature_no: 025
status: implemented
last_updated: 2026-10-06
aliases: ["025-user-profile"]
---

# 025 · 用户配置（User Profile）

> 状态：已实现（前端视图存在；后端暂无独立 Profile 模块）
> 最后更新：2026-08-13

## 概述

用户个人配置/偏好界面。前端 `ProfileView.vue` 提供个人资料与偏好设置入口。**后端经代码核查未找到独立的 `Profile` 实体或 `ProfileController`**——用户级配置目前内嵌于应用配置/前端本地或借由其他接口承载，尚未抽象为独立后端模块。

## 代码落点

| 层 | 文件 |
|----|------|
| 前端视图 | `src/views/ProfileView.vue` |
| 后端 | 暂无独立 `Profile` 实体 / 控制器（Grep `Profile` 在 `Controllers/`、`Services/`、`Entities/` 无对应实现） |

## 说明

- 本文档如实标注：**后端 Profile 能力缺口**。若后续需持久化用户偏好（主题/默认 provider/快捷键等），应新建 `ProfileController` + `UserProfile` 实体，并与前端 `ProfileView` 对接。
- 当前用户级配置可能经由 `ForgeSetting`（全局配置，见 009）或前端本地存储承载。

## 待办

- 确认 Profile 配置的实际存储位置（全局 vs 用户级）。
- 若需独立用户配置，登记 spec 并补齐后端模块。
