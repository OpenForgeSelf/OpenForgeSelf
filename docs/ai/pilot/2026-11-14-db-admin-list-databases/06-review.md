# Review

> 阶段：Stage 8｜对照 02-spec（含「同批：044 §8.2」）、03-plan、04-task 与本工作树代码、05-evidence。
> 本结论不包含走查和 e2e。那两项按任务要求留给主会话。

## 审查八问（逐项回答）

1. 实现是否真正满足 Intent？
   满足本工作树要做的两块：mysql / sqlserver / postgresql 空库名可保存，点连接先列库再点库列表；SQLite、系统连接、Oracle 仍直接列表；§8.2 十条落在 `DbAdminView.vue` 与纯函数里。没有真实服务器上的列库证据。
2. 实现是否符合 Spec？
   文案、空 `Database=`、`GET .../databases`、`tables`/`tableDetail`/`query` 的可选 `database`、系统库置后、默认标记（仅 sqlserver 忽略大小写）、表标签 key、筛选三项、快捷键与浮层、0 行文案、导出全部上限 50 页、当前页 JSON/INSERT，与 02-spec 一致。未做 §8.3。
3. 是否超出了 Scope？
   没有改 Services / Controllers / Models，没有新依赖，没有新裸色值（design:check PASS）。`03-架构与接口.md` 补了一句「页面会调用 databases」，因为原文写「页面尚未调用」。
4. 是否修改了不应该修改的文件？
   改动都在本工作树。未改主工作区，未 commit，未 push，未停启宿主。
5. 测试是否覆盖 Acceptance Criteria？
   纯函数覆盖空库名矩阵、databases/tables URL、筛选、标签切换与关闭、CSV 多页、JSON、INSERT、0 行文案。CT 覆盖 Oracle「请填写服务名」的 DOM 断言，以及 mysql 夹具先见库节点。列宽、右键、快捷键、导出全部的界面动作没有自动化用例。
6. 是否存在明显回归风险？
   ConnFormInvalid 从 postgresql 空库名改成 oracle 空服务名，否则点保存会因空库名现在合法而清掉错误。SQLite 故事的表 testid 未改。`toHaveScreenshot` 在无基线时失败，见 05。
7. 是否存在架构不一致？
   库层只放在三类用户连接上。Oracle schema 没有做成库层。表缓存无库名时仍用连接 id，夹具 `tables['1']` 与 `col-1-users-id` 保持原样。
8. Evidence 是否足以证明任务完成？
   足以证明插件 `pnpm test`（118）和 `pnpm build`，以及 `design:check`。足以证明两条指定 CT 里除截图以外的断言。不足以证明真实库列举、快捷键和导出在浏览器里的手感，也不足以代替走查 / e2e。

## Requirement Check

PASS（相对本工人范围：前端实现 + 指定命令）。走查 / e2e 不在本项里算通过。

## Scope Check

PASS

## Test Check

PASS（插件 vitest 118/118，build，design:check）。CT：1 passed（库节点）+ 1 failed（仅缺截图基线）。后端测试未跑。

## Architecture Check

PASS

## Risk

L2

界面行为集中在 `DbAdminView.vue`。真实数据库与整页走查未做。

## Findings

### Critical

无。

### Major

无。未把缺基线的 `toHaveScreenshot` 记成 Major：`DbAdmin.ct.spec.ts` 头注释写明基线不入库，失败发生在断言之前的「请填写服务名」和留窗检查都已走过。

### Minor

- 右键菜单、列宽拖拽、Ctrl+W/Tab/T、PgUp/PgDn、导出全部的失败横幅，没有 CT。
- `03-架构与接口.md` 仍保留后端枚举 SQL 原文（该文件本来就写接口），不是设计师说明。

## Final Decision

APPROVED

本结论只覆盖已执行的插件单测、构建、design:check，以及 CT 里已经通过的 DOM 断言。`toHaveScreenshot` 未通过，原因是快照文件不存在。走查与 e2e 未做，留给主会话，不计入本结论。
