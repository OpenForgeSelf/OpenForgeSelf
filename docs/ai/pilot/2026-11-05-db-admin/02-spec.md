# Spec — db-admin 并入 main（收口与验收）

> Task ID：`2026-11-05-db-admin`｜AC 编号**无连字符**（EnforceAcCoverage 机器查，05 映射表逐字含同 token）
> 插件功能规格的完整真源 = `Plugins/DbAdmin/docs/01~07`（炳宏定稿）+ `docs/03 §3.1 已定契约`；本文件只承载「收口验收」口径并摘要功能契约。

## 1 背景与目标

DbAdmin（可审批的 SQL 工作台）代码已完成于 feat/db-admin（10 个自有提交）。本任务把该分支收口到可合并状态：历史重排（工件提交垫底 + 代码提交带 trailer）→ 新 main 之上全门禁 → 测试 → e2e 冒烟 → 路由一致性 → 起源码走查 → 工件 00-07 齐 → 报 lead merge-ready。**不新增功能、不改既有逻辑**（验证暴露缺陷才修，逐条记 03-plan 偏差）。

## 2 范围（In / Out）

**In**：docs/ai/pilot/2026-11-05-db-admin/ 00-07、docs/02-features/044-db-admin.md、git 历史重排（wt-db）、`ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`（新）、`ForgeSelf.Web/e2e/menu-route-consistency.spec.ts`（④ 回归清单加 smart-terminal + db-admin 两项，与 st-dev 协调定稿）、验证与走查产生的截图（screenshots/ 不入库）、测试数据清理。

