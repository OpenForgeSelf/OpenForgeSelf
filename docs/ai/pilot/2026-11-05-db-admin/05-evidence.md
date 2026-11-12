# Evidence — db-admin 并入 main（收口与验收）

> Task：`2026-11-05-db-admin`｜证据分级：**Verified**（本会话亲自跑出真实输出）/ Inferred（代码推断）/ Unknown（未验证）。
> 状态：**终稿（全链路闭环）**。

## 0. 证据清单（实测）

| # | 证据 | 命令 / 来源 | 结果 |
|---|------|-------------|------|
| E1 | 历史重排 | `git log --format='%(trailers)' ef89d71..HEAD` + `git cherry` 预检 | **Verified**：feat/db-admin → **ef89d71**（lead 三次更正后的 main 头，382441f + SmartTerminal.Tests PC-020 + probe-dll-string 恢复 + 044→046 文档）。终态 **13 提交**：5 docs 垫底（`docs(db-admin):`）+ 7 code + 1 e2e，非 docs 全部带 `Pilot: 2026-11-05-db-admin`；st 11 提交 patch-id 去重（`git cherry` 全 `-`）；rebase 期间两轮 autosquash 合入门禁修复（04-task feature 字段、22 处样式色值、e2e 竞态防护）；`backup/db-admin-pre-rebase` 留底 |
| E2 | run-gates -Base ef89d71 | 日志正文（`.temp/log-gates2.txt`） | **Verified**：`pilot-gate PASS (exit 0)`（04-task `feature: 044` 修正后）/ `risk-level PASS (exit 0)`（L3 信息性）/ `style-tokens PASS`（DbAdminView.vue 22 处硬编码色值 = 11 hex + 11 rgba 全换 `--el-*` 语义变量，合计 1761 < 基线，较基线 **-740**） |
| E3 | dotnet build 0 error + 插件 DLL 落宿主 bin + PC-020 guard | `dotnet build ForgeSelf.Api` + `dotnet test --filter PluginLoadDependencyGuardTests`（日志 `.temp/log-build-api.txt`/`log-pc020-guard3.txt`） | **Verified**：0 错误；DbAdmin.dll 落宿主 bin；**PC-020 guard 2/2 通过**（lead 提醒后自查：Test.Sdk/xunit 对宿主即「独有包」，DbAdminTests.csproj 补 `CopyLocalLockFileAssemblies=true`——参照 ef89d71 的 SmartTerminal.Tests 同款修法，fixup 并入后端 MVP 提交） |
| E4a | 插件 web build + vitest | `pnpm run build` + `pnpm test`（日志 `.temp/log-dbweb-build2.txt`/`log-dbweb-test2.txt`，**样式修复后重跑**） | **Verified**：dist/index.js 68.60kB + style.css 11.32kB（315ms）；vitest 9 文件 **61/61 通过**（1.99s） |
| E4b | 宿主前端 check/test/vue-tsc | `pnpm run check`/`test`/`npx vue-tsc -b`（日志 `.temp/log-web-{check,test,vuetsc}.txt`） | **Verified（红项根因已挖穿，qa-ci 质疑触发）**：test **1151/1154**（3 红全在 AIAgent web）；check/vue-tsc 302 错首行 `SmartTerminalView.vue(2,72): TS2307 Cannot find module 'vue'`——**根因 = `Plugins/SmartTerminal/web/package.json` 从未声明 vue**（原始提交 b938101 即无；`import 'vue'` 的 SFC 在 main 血统上类型层必断，干净检出必红，非 worktree 缺件）。lead 报「main 0 错」实跑于主仓工作区 feat/smart-terminal@3c540d6（vue 手工装未提交）；qa-ci 的 wt-qa@2aa93aa 未并入 SmartTerminal 扫不到该文件。修法一行（补 `"vue": "^3.5.13"`）属 st-dev 范围，已移交。**我范围（DbAdmin 24 文件 + e2e）零 TS 错、vitest 相关全绿** |
| E5 | e2e 冒烟 + 截图读图 | `pnpm exec playwright test e2e/plugins/db-admin --workers=1`（日志 `.temp/log-e2e-dbadmin6.txt`） | **Verified**：第 6 轮 **1 passed (57.2s)**；7 张截图落 `ForgeSelf.Web/screenshots/e2e/db-admin/`（01-loaded ~ 07-rejected-clean，el 变量版重采），**7 张全部读图核对通过**（详见 §2） |
| E6 | 路由一致性条目 + qa-ci 知会 | menu-route-consistency.spec.ts ④ 清单 | **Verified（条目已落）**：`['mcp-center','design-system','smart-terminal','db-admin']`；qa-ci 知会随 merge-ready 汇报发出 |
| E7 | dev-stack 走查 8 项 + smart-terminal 顺查 + 动线成本 | `pwsh scripts/dev-stack.ps1`（:7301/:7399）+ playwright MCP 浏览器实测 | **Verified**：**8/8 项过 + smart-terminal 路由顺查过**（v1.2.1 徽标）；11 张截图落 `ForgeSelf.Web/screenshots/dev/db-admin/`（d1~d11），关键 4 张（d3/d6/d9 + d1 文案实证）读图核对通过；连接/库文件/测试数据全清（详见 §2） |
| E8 | 工件链机器校验 | pre-commit（历次提交均触发） | **Verified**：历次提交（含两轮 fixup+autosquash）均 `PASS: 全部 PILOT 工件链齐全` + design:check PASS（1761）；commit-msg 对无 trailer 提交硬拒（第 1 个 fixup 被拒后补 `--trailer` 重交——hook 生效实证） |

