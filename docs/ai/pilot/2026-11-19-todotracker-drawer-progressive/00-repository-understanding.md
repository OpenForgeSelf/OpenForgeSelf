# Repository Understanding

> 任务：TodoTracker 创建轻校验 + 侧边抽屉详情/编辑 + 渐进式披露  
> Task ID：`2026-11-19-todotracker-drawer-progressive`｜日期：2026-11-19

## 项目用途与技术栈（仓库实测）

- 宿主：`ForgeSelf.Api`（.NET 10）+ `ForgeSelf.Web`（Vue 3.5 / Vite 6 / Element Plus / pnpm）
- 插件：`Plugins/TodoTracker`（id `todo-tracker`），自带 `web/` 远程加载界面
- 测试：插件前端 vitest/build；后端 `ForgeSelf.Api.Tests/Plugins/TodoTracker/*`；e2e `ForgeSelf.Web/e2e/plugins/todo-tracker/`

## 本任务相关现状

| 点 | 现状（Verified） | 依据 |
| --- | --- | --- |
| 创建必填 | 后端/工具/UI 均只需 `title`；四栏可选 | `CreateTodoRequest`、`TodoService.ValidateTitle`、`TodoView.vue` 快捷新建 |
| 下发必填 | `objective/content/acceptance/verification` 由 `DispatchPayloadBuilder.Missing` 单点判定 | `DispatchPayloadBuilder.cs` |
| 详情布局 | 主区左右分栏：列表 ~400px + 右侧 `aside.tt-detail-panel` 吃满剩余 | `TodoView.vue` `.tt-body` / `.tt-detail-panel` |
| 详情/编辑 | 同一 `TaskDetail.vue`，失焦即保存；三页签 | `TaskDetail.vue` |
| 披露方式 | 三页签 + 页签内平铺卡片；工作单元页一次展开任务信息/项目/四栏+范围 | `TaskDetail.vue` 模板 |
| 缺项文案 | 列表行、看板卡、详情头均「还缺：…」；下发按钮 disabled + toast | `TodoView.vue` / `TaskDetail.vue` |
| 抽屉先例 | 宿主设置页 `AiProvidersPanel.vue` 用 `el-drawer`；TodoTracker 未用 | `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue` |
| 设计稿 | `Plugins/TodoTracker/docs/` 缺失；历史交互原型桌面 HTML 未入库 | 探索结果 |

## 适用问题类

- 无直接命中 PC（非缺陷复发）。相邻 UX 教训：PC-012（设置页抽屉裁剪）——本任务抽屉须 `append-to-body` / 脱离插件滚动容器，避免宿主布局裁切。

## 候选低风险任务

1. **本任务（推荐）**：UI 壳层改为抽屉 + 渐进披露；文案/交互把「必填压力」挪到下发前；后端创建/下发契约不变。
2. 仅改文案「还缺」→「下发前待补齐」而不改布局——不足以解决挤压与一次展示过多。
3. 恢复窄侧栏——与用户「侧边抽屉、不占用面板」相反，否决。
