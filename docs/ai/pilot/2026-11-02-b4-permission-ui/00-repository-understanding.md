# Repository Understanding

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 范围：B4 权限可配置化 · UI 主攻（taskcenter-dev）。本文聚焦 UI 写面（ForgeSelf.Web），后端契约由 verify-dev 在 b4-permission-profiles 链承载（两链并存，lead 2026-11-02 裁决）。

## 项目结构

- `ForgeSelf.Web/`：Vue 3 SPA（本任务全部改动所在）
- `ForgeSelf.Api/`：ASP.NET Core 宿主（`Controllers/PermissionsController.cs` 为只读对表对象，不改）
- `ForgeSelf.Abstractions/PermissionProfiles.cs`：契约真源（只读引用）
- `docs/ai/pilot/2026-11-02-b4-permission-ui/`：本链工件

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Pinia + pnpm | `ForgeSelf.Web/package.json` |
| 样式 | Element Plus `--el-*` 变量 + 设计稿 token（禁裸色值） | `DESIGN.md` + `scripts/check-style-tokens.mjs` |
| API 层 | 服务目录 `src/services/*.ts`，fetch + `getStoredToken` 头 | `src/services/taskCenterApi.ts`（B3 范式） |
| 测试 | vitest（`src/**/__tests__/`） | `ForgeSelf.Web/vitest.config.ts` |

## 架构特点

- 设置页注册结构：`SettingsCategory` 联合类型 + `pageHeadMap` Record + `navItems` 数组 + 面板 `v-show` 惰性挂载（`src/views/SettingsView.vue`）。
- 组件范式参照 `AiProvidersPanel.vue`：列表 + 展开/收起 + ElMessage 反馈 + 骨架/错误/空三态。
- 无路由级嵌套设置页；分类键内聚于 SettingsView，深链经 `?cat=` 查询参数实现。

## 测试方式

- 快档门禁：`pnpm run check`（vue-tsc + eslint + 组件规范守卫）/ `pnpm run test`（vitest）/ `pnpm run design:check`（双棘轮基线）。
- 走查：起源码 dev-stack（`scripts/dev-stack.ps1`），Playwright MCP 截图读图。

## 构建命令

```bash
cd ForgeSelf.Web
pnpm run check && pnpm run test && pnpm run design:check
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Web/src/components/settings/` | 设置页各分类面板 |
| `ForgeSelf.Web/src/services/` | 后端 API 层（裸封套解析 + 错误原文透出） |
| `ForgeSelf.Web/src/data/features.ts` | 内置特性清单（首页功能卡入口） |

## 代码组织方式

面板组件自含（script setup + scoped style）；API 层独立文件便于单测；枚举等线上格式差异集中在 API 层映射，UI 恒用字面量。

## 现有工程规范

- AGENTS.md §8 设计门禁三步（design:check 棘轮 + Spec 交互设计节 + 走查符合性）。
- DESIGN.md §新建页面与查询执行规范（PageHead 三件套/五态/token/品牌语汇）。
- AC 写法无连字符 + 05 逐字对应（EnforceAcCoverage 机器校验）；04 front-matter 全键（rollback/writeback）。

## 候选低风险任务

（战役批任务，由 lead 开工令指定，不适用自选流程）

## 选择该任务的原因

lead 开工令 seq6（2026-11-02 22:38）指派 B4 UI 主攻；契约先行由 verify-dev 承担。
