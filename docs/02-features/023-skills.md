# 023 · 技能系统（Skills）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

可注册的技能（Skill）管理系统。技能是比 MCP 工具更上层的「能力单元」，可配置开关与参数，供 AIAgent（021）在规划/执行时选用（见 `AIAgent/Models/SkillDefinition.cs`）。后端以**核心控制器**形式提供。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Controllers/SkillsController.cs`（`[Route("api/skills")]`） |
| 服务 | `Services/Skills/SkillsService.cs`、`SkillEntity.cs` |
| 模型 | `Models/Skills/SkillItemDto.cs`、`SkillDetailDto.cs` |
| 前端视图 | `src/views/SkillsView.vue` |
| 前端服务 | `src/services/skillsApi.ts` |

## 核心 API（`api/skills`）

| 方法 | 路由 | 说明 |
|------|------|------|
| GET/POST/PUT | `/`、`/{id}` | 技能 CRUD |
| POST | `/{id}/toggle` | 开关 |
| GET/PUT | `/{id}/settings` | 技能参数设置 |

## 使用要点

- 技能与 AIAgent 的 `SkillDefinition` 关联，是 agent 能力注册的中心。
- 参数（`settings`）持久化，运行前由 `SkillsService` 加载。