**Out**：Plugins/DbAdmin/** 业务逻辑（已实现，不改）、宿主登记文件（已在历史提交中）、非 SQLite 引擎驱动打包（决策1：不打包）、`:51888` 实例任何操作、git push、ImGateway/AIAgent WIP 全部路径、docs/02-features/031-im-gateway.md（只读）。

## 3 功能契约摘要（验收对照用；真源 Plugins/DbAdmin/docs/03 §3 + §3.1）

- HTTP 前缀 `/api/db-admin/*`，类级 `[Authorize("ApiKeyPolicy")]`；错误体 `{code,message}`；provider id 小写（sqlite/mysql/postgresql/sqlserver）；连接 id=数字串（用户）| `sys:<ConnName>`（系统）；kind 对外只有 read/write，破坏性独立 `destructive:true`；`/query` 200 ok/rejected、202 awaiting_approval、error 按 code 映射；测连恒 200 `{ok,code,message,serverVersion,elapsedMs}`；审批轮询 `GET /approvals?status=pending`（5s）。
- 安全模型：系统只读连接（宿主+PluginDbs）任何写硬拒；用户连接默认 AskWrite（Auto 只免普通 DML 询问）；SqlGuard 硬拒（无 WHERE UPDATE/DELETE、多语句、DROP DATABASE/SCHEMA、ATTACH、shell/文件越界约 20 模式）；破坏性须 UI 二次确认（Agent 恒 confirmation_required）；审批 TTL 30 分钟、原始 SQL 仅内存；密码 AES-256-GCM（dbadmin.key）落库、GET 恒 `****`；审计全量脱敏；Agent 上限 1000 行/256KB（truncated），UI 分页 ≤500。
- Agent 工具 7 件（db_*）：list_connections/list_tables/describe_table/query_readonly（严格 SELECT/WITH）/execute_write/set_permission_mode（会话级）/resolve_approval（破坏性不可批、批准需用户原话）。

## 4 Interaction Design（有 UI ·「点什么出现什么」· 五态 · 验收）

布局：左栏「+ 新建连接 + 系统只读区(🔒) + 我的连接 → 表 → 列」；右区「连接条 + SQL 编辑器（Ctrl+Enter 执行）+ 执行/历史 + 结果格（分页 50/100/500）」；浮层互斥：连接表单→审批→破坏性二次确认→删除确认→审计抽屉。以下触发→结果表（data-testid 见 DbAdminView.vue）：

| 触发 | 出现什么 |
|------|----------|
| 进入 /db-admin | 版本徽标（先「加载中」后 v0.1.0）+ 左树（系统只读区恒显）+ 权限徽章（AskWrite/Auto） |
| 点「+ 新建连接」（empty-guide/new-connection） | 连接表单浮层（cf-*）：provider 下拉（缺驱动项灰标「需驱动」）、表单↔连接串切换、密码遮罩、非 SQLite 显示「未实测」标注 |
| 表单点「测试连接」（cf-test） | test-result 三态：成功（绿，含服务器版本）/ 失败（红，脱敏原因）/ 驱动缺失（黄，指明 NuGet 包名） |
| 表单点「保存」（cf-save） | 校验失败留窗显示 cf-error；成功关浮层、左树出现新连接、落盘持久化 |
| 点连接（conn-{id}） | 右区连接条更新（名称/provider 标签/只读徽标）、schema 树可展开（table-{id}-{name} → 列/类型/PK） |
| 编辑器输入+Ctrl+Enter 或点「执行」（run） | 只读语句→结果格（result-grid：列头+行+result-stats 耗时/行数，分页 pager 可翻页）；写语句（AskWrite）→ 202 → 审批弹窗；硬拒/只读连接写 → result-error 红条（result-error-code），**不弹审批** |
| 审批弹窗点「批准」（approval-approve） | 普通写→执行并显示 affectedRows；破坏性→先二次确认（destructive-confirm/cancel），确认后执行、取消则保持 pending 不执行 |
| 审批弹窗点「拒绝」（approval-reject） | 弹窗关闭、状态 rejected、语句不执行、审计有记录（approval_rejected） |
| 点「审计」（open-audit） | 审计抽屉（audit-drawer）：kind 过滤（audit-filter）、条目含 query/approval/rejected/agent 等；密码与密钥恒 `****`（S7） |
| 浏览器刷新（S-R） | 恢复连接列表+当前连接+编辑器草稿，顶部 restore-hint 提示；**不恢复**密码/结果集/待审批弹窗（待审批从服务端重拉） |
| Agent 发起写（S8） | 同一审批队列，弹窗标注「来自 Agent」（approval-from-agent）；tab-agent 回显审计 kind=agent 摘要 |

五态：加载=版本徽标「加载中」+树骨架（tree-skeleton）；空=无自建连接引导（empty-guide，系统区仍显）；出错=顶栏红横幅（banner）+结果区错误；无权限=401 横幅（banner-401），不假装有数据；正常=树/执行/审批可用。走查按 `ui-ux-design` 符合性清单 + `Plugins/DbAdmin/docs/06`（S0–S9+S-R）逐项核对，不只「能显示」。

## 5 Failure Modes（00 §4 命中 PC 的预填）

- PC-003（PluginDbs 未登记）：已登记且 AC3 过滤集复核 → 满足。
- PC-020（插件依赖/构造注入宿主契约）：控制器只注入自有服务；PluginLoadDependencyGuardTests+PluginToolConstructorDiGuardTests 在 AC3 → 满足。
- PC-005（构造期 eager provider）：DbToolBase 执行期 CreateHostScope → 满足。
- PC-014（测试换库）：Fixture 集合级单次建连+随机隔离目录 → 满足。
- PC-019（flex-shrink 压扁）：06 清单项5 + ⑥ 走查读图核对 → 满足。
- PC-002（前端裸 fetch）：http.ts 统一封装 + 401 态 UI + e2e 真实 token → 满足。
- PC-006/007（PS 5.1/手敲起服务）：全程 pwsh + 仅 dev-stack.ps1 → 满足。
- e2e 新 worktree 两缺件（SQLite provider DLL ×2 + 各插件 web dist）：④ 显式补齐步骤 → 满足。

## 6 非功能

- 验证零 mock（e2e 走真实宿主+真实 token）；测试库文件一律 .temp 随机目录；绝不真执行破坏性写（审批走拒绝路径）。
- 走查后清测试数据：只删本批自建连接/查询历史（库文件留 .temp 不动）。
- 门禁分档：本批改动面=文档+e2e+路由 spec → 快档为主；run-gates -Base main + 后端过滤集 + 前端三项 + 插件 e2e；全量中档由 qa-ci 在 main 侧跑（本任务知会）。

## 7 数据模型

插件自有库 ConnName=DbAdmin 四表（Model.xml 真源）：DbConnection（Name 唯一、PasswordEnc/ConnStrEnc）、DbQueryHistory、DbApproval（Status 索引）、DbAuditLog（kind=agent 摘要列）。本批不改实体；旧库由 XCode 反向工程自动补列（docs/03 §3.1④）。

## 8 Unknown（不猜，显式登记）

- rebase 冲突的具体形态（slnx/csproj 两侧同段新增行）以实际执行为准，解法=main 内容为准+保留本侧必要行。
- ForgeSelf.Web pnpm run test / vue-tsc 在本 worktree 的基线红清单以实测日志为准（对表 TODO/最近全量记录，新增红才归本批）。
- e2e 冒烟在 publish 宿主上的端口/token 注入细节以 e2e-env.ts + globalSetup 实测为准。

## 9 验收标准（AC1..AC8，05 映射表逐字含同 token）

- **AC1** 历史重排：feat/db-admin 在新 main 之上 = 「工件+功能文档」提交（最底）→ 10 个代码提交（逐个带 `Pilot: 2026-11-05-db-admin` trailer）→ 收口新增提交（e2e/路由，同 trailer）；smart-terminal 11 提交 patch-id 去重消失；`git range-diff`/逐提交 diff 确认代码内容零意外变化。
- **AC2** 门禁：`pwsh scripts/ci/run-gates.ps1 -Base main` 日志正文 PASS（pilot-gate + style-tokens 两必过项）。
- **AC3** 后端：`dotnet build` 0 error；`dotnet test` 过滤集（FullyQualifiedName~DbAdmin | XCodeConfigTests | PluginLoadDependencyGuardTests | PluginToolConstructorDiGuardTests）全绿（日志计数为准）。
- **AC4** 前端：Plugins/DbAdmin/web `pnpm run build` + `pnpm test` 全绿；ForgeSelf.Web `pnpm run check` + `pnpm run test` + `npx vue-tsc -b` 绿（对表基线，新增红=0）。
- **AC5** e2e：`e2e/plugins/db-admin/db-admin.spec.ts` 恰 1 条冒烟主链路（SQLite 测试连接[库文件在 .temp]→schema 浏览→SELECT 见结果→写 SQL 审批弹窗→拒绝，绝不真执行破坏性写）跑绿；截图落 `ForgeSelf.Web/screenshots/e2e/db-admin/` 并读图核对。
- **AC6** 路由一致性：menu-route-consistency.spec.ts 由本任务一人补 smart-terminal+db-admin 回归条目；改完 send_message 知会 qa-ci 跑中档。
- **AC7** 走查：dev-stack 起源码逐功能走查 8 项（连接 CRUD 持久化/连接测试/Schema/查询分页/SQL 分级提示/审批流/审计脱敏展示/版本徽标）截图落 `ForgeSelf.Web/screenshots/dev/db-admin/` 并读图核对 + 清测试数据；05-evidence 记「用户动线成本」。
- **AC8** 工件链与交付：00-07 齐且关键节过机器校验；04 expected_files 覆盖 rebase 后 main..HEAD 全部非文档文件；07 含「问题类归档/重构提案/悬空教训」三节；send_message 报 lead（门禁输出/测试计数/截图路径/merge-ready 声明）。
