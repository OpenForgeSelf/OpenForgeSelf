# Intent — DbAdmin 连接级多库（数据库非必填 + 一连接多库浏览）

> Task ID：PILOT-db-multi-database ｜ 日期：2026-11-07 ｜ 来源：输入5（用户原话见日记）

## Problem

- 现在添加连接**必须**填「数据库」：后端 `DbConnectionService.cs:201/:446` 与前端 `connForm.ts:54/:59` 双侧硬校验。想先连上服务器看看有哪些库、再挑库浏览，做不到——必须预先知道库名。
- 打开一个连接后只能看到连接串里写死的那个库的表，无法在多个库之间浏览/切换。
- Agent（MCP 工具）同样被锁死在连接的默认库上，没有任何枚举库/指定库的工具能力。

## Why

- 服务器型数据库（mysql/sqlserver/postgresql）天然是「一个实例多个库」，连接串里钉死单库与真实使用方式相悖；用户原话：「添加连接，数据库非必填，一个连接打开，可看到多个数据库」。
- Agent 场景（LLM 调 db_* 工具）需要先枚举库再查询，否则只能靠用户预先告知库名，链路不自治。

## Expected Outcome

1. 添加连接：mysql/sqlserver/postgresql 的「数据库」**非必填**（留空=连服务器默认上下文）；sqlite（=文件路径）与 oracle（=服务名）保持必填（这是其寻址结构，不是同一个概念）。
2. 打开连接后可**枚举该连接可见的数据库**并**切换**浏览：表列表、表结构、查询、写操作（经既有审批流）都跟随所选库。
3. MCP 工具对齐：新增枚举库工具；4 个既有查询/写工具加可选 `database` 参数。
4. 顺带同批落地输入4 的 connectionId 类型契约（去 type 限制、数字/字符串双收），一次闸门。

## Constraints

- 不改 `DbConnection` 实体（Model.xml 无需动——Database 列本就可空）。
- 系统连接（`sys:<宿主连接名>`）保持单库只读，不参与多库。
- 不改既有 HTTP 端点语义：`database` 全部作为**可选**参数追加，不传 = 现行为（兼容既有前端/评测集）。
- Provider 差异明示：oracle 不做「切库」（schema 枚举仅供参考，查询需显式 `schema.table`）；sqlite 不适用。
- 遵守 plugin-development 六步闭环 + 全量工件档（新功能 + 契约变更）。

## Success Criteria

- mysql/sqlserver/postgresql 三类连接不填 Database 可创建成功（HTTP + 实体回显 database=null）；sqlite/oracle 不填仍 400，文案不变。
- `GET /api/db-admin/connections/{id}/databases` 按上表返回库清单（含 isDefault 标记；oracle 返回 schema）。
- 带 `database` 参数的表/描述/查询/写端点与工具，在该库上执行（SQLite 端到端真实验证；mysql/sqlserver/pg 为连接串+SQL 生成断言，真机行为标 Unknown 留走查）。
- 带 `database` 的写操作走审批后**在同一库**重放（审批内存记录补 database 字段，单测覆盖）。
- `db_list_databases` 工具经 MCP 网关可发现、可调用（sqlite 实测）。
- connectionId 数字直传不再被网关拒（输入4 判据）。
- 前端：库选择器五态齐备（加载/出错+重试/空/正常/不支持类型隐藏），切库重载表、竞态防护、切库后查询携带所选库。
