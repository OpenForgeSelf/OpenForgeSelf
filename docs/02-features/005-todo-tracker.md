# 005 Todo Tracker（待办追踪插件）— 功能需求与设计

> 功能编号：005
> 状态：已实现（测试补齐完成，本地 c37cba0，7 文件 / +574 -36）
> 关联：specs/005-todo-tracker/；插件体系（01-architecture §3.3）
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
把"待办"作为可插拔插件（`Plugins/TodoTracker/`），通过 `plugin.json` 注册，前端 `TodoView` 提供增删改查与完成状态管理。

### 1.2 目标
1. 待办 CRUD REST（`api/todos`）；
2. 作为插件随宿主动态加载/启用；
3. 分页 `total` 正确返回（修复其他插件 `RetrieveTotalCount` 未开启的同类问题）。

## 2. 设计

### 2.1 实现位置
| 文件 | 职责 |
|------|------|
| `Plugins/TodoTracker/TodoTrackerPlugin.cs` | 插件入口 |
| `Plugins/TodoTracker/Controllers/TodosController.cs` | `api/todos` CRUD |
| `Plugins/TodoTracker/Services/` `Data/` `Models/` | 业务/数据/模型 |
| `ForgeSelf.Web/src/views/TodoView.vue` | 前端面板 |

### 2.2 实体
插件内自有实体（XCode），随插件迁移建表。

## 3. 使用指南
前端导航 → 待办，新增/编辑/完成/删除；数据落插件专属表。

## 4. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| TodoTracker 相关单测/集成（T011/T012/T020/T021） | CRUD、分页 total、完成态 |

## 5. 已知约束
- 其他插件（QuickLinks/Scheduler/WorkflowEngine/ScriptRunner 等）分页 `total=0` 仍未修（TODO.md T032，与本功能独立）。
