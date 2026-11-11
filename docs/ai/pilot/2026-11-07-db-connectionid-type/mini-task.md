# mini-task · DbAdmin connectionId 类型契约改「数字/字符串双收」

- 任务ID：`2026-11-07-db-connectionid-type` ｜ 日期：2026-11-07 ｜ 级别：轻量（≤3 文件，mini 合并 Intent/Spec/Plan/Task）
- 状态：**闸门1 待批**

## 1. Intent（为什么 / 做什么 / 到什么程度）

- 背景：外部 LLM 客户端经 MCP 网关调 `db_query_readonly` 等工具时，常把用户连接 id 直传为 JSON 数字（`"connectionId": 5`）。网关 ToolRegistry 按 schema `"type":"string"` 严格校验 ValueKind，直接拒收「参数 'connectionId' 类型错误，期望 string，实际 Number」（今日运行日志 14 次参数验证失败，约 10 次为此形态；客户端收错后还得自己转成字符串重试——即用户说的"幺蛾子"）。
- 目标：`connectionId` **数字直传可用**（`5` 与 `"5"` 等价），字符串传法与 `sys:<连接名>` 系统连接寻址完全不受影响。
- 成功判据：网关放行 `{"connectionId":5,"sql":"SELECT COUNT(*) ..."}` 返回正常数据；既有字符串路径回归绿、7 个工具既有测试全绿。

## 2. Spec（方案核心）

- 关键约束：用户连接 id 本质是 Int32（`DbConnectionService.LoadUser` 用 `Int32.TryParse`），系统连接 id 是 `sys:<ConnName>` 字符串——**不能改成字面 `"type":"integer"`**（会废掉系统连接寻址，除非连系统连接一起改数字 id，动 Resolve + HTTP + 前端，代价大）。
- **方案 A（推荐）**：db_* 四个工具 schema 的 `connectionId` **删掉 `"type": "string"` 键**。ToolRegistry 对无 type 属性的参数放行任意 ValueKind（`ToolRegistry.cs:227-230` 早退分支），插件层 `OptString` 早已把 Number → `GetRawText()` 容错（`DbAdminToolFunctions.cs:78-85`），`sys:` 字符串原样直达。即：**网关不再拦，插件双收**。
- 同步改 4 处参数 description：注明「用户连接 id 传数字或数字串均可；系统连接传 `sys:<连接名>`」。
- 兜底：布尔/对象/数组误传时网关放行，但插件 `OptString` 返回「参数 connectionId 应为字符串」业务错误——兜底仍在，只是从网关层下沉到插件层。

## 3. 改动清单（Plan，真实文件）

1. `Plugins/DbAdmin/Services/DbAdminToolFunctions.cs` :390 / :415 / :446 / :483 — db_list_tables / db_describe_table / db_query_readonly / db_execute_write 四处删 `"type": "string"`，description 改为双收说明。
2. `ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminToolFunctionTests.cs` — 新增 2 例：`connectionId = 5`（int，匿名对象序列化为 JSON number）调 db_query_readonly、db_list_tables → 成功；sys: 字符串路径回归不变。
3. `ForgeSelf.Api.Tests/Unit/ToolRegistryTests.cs` — 新增 1 例：schema 无 type 的属性传 Number → `ValidateParameters().IsValid == true`（锁住网关放行行为，防未来回改）。
4. `docs/02-features/044-db-admin.md` §2.1 — 把今晨写的「connectionId 类型与容错（2026-11-07 三态实测…）」段更新为新契约：数字/字符串双收 + sys: 说明 + 网关拦截行为已移除。

## 4. 不改（边界）

- 不改字面 `"type":"integer"`（废 sys:）；不改 ToolRegistry 对**有** type 属性参数的严格校验（其他工具契约不动）；不改 db_list_connections 出参（Id 仍为字符串 `"5"`，调用方回传字符串天然兼容）；不改 OptString/RequireInt64 实现；不动 Plugins/DbAdmin/docs（契约真源本就未给 connectionId 标类型，无需改）；评测集 `agent-tool-eval-cases.json` 回放用 `$user` 替换字符串 id，双收兼容，不动。

## 5. Task（Allowed / Forbidden + 验证）

- Allowed：上列 4 文件 + 当日日记 / TODO.md。
- Forbidden：其余 DbAdmin 代码、其他插件、宿主源码（ForgeSelf.Api 非 Tests 部分）、运行实例（禁启停，只读复验）。
- Interaction Design（无界面交互声明）：本批纯工具 schema 变更，**无 UI 改动**；交互面 = db_* 工具调用。入参示例 `{"connectionId":5,"sql":"SELECT ..."}"`；错误提示变化：数字直传从网关「类型错误，期望 string」变为正常返回；仅当误传布尔/对象时落到插件级「参数 connectionId 应为字符串」，对正常调用者不可见。无前端五态需求。
- 验证（快档 + 插件六步闭环）：① `dotnet build`（ForgeSelf.Api）② `dotnet test --filter "FullyQualifiedName~DbAdmin"` + ToolRegistryTests 过滤集 ③ 本插件 1 条冒烟 e2e ④ 起源码实例走 1 条数字直传 db_query_readonly 复验（dev-stack，只读）⑤ 文档回写 044。发布与运行实例更新待用户同意后另行触发。
- 实现前置：按 §0 规则先读 `plugin-development` 技能再动插件代码。

## 6. 代价与风险

- 网关对该参数失去类型把关（误传布尔/对象落到插件层报错）——影响面 = 单参数、有插件兜底；其余 7 工具校验行为零变化。
- 回滚点：4 处 schema 还原 `"type":"string"` 即回到现状（单文件 revert，无数据/契约迁移）。

## 7. 待拍板（闸门1）

- **Q1：connectionId 契约采用方案 A（去 type 限制、数字/字符串双收，保留 sys: 系统连接寻址）？** 默认推荐 A。备选 B = 字面 integer（需同时把系统连接改成数字 id，动 Resolve + HTTP 契约 + 前端，本批不做，可另立任务）。