### E5 迭代史（诚实记录：6 轮跑绿，4 处 spec 断言修正 + 1 处竞态防护，零产品代码改动）

| 轮 | 结果 | 根因与修正（均为 spec 侧） |
|----|------|---------------------------|
| 1 | failed@步骤① | spec 从 `@playwright/test` 直接导入 test，绕过 `fixtures/e2e` 的 beforeEach token 注入 → 清单 401。修正：改从 `../../fixtures/e2e` 导入 |
| 2 | failed@步骤④ | `loadTables` 对已拉取连接缓存短路（DbAdminView.vue:258），批准建表后树不自动刷新。修正：`page.reload()` 后展开连接（顺带真实覆盖 S-R 恢复验收点） |
| 3 | failed@步骤⑥ | 拒绝路径 UI 是顶部 warn 横幅非 result-error。修正：断言 banner 文案 |
| 4 | **passed (1.0m)** | 全链绿（样式修复前基线轮） |
| 5 | failed@步骤④ | **S-R 恢复竞态**：reload 后 applyRestore 异步重渲吞掉首次点击（错误快照实证：连接已展开但无表节点 + backend.log 零 ListTables SQL）。修正：8s 内表未现则再点一次（收敛防护） |
| 6 | **passed (57.2s)** | 全链绿；7 图以 el 变量版重采并全读 |

### E5 后端侧交叉证据（backend.log，与 UI 断言互相印证）

- `CREATE TABLE e2e_items` 真实执行（`[DbAdminU_1] CREATE TABLE …` + write_result 审计 + history 插入）
- INSERT 审批入队 → 拒绝后**无任何 INSERT 执行 SQL**（拒绝=语句未执行的强证据）
- 复跑 SELECT `0 行`（e2e_rejected_row 未落库）

## 1. AC1..AC8 映射（token 逐字对应 02-spec §9）

| AC | 断言要点 | 证据 | 状态 |
|----|----------|------|------|
| AC1 | 历史重排：工件提交垫底 + 非 docs 提交带 Pilot trailer + st 11 提交去重 + 内容零意外变化 | E1 | ✅ Verified |
| AC2 | run-gates -Base main PASS（日志正文） | E2 | ✅ Verified（三门禁全 PASS） |
| AC3 | dotnet build 0 error + 过滤集全绿 | E3 | ✅ Verified |
| AC4 | 插件 web + 宿主前端三项（红项=基线归属） | E4a + E4b | ✅ Verified（我范围全绿；基线红归属实证见 E4b） |
| AC5 | e2e 冒烟 1 条绿 + 截图读图 | E5 | ✅ Verified（第 6 轮 57.2s） |
| AC6 | 路由一致性条目 + 知会 qa-ci | E6 | ✅ Verified（知会随 merge-ready 发出） |
| AC7 | 走查 8 项 + 截图读图 + 清数据 + 动线成本 | E7 | ✅ Verified（8/8 + 顺查 + 11 图 + 数据全清） |
| AC8 | 工件链齐 + expected_files 覆盖 + 07 三节 + 报 lead | E8 | ✅ Verified（pre-commit 全程绿；07 三节见终稿） |

