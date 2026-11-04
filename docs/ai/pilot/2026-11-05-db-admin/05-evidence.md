# Evidence — db-admin 并入 main（收口与验收）

> Task：`2026-11-05-db-admin`｜证据分级：**Verified**（本会话亲自跑出真实输出）/ Inferred（代码推断）/ Unknown（未验证）。
> 状态：**骨架版（备料期提交过门禁用）——AC1..AC8 真值随执行回填（03-plan P12），回填后本节为最终版。**

## 0. 证据清单（执行后逐条回填，含命令与日志路径）

| # | 证据 | 命令/来源 | 结果 |
|---|------|-----------|------|
| E1 | 历史重排 | git log/range-diff | ⏳ 回填（AC1） |
| E2 | run-gates -Base main | 日志正文 | ⏳ 回填（AC2） |
| E3 | dotnet build + 过滤集 | 日志计数 | ⏳ 回填（AC3） |
| E4 | 插件 web build+vitest / 宿主 check+test+vue-tsc | 日志计数 | ⏳ 回填（AC4） |
| E5 | e2e 冒烟 + 截图读图 | playwright 日志 + screenshots/e2e/db-admin/ | ⏳ 回填（AC5） |
| E6 | 路由一致性条目 + qa-ci 知会 | spec diff + send_message 回执 | ⏳ 回填（AC6） |
| E7 | dev-stack 走查 8 项 + 截图读图 + 用户动线成本 | screenshots/dev/db-admin/ | ⏳ 回填（AC7） |
| E8 | 工件链机器校验 | pre-commit / verify-pilot-artifacts | ⏳ 回填（AC8） |

## 1. AC1..AC8 映射（token 逐字对应 02-spec §9；状态列回填）

| AC | 断言要点 | 证据 | 状态 |
|----|----------|------|------|
| AC1 | 历史重排：工件提交垫底 + 10 代码提交带 Pilot trailer + st 11 提交去重 + 内容零意外变化 | E1 | ⏳ 待验证 |
| AC2 | run-gates -Base main PASS（日志正文） | E2 | ⏳ 待验证 |
| AC3 | dotnet build 0 error + 过滤集全绿 | E3 | ⏳ 待验证 |
| AC4 | 插件 web + 宿主前端三项绿 | E4 | ⏳ 待验证 |
| AC5 | e2e 冒烟 1 条绿 + 截图读图 | E5 | ⏳ 待验证 |
| AC6 | 路由一致性条目 + 知会 qa-ci | E6 | ⏳ 待验证 |
| AC7 | 走查 8 项 + 截图读图 + 清数据 + 动线成本 | E7 | ⏳ 待验证 |
| AC8 | 工件链齐 + expected_files 覆盖 + 07 三节 + 报 lead | E8 | ⏳ 待验证 |

（预置事实：备料期已实测——DbAdmin/web vitest 61/61 绿、dotnet build 0 error、插件 DLL 落宿主 bin，日志 .temp/log-dbweb-*.txt、log-build-*.txt；终态以 AC3/AC4 回填为准。）

## 2. 用户动线成本（AC7 走查后回填）

⏳ 待 ⑥ 走查实测回填。

## 3. 已知边界与风险

⏳ 回填。
