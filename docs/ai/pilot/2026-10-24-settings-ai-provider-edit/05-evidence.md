# Evidence

> Stage 7｜Task ID：2026-10-24-settings-ai-provider-edit｜状态：APPROVED（2026-10-25 补跑浏览器 E2E 与全前端门禁后闭环）

## Changed Files

- `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue`：提供方新增/编辑表单从 drawer 换成 `el-dialog append-to-body`，宽度随视口收缩，表单区域限高滚动；沿用全局背景图透明度主题样式。
- `ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts`：新增 2 个编辑用例，覆盖 body 级弹窗、字段回填、取消、保存及 API Key 留空。
- `ForgeSelf.Web/e2e/ai-providers.spec.ts`：更新 provider CRUD 选择器并增加透明度断言和截图。
- `docs/02-features/001-ai-provider-config.md`：同步编辑 UI 说明。
- `docs/11-troubleshooting/`：新增 PC-012/case 并更新索引。

## Verification

| 检查 | 命令/动作 | 结果 | 等级 |
|---|---|---|---|
| 组件测试 | `pnpm --dir ForgeSelf.Web run test -- AiProvidersPanel` | 1 file / 10 tests passed | Verified |
| 全前端 check | `pnpm --dir ForgeSelf.Web run check` | **PASS**（2026-10-25 复核：先清掉 `e2e/plugins/todo-tracker` 遗留未使用导入 `existsSync`，`vue-tsc` + `eslint` 整体 exit 0） | Verified |
| 全前端 build | `pnpm --dir ForgeSelf.Web run build` | **PASS**（2026-10-25，见本节末「补跑记录」） | Verified |
| Vite production bundle | `pnpm --dir ForgeSelf.Web exec vite build` | 由 `pnpm run build` 覆盖（同一产物链），无需单独再跑 | Verified |
| 变更文件 ESLint | `pnpm --dir ForgeSelf.Web exec eslint src/components/settings/AiProvidersPanel.vue src/__tests__/AiProvidersPanel.test.ts e2e/ai-providers.spec.ts` | 0 errors；AiProvidersPanel.vue 有 32 条既有格式 warning | Verified |
| PC-012 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1 -PcId PC-012` | OK | Verified |
| 全部问题类 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1` | **13 个 PC 全部通过**（2026-10-25 复核） | Verified |
| Registry 测试 | `dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~ProblemClassRegistryTests` | **PASS**（2026-10-25 随 182 项定向过滤集与全量中档门禁一同绿） | Verified |
| provider edit e2e/截图 | `pnpm --dir ForgeSelf.Web run test:e2e -- e2e/ai-providers.spec.ts` | **11 passed / 54.9s**（2026-10-25；含 T021 编辑回填 + `app-bg-image-mode` 透明度断言 + 截图 `ForgeSelf.Web/screenshots/e2e/ai-provider-edit-dialog.png`） | Verified |
| 截图读图（UI 走查） | 读 `screenshots/e2e/ai-provider-edit-dialog.png` | 弹窗为视口级浮层、表单五栏（名称/类型/接入地址/API Key/超时）与「取消·保存」齐全、背景图透出且文字可读 | Verified |

## 红灯记录

组件回归测试先于组件改动添加，但当时沙箱内 `pnpm` 路径规范化报 Access Denied，直接 Vitest 又报依赖 realpath ENOENT，未取得修复前的测试红灯。后续自动审核拒绝回退 UI 实现以复现红灯。未把任何失败伪报为根因红灯。

## Known Limitations / Unresolved Issues

- ~~背景图透明度有代码级样式规则和 E2E 断言，但浏览器用例未执行~~ → 2026-10-25 已执行：T021 断言 `0 < dialogAlpha < 1` 通过，截图已读图。
- ~~完整前端 check/build 被工作区现有 TodoTracker 未使用导入阻断~~ → 2026-10-25 已清掉该未使用导入（属于 todo-tracker e2e 规格自身的遗留），check/build 均 PASS。
- 补跑遗留的 P3 视觉观察：fullPage 截图下弹窗顶部标题与页面头部区域的层叠在滚动状态时略显拥挤，非阻断；已登记 TODO（P3），随下次设置页 UI 批次处理。

## 补跑记录（2026-10-25 验收复核）

| 命令 | 结果 |
|---|---|
| `pnpm run test -- AiProvidersPanel` | 1 file / **10 tests passed** |
| `pnpm run check` | exit 0（vue-tsc + eslint 全绿；日志 `.temp/accept-check3.log`） |
| `pnpm run build` | 见 `.temp/accept-build-frontend.log` |
| `pnpm run test:e2e -- e2e/ai-providers.spec.ts` | **11 passed / 54.9s**（日志 `.temp/accept-e2e-providers.log`） |
| `pwsh scripts/check-problem-classes.ps1` | exit 0，13 个 PC 通过 |
