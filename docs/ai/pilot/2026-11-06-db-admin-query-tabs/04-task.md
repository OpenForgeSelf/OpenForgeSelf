---
task_id: 2026-11-06-db-admin-query-tabs
feature: 044-db-admin
risk: { level: L2, triggers: ["Plugins/DbAdmin/web/src/DbAdminView.vue", "localStorage 快照格式升级"], raised_by_agent: false }
gate1: { mode: user, at: "2026-11-06", decision: approved }
gate2: { mode: user, at: "2026-11-06T15:05", decision: approved }
gate3: { mode: agent, at: "2026-11-06T15:05" }
expected_files:
  - Plugins/DbAdmin/web/src/tabs.ts
  - Plugins/DbAdmin/web/src/tabs.test.ts
  - Plugins/DbAdmin/web/src/sqlSplit.ts
  - Plugins/DbAdmin/web/src/sqlSplit.test.ts
  - Plugins/DbAdmin/web/src/results.ts
  - Plugins/DbAdmin/web/src/results.test.ts
  - Plugins/DbAdmin/web/src/exportCsv.ts
  - Plugins/DbAdmin/web/src/exportCsv.test.ts
  - Plugins/DbAdmin/web/src/restore.ts
  - Plugins/DbAdmin/web/src/restore.test.ts
  - Plugins/DbAdmin/web/src/types.ts
  - Plugins/DbAdmin/web/src/connForm.ts
  - Plugins/DbAdmin/web/src/connForm.test.ts
  - Plugins/DbAdmin/web/src/DbAdminView.vue
  - ForgeSelf.Web/src/components/DbAdmin.story.ts
  - ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts
  - ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts
  - Plugins/DbAdmin/docs/ui-design-brief.md
  - docs/02-features/044-db-admin.md
  - Plugins/DbAdmin/plugin.json
rollback: "git revert <commit>（插件前端 + 文档均为本批文件）"
writeback: { feature_doc: done, reason: "044-db-admin.md §4 多标签工作区 + §7 验收证据 + §8 竞品 backlog 已回写；ADR 056 已入库" }
---

# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。

## Task ID

`2026-11-06-db-admin-query-tabs`

## Objective

DbAdmin 右侧查询区改为多标签工作区：不同表各开独立查询标签、同表重复打开只聚焦原标签，
标签间切换不丢失任何一方的 SQL/结果；恢复快照与审批回填同步按标签作用域。
另：连接支持可选的「默认表」（仅本机偏好，不进后端请求体）——填了则点击连接直接打开该表标签，
留空则点击只展开并加载全部表，一个连接可服务任意多张表。
再：编辑器有选区时只执行选中语句；支持单标签内多条语句顺序执行，每条结果各占一个结果标签并独立分页。
并按竞品对标（DBeaver/Navicat/TablePlus/HeidiSQL）一次补齐查询工作台常规能力：表过滤与刷新、结果复制/CSV 导出、
单元格详情、结果状态栏、快捷键统一；其余大特性（高亮/补全/格式化/事务/取消/排序过滤/内联编辑等）明确不纳入并落 NOT-to-do。

## Scope

### Allowed

- 仅 03-plan.md「Files To Change」列出的 20 个文件。
- 新增纯逻辑模块 `tabs.ts` / `sqlSplit.ts` / `results.ts` / `exportCsv.ts` 与其单测；扩展既有单测/CT/story/e2e（不新增第二条 e2e 用例）。
- 更新 `044-db-admin.md`、`ui-design-brief.md`、`plugin.json` 版本号。

### Forbidden

- 不改后端（`Plugins/DbAdmin/*.cs`、`ForgeSelf.Api/**`）、不改 API 契约、不改 DB 结构。
- 不新增第三方依赖、不改 `vite.config.ts` 产物契约、不动宿主 `src/views` 与路由。
- 不做无关重构（连接表单/审批/审计/脱敏保持原样）。
- 不发布、不提交 git（闸门2/3 前禁止）。
- 不新建一次性临时脚本作为验证手段（`AGENTS.md` §0 红线）。

## Acceptance Criteria

- [ ] AC-1/2/3：单击表行名开两个表标签、切换不丢、重复打开只聚焦且不重置内容；点 `▸/▾` 不建标签。
- [ ] AC-4：关闭标签邻居激活；最后一标签关闭进入空态。
- [ ] AC-5：12 标签上限 + banner 原因。
- [ ] AC-6：删除连接关闭其标签并清默认表，不影响其他连接。
- [ ] AC-7：刷新恢复标签 + SQL + 活动标签 + 默认表（旧快照迁移成功），结果集不恢复、提示文案为标签口径。
- [ ] AC-8：审批结果写回发起标签。
- [ ] AC-9：执行中切走/关闭后的响应处理正确（回原标签 / 丢弃）。
- [ ] AC-10：历史回填按连接分流。
- [ ] AC-11：活动标签 light/dark 对比度 ≥4.5:1 实测。
- [ ] AC-12：默认表填了→点连接开该表标签；留空→只展开加载全部表；请求体无 `defaultTable`（单测）。
- [ ] AC-13：点表行首 `▸/▾` 只展开/收起列，不建标签、不改变活动标签。
- [ ] AC-14：有选区时只执行选中语句（真实宿主抓 1 条请求且结果只对应选区）。
- [ ] AC-15/16：多语句 → 多结果标签；失败即停、后续标「未执行」。
- [ ] AC-17：批量含写语句 → 审批暂停/继续，拒绝后其余未执行。
- [ ] AC-18/19：每结果标签独立分页；单语句不显示结果标签条。
- [ ] AC-20：切分器单测（字符串/注释/标识符中的 `;`、TRIGGER 块、未闭合引号）。
- [ ] AC-21：表过滤只显示匹配表、不发请求；无匹配有空态。
- [ ] AC-22：双击单元格详情浮层可复制；复制整行制表符分隔；剪贴板失败有 banner。
- [ ] AC-23：CSV 导出含表头、转义正确、文件名含页码、UTF-8 BOM。
- [ ] AC-24：结果状态栏「N 行 · X ms」按结果标签独立。
- [ ] AC-25：门禁全绿（插件 build + 插件单测 + 宿主 check/test/design:check + CT + 冒烟 e2e）+ 文档同步 + NOT-to-do 落盘。

## Expected Files

- `Plugins/DbAdmin/web/src/tabs.ts` / `tabs.test.ts`（新增）
- `Plugins/DbAdmin/web/src/sqlSplit.ts` / `sqlSplit.test.ts` / `results.ts` / `results.test.ts`（新增）
- `Plugins/DbAdmin/web/src/exportCsv.ts` / `exportCsv.test.ts`（新增）
- `Plugins/DbAdmin/web/src/restore.ts` / `restore.test.ts`
- `Plugins/DbAdmin/web/src/types.ts` / `connForm.ts` / `connForm.test.ts` / `DbAdminView.vue`
- `ForgeSelf.Web/src/components/DbAdmin.story.ts` / `DbAdmin.ct.spec.ts`
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`
- `Plugins/DbAdmin/docs/ui-design-brief.md` / `docs/02-features/044-db-admin.md` / `Plugins/DbAdmin/plugin.json`

## Verification Commands

```bash
cd Plugins/DbAdmin/web && pnpm run build && pnpm run test
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
cd ForgeSelf.Web && pnpm run test:ct
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\dev-stack.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\dev-stack.ps1 -Stop
```
