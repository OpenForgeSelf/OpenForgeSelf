# Plan

> 阶段：Stage 3｜必须具体到真实文件路径
> Task ID：`2026-11-06-dbadmin-dark-contrast`

## Files To Change

- `ForgeSelf.Web/src/styles/themes/workshop-forge.css`
  reason: 暗色 `--el-color-primary-light-3/5/7/8/9` 由亮金倒置改为设计稿深棕 ramp（canonical fix）。
- `ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts`（新增）
  reason: 类级守卫，解析两套主题 token 并断言明暗语义 + 对比度（`vitest.config.ts` exclude 含 `**/*.spec.ts`，必须用 `.test.ts`）。
- `Plugins/AIAgent/web/src/components/RunRecordPanel.vue`
  reason: 同根因命中：`color: var(--el-color-primary-light-7)` 在暗色变成深棕文字，改为 `--el-color-primary`。
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`
  reason: 在既有唯一冒烟用例内选中连接后，实测并断言 light/dark 选中行对比度 ≥4.5。
- `AGENTS.md`
  reason: §8 设计门禁第 3 步补「亮/暗两态、默认主题不算暗色验证、选中/标签/横幅 ≥4.5:1」顶级规则。
- `DESIGN.md`
  reason: token 单一真源补「暗色 light-N 逐级更暗、禁止当文字色」不变量 + 守卫入口。
- `.agents/skills/ui-ux-design/SKILL.md`
  reason: 走查清单补「列表/树选中行、标签底、横幅底 × light/dark」与「默认主题不算暗色验证」。
- `docs/04-standards/agent-workflow/b-engineering-daily.md`
  reason: UI 批（含缺陷修复）规则补主题矩阵具体表面与 e2e 暗色显式切换要求。
- `docs/18-templates/ai-pilot/02-spec.tpl.md`
  reason: 视觉规格「主题矩阵」条目补选中/激活/标签/横幅底与暗色 e2e 要求。
- `Plugins/DbAdmin/docs/ui-design-brief.md`
  reason: 面向设计师补选中态亮/暗 token 与对比度验收点。
- `Plugins/DbAdmin/docs/06-UI交互评审稿.md`
  reason: 验收清单补「亮/暗两态截图 + 选中行对比度实测」。
- `docs/11-troubleshooting/problem-classes/PC-025-dark-theme-token-contrast.md`（新增）
  reason: 问题类唯一真源（不变量/签名/canonical fix/守卫/lens）。
- `docs/11-troubleshooting/cases/2026-11-06-dbadmin-dark-connection-contrast.md`（新增）
  reason: 本次事件档案（复现/5 Whys/扫查/修复/守卫/逃逸/基建）。
- `docs/11-troubleshooting/problem-classes/README.md`
  reason: tags 检索表 + 台账总表加 PC-025。
- `docs/11-troubleshooting/cases/README.md`
  reason: case 索引加行。
- `docs/11-troubleshooting/index.md`
  reason: 现象速查加「暗色下文字看不见」入口。
- `docs/ai/pilot/2026-11-06-dbadmin-dark-contrast/00-07`
  reason: 本任务工件链。
- `TODO.md` + `.forgeself/memory/2026-11-06.md`
  reason: 队列与日志（开工登记、完成移除、验证结果）。

## Implementation Steps

1. 先写 `workshop-forge.contrast.spec.ts` 并跑一次，记录修复前红灯（dark light-8 vs text-primary 1.10）。
2. 修改 `workshop-forge.css` 暗色 primary ramp 为设计稿五值（#D97706/#B45309/#92400E/#78350F/#451A03）。
3. 同类扫查 `--el-color-primary-light-(3|5|7|8|9)` 的 `color:` 用法；修 `RunRecordPanel.vue:1363`；其余命中按对比度判定（border/box-shadow 不动）。
4. 在 DbAdmin e2e 冒烟中选中连接后加入 `evaluate` 对比度计算（light 态 + 临时 `html.dark` 态），断言两态 ≥4.5 并输出数值。
5. 补强四份规范/模板与两份 DbAdmin 文档（DESIGN.md / ui-ux-design / b-engineering-daily / 02-spec.tpl）。
6. 新建 PC-025 + case，更新三处索引；跑 `check-problem-classes.ps1`。
7. 验证：前端 check/test/design:check、插件 build、DbAdmin 冒烟 e2e、dev-stack 亮暗两态走查截图。
8. 补齐 05/06/07，更新日记与 TODO（完成项移除），汇报。

## Test Plan

1. **红灯（G2 类级）**：`cd ForgeSelf.Web && pnpm exec vitest run src/styles/themes/workshop-forge.contrast.spec.ts` → 修复前 FAIL（断言 dark light-8 vs text 1.10 < 4.5、light-9 亮度 > primary）。
2. **绿灯**：同命令 PASS；`pnpm run test` 全量（或过滤前端样式相关）PASS。
3. **实例（G1）**：DbAdmin 冒烟 e2e 选中连接后断言 light/dark ratio ≥4.5；e2e 输出实际 `rgb` 与 ratio 入 05。
4. **设计守卫**：`pnpm run design:check` PASS（token 文件豁免、无新增硬编码色）。
5. **插件构建**：`cd Plugins/DbAdmin/web && pnpm run build` PASS；产物存在。
6. **走查**：`pwsh scripts/dev-stack.ps1` 起隔离 dev 前后端；DbAdmin 亮/暗两态截图 + 选中行可读；清测试数据后 `-Stop`。
7. **问题类**：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1` PASS；PC/case 索引校验。

## Verification

### Build

```bash
cd ForgeSelf.Web && pnpm run check
cd Plugins/DbAdmin/web && pnpm run build
```

### Unit Test

```bash
cd ForgeSelf.Web && pnpm exec vitest run src/styles/themes/workshop-forge.contrast.spec.ts
cd ForgeSelf.Web && pnpm run test
```

### Integration Test

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~DbAdmin"
```

### E2E

```bash
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run design:check
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-11-06 09:19 | 守卫文件名 | `workshop-forge.contrast.spec.ts` | `workshop-forge.contrast.test.ts`：`vitest.config.ts` 的 exclude 含 `**/*.spec.ts`（留给 Playwright），spec 会被静默跳过；已实测修正后 5 用例 3 红 2 绿 |
| 2026-11-06 09:19 | 守卫读取路径 | `import.meta.url` | Vite 转译后 `import.meta.url` 是 http 形态，`readFileSync` 报 `URL must be of scheme file`；改为 `path.resolve(process.cwd(), ...)`（测试统一从 `ForgeSelf.Web` 运行） |