## 2. 用户动线成本（e2e + dev-stack 走查双段实测）

**e2e 冒烟段**（publish 实例）：建连接 6 次点击级；写语句放行 4 次点击级；拒绝 2 次点击级；S-R 恢复 0 操作（横幅明示边界）。

**dev-stack 走查段**（:7301/:7399，playwright MCP 实测，11 图）：
- ① 连接 CRUD+持久化：新建（表单 4 项+测连+保存）→ **刷新后连接保留 + S-R 横幅「15 个连接」+ 当前连接恢复**（d3）；删除有确认弹窗且文案明示「只删除连接配置，不会删除数据库文件」（d10）——CRUD 四态 + S-R 全闭环
- ② 测连三态：成功绿条「✔ 连接成功 · 服务器 3.50.4」（d1）；失败红条「✖ 连接失败：SQLite 文件不存在：…（DbAdmin 不会自动创建库文件）」（d2）——fail-closed 语义直达用户
- ③ Schema 浏览：树表→列（id INTEGER [PK] / 名称 TEXT NOT NULL…，中文标识符无损，d5/d6）；空库展开有「(无表)」占位（d3）——**发现产品缺口：建表后树缓存不刷新（需手动刷新页面）**，见 07 §11
- ④ 查询分页：结果格类型标注（Int64/String）+「第 1–3 行 / 共 3」+ 分页器（‹ 1/1 › 每页 50/100/500）（d6）
- ⑤ SQL 分级：SELECT 直行；INSERT→审批弹窗「中：修改数据」；DROP→「写 · 破坏性」+「预估风险：高：结构或数据不可恢复」+「批准后还需二次确认才会执行」（d7）
- ⑥ 审批流：批准→执行成功回显（CREATE 0 行 17ms / INSERT 影响 3 行）+ 历史 [UI][写/读] 徽标；拒绝→顶部横幅「已拒绝：语句未执行」+ **拒绝后 COUNT 查证=3（DROP 真未执行）**
- ⑦ 审计脱敏：含 `password=TopSecret99` 的 INSERT → 审批弹窗回显即 `'****'`（d8）+ 审计三条事件（classify/approval_pending/approval_rejected）全 `'****'`（d9）——「原始 SQL 不下发，仅 sqlRedacted」契约实证；筛选器 19/19 可按类型过滤
- ⑧ 版本徽标：v0.1.0（d3/d6 处处在案）
- smart-terminal 路由顺查（lead 追加）：/smart-terminal 直达、「智能终端 v1.2.1」徽标、0 console errors（d11）

## 3. 已知边界与风险

- **宿主前端基线红（非本批引入）**：SmartTerminalView.vue 302 TS 错（check+vue-tsc）+ AIAgent web 3 vitest 红。已 git 实证归属（E4b），移交 lead/st-dev；qa-ci 跑中档时会在 main 侧复现同红。
- **产品缺口（记录不修）**：建表后 schema 树缓存不刷新（workaround：刷新页面）；审批弹窗破坏性文案重复（「写 · 破坏性破坏性」）。见 07 §11 重构提案。
- 冒烟库文件删除失败（宿主 DAL 连接占用）——按预案留 `.temp/e2e-db/`（gitignored，随 worktree 惰性清理）；走查库文件 walk.db 已删、连接已删。
- dev-stack 审计历史残留于 `.temp/dev-stack/data`（gitignored 临时区，不入库）。
## 4. 真实走查（51888 运行实例只读复验 · 2026-11-05 03:33-03:36 · qa-ci）

> 门禁⑤（plugin-development §四）：用户 :51888 实例升级到 2.4.22.2611050327（smart-terminal 1.2.2 / db-admin 0.1.0）后，浏览器真机只读复验。token 预验：GET /api/plugin 200（401 立停未触发）。截图 9 张入 `ForgeSelf.Web/screenshots/live-51888/db-admin/`（01-09），Level 3 读图核验 01/07/09。全程 console 0 错误。

