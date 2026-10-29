---
task_id: 2026-10-26-component-ct-gallery
feature: none                     # 无对应的 docs/02-features 编号：本任务是前端组件开发基建（规范 + 工具），不属单个功能档案
risk: { level: L4, triggers: ["AGENTS.md", ".agents/skills/**", "ForgeSelf.Web/package.json", "ForgeSelf.Web/playwright-ct.config.ts"], raised_by_agent: false }
gate1: { mode: reviewer, at: 2026-10-26T17:05, ref: "用户输入13「用AI Agent插件的聊天助手测试」视为放行；默认项按 03-plan 执行" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Web/ct/index.html
  - ForgeSelf.Web/ct/main.ts
  - ForgeSelf.Web/ct/stories.ts
  - ForgeSelf.Web/ct/types.ts
  - ForgeSelf.Web/ct/vite.config.ts
  - ForgeSelf.Web/ct/README.md
  - ForgeSelf.Web/playwright-ct.config.ts
  - ForgeSelf.Web/tsconfig.ct.json
  - ForgeSelf.Web/src/components/ForgeCard.story.ts
  - ForgeSelf.Web/src/components/PageHead.story.ts
  - ForgeSelf.Web/src/components/EmptyState.story.ts
  - ForgeSelf.Web/src/components/ForgeCard.ct.spec.ts
  - ForgeSelf.Web/src/components/EmptyState.ct.spec.ts
  - ForgeSelf.Web/src/components/AIAgentMessageStoryHost.vue
  - ForgeSelf.Web/src/components/AIAgentMessage.story.ts
  - ForgeSelf.Web/src/components/AIAgentMessage.ct.spec.ts
  - ForgeSelf.Web/src/components/Gallery.ct.spec.ts
  - ForgeSelf.Web/src/components/ct-support.ts
  - ForgeSelf.Web/scripts/check-component-conventions.mjs
  - docs/04-standards/frontend-component-development.md
  - .agents/skills/component-visual-loop/SKILL.md
  - docs/README.md
  - AGENTS.md
  - .gitignore
  - ForgeSelf.Web/package.json
  - ForgeSelf.Web/eslint.config.mjs
  - ForgeSelf.Web/tsconfig.json
  - ForgeSelf.Web/tsconfig.app.json
rollback: "git checkout -- ForgeSelf.Web/package.json ForgeSelf.Web/eslint.config.mjs ForgeSelf.Web/tsconfig.json ForgeSelf.Web/tsconfig.app.json docs/README.md AGENTS.md .gitignore && 删除本任务新增文件（ct/、playwright-ct.config.ts、tsconfig.ct.json、*.story.ts、*.ct.spec.ts、ct-support.ts、AIAgentMessageStoryHost.vue、*-snapshots/、check-component-conventions.mjs、规范文档、技能目录）"
writeback: { feature_doc: not_applicable, reason: "无功能编号；写入 docs/04-standards + AGENTS 技能表 + docs/README 索引" }
---

# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。

## Task ID

2026-10-26-component-ct-gallery

## Objective

`ForgeSelf.Web` 具备「单组件可独立预览 + 像素基线回归」的自建画廊（`ct/` + `playwright-ct.config.ts`），并落地一份组件分层规范（展示组件 / 页面容器 / `*.logic.ts` 纯逻辑 / 组合式）与配套技能、机器守卫；`pnpm run check` / `test` / `test:ct` / `design:check` 四门禁全绿。

## Scope

### Allowed

严格按 `03-plan.md` 的 Files To Change：
- 新增 `ForgeSelf.Web/ct/`（5 个源文件 + README）、`playwright-ct.config.ts`、`tsconfig.ct.json`。
- 新增三个 `*.story.ts` 与两个 `*.ct.spec.ts`（只读引用组件，不改组件源码）。
- 新增 `ForgeSelf.Web/scripts/check-component-conventions.mjs`。
- 新增 `docs/04-standards/frontend-component-development.md`、`.agents/skills/component-visual-loop/SKILL.md`。
- 修改 `ForgeSelf.Web/package.json`（scripts）、`eslint.config.mjs`（ct globals）、`tsconfig.json`（引用）、`docs/README.md`（索引行）、`AGENTS.md`（§2.4 技能行）、`.gitignore`（`*-snapshots/`）。
- 补齐本任务 `05/06/07` 工件。

### Forbidden

- 改任何业务组件源码（`src/components/*.vue`）、`src/views/**`、`src/services/**`、`src/stores/**`。
- 改 `playwright.config.ts`、`e2e/**`、`vitest.config.ts`、`Playwright` 的 e2e 共享基建。
- 改 `Plugins/**`、`ForgeSelf.Api/**`、`scripts/`（根）、`plugin.json`、版本号。
- 新增 npm 依赖（`package.json` 的 dependencies/devDependencies 零变更；不得 `pnpm add`）。
- 引入 Storybook / `@playwright/experimental-ct-vue` / MSW。
- 把 `*.ct.spec.ts` 写成只有截图没有行为断言的用例；不得用 `@ts-ignore` 绕过类型。
- 在 story/spec 中写裸色值（hex/rgb/oklch/hsl）或定义新 CSS 变量族。
- 扩大范围去重构存量视图（如把 32 KB 的 `WorkflowLibrary.vue` 拆开）——记 TODO，不本批做。

## Acceptance Criteria

- [ ] AC1 `ct:server` 在 127.0.0.1:5190 起服务，`/?story=ForgeCard/Default` 渲染 `.gal-preview .forge-card` 且无 `#gallery-nav`
- [ ] AC2 根路径渲染侧栏，条目数 = `stories.ts` 聚合长度；点击切换生效且 active 态正确
- [ ] AC3 `test:ct:update` 生成基线后 `test:ct` 全绿；每用例含行为断言 + `toHaveScreenshot()`
- [ ] AC4 `pnpm run check` 通过（含新守卫与 4 个 tsconfig project）
- [ ] AC5 `pnpm run test` 全绿且未收集 `.ct.spec`
- [ ] AC6 `pnpm run design:check` 通过且棘轮计数未上涨
- [ ] AC7 规范文档 7 项内容齐备；`docs/README.md` §一、`AGENTS.md` §2.4 各一行
- [ ] AC8 守卫三类违规均 `exit 1` 指名文件；恢复后 `exit 0`
- [ ] AC9 未知 story id 显示 `Unknown story` + 可用 id 列表

## Expected Files

见头部 `expected_files`（19 项 + 本任务 05/06/07 工件）。实际落地若有增删，必须在 `05-evidence.md` 的 Changed Files 说明并在 `03-plan.md` 偏差表登记。

## Verification Commands

```bash
cd ForgeSelf.Web && pnpm run check
cd ForgeSelf.Web && pnpm run test
cd ForgeSelf.Web && pnpm run test:ct:update
cd ForgeSelf.Web && pnpm run test:ct
cd ForgeSelf.Web && pnpm run design:check
```

判定：全部 exit 0，且以日志正文（PASS/通过数/无 error）为证据，不以 exit code 单独作证。

## 风险与拍板项（闸门1）

| 项 | 内容 | 默认 |
| --- | --- | --- |
| Q1 | 是否按本方案实现（19 项文件 + 规范 + 技能 + 守卫） | 按上述执行 |
| Q2 | §5.6 档位：本任务是否需跑全量 e2e（约 38 分钟） | 不跑（新 CT 配置与 e2e 零耦合，依据见 03-plan E2E 节） |
| Q3 | CT 基线 `*-snapshots/` 是否入库 | 不入库（照 Cube.Vue） |
