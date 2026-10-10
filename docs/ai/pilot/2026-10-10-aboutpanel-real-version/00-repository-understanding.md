# Repository Understanding

> 阶段：Stage 0。按 D:\src\my-proj\OpenForgeSelf\OpenForgeSelf 实仓确认。

## 项目结构
- `ForgeSelf.Web/` Vue 3 SPA（本次改动落点）
- `ForgeSelf.Api/` ASP.NET Core（.NET 10 + SQLite + NewLife.XCode）
- `forgeself-design/` 交互稿；`docs/ai/pilot/` 飞行工件；`TODO.md`/`.forgeself/memory/` 任务与日记

## 技术栈（实仓依据）
| 项 | 依据 |
| --- | --- |
| Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia + pnpm | ForgeSelf.Web/package.json |
| 前端门禁 `pnpm run check` = vue-tsc -b && eslint；`test` = vitest | package.json scripts |
| 统一请求层基于全局 fetch（`src/services/request.ts`，Bearer token 注入）→ 测试用 `vi.stubGlobal('fetch')` 可覆盖 | request.ts |

## 架构要点
- 前端按面板拆分 `components/settings/`；`UpdatePanel.vue` 已用 `updateApi.getStatus()` 的 `status?.currentVersion ?? '…'` 展示"当前版本"——本次对齐该既有调用方式。
- `updateApi.getStatus()` → `GET /api/update/status`，返回 `UpdateStatus`，`currentVersion: string`。

## 测试方式
- 单测：vitest + @vue/test-utils（参照 `__tests__/ApiKeysPanel.test.ts`，fetch mock 模式）。
- e2e：Playwright（本轮 N/A：改动未发布、publish/ 禁止改动、用户禁止启停进程）。

## 常用命令
```bash
cd ForgeSelf.Web
pnpm run check
pnpm vitest run
```

## 既行规范
AGENTS.md §0 预检（日记→TODO→工件→实现→验证）；§5.1 前端验证；§11 AI-Native 闭环（本套 00-07）。

## 本轮结论
小范围 UI 展示修正：单文件 + 单测，无架构影响；硬编码假版本 v0.1.0 与运行实例真实版本（2.4.x 线）不符是需要修复的真实缺口。