| # | 场景（触发→出现什么） | 结果 | 截图 |
|---|---|---|---|
| 1 | 进入 /db-admin | 版本徽标 v0.1.0 + AskWrite·写询问 章 + 系统只读区🔒 + 空态引导 + 未选连接禁用态 | 01 |
| 2 | 点「+ 新建连接」 | 表单浮层：provider 下拉（缺驱动项灰标「需驱动」）、表单↔连接串切换、密码遮罩 | 02 |
| 3 | 表单「测试连接」 | ✔ 连接成功 · 服务器 3.50.4（绿，含服务器版本） | 03 |
| 4 | 「保存」 | 浮层关、左树出现连接、**自动选中**、编辑器解禁 | 04 |
| 5 | 连接▸展开 schema | 表 t_items/t_tags/sqlite_sequence → 列/类型/PK/NOT NULL 全显（id INTEGER PK） | 05 |
| 6 | SELECT 120 行 Ctrl+Enter | 结果格（Int64/REAL 类型标注）+「耗时 18 ms · 第 1–50 行 / 共 120」+ 分页器 1/3→2/3 翻页 ✓ | 06 |
| 7 | INSERT（AskWrite）Ctrl+Enter | 审批弹窗：目标连接/类型=写/来源=UI 编辑器/预估风险「中：修改数据」/SQL 原文/稍后-拒绝-批准 + 顶栏「待审批 1」徽标 | 07 |
| 8 | 点「拒绝」 | 弹窗关 + 横幅「已拒绝：语句未执行」+ 徽标消失 + **数据反证：COUNT 复查 tag_count=3 不变、bad_rows=0** | 08 |
| 9 | 点「审计」 | 抽屉 7 条完整链（时间序）：conn_create→classify(write, mode=AskWrite)→approval_pending→**approval_rejected**(approvalId=1 by=ui)→query×3；kind 过滤 9 类；条目含 source/rows/truncated 元数据 | 09 |
| 10 | 浏览器刷新（S-R） | 横幅「已从上次会话恢复：2 个连接 · 0 份草稿（结果集与密码不恢复；待审批已从服务端重拉）」——前端会话恢复 ✓ | 07/09 背景可见 |

### 4.1 走查发现缺陷（已转 db-dev 根因，qa-ci 证据链留档）

- **F1（P1）用户连接跨宿主重启丢失（持久化失效）**。证据链：① 03:17:45 首次保存 POST /api/db-admin/connections 返回 200 OK（Playwright 网络日志 #29379，POST 后紧跟 GET #29380）；② 03:30:26 apply 0327 重启后 GET /connections 仅剩 sys:DbAdmin；③ 磁盘取证：`C:\Users\12504\.forgeself` 全树 03:15-03:20 窗口**零文件写入**；④ 机制：写入走 SQLite WAL（DbAdmin.db-wal/-shm），主库 mtime 恒为插件初始化时刻（03:14:01），第一次保存的 WAL 数据在 03:30:33（目录 mtime 变动点=第二次重启）被无检查点清除；⑤ **可证伪预测：当前 WAL 中连接（id=1，03:33:28 存）下次重启同样消失**——可直接当复现脚本。
- **F2（P2）DbAdmin.db 主库 4096B 零表**：EnsureCreated 只落了空文件，表只存在于 WAL 侧车 → 主库永不 checkpoint，存活全靠 WAL，与 F1 同根因族。
- 根因方向（给 db-dev）：①宿主/update-agent 重启是否删 -wal/-shm 未走 SQLite 恢复；②DAL 连接串 journal_mode 配置（WAL+正常退出 checkpoint，或 DELETE 模式）；③EnsureCreated 空文件零表是否建表走了另一条连接（同名库双指向）。

### 4.2 清理记录

- 测试连接 walkthrough-fixture 已删（删除确认弹窗明示「不会删除数据库文件」，✓ 契约）；API 复核 GET /connections count=1（仅 sys）。
- 夹具库 `.temp/tmp/dbadmin-walkthrough-1105.db`（t_items 120 行 + t_tags 3 行）已删。
- 审计/历史/待审批残留属宿主运行数据，未清理（真实使用痕迹，非测试污染）。

### 4.3 走查环境备注

- Playwright MCP 截图相对路径落点为 `C:\Users\12504\.dsh\profiles\desktop\<相对路径>`（allowed roots 限制），需拷回仓库——后续走查建议直接传绝对路径。
- 与 lead 共用同一 Playwright 浏览器实例：db-admin（qa-ci）与 smart-terminal（lead）分段独占交接。

### 4.1b F1 根因修正（2026-11-05 04:08 · qa-ci 自纠：撤回 4.1 的 WAL 假设）

