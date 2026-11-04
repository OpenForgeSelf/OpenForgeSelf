# Plan — db-admin 并入 main（收口与验收）

> Task ID：`2026-11-05-db-admin`｜执行者：db-dev｜worktree：`D:\src\my-proj\OpenForgeSelf\wt-db`（feat/db-admin@ d9fca5a = smart-terminal 11 + db-admin 10）

## 步骤（每步可独立验证）

| # | 步骤 | 改什么 | 验证 |
|---|------|--------|------|
| P1 | 备料期工件 | 本目录 00-04 + docs/02-features/044-db-admin.md + 05/06/07 骨架（05 含全部 AC token、06 含 Final Decision 字样，过 pre-commit 机器校验；真值 ⑦ 回填） | 提交成功（pre-commit 绿） |
| P2 | 提交工件 | wt-db 内 `git add docs/**` 单提交「docs(db-admin): pilot 工件链 + 功能文档 044」（纯文档，无 trailer） | git log -1 核对 |
| P3 | 等 lead「T-C 完成」→ rebase | `git rebase main`（smart-terminal 11 提交 patch-id 自动去重；冲突以 main 内容为准手动解：登记类文件保留两侧新增行） | `git log --oneline main..HEAD` = 工件提交 + 10 代码提交 |
| P4 | 历史重排 | `git rebase -i`（GIT_SEQUENCE_EDITOR 脚本化）：工件提交移至最底；10 个代码提交逐个 reword 追加 trailer；`git branch backup/db-admin-pre-rebase` 留回滚点 | `git log --format='%s%n%(trailers)' main..HEAD` 逐提交核对 |
| P5 | 门禁 | `pwsh scripts/ci/run-gates.ps1 -Base main`（读日志正文 PASS，不认 exit code） | pilot-gate + style-tokens PASS |
| P6 | 后端验证 | `$env:TEMP/TMP=<wt>\.temp\tmp` + NO_PROXY → `dotnet build`（0 error）→ `dotnet test --filter "FullyQualifiedName~DbAdmin|XCodeConfigTests|PluginLoadDependencyGuardTests|PluginToolConstructorDiGuardTests"` | 日志计数全绿 |
| P7 | 前端验证 | DbAdmin/web build+test（已预跑 61/61 绿，重跑确认）；ForgeSelf.Web check + test + `npx vue-tsc -b`（对表基线） | 日志正文计数 |
| P8 | e2e 补缺件 | 拷 System.Data.SQLite.dll + e_sqlite3.dll → `build/runtime/Plugins/`；`for d in Plugins/*/web: pnpm install && pnpm build` | 两缺件存在性断言 |
| P9 | e2e 冒烟 | 新建 `e2e/plugins/db-admin/db-admin.spec.ts` 恰 1 条主链路（建 SQLite 测试连接[库文件 .temp]→schema→SELECT→结果；写 SQL 审批→拒绝）→ 定向跑绿 → 截图 + 读图 | playwright 日志 1 passed + 截图文件在 |
| P10 | 路由一致性 | menu-route-consistency.spec.ts ④ 回归清单补 smart-terminal+db-admin（st-dev 已确认由我落笔）→ 提交（带 trailer）→ 知会 qa-ci | spec 内含两 id；消息回执 |
| P11 | 走查 | `pwsh scripts/dev-stack.ps1`（worktree 内起，起停登记 TODO.md 协作交接）→ 8 功能逐项走查+截图 `screenshots/dev/db-admin/` + 读图核对 + 清测试数据 → 停 dev-stack | 截图在 + 读图记录 |
| P12 | 收口 | 05/06/07 真值回填（07 三节）→ 提交 → send_message 报 lead | 机器校验绿 + 回执 |

## 风险与对策

- **rebase 冲突**：slnx/csproj/XCodeConfig 等登记类文件两侧同段新增 → 以 main 内容为准、保留本侧必要行；解后必跑 dotnet build 复核；backup ref 随时可弃。
- **DLL 锁**：qa-ci 全量跑期间不 build（任务板 task-4 约定）；本 worktree build 独立目录互不相干；撞锁（MSB3027）→ 报 lead 开窗。
- **e2e 环境**：TEMP 重定向 + NO_PROXY 前置；:7102 被占→globalSetup 用例端口约定（读 e2e-env.ts 实测）；publish 宿主由 globalSetup 自建自毁，不碰用户实例。
- **基线红对表**：前端 check/test 与后端过滤集之外的全量归 qa-ci；本批新增红才归责。

## 偏差记录（执行期回填）

- （暂无）
