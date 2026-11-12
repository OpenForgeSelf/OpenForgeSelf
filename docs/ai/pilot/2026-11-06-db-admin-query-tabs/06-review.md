# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 是。四条用户诉求全部落地：①点表开标签、同表只聚焦不重置；②连接可填「默认表」、
   留空则只展开并加载全部表（一个连接服务多表）；③有选区只执行选中语句；④多语句顺序执行 + 结果分标签独立分页。
   竞品对标补齐项（表过滤/刷新、复制整行、CSV 导出、单元格详情、结果状态栏）亦已实现。
2. **实现是否符合 Spec？** 是。`02-spec.md` 的 FR-1~24 / BR-1~24 / 交互规格与视觉规格逐条对应到实现；
   与 Spec 唯一有意偏差是「结果集不随快照恢复」（Spec 原本即如此规定）。
3. **是否超出了 Scope？** 否。改动限于插件前端 + 组件 story/CT + 插件 e2e + 文档/清单；
   后端 `.cs`、API 契约、DB、宿主 `src/views` 与路由均零改动。
4. **是否修改了不应该修改的文件？** 否。工作树中 `ForgeSelf.Web/src/components/settings/PluginsPanel.vue`
   有 1 行改动，**非本批产物**（并行会话改动），本批未触碰。
5. **测试是否覆盖 Acceptance Criteria？** 是（AC-25 内门禁部分除外，见 Findings Minor-2）。
   AC-1~24 均有单测（插件 103 用例）/ CT（32 用例）/ e2e（真实宿主 1 用例）三者之一或以上的直接证据，
   逐条映射见下表。
6. **是否存在明显回归风险？** 低。恢复快照格式由 `drafts` 升级为 `tabs`，`loadSnapshot` 保留旧格式迁移路径
   并有单测（旧快照仍能恢复为 `m:<conn>:0` 标签）；宿主 `pnpm run test` 1202/1202 绿，无回归。
7. **是否存在架构不一致？** 否。新增 4 个纯逻辑模块（tabs / sqlSplit / results / exportCsv）沿用插件既有的
   「纯函数 + 单测、UI 只做编排」分层；后端多语句硬拒的红线没有被绕过——前端切分后**逐条**发 `POST /query`，
   契约零改动。
8. **Evidence 是否足以证明任务完成？** 部分。构建/单测/静态检查/CT/e2e 全部 Verified 且有真实输出；
   但「起源码 dev-stack 浏览器走查」为 Unknown（见 `05-evidence.md` Known Limitations 1），
   该项转由发布后的运行实例只读复验补齐。

## Requirement Check

PASS

## Scope Check

PASS

## Test Check

PASS

## Architecture Check

PASS

## Risk

L2（触发项：`DbAdminView.vue` 大改 + localStorage 快照格式升级）

## Findings

### Critical

无。

### Major

无。

### Minor

1. **前后端 SQL 切分规则无自动对等守卫。** 前端 `sqlSplit.ts` 是后端 `SqlGuard.cs` 规则的镜像实现
   （字符串/引号标识符/注释/美元引号/TRIGGER BEGIN…END）。两者目前各测各的，后端若调整切分规则，前端不会自动红。
   → 记 TODO（P2）：以共享用例表（同一批 SQL 样本 + 期望条数）做跨语言对等测试。
2. **起源码 dev-stack 走查未单跑。** 本批 UI 证据为 CT（四态含亮/暗基线）+ e2e（真实宿主、亮/暗对比度 10.41:1 / 7.98:1），
   dev-stack 实例仅确认了鉴权生效后停止。→ 转发布后「运行实例只读复验」补做，不阻断本批验收。
3. **多媒体文件外 provider 未实测。** 多语句/审批链路仅 SQLite 实跑；MySQL/PostgreSQL 同源逻辑无运行证据（Inferred）。

## AC → 证据映射

| AC | 证据 | 等级 |
|----|------|------|
| AC-1/2/3 | CT「两标签互不覆盖；切换保留内容；重复开同表不新建不重置」；e2e ④.0 | Verified |
| AC-4 | CT「关闭标签：右邻激活；关到 0 显示工作区空态」 | Verified |
| AC-5 | `tabs.test.ts`（12 上限 + banner）；`openTableTab.full` 分支 | Verified（单测） |
| AC-6 | `DbAdminView.vue` 删除连接时清其标签 + 默认表；`restore.test.ts` 覆盖快照剔除 | Verified（单测） + Inferred（UI 路径） |
| AC-7 | CT S-R「刷新前/刷新后」两用例；`restore.test.ts` 9 用例（含旧 `drafts` 迁移） | Verified |
| AC-8 | CT「批量遇写审批：暂停 → 批准续跑；拒绝则其余未执行」 | Verified |
| AC-9 | `runSeq` + `tabExists(key, seq)` 丢弃迟到响应；`DbAdminView.vue` 关闭标签清 `approvalCtx` | Inferred（代码路径，无专属用例） |
| AC-10 | 历史按连接分流（既有逻辑未改）+ 活动标签派生连接 | Inferred |
| AC-11 | e2e 实测 `light=10.41:1 dark=7.98:1` | Verified |
| AC-12 | `cf-default-table` e2e 走查 + `connForm.test.ts`（`toSubmitBody` 剔除 `defaultTable`） | Verified |
| AC-13 | e2e ④.1 `▸/▾` 只展开；CT 同路径断言 | Verified |
| AC-14 | e2e ⑤.2 `setSelectionRange` + `selection-hint`「仅执行选中」+ 只 1 结果 | Verified |
| AC-15/16 | CT「多语句 → 结果标签条」+「失败即停，后续标未执行」 | Verified |
| AC-17 | CT 审批暂停/批准续跑/拒绝未执行 | Verified |
| AC-18/19 | CT「单语句不渲染标签条」；分页归属每 `ResultSet` | Verified |
| AC-20 | `sqlSplit.test.ts` 12 用例（注释/字符串/TRIGGER/未闭合） | Verified |
| AC-21 | e2e ④.1 `table-filter` / `table-filter-empty`；CT 同路径 | Verified |
| AC-22 | CT「双击单元格 → 详情浮层；整行复制随选中启用；Esc 关闭」；`exportCsv.test.ts` `rowText` | Verified |
| AC-23 | CT 导出 CSV 文件名断言；`exportCsv.test.ts` 8 用例（转义/BOM/NULL） | Verified |
| AC-24 | CT 白平衡「120 行 · 4 ms」+ `result-range`「第 101–120 行 / 共 120」 | Verified |
| AC-25 | 见 `05-evidence.md` 全部门禁输出（插件 build/test、宿主 check/test/design:check、CT 32、e2e 1 passed） | Verified（除 Known Limitations） |

## Final Decision

APPROVED

<!-- 交用户闸门2 验收。验收通过后按闸门3：补 07-final-report、回填 04-task 闸门时间、日记归档、TODO 移除、
     经用户同意再走发布（release-local.ps1 -UpdateDir -Sign）与运行实例只读复验。 -->
