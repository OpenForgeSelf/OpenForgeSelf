# Review — DbAdmin 连接级多库 + connectionId 双收

> 阶段：Stage 8｜Task ID：`2026-11-07-db-multi-database`｜Review 日期：2026-11-11（验证收口会话）

## 八问

1. **改动是否超出 04-task Allowed 边界？** 否。全部改动落在 S1-S6 声明的文件族：DbAdmin 服务/控制器/模型、插件 web、DbAdminTests、ToolRegistryTests、e2e spec、CT story、docs 044 与插件 docs；实体/Model.xml 零改动（Forbidden 遵守）。
2. **AC 覆盖是否完整？** AC1-AC6 机制级 Verified（见 05 对照表）；AC7 的 CT/e2e 两项未跑（显式披露 + 记 TODO 补跑）。AC-I1…I5 走查留痕未做（Unknown）。
3. **验证是否充分？** 快档全绿（后端过滤集 425/425、插件 web 104/104+build、宿主 web check/1203/design 三绿）。多库/server 型真机与走查/运行实例复验未做——属 plugin-development 六步闭环的 ③⑤ 缺口，随提交后补。
4. **主要风险？** ① server 型（mysql/sqlserver/pg/oracle）真机行为未验——spec Unknown 表预登记，错误以 sql_error 脱敏回显；② 审批保库在「派生库名≠默认库」真机场景未验；③ e2e/CT 用例已写未跑，存在用例自身有错未暴露的风险。
5. **回归面？** 既有 DbAdmin 全部测试、插件 web 全部单测、宿主 web 1203 用例、design 棘轮零增量——均绿；connectionId 双收对既有字符串调用天然兼容（`db_list_connections` 出参 Id 恒字符串）。
6. **文档是否同步？** docs/02-features/044-db-admin.md（必填矩阵/库枚举切换/工具契约/connectionId 双收段）+ 插件 docs（00/01/02/03/05/06/README/ui-design-brief）均已随批更新。
7. **同类扫查？** 本批为功能而非缺陷类，未触发 problem-class-closure；但「工具目录新增工具后计数断言陈旧」属测试脆弱性——已将契约测试改为显式名单断言（8 工具全列出），后续新增工具会再次显式失败提醒更新，不再依赖魔法数。
8. **遗留项？** e2e/CT 执行、起源码走查、运行实例复验、server 型真机验证——均已记入 07 风险与 TODO。

## Final Decision

**APPROVED**（机制级验收通过）：核心判据（多库枚举/切换/审批保库/connectionId 双收）均有自动化测试实证；风险全部显式披露于 05 Unknown 节与 07，不阻塞提交归档；CT/e2e/走查缺口在提交后按 plugin-development ⑥ 补齐。
