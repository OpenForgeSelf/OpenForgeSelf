# data-service-env-mirror — 数据服务接口跨环境镜像（测试→正式/多环境）

> 触发词：镜像到正式环境、同步接口到生产、第二环境建同款接口、正式环境也配上、测试环境的接口搬到线上、配置包。
> 场景：源环境（如 .188 测试）已建好、已调通的数据服务接口，要在一个**独立部署**的目标环境（如 9180 生产集群，`ioc.luxvisions-inno.com:9180`）原样重建。
> 姊妹技能：`db-data-service-development`（单环境建接口全流程）、`data-service-sql-alignment`（SQL 口径改写铁律）——**两者铁律全部沿用**；本技能只管"跨环境搬运"这一段。
> 实例：`Doc/03-feature-guides/数据服务API/宿舍统计-需求记录.md` §7（9180:1）；`正式环境镜像配置包/`（越南 vnimes 20 条，**2026-11-12 已执行：9180 ID 3~22，验证四连闭环，见该包 README §8**）。

## 铁律（先读）

1. **权威回读，不靠文档重建**：源接口配置一律 `GET /api/ioc/Admin/DataService?pageIndex=1&pageSize=500`（列表即全字段含 sqlText，无截断）从**源平台**回读落成配置包；文档（需求记录/改写稿）只作交叉核对。手工誊抄 SQL 迟早抄错。
2. **真实数据命中才算调通，空数组不算成功**：镜像验证必须用源环境实测过的真实参数回放（配置包 manifest 的 `verify.sample` 就是干这个的）；EMS 空表类"恒空"接口不值得镜像（无 DBA 不补表的前提下），默认排除并写明原因。
3. **目标环境只读选用既有连接、不删改任何既有连接/接口**（先例：9180 ID=1 宿舍接口、CN2B_MachineStatus、DCS 连接不可触碰）；新连接**不自动建**——凭据不入档，属生产变更，由用户操作或明确授权。
4. **执行前打勾前置条件**：目标环境可达 ☐ → 网络可达源库（.188 可达 ≠ 目标可达）☐ → 连接已建并拿到目标连接 ID ☐ → 新功能已发布（用户指令前置）☐。任一不满足就停在准备态，不硬跑。
5. **镜像 ≠ 复制 ID**：目标环境 ID 独立自增，文档回写一律 `环境:ID` 形态（如 `9180:3`）；`connectionId` 必须重映射为目标环境连接 ID（配置包 capture 里 `connectionId=0` 占位、执行时回填）。
6. **capture 文件保持 POST 就绪**：摘除 id/createUser/createTime/updateUser/updateTime/isDeleted/lastTestParamsJson；保存端会自动补默认值、校验（描述≤512、占位符须声明、路由冲突警告）并刷新路由。
7. **验证四连缺一不可**：token 直连真实命中 + 匿名 401/40102 + 管理面落库确认 + 映射表落盘；契约差异以**目标环境发布后的代码**为准（例：2026-11-11 分页契约改 `pageIndex/pageSize/totalCount`，接口配置本身不变）。
8. **未经 commit-audit 批准不提交**；在生产平台「保存并发布」即时生效，属生产变更，先经用户同意再执行。

## 六步流程

### 1. 盘点源环境
- 管理面登录（源环境凭据见姊妹技能记录）→ localStorage `ioc_token` → `GET /api/ioc/Admin/DataService?pageIndex=1&pageSize=500` 一次拉全（注意默认分页 20 条，**必须带 pageSize**）。
- 按数据源连接（`connectionId`）筛目标范围；排除禁用的临时验证接口、空数据关闭项，逐条写明排除原因。

### 2. 落配置包
- 目录：`Doc/03-feature-guides/数据服务API/<目标>-镜像配置包/`；`captures/{源ID}_{Code}.json` + `manifest.json`（含验证样例与预期行数）+ `mirror-to-target.ps1` + `README.md`（前置条件/执行/验证/回写）。
- manifest 的 `verify.sample` 取自源环境验证记录的**真实参数**；当日窗类接口注明"空数组=正确行为"的例外。

### 3. 前置检查（目标环境）
- 连接：`GET /api/ioc/Admin/DataService/GetConnections` 确认目标连接存在（没有则先走生产变更流程建连接）。
- 路由冲突预检：数据服务与既有系统接口同路径时**警告不拒绝**、运行期数据服务接管——先肉眼核对目标环境有无同路径系统接口。

### 4. 执行重建
- `mirror-to-target.ps1 -BaseUrl <目标> -Token <JWT> -ConnectionId <连接ID> -Verify -WhatIf` 先看计划，去掉 `-WhatIf` 执行。
- 单条失败不阻塞其余；失败条目原样记录，处理后 `-Include <源ID>` 重跑。
- 结果映射 `mirror-result-<时间戳>.json` 随包归档。

### 5. 验证闭环（见铁律 7）+ 文档回写
- README 实况表按 `环境:ID` 加行；需求记录补 §第二环境；manifest 更新执行状态；新坑回写本技能。
- **收尾必做：测试参数留存**——逐条 `POST TestSql{id, parameters:样例}`（成功后服务端写 LastTestParamsJson）+ `GET TestParams/{id}` 回读非空；对外路由直连验证不产生留存（坑清单详）。

