# 04-standards — 工程规范

> 状态：已实现（从 AGENTS.md / working memory 提炼，与代码对齐，2026-08-12）
> 最后更新：2026-08-12

本文档沉淀**硬规则**。与 AGENTS.md 分工：AGENTS 管"怎么干活（流程）"，本文件管"干成什么样（规范）"。"为什么"见 `07-decisions/`（待补）。

## 1. 前端规范

| 规则 | 说明 |
|------|------|
| 样式零自定义 token | 仅用官方 Element Plus `--el-*` + Tailwind 布局原语；禁止 `--surface-*`/`--forge-*`/`--fs-*` 等；半透明用 `color-mix(in srgb, var(--el-*) N%, transparent)`，不用 `rgba()` |
| 组件禁止显式 import | `Element Plus` 组件禁止 `import { ElXxx } from 'element-plus'`（type 导入除外），统一在模板用 `<ElXxx>` 由 unplugin 自动注入样式；显式导入会绕过解析致无样式（已验证：TodoEditDialog 透明/无圆角） |
| canvas 取色 | canvas 2D 不解析 CSS 变量，JS 用 `getComputedStyle(document.documentElement).getPropertyValue('--el-color-*')` 读计算值 |
| 背景图 | 固定定位 `<img>`（`position:fixed; inset:0; object-fit:cover`），禁止 CSS `background-image` 外链 |
| 类型 | 全部 TS 类型，不用 `any` |
| 导航 | `composables/useOpenPage.ts` 的 `openPage` 统一走 `stores/tabs.ts`，禁止散落 `router.push`/`router.back()` |
| API 封装 | `services/*Api.ts` 走 `request.ts` 的 fetch 封装；类型在 `types/*.ts` |

## 2. 后端规范

| 规则 | 说明 |
|------|------|
| 分层 | Controllers / Services / Entities（XCode），插件走 `Plugins/` + `plugin.json` |
| 异步 | 统一 `async/await`，不 `.Result` 阻塞 |
| 密钥 | 敏感字段加密落库（AES-256-CBC，见 `02-features/100-secret-encryption.md`），接口仅掩码 |
| 密文边界 | 仓储写时加密、读返回密文原样不解密；明文仅调用方显式 `Decrypt` |
| 鉴权 | 管理接口加 `[Authorize("ApiKeyPolicy")]` |

## 3. 提交与流程规范

| 规则 | 说明 |
|------|------|
| 门禁 | 前端 `pnpm run check` + `pnpm run test`；后端 `dotnet build` + `dotnet test`；设计变更需 Playwright 截图对齐 |
| 范围 | 单任务原子化，不顺手重构无关代码（N5） |
| 测试铁律 | 新功能后端 MUST 写单测、前端 MUST 写 vitest 组件测试（渲染+关键交互）；纯配置/元数据变更可免（tasks.md 注明） |
| 预飞 | AGENTS.md §0：写日志 → 建 TODO → Context→Plan→Execute→Verify→Persist |

## 4. 目录结构约定

```
ForgeSelf.Api/   后端（.NET 10 / XCode / 插件）
ForgeSelf.Web/  前端（Vue3/Vite/Element Plus/Pinia）
docs/ai/pilot/YYYY-MM-DD-<task-id>/   开发任务工件（AI-Native 闭环；替代已 gitignore 的 specs/）
docs/                    人工沉淀知识（本体系）
.forgeself/memory/       按天工作日记 + MEMORY
```
