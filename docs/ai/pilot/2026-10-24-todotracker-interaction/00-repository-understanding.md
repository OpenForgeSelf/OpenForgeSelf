# 仓库理解

> 阶段：Stage 0 · 日期：2026-10-24 · 任务：TodoTracker 交互调整

## 项目结构与技术栈

- `Plugins/TodoTracker/` 是独立构建的待办追踪插件。其 `plugin.json` 声明插件 id 为 `todo-tracker`，远程视图为 `TodoView`，路由为 `/todo`，入口为 `web/dist/index.js`。
- 根目录 `README.md` 确认项目采用插件架构，并将 TodoTracker 列为功能模块；`ForgeSelf.slnx` 是仓库解决方案文件，也是现有插件 e2e 用来定位仓库根的锚点。
- 插件界面使用 Vue 3、TypeScript 和 Vite（`Plugins/TodoTracker/web/`）；后端是 .NET 10 插件控制器与服务；任务持久化和接口契约已存在。

## 当前交互与数据契约

- `TodoView.vue` 当前显示 8 个阶段筛选按钮、项目/状态/搜索筛选、分页列表和侧边详情面板；没有看板视图和阶段流水线。
- `TaskDetail.vue` 将工作单元编辑、下发与委派、项目/工件导入、阶段控制和执行记录放在长页面中。阶段操作使用任务 DTO 的 `allowedTargets`。
- `ExecutionTimeline.vue` 目前把阶段选项写死为英文（`Running`、`Review`、`Done`、`Blocked`、`Cancelled`），而后端已返回 `AllowedTargets`。
- `TodoStage.cs` 是阶段状态机的唯一真源。现有 API 返回 `AllowedTargets`；`TodoDto.Missing` 返回下发必填项缺口。Home 仍消费旧的 `Status` 与完成/重开接口，必须保持兼容。
- 现有列表 API 支持阶段、完成状态、项目、关键词筛选和分页（每页最多 100 条）。当前 API 不支持优先级筛选；只在客户端筛选会导致分页结果失真，因此方案会为现有列表查询增加一个可选、向后兼容的优先级参数。看板各列可使用阶段筛选分页，流水线数量可使用服务端返回的 `total`。
- 插件 e2e 文件目前包含多个独立用户流程和回归场景，超过插件技能规定的单条冒烟用例要求。实现计划会把它收敛成一个主链路冒烟用例，并将必要的非 UI 回归保留在可重复的单元/集成测试中。

## 验证入口

- 插件前端构建：`cd Plugins/TodoTracker/web && pnpm run build`。
- 后端相关回归：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TodoTracker"`。
- 插件宿主冒烟：`ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts`（Playwright，真实宿主；每个插件保留一条冒烟主链路）。
- 必须进行源码环境验收：运行 `pwsh scripts/dev-stack.ps1`，按交互标准逐项走查并检查截图；不操作用户正在运行的宿主。

## 约束与基线

- 界面阶段可达性必须采用后端的 `allowedTargets`；不得复制 HTML 原型中的模拟 `NEXT` 映射。
- 保持现有 API、状态语义、只追加执行记录和委派行为；不改数据库结构。
- 桌面 HTML 是独立原型，使用示例数据和模拟状态机；原型注释也明确说明了这些限制。
- Git 工作区已有与本任务无关的运行时/进程输出改动及 `TODO.md` 修改；这些改动在本任务开始前已存在，保持不动。本任务待办行已单独追加。

## 选定任务与理由

在现有 TodoTracker 界面中实现桌面原型的任务控制台交互，并复用当前 API 契约。此项是插件界面改动，需要完整 pilot、单条冒烟 e2e、源码宿主走查和插件维护闭环。