## 实证坑清单（持续追加）

- 列表接口默认分页 20 条——不带 `pageSize=500` 会漏掉大半接口还以为"就这么多"（2026-11-12，.188 总 253 条实测）。
- 列表响应即全字段（含 sqlText/parametersJson/responseSchemaJson），**无需**逐条 `GET /{id}`（2026-11-12 实测）。
- Playwright MCP 输出目录与工作区不一致、`browser_run_code_unsafe` 内 `require('fs')` 不可用——token 直接从 localStorage 取出经 write 工具落临时文件，后续全在 pwsh 侧完成（2026-11-12）。
- PowerShell 侧调源平台 Admin API：`Invoke-RestMethod -SkipCertificateCheck -Headers @{authorization="bearer <jwt>"}`；GET 列表带 `?pageIndex=1&pageSize=500`（2026-11-12）。
- **TestSql 响应是扁平结构**：`{code,message,data:[...行],elapsedMs,cached}`，`data` 直接是行数组；数行数用 `$r.data.Count`，别按嵌套 `data.data` 解析（会误报"0 行"，2026-11-12 实踩）。
- **POST /api/ioc/Admin/DataService 响应 `$r.data` 是完整实体对象**（非纯 id）——映射表落盘取 `newId.id`；脚本若把整个实体当 newId 存，回写文档时补 `.id`（2026-11-12 实测）。
- **重 SQL 冷缓存首跑 408**（786：30s 接口超时，首跑超时）——复跑即 14.8s 正常，属首跑超时非配置问题；验证脚本要对 408 重试一次再定论（2026-11-12 实测）。
- **POST 接口脚本验证必须带 body**：对 POST 接口发 `{}` 会 400 缺参——先从源表取真实值（如 `SELECT MACHINE_CODE FROM IMES.M_MACHINE`）构造 body 复验（2026-11-12 实测）。
- **空数组排查先证"同库同态"再谈差异**：用一条无关轻量 SQL（如近4h `P_SN_STATUS` 计数、`F_MTBA` N+MAX(PDATE)）在源/目标连接双跑，计数一致即同库——剩下的空数组全部归因数据演进（历史窗被清理、当日窗无数据），用"今日真实有数据的机台"（源表 GROUP BY 反查 TOP）复验即可拿到真实命中；别急着怀疑连接配置（2026-11-12，.188:2 vs 9180:4 双 1054 实证）。
- **测试参数留存只认 `TestSql(id>0)` 成功**：`LastTestParamsJson` 由 TestSql 成功后服务端回写（谁测谁覆盖），**对外路由 token 直连不写**——镜像脚本 Verify 走路由直连 20/20 命中也不会留参数。镜像收尾必须补一轮 `POST TestSql{id, parameters:样例}` + `GET TestParams/{id}` 回读非空，前端「测试执行」Tab 才能自动填充（2026-11-12 实踩：用户指出 20 条全没留存）。
- **留存样例要换"当日实证命中参数"再存**：manifest 旧样例（10-25/08-11 窗、AOI_001 等）执行成功但可能 0 行——留存的意义是"下次打开一键复测命中"，用近窗/今日活跃机台覆盖后再留存（2026-11-12）。
- **TestSql 顶层 `pageIndex/pageSize` 字段被忽略**（2026-11-12 实测 page=null 全量返回）：分页参数是"系统公共参数"，必须放进 `parameters` 字典（或走对外路由 query）才生效；用 TestSql 探 totalCount 时注意。
- **`TestParams/{id}` 返回的 data 是 JSON 字符串不是对象**（2026-11-12 实测）：直接塞回 TestSql 请求体报 "The req field is required."——先 `| ConvertFrom-Json` 再用。
- **建接口必须配 `ResponseSchemaJson`（响应字段说明）**：不配则 Swagger data 节点只有裸 object，调用方不知道返回什么字段/含义/取值（2026-11-12 用户指出，9180+.188 共 20 条全漏，补 135 字段）。字段名以真实响应列为准、description 写含义+格式+取值示例；配完拉 `/swagger/dataservice/swagger.json` 核对字段已展开。
- **目标端独有调优（如补开分页）不回写 captures**：captures 保持源环境权威快照；目标端 delta（enablePaging 等逐条改动）记 manifest `targetEnv.pagingDelta` + 两边 README，并在实况表路由列标"分页✅"（2026-11-12，9180 补开 5 条实例）。
- **当日/滚动窗类接口的验证样例会过期**：10-26 实测 21 行的历史窗，11-12 复跑双环境同为 0（源库数据演进）——manifest 样例优先用"近期窗口"，并把源表分布反查法写进 verify 步骤（2026-11-12）。
- **`.188:443` 登录页已改扫码-only**（账号登录切换图标点不动）；测试平台账号登录走 **`https://10.188.32.188:8082/`**（2026-11-12 用户指路实测）。

## 关联

- 配置包实例（20 条 vnimes 接口，2026-11-12 已执行完成）：`Doc/03-feature-guides/数据服务API/正式环境镜像配置包/`
- 镜像先例（单条手工版）：`Doc/03-feature-guides/数据服务API/宿舍统计-需求记录.md` §7
- 建接口全流程：`.agents/skills/db-data-service-development/SKILL.md`
