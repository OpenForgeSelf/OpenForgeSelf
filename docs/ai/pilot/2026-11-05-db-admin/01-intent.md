# Intent — db-admin 并入 main（收口与验收）

> Task ID：`2026-11-05-db-admin`｜来源：lead 夜冲刺派发（团队任务板 task-5 [T-E]）

## Problem

`feat/db-admin` 分支上 DbAdmin 插件的 10 个自有提交（后端 MVP / Web UI / Agent 工具 / 契约对齐 / 评测）停在旧基座（含 smart-terminal 的 11 个提交），尚未在新 main 之上验证；工件链（docs/ai/pilot/2026-11-05-db-admin/ 00-07）与功能文档（docs/02-features/044-db-admin.md）缺失；e2e 冒烟用例缺失；没有走查证据。没有这些，分支不可并入 main、更不可发到用户 ：51888 宿主。

## Why

今夜冲刺目标：早上把「智能终端 + 数据库管理」两插件合并进 main 并发到用户宿主。db-admin 侧（本任务）必须产出 **merge-ready** 状态：rebase 后全门禁绿 + 测试绿 + 1 条冒烟 e2e + 起源码走查证据 + 工件 00-07 齐，lead 才能安全合并与发布。

## Expected Outcome

1. `feat/db-admin` = 新 main 之上的「工件+功能文档」提交 → 10 个代码提交（逐个带 `Pilot: 2026-11-05-db-admin` trailer）→（收口期新增）e2e 冒烟与路由一致性提交，内容零意外变化。
2. `pwsh scripts/ci/run-gates.ps1 -Base main` PASS（pilot-gate + style-tokens）。
3. 后端过滤集全绿（DbAdmin* + XCodeConfigTests + PluginLoadDependencyGuardTests + PluginToolConstructorDiGuardTests）；前端 check/test/vue-tsc 绿；插件 web build + vitest 绿。
4. `e2e/plugins/db-admin/db-admin.spec.ts` 恰 1 条冒烟主链路跑绿 + 截图读图。
5. `/db-admin` 路由一致性条目落 menu-route-consistency.spec.ts（与 st-dev 协调后由本任务一人落笔），知会 qa-ci 跑中档。
6. dev-stack 起源码逐功能走查（8 项）+ 截图读图 + 清测试数据；05 记用户动线成本。
7. 05/06/07 补齐（07 含问题类归档/重构提案/悬空教训三节），报 lead merge-ready。

## Constraints

- 禁 git push；禁停/启/杀宿主进程；主工作区禁切分支、禁碰 WIP 清单；实体不改（改则必走 Model.xml→xcode）。
- rebase 冲突以 main 内容为准手动解；历史重排保持内容零变化（仅信息与顺序）。
- 破坏性 SQL 在任何验证里只走「审批→拒绝」路径，绝不真执行；测试库文件一律放 .temp。
- 功能文档编号 044（043 已归 smart-terminal，st-dev 2026-11-05 01:50 确认）。

## Success Criteria

见 02-spec §9 AC1..AC8；全部 Verified 后任务完成，向 lead 报 merge-ready。
