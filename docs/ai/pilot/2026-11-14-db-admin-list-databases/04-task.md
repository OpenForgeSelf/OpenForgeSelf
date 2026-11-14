---
task_id: 2026-11-14-db-admin-list-databases
feature: 044-db-admin
risk: { level: L2, triggers: ["Plugins/DbAdmin/web/src/DbAdminView.vue"], raised_by_agent: true }
gate1: { mode: reviewer, at: "2026-11-14", decision: "approved", ref: "用户原文：P1 一并做，开工作树去做" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/DbAdmin/web/src/connForm.ts
  - Plugins/DbAdmin/web/src/connForm.test.ts
  - Plugins/DbAdmin/web/src/api.ts
  - Plugins/DbAdmin/web/src/normalize.ts
  - Plugins/DbAdmin/web/src/types.ts
  - Plugins/DbAdmin/web/src/tabs.ts
  - Plugins/DbAdmin/web/src/DbAdminView.vue
  - ForgeSelf.Web/src/components/DbAdmin.story.ts
  - ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts
  - docs/02-features/044-db-admin.md
  - Plugins/DbAdmin/docs/ui-design-brief.md
  - Plugins/DbAdmin/web/src/exportResult.ts
  - Plugins/DbAdmin/plugin.json
rollback: "git checkout -- Plugins/DbAdmin/web ForgeSelf.Web/src/components/DbAdmin.story.ts ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts"
writeback: { feature_doc: docs/02-features/044-db-admin.md, reason: "实现后把缺口句改成已实现行为" }
---

# Agent Task

> 阶段：Stage 4｜Task ID：`2026-11-14-db-admin-list-databases`
> 闸门1：已批（2026-11-14「P1 一并做，开工作树去做」）。实现目录是本工作树，不是主工作区。

## Task ID

`2026-11-14-db-admin-list-databases`

## Objective

mysql / sqlserver / postgresql 可以不填数据库名；点开这种连接先列出全部可见数据库，点库再列出该库的表并在该库执行查询。SQLite、系统连接、Oracle 的树保持直接列表。同批完成 044 §8.2 十条（见 02-spec「同批：044 §8.2」）。

## Scope

### Allowed

- 03-plan 列出的文件。
- 插件补丁版本 `plugin.json` 0.2.1 → 0.2.2。
- 交互说明里「点连接只加载全部表」与新行为冲突的句子。

### Forbidden

- `Plugins/DbAdmin/Services/**` 与控制器、模型（枚举与必填矩阵已正确）。
- 044 §8.3、决策 056、语法高亮、新依赖。§8.2 十条属于 Allowed。
- 停启 `:51888` 或用户宿主。
- 把 Oracle schema 做成可切换库层。
- 做成下拉选择器（那是 2026-11-07 未落地的旧交互）。

## Acceptance Criteria

- [ ] AC-I1 表单矩阵与文案
- [ ] AC-I2 点 mysql 连接先 loading 再出库，空态不抢先
- [ ] AC-I3 点库后 tables 请求带 `database`
- [ ] AC-I4 sqlite/系统连接无库层
- [ ] AC-I5 列库失败可重试
- [ ] AC-I6 执行查询 body 带当前库
- [ ] 插件 web test + build 绿
- [ ] 044 §4 与 ui-design-brief 改成实现后行为
- [ ] 05-evidence / 06-review 在验证之后补，未跑的 server 型标 Unknown

## Expected Files

见文首 `expected_files`。

## Verification Commands

```bash
cd Plugins/DbAdmin/web && pnpm run test
cd Plugins/DbAdmin/web && pnpm run build
cd ForgeSelf.Web && pnpm run design:check
dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~DbConnectionServiceTests
```

宿主 CT 若环境允许再跑 `DbAdmin.ct.spec.ts`。e2e 只复跑 db-admin 冒烟里的 sqlite 主链路。