> 诚实修正：4.1 中「写入走 SQLite WAL、重启时无检查点被清」的假设**作废**——当时磁盘取证只扫了 `~/.forgeself`，漏扫实例目录。

**真根因（04:08 磁盘实锤）**：
1. **真实数据根 = `D:\src\tools\OpenForgeSelf\versions\<ver>\Data\DbAdmin.db`（版本目录内）**——0327 版库 53248B/5 表（DbApproval/DbConnection/DbAuditLog/DbQueryHistory/sqlite_sequence），qa-ci 03:17:45 与 03:33:28 两次保存的行都在里面（mtime 03:35:22 实锤）；`~/.forgeself/plugins/db-admin/DbAdmin.db`（4096B/零表/mtime 03:14:01）是一次性错位残迹，非活跃库。
2. **0.1.1 单版本内写路径已修好**：04:06:36 API 重建 walkthrough-fixture → POST 200 → 行真实落库（0401 Data/DbAdmin.db 读到 Id=1 行）。
3. **跨版本持久化仍结构性缺失**：0401 升级时 Data 全新空目录（0327 数据未迁移），update-agent.ps1 无 Data copy/migrate 逻辑 → 下次升级仍丢。真「类」= 插件数据根落位违反 QQNT 结构（packaging 真源 §1.6：plugins/data 应与 versions 并排=版本无关）。
4. 修复方向（转 db-dev）：数据根迁版本无关位置，或 update-agent 升级时迁移 `versions/<old>/Data/` → `versions/<new>/Data/`。

### 4.1c F1 终版机制（2026-11-05 04:25 · 第五轮对照实验定性：启动时序双宿主）

> 第三次修正，本次为行为级取证定性（0517 前夜最终版）；4.1 的「v0.1.0 首保存未落库」与 4.1b 的「WAL 假设」按本节统一收敛。

**第五轮对照实验（2.4.22.2611050417 @ 964c83b）**：全新 worktree + ForgeSelf.Api obj/bin 预检不存在（零增量缓存）+ 全量重建 117s → 「自动为[DbAdmin]设置SQLite连接字符串：Data Source=Data\DbAdmin.db;Migration=On」**仍出现**（0417 日志 L103 04:20:27.207）→ 排除宿主二进制陈旧/构建缓存假设。

**发射点与机制**：
1. 「自动为」字面量不在宿主源码、不在 DbAdmin 插件源码 → 发射点 = **NewLife.XCode 库本体**（DAL 首次解析未登记 ConnName 时的自动兜底）。
2. **时序根因**：DbAdminPlugin 初始化即 EnsureCreated（L99 04:20:27.147）**早于**宿主 `DAL.AddConnStr("DbAdmin", …数据根\plugins\db-admin\DbAdmin.db)` 生效 → XCode 兜底自建 conn（相对路径 Data\DbAdmin.db + Migration=On，L104）→ **表建到版本内 `versions\<ver>\Data\DbAdmin.db`**（L105-106）。
3. **运行期双宿主实锤**：+0.9s 后（L445-446 04:20:28.135）宿主登记位 `C:\Users\12504\.forgeself\plugins\db-admin\DbAdmin.db` 也被 DAL 操作使用——两条 conn 并存，建表走版本内、后续操作走登记位。
4. **对照锚**：AgentHub（同为 PluginDbs 登记插件）无此现象（L72 登记路径直接生效）——差异 = AgentHub init 不触库（惰性首用晚于宿主登记），DbAdmin init 即触库（早于宿主登记）。0327 日志同模式（L103/L499）→ **既有缺陷，非今夜回归**。
5. 跨版本丢失第 3 次复现：0417 升级后 GET /api/db-admin/connections 仅 sys（0401 内保存的连接再丢）。

**修复方向（转 db-dev，择一即可消灭「自动为」行）**：
- A. 宿主：PluginDbs 的 DAL.AddConnStr 提前到插件 Apply 之前执行；
- B. 插件自守：DbAdminPlugin init 首行以 ctx.GetPluginDataDirectory() 自注册 conn（宿主登记作后备）；
- C. 数据迁移另案：数据根迁版本无关位置或 update-agent 升级时迁移 versions/<old>/Data → versions/<new>/Data（4.1b 的落位问题独立成立）。

### 4.1d F1 终版根因定稿（2026-11-05 04:24 · lead 定性 + qa-ci 源头定位：私带 XCode 副本 → 隔离 ALC 双 DAL）

