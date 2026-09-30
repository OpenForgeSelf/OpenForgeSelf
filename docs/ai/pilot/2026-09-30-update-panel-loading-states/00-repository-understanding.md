# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2

## 项目结构

- 根目录：`D:\src\my-proj\OpenForgeSelf\OpenForgeSelf`（Git 管理）
- 前端：`ForgeSelf.Web/`（Vue 3.5 + Vite + TS，pnpm）
- 后端：`ForgeSelf.Api/`（.NET 10 + SQLite + NewLife.XCode）
- 后端测试：`ForgeSelf.Api.Tests/`
- 插件：`Plugins/` + `plugin.json`
- 流程工件：`docs/ai/pilot/`；设计稿：`forgeself-design/`；规则库：`docs/04-standards/`

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus + Tailwind + Pinia | ForgeSelf.Web/package.json、AGENTS.md §2.1 |
| 前端测试 | vitest + @vue/test-utils | ForgeSelf.Web/src/__tests__/*.test.ts |
| 后端 | .NET 10 + SQLite + NewLife.XCode | AGENTS.md §2.1 |
| 后端测试 | xUnit + Moq + FluentAssertions | ForgeSelf.Api.Tests/ |

## 架构特点

- 设置页组件位于 `ForgeSelf.Web/src/components/settings/`，由 `SettingsView.vue` 挂载；本任务对象为 `UpdatePanel.vue`（版本更新面板）。
- 更新流程（spec 036）：检查 → 下载（暂存）→ 轮询进度 → 重启并更新（宿主退出，更新代理换文件后重启，前端轮询 `/api/update/progress` 等待恢复后 reload）。
- 后端 `UpdateController` 走 ApiKeyPolicy 鉴权；前端 `request` 服务自动带 token。

## 测试方式

- 前端单测：`cd ForgeSelf.Web && pnpm run test`（vitest，组件用 @vue/test-utils mount + vi.mock 隔离）。
- 类型/lint：`pnpm run check`（vue-tsc --noEmit + eslint）。
- 后端：`dotnet build` / `dotnet test`（本任务不涉及后端，N/A）。

## 构建命令

```bash
cd ForgeSelf.Web
pnpm run check
pnpm run test
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| ForgeSelf.Web/src/components/settings/ | 设置页各面板组件（UpdatePanel/ApiServerPanel/AppearancePanel 等） |
| ForgeSelf.Web/src/services/ | API 服务封装（updateApi.ts / request.ts） |
| ForgeSelf.Web/src/__tests__/ | 前端组件/服务单测 |
| docs/ai/pilot/ | AI-Native 闭环任务工件 |

## 代码组织方式

- 面板组件 `<script setup lang="ts">` + 模板；加载状态用 `ref` 维护；API 调用经 `@/services/*` 封装。
- `UpdatePanel.vue` 维护 `checking` / `downloading` / `applying` 三个本地动作 ref，另有 `busy()` 根据后端 stage 判断「任一更新流程进行中」。

## 现有工程规范

- AGENTS.md §11：开发任务唯一流程 = AI-Native 闭环，≤3 文件缺陷修复可轻量（合并 mini-task），Evidence/Review 必须单独产出。
- AGENTS.md §5.1：改 `ForgeSelf.Web/` 后必须 `pnpm run check` + `pnpm run test`。
- 用户偏好：同一任务改动汇总一次性提交；未经明确指示不 commit/push；无害决策自主执行。

## 候选低风险任务

- 本次输入51：设置页更新按钮加载状态共用 bug（`UpdatePanel.vue` 模板 2 行 + 回归测试 1 文件）。

## 选择该任务的原因

- 用户直接上报的 UI 缺陷，改动面小（单组件模板绑定 + 单测试文件），不涉后端/DB/权限，可快速闭环验证。
