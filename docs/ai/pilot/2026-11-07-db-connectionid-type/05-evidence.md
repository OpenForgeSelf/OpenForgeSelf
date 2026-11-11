# Evidence — DbAdmin connectionId 数字/字符串双收（lite）

> Task ID：`2026-11-07-db-connectionid-type`｜方案 A 实施：2026-11-07（并行会话）｜验证收口：2026-11-11
> 闸门1 依据：**输入4 用户直接指令**（「你把定义改成数字…」2026-11-07 原文见 `.forgeself/memory/2026-11-07.md`）+ 2026-11-09 提交计划授权归档；mini-task §7 Q1 决议见下。

## Validation 结果（实际执行）

| 项 | 命令/位置 | 结果 | 级别 |
| --- | --- | --- | --- |
| 修复前三态基线 | 2026-11-07 输入1 用户实测：数字 5 直传 → `INVALID_PARAMETERS`（网关 `ToolRegistry.cs` 有 `"type":"string"` 时按类型拒 Number） | 修复前行为留档 | ✅ Verified（用户现场反馈） |
| schema 去 type（4 处） | `Plugins/DbAdmin/Services/DbAdminToolFunctions.cs`：`db_list_tables`/`db_describe_table`/`db_query_readonly`/`db_execute_write` 的 connectionId 属性仅留 description（双收文案）；`db_list_databases`（新增工具）同构 | 提交 diff 可核 | ✅ Verified |
| 网关层放行 | `ToolRegistryTests.ValidateParameters_PropertyWithoutType_AcceptsNumber`：无 type 属性 → `{"connectionId":5}` 与 `{"connectionId":"sys:Membership"}` 均 IsValid；对照有 type 属性 `{"table":7}` 仍拒 | 过滤集 r3 绿 | ✅ Verified |
| 插件层双收 | `DbAdminToolFunctionTests.ConnectionId_NumberDirect_QueryReadonly_And_ListTables_Ok`：`connectionId = 5`（Number）直传 db_query_readonly/db_list_tables 成功 | 过滤集 r3 绿 | ✅ Verified |
| 兜底不破坏 | `DbAdminToolFunctionTests.ConnectionId_BooleanStillRejected_AtPluginLayer`：布尔误传 → 插件层业务错误「参数 connectionId 应为字符串」（错误形态从网关类型错变为插件参数错，信息量更好） | 过滤集 r3 绿 | ✅ Verified |
| 字符串路径回归 | 既有全部测试（`sys:*`、`"999999"` NotFound、`db_list_connections` 回显 Id 恒字符串）+ r3 425/425 | 全绿 | ✅ Verified |
| 044 文档同步 | `docs/02-features/044-db-admin.md`：connectionId 双收说明 | 已更新 | ✅ Verified |

## 判据映射（mini-task §2 方案A）

- ①4 处 schema 删 `"type":"string"` → Verified（diff）。
- ②description 双收文案 → Verified（4 处工具 schema）。
- ③网关无 type 放行 + 对照严校 → Verified（ToolRegistryTests 新例）。
- ④插件 Number→GetRawText 容错 + 布尔兜底 → Verified（2 新例）。
- ⑤`sys:*` 字符串兼容 → Verified（既有回归 + 新例含 `sys:Membership` 字符串分支）。

## Unknown / 未覆盖

- 冒烟 e2e 与起源码走查未跑（合并入多库批 2026-11-07-db-multi-database 的收口缺口，同批提交后补）——Unknown。
- 运行实例只读复验未做（触发条件：用户实例更新到新版本后）——Unknown。
- 本目录为 lite 档（≤3 文件契约修复，mini-task 已载全量档指针：实现并入 `2026-11-07-db-multi-database`）。

## 决议（mini-task §7 Q1 回写）

Q1「网关 connectionId 类型定义数字 or 字符串」→ **按输入4 用户直接指令采用方案 A**（网关删 type 锁 + 插件双收兜底），2026-11-07 实施完成、2026-11-11 验证收口；验证证据即本文件，缺陷判据（三态）留档于上表首行。