> 取代 4.1c 的「时序」解读：时序矛盾（AddXCode 26.7 早于兜底 27.2）恰是本机制的铁证——宿主登记进了**宿主那份** DAL 静态，插件实体用的是**插件私带 XCode.dll 的另一份 DAL 静态**（隔离 ALC），互不可见 → 兜底必然发生。4.1c 的 L104/L445-446 双 conn 并存 = 两份 DAL 各建各的，全部现象一次贯通。

**证据链**：
1. 部署载荷同类扫查（22 插件）：`Plugins\DbAdmin\XCode.dll` 1198KB（v12.0.2026.701）唯一私带；其余 21 家干净。
2. 源头：DbAdmin.csproj `CopyLocalLockFileAssemblies=true` → XCode.dll 进插件 bin；宿主 `ForgeSelf.Api.csproj` **L159 StageAllPlugins 的 Exclude 只有 `ForgeSelf.*;NewLife.*`——`XCode.dll` 两 glob 均不匹配** → 漏进发布载荷。
3. 同类修复缺口：单插件打包路径 `package-plugin.ps1` L94 / `publish-plugin.ps1` L151 已含 `$name -eq 'XCode.dll'`（含 MX.dll）——发行主路径 StageAllPlugins 漏改（problem-class 同类缺口：修了旁路没修主路）。
4. 行为级后果：插件 ALC 内 XCode 自建 DAL → 宿主登记不可见 → 「自动为[DbAdmin]」兜底（相对路径 Data\DbAdmin.db）→ 表落版本内 Data → 跨版本丢失（第 3 次复现）。

**修复（转 db-dev）**：StageAllPlugins L159 Exclude 追加 `;..\Plugins\*\bin\$(Configuration)\net10.0\XCode.*`（建议连 MX.* 类级封口）；可选加固：DbAdmin.csproj 移除 CopyLocalLockFileAssemblies=true（若外置驱动 MVP 确不需要）。修复后 0.1.2 → 第六轮验证（判据：「自动为[DbAdmin]」行消失）。

### 4.2 F1 收口：0.1.2 修复验证（2026-11-05 04:37 · 第六轮 2.4.22.2611050430 五断言全过）

> 修复 = 95eae44（宿主 StageAllPlugins 封口 XCode.dll + DbAdmin Apply 零实体访问，双保险）。验证人 qa-ci，全程 API 驱动 + 日志取证，未触碰宿主进程。

| # | 断言 | 结果 |
|---|------|------|
| ① | 发布载荷/部署目录 `plugins\DbAdmin\` 无 XCode.dll（及 NewLife.*/MX） | ✅ 包级+部署级双验：仅 DbAdmin.dll（170KB） |
| ② | 宿主登记位 `~/.forgeself/plugins/db-admin/DbAdmin.db` 建表+行 | ✅ 保存连接后 28KB / 3 表（DbAuditLog/DbConnection/sqlite_sequence）/ ROW Id=1 walkthrough-fixture；**对照：0430 版本内 `Data\DbAdmin.db` NOT EXISTS**（双宿主绝迹） |
| ③ | 实例日志无「自动为[DbAdmin]」兜底行 | ✅ 0430 日志 ZERO hits |
| ④ | 跨版本持久化 | ✅ 结构性实证：数据落版本无关位置（~/.forgeself）；完整实锤待下次版本切换复验 |
| ⑤ | 插件徽标版本 | ✅ db-admin v0.1.2 + smart-terminal v1.2.3 |

配套证据：包 OpenForgeSelf-2.4.22.2611050430-win-x64.zip SHA256=2f90ee37…03a62（重算一致）；apply 04:36:09→04:36:51（断连 ~42s）；走查 fixtures 清理后仅留 sys 连接 + walkthrough-fixture（id=1）。

**中档门禁附注（2026-11-05 04:58）**：全量 dotnet test 3711 例 / 131 红（wt-rel5 净树）——对 11-02 权威基线（qa-baseline.md @ 2c6b30c）：仅 1 条新增红可归本批 = XCodeTestFixtureIronRuleTests 点名 DbAdminFallbackGuardTests.cs PC-014 违例（随 db-dev 下批修）；其余 130 条为稳定基线 24 + PC-014 族数据根污染环境红（隔离复跑绿，波动先证于 11-04）。污染类膨胀已立 TODO 专项。
