# 项目工程规则 B-1（B1-B6：全局约定 / 验证与测试铁律 / 前端 / 后端 / 插件体系与发布 / PowerShell 坑）

> 本文件是 `agent-workflow.md`（2026-10-07 起拆分）的**一个分册**。§编号（A1-A10 / B1-B12）与规则文字**未作任何改动**，
> 索引与「§编号 → 文件」地址表见同目录 [`README.md`](README.md)；旧路径 `../agent-workflow.md` 保留为薄指路文件。

<!-- ===== 以下为原文（自 docs/04-standards/agent-workflow.md 按行区间拆入，未作任何改写） ===== -->
## Part B — 项目不变工程规则与踩坑规律（原 MEMORY.md 归档）

> 2026-09-24 起：`.forgeself` 被 git 忽略（不入库），`.forgeself/memory/MEMORY.md` 不再承载不变项目规则；以下规则自原 MEMORY.md 归档于此，随 `docs/` 入库可追溯。新增规则按 B 各小节归类追加。

## B1 全局约定

### 任务完成判定 = Verification-Centric Completion（最高优先级）
- **「代码实现完成」≠「任务完成」**：只有「需求满足 + 实现完成 + 实际验证通过」三者同时成立才算完成（详见 A9）。
- 验证结果须标注来源等级：**Verified**（亲自跑过命令/测试/构建/截图/接口拿到真实输出）/ **Inferred**（凭代码推断未实跑）/ **Unknown**（未验证或无法确认）。
- **禁止虚构**测试结果、截图、日志、接口响应、验证结论；存在未验证场景必须明说；存在阻塞不得宣称完成。
- **运行经济学**：时间资源有限 → 只做有意义的事；做不了/不做的选择必须记账——「审慎不做」台账 `docs/07-decisions/not-taken-decisions.md`（先 `ls` 确认编号/格式再追加，模板见该文件），避免下次重复评估/前后矛盾。

### 品牌命名（终决，2026-08-29，已落地）
- **全量命名 = `ForgeSelf`，不含 `Open` 前缀**。目录 `OpenForgeSelf.*`→`ForgeSelf.*`（Backend→Api、Frontend→Web、sln→`ForgeSelf.sln`）；`AssemblyName`/`RootNamespace`/namespace/using/`ConnName`/`ServiceName`/appName/库名 `ForgeSelf.db`/ico/http 变量/加密密钥 `FORGESELF_ENCRYPTION_KEY` 一律 `ForgeSelf`。
- **品牌展示也改 `ForgeSelf`**：DisplayName / Swagger 标题 / MCP 名 / AI 文案 / 关于页 / 脚本展示文案 / plugin.json Author 不再保留 `OpenForgeSelf`。中文品牌「铸己匣」不变。
- **已知保留项（非缺陷）**：`appsettings.Production.json` 占位域名 `openforgeself.example.com`（生产配置敏感只读）；各类 `.md` 文档保留历史原名；`temp_status.txt`；`forgeself-design/` 设计原型。

### Git 提交纪律（最高优先级）
- **未经用户显式要求，绝不执行 `git commit` / `git push`**：所有改动先留在工作区/暂存区，由用户 review 后确认无误，再由用户主动说「提交 / commit / 推 / 保存」才提交。Agent 自行提交 = 严重违反本纪律。
- **踩坑实例（2026-08-29）**：用户要求「库文件名改 {连接名}.db + 更新相关文档」，未说提交；Agent 却自动 `git commit`，被纠正后 `git reset --soft HEAD~1` 撤销、改动退回暂存区待审。
- 全局记忆 / `.forgeself/memory/*` 文件的写入可以（属 Agent 正常产出），但**不要顺手把这些文件或代码 `git commit`**，除非用户要求。
- 例外：仅当用户明确说「提交 / commit / 推 / 按功能块提交」等才提交。即便上轮已提交过，本轮若未再要求，仍不提交。

### TODO 清理/归类必须逐项先验证（2026-08-30 踩坑）
- **清理/归类 TODO 前，必须对每个遗留项实际核验**（grep 代码 / 读文件 / `dotnet build` / `pnpm test`）确认是否已解决；**已解决的标「✅ 已解决」并从活跃队列移除，不得凭印象归类**。
- 归类结论要可追溯：在 TODO 加「✅ 已解决」区块列证据，正文删去对应活跃条目，避免下次重复评估。
- **TODO.md 移出版本控制（2026-09-24）**：工作队列不入库（gitignore），原始历史已用 `git filter-branch` 全量重写剔除；工作区文件保留、TODO 流程不变（仅不再提交）。
- **历史重写补充规律**：`filter-branch` 会删工作区文件，重写后需从备份分支 `git show <branch>:<path>` 恢复；中文文件名 git 默认 `core.quotepath=true` 输出八进制转义，程序化处理用 `git -c core.quotepath=false`。

### 多 worktree 并行下的「同步最新代码」纪律（2026-09-29/30 三次同步实证）
- **`git fetch` 失败 ≠ 拿不到更新**：refs 与对象库跨 worktree 共享，并行会话/主检出 fetch 过就能直接用。先查 `git log -1 <remote>/<branch>` + 对象自洽性（`git rev-list --count <remote>/<branch>` 不报错），再决定是否需要网络（2026-09-30 github.com:443 连不上仍完成两次 FF 即此情形）。
- **落后/领先必须用三点语法**：`git rev-list --left-right --count HEAD...github/main`；写成两个参数（无 `...`）得到的 "0 191" 是**废数**（我 2026-09-29 就这么误判过一次 Gitee 领先，实为落后）。
- **快进前先算重叠面**：`git diff --name-only HEAD <target>` ∩（`git diff --name-only HEAD` + untracked）。交集为 0 → 可裸 `merge --ff-only`；有交集 → 先做可回退点（`.temp/sync-backup-<date>/`：`old-head.txt` + `wip-tracked.patch` + untracked 副本 + 重叠文件原件），再 `git checkout -- <重叠文件>` 让路，FF 后**按上游新版重贴**自己的编辑（回贴前先 grep 上游是否已覆盖同一学习点，避免重复表述）。
- **重贴台账要先看编号占用**：上游可能已用掉同一编号（`not-taken-decisions.md` 009 被批次C 占用 → sems 六条重编 015–020）；先 `grep` 尾号再追加，历史条目原文照录不改写。
- **同步后第一道门禁 = `dotnet build` 读真实错误数**：上游 main 可能带**漏提交断链**（2026-09-29 `81b9609`：`a4543e9` 改了 `AppBuilder.cs` 引用 `PersistentSessionStore`/`SessionProjectionService` 却没提交定义文件，全仓 `git grep` 零定义 → HEAD 编译不过；两个远端都缺，次日上游 `d7ee7c9` 自行补齐）。判定用日志里的「N 个错误」，**管道后的 exit 0 不算证据**（同 B2 exit-code 教训）。
- **断链归属他人时不越界代写**：优先等其补件推 main；临时回退他人改动只为自验须用户授权，且不提交。
- **上游可能改变本任务的验收口径**：同步后须重读 AGENTS.md/规范的变更面再决定动作（实例：PILOT-050 把 e2e 端口改动态、spec 禁硬编码 7102/7002 → 未提交的 `sems.spec.ts` 要跟着改取 `e2e-env.ts`；pilot 目录日期前缀规则注明「旧目录不回溯」→ 09-28 建的目录不改名）。

### 大特性按逻辑边界分批次提交（2026-08-30 实践）
跨多文件的大特性按「后端/前端桥/路由/插件/文档/skill/e2e/记忆」单一职责拆多批；用显式路径 `git add <paths>`（禁用 `git add -A` 防误带运行时日志）；同 shell 串行 `git commit` 避免并发竞争；临时调试文件（如 `e2e/temp-*.spec.ts`）删后再提交。

## B2 验证与测试铁律（e2e / Playwright / 工具）

### e2e 测试铁律
- **绝不 mock**：Playwright e2e 一律对接真实后端（地址取 `e2e/helpers/e2e-env.ts` 的 `backendUrl()`，默认回落 `http://localhost:7102`，PILOT-050 起禁止 spec 硬编码端口）+ 真实认证（`e2e/helpers/real-auth.ts` 解密 ForgeSetting.config 注入 localStorage）；防破坏类隔离（如真实重启）除外并注释说明。
- 新增功能测试遵循「测试-修改-验证-推进」循环。
- **正常验证流程必须走 root `playwright.config.ts`（含 globalSetup），禁止 `E2E_SKIP_GLOBAL_SETUP=1` 直连 51888**：globalSetup 会 `dotnet publish` 临时宿主（动态端口，默认回落 7102）+ 前端 dev（动态端口，默认回落 7002）+ 首启 `GET /api/api-server/init-token` 拿明文 token 注入 `E2E_API_TOKEN`。跳过它 → token 未注入 → `real-auth.ts` 回退解密 `ForgeSetting.config`，而 030 已升级 v2 机器绑定令牌（`v2:` 前缀 + PBKDF2 机器派生），旧 v1 写法直接崩 → 鉴权全挂。
- **`real-auth.ts` 与 `host-api-token.ts` 解密算法必须一致**：均按 v2（PBKDF2 机器派生，与 `ForgeSelf-AIProvider-Default-Encryption-Key` 同源）优先、非 `v2:` 前缀再回退 v1。两处重复逻辑易漂移，改一处须同步另一处。
- **global-setup SQLite provider 复制（输入38 简化）**：`System.Data.SQLite.dll` 现为包依赖（bin/发布自然落盘），复制源优先 `publish/` 根；旧「根 + plugins/ 双候选双目标」逻辑仅为兼容历史产物可保留兜底。
- **正常 e2e 用全新临时 DB**：会暴露宿主建表未覆盖的插件表缺失 bug（如 MemorySystem `chat/memories` 500）。51888 旧库已迁移故不显，勿以 51888 通过等同「全新库通过」。
- **共享可变外部状态的用例组必须 `test.describe.configure({ mode: 'serial' })`**：`playwright.config.ts` 是 `fullyParallel:true`——同文件多个用例默认并行；若用例共享同一端口/文件且某用例会临时改状态再改回（如 MCP 端口 PUT 热重启），并行用例会在切换窗口拿到 `ECONNREFUSED`/旧状态。修法：① 此类用例组从一开始就 serial；② 状态改回后轮询真实就绪信号（如 `/health` 200）再结束用例，不要只断言 PUT ok。
- **断言错误提示前先看真实响应原文**：不要凭实现猜测错误文案写 `toContain`——① 错误文本可能经 JSON 序列化（中文变 `\uXXXX`），直接 `toContain('中文')` 匹配不到，须 `JSON.parse` 后再断言；② 场景要选对（如「mcp.<id> 缺工具名段」才走格式提示，缺服务器 id 走未连接提示）。写断言前先跑一次拿真实响应。
- **浏览器走查截图立即存档**：`bu.screenshot()` 每次覆盖同一临时文件，多场景截图必须每张立即 copy 到项目 `screenshots/` 目录，不要攒到后面统一存。
- **e2e 标题断言 vs 版本徽标**：铁律要求插件根视图标题旁带版本徽标（`标题 v{{version}}`），Playwright `toHaveText('标题')` 是**精确匹配** → 必然失败。标题类断言一律用前缀匹配（`toHaveText(/^标题/)`）；任何含动态后缀（版本/计数）的文案同理。

### 仓内验证工具（禁止每次现写）
- **插件页走查** = 跑 `e2e/plugin-store.spec.ts`（globalSetup 自动构建宿主→起实例→解密注入→断言版本徽标/启用标签/截图），**必须** `--output=<空目录>`。
- **运行态宿主手工 token** = `node scripts/get-forge-token.cjs`（默认读 `~/.forgeself/Config/ForgeSetting.config`，v2 PBKDF2-SHA256 210k + AES-256-CBC；与 real-auth.ts 同算法）。
- **产物 DLL 字符串验证** = `node scripts/probe-dll-string.cjs <dll> <str> [--expect-absent]`（UTF-8+UTF-16LE 双检；expect-absent 验证「已删实现不在产物」）。
- 新验证场景缺正规入口 → 先补工具/用例再走，不现写一次性脚本（教训：2026-09-24 用户质询「为何反复手工、未上报/记待办」）。

### 统一 e2e 测试体系（2026-08-31 建立）
- 现状：**前端 e2e 是项目唯一前后端集成测试手段**（无独立单测体系）。既有 `ForgeSelf.Web/e2e/*.spec.ts` 28 个（应用层）+ 插件层 e2e，统一归口 `e2e-testing` 技能，**单一 Playwright 配置 + 单一 globalSetup**。
- 关键代码事实：后端默认端口 `7102`（`ForgeSetting.Current.PortNumber`，config 可改；51888 是历史手动冷启验收端口非默认）；前端 dev `7002`（均为**默认回落值**，PILOT-050 起实际端口动态派生）；token 键 `localStorage['forge_api_token']`；插件前端路由 = `plugin.json` 的 `frontend.route`（sems=`/sems`），宿主经 `/plugin-view/<id>` 命名空间注册（仅冲突回退时）。
- **宿主全局 Mutex 需实例标识**：`Program.cs` 硬编码 `Global\ForgeSelf-{GUID}` 单例锁；e2e 经 `FORGESelf_INSTANCE_ID`（env）或 `--instance-id=`（CLI）以独立 Mutex 并存。
- **数据目录隔离（PILOT-050 更新）**：globalSetup 显式设 `FORGESELF_DATA_ROOT=<publish>/data`（小写，B9-4 最前置重载）→ 数据根完全隔离 `~/.forgeself`，宿主落盘的 ForgeSetting.config 全进隔离目录。
- **token**：宿主启动幂等生成 `ApiToken`，globalSetup 调 `GET /api/api-server/init-token`（首启无鉴权）拿明文注入 `E2E_API_TOKEN`；跨进程真源 = `.temp/e2e/current.json`（含 hostPid 存活校验），`real-auth.ts` 经 `e2e-env.readCurrentRun()` 读取（不再扫描时间戳目录）。
- **动态端口基建（PILOT-050，2026-09-30）**：`playwright.config.ts` 求值期经 `e2e/helpers/free-port.ts` 同步认领前后端端口（tmpdir 认领注册表 `forgeself-e2e-ports/<port>.lock`，wx 独占 + PID/cwd/TTL 陈旧判定，跨 worktree 互斥；单 worktree 无冲突仍得 7002/7102）→ `webServer.env` 透传 `E2E_FRONTEND_PORT`/`E2E_BACKEND_URL` → globalSetup 经 `FORGESELF_PORT` 注入宿主（宿主 `StartupPortResolver` 覆盖 `ForgeSetting.Current.PortNumber` 并落盘，重启一致）。运行目录 = `<仓库根>/.temp/e2e/wt-<hash8>`（按 worktree 稳定派生，不再用时间戳——时间戳随机目录是 Windows 防火墙弹窗根因）；残留宿主按 current.json 保护性清理（只认本 worktree 记录）。**e2e 侧地址真源 = `e2e-env.ts`（env → current.json → 默认），新 spec 禁止硬编码 7102/7002**。

### WebApplicationFactory 测试宿主
- **AddControllers 必须显式 AddApplicationPart**：宿主 AppBuilder.cs 用 AddControllers() 裸调用，WAF 测试宿主下 entry assembly 是 testhost → 控制器扫描不到宿主程序集（40 例全 404）。修法：`AddControllers().AddApplicationPart(typeof(AppBuilder).Assembly)`（生产幂等）。
- 残余（已登记 TODO）：**插件控制器**在 WAF 下未注册（插件 Apply 的控制器注册机制与 WAF host 重建时序不兼容），需插件框架专项。
- 改写进程级全局状态的测试（ForgeConfig/ConfigUnifier/ProxyCapture/CaptureEngine 单例/ScriptRunner）需在用例内自行 **save+restore**（保存原 `Config<T>.Provider.FileName` 并 finally 还原）。
- `RealLLMIntegrationTests` 依赖真实 AI 端点 → 用 `Tests/TestDoubles/FakeAIService`（固定返回，无网络）替代 `AIService`。

### XCode 测试类隔离铁律（2026-09-29，B5 期间实证）
- **新增 XCode 测试类一律用 `IClassFixture<XCodeTestFixture>`**（`ForgeSelf.Api.Tests/XCodeTestFixture.cs`：每类独立临时库 + 全部连接名统一注册），**禁止手搓 `DAL.AddConnStr` 注册新连接名**。
- 手搓新连接名的实证后果（对照实验坐实因果）：全量失败 9→24——`AIProviderRegistryTests`/`MultimodalProcessorTests` 报 `SQLiteException: unable to open database file`、`AgentHubRegistryTests` 报「厂商标识已被占用」，且小子集里两者都绿、纯全量排序下的全局状态扰动；换 XCodeTestFixture 后三族全部消失。
- 既有 `PersistentSessionStoreTests` 复用 `"ForgeSelf"` 连接名 + 每用例独立临时库目录 + `Meta.Cache.Clear` 的手法是 B2 遗留约定（不注册新连接名，扰动面小），历史用例不动；**新代码不再模仿**。
- 小子集绿 ≠ 全量绿：XCode 实体缓存 / DAL 连接注册是进程级全局单例，任何新测试类入库前必须跑一次全量对照（排除新类 vs 含新类各一轮），失败数无差才收。

### 受控复现「0 残留」证据法补丁（2026-09-29 B6 复验发现）
- 新建文件在首次提交前是 **untracked**——`git diff` 对它恒为空，「git diff 0 残留」证据法对未跟踪文件**失效**。
- 补丁：未跟踪文件的受控复现还原验证改用**内容校验**——① 变异标记 grep 计数 = 0；② 被变异的原始行在位（行号+内容双核）。B6 复验已按此执行（ReactLoopAgent 红轮还原验证）。
- 该坑提示：受控复现尽量对**已跟踪**文件做变异；确需变异新文件时，红轮跑完先 `git add -N`（intent-to-add）再验证 diff 亦可。

### 测试里起外部子进程做判据的写法（2026-10-04 输入19 实证两次）
- **必须并发抽干 stdout 与 stderr 再等退出**：`Process.Start` 后先 `WaitForExit(timeout)`、再 `StandardOutput.ReadToEnd()` 的写法，会在子进程输出填满管道缓冲（Windows 约 4KB）时**双向死锁**——子进程卡在写、父进程卡在等。症状极具迷惑性：短输出用例正常通过，长输出用例**超时或退出码 -1 且两条管道全空**（`HostInstallRootTests` 跑 `scripts/update-agent.ps1` 时实测：robocopy 回显足够填满 ⇒ 30s 后 exit=-1、stdout/stderr 皆空，一度被误判成「脚本没运行/环境变量没生效」）。正解：`BeginOutputReadLine`/`BeginErrorReadLine` + 事件累加，`WaitForExit(timeout)` 通过后再无参 `WaitForExit()` 一次确保回调把剩余行投完。
- **判据优先读子进程自己的日志文件，不读 stdout**：`Write-Host` 在子进程管道里不可靠（实测全空）。代理脚本 `%LOCALAPPDATA%/ForgeSelf/Updates/agent-*.log` 这类"它对外承诺的落盘产物"才是稳定判据源；失败消息里把日志正文带上，否则读数只剩 `exit=-1`。
- **给子进程传"必须已消失"的 PID 时，要用子进程同一套 API 预检**：代理步骤 1 用 PowerShell `Get-Process -Id`，测试若用 .NET `Process.GetProcessById` 判"已退出"，两者对"刚退出、句柄尚未完全释放"的 PID 判定不一致。做法：起一个立刻退出的进程 ⇒ `Dispose()` 释放句柄 ⇒ 用 `pwsh -Command "Get-Process -Id N"` 确认 GONE ⇒ 才传给被测脚本（被测脚本 60s 后会对该 PID 下 `Stop-Process -Force`，预检不严有误杀风险）。
- **临时目录隔离要连 `LOCALAPPDATA` 一起重定向**：被测脚本会往 `%LOCALAPPDATA%` 写日志并清理历史目录（`Backups`）；测试用 `psi.Environment["LOCALAPPDATA"]=<沙箱>` 把副作用关进临时目录，别让它碰用户配置目录。

### 本机跑 e2e / 测试前先排环境（2026-10-05 PILOT-052 实证，两类「假红」）
- **Playwright webServer 恒 120s 超时 → 先查 `HTTP_PROXY`**：本机环境注入 `HTTP_PROXY/HTTPS_PROXY=http://127.0.0.1:10808` 而**不设 `NO_PROXY`** 时，Playwright 对 `http://localhost:<port>` 的可用性探测走代理 → **恒返回 502** → 永远等不到"可用"，报 `Error: Timed out waiting 120000ms from config.webServer.`。此时 vite 其实早已 ready（`[WebServer] VITE v6.4.3 ready ... ➜ Local: http://localhost:7002/`），默认 reporter 只显示"超时"，极易被误判成前端构建/依赖问题（本轮为此白跑两轮回合）。定位手法：`$env:DEBUG='pw:webserver'` 再跑一次，日志里 `pw:webserver HTTP Status: 502` 与 `[WebServer] ... ready` 同框即命中。处置：跑 e2e 前 `$env:NO_PROXY='localhost,127.0.0.1,::1'`（小写 `no_proxy` 一并设）；**不要**为此改仓库配置/代码。
- **后端测试整片 `UnauthorizedAccessException` → 先查 `%TEMP%` 能否建目录**：本机不允许在用户 `%TEMP%` 下新建目录，凡用 `Path.GetTempPath()` 建隔离目录的用例会集体报 `Access to the path 'C:\Users\...\Temp\<前缀>_<guid>' is denied`（PILOT-052 实测 `--filter McpCenter` **22/96 红**，形似大面积回归）。处置：把 `TEMP`/`TMP` 重定向到仓库内目录（如 `.temp/api-tests-tmp`）再跑 → 同一命令 **96/96 绿**。**这类红不会出现在仓库全量基线里**，故 §5.6「基线红先对表」查不出来，须按本条先排环境再判责。**同因的另外两面（2026-10-05 当天各命中一次，别当成三个问题）**：① 宿主前端 `vite build` 的 esbuild 临时文件清理同样被拒 —— `[vite:esbuild-transpile] remove C:\Users\...\Temp\esbuild-<hash>: Access is denied`，会让 `release-local.ps1` 在 `build-frontend` 段整体失败（本地发布链首跑即红，改成仓库内 TEMP 后 EXIT=0）；② 插件 e2e 的 vite 依赖预构建写 `node_modules/.vite/deps_temp_*` 被拒 → dev server 退不出启动 ⇒ `ERR_CONNECTION_REFUSED`（并行 worktree 实测）。**统一处置（一条命令覆盖三面）**：跑「后端测试 / 插件 e2e / 本地发布链」前先 `$env:TEMP = $env:TMP = '<repo>\.temp\tmp'`。

## B3 前端工程规则

### Element Plus 组件导入
- **禁止显式 `import { ElXxx } from 'element-plus'`**（type 导入除外）：组件样式依赖 unplugin-vue-components 按需注入，显式导入会绕过自动解析导致组件无样式（已踩坑：TodoEditDialog 弹窗样式缺失）。统一在模板中使用 `<ElXxx>` 自动解析。
- 例外（API 调用型组件，unplugin 不解析 JS 调用，必须显式导入）：`ElMessage`、`ElMessageBox`、`ElNotification`、`ElLoading`。
- ESLint 已配 `no-restricted-imports` 拦截。

### 前端 DTO 枚举形态必须与后端 JSON 序列化一致（2026-09-22 踩坑）
- **坑**：后端 `PluginState` 是**默认数字枚举 0-9**（JSON `state`=数字），前端 `types/plugin.ts` 却定义成**字符串枚举**（'Running'…）→ 模板 `plugin.state.toLowerCase()` 在数字上调用抛 `TypeError` → 插件市场列表渲染即白屏。此前 404 空列表掩盖了此 bug。
- **对策**：前端枚举成员值必须与后端序列化形态对齐（后端数字 → 前端数字）。数字枚举**反查** `PluginState[state]` 返回**成员名 PascalCase**（4→'Running'）；状态文案 switch 须覆盖**全部**枚举成员（曾漏 Stopping=5 → 徽标显示「未知」）。
- **排查口诀**：前端对后端返回字段做 `.toLowerCase()`/字符串比较却报 TypeError 时，先查后端 DTO 该字段是数字枚举还是字符串。

### 前端统一 request 助手：204 / 空 body 必须短路（2026-09-09 踩坑）
- **坑**：`request` 助手对成功响应**无条件 `res.json()`**；后端 DELETE 等端点返回 204 No Content（空 body）时抛 `Unexpected end of JSON input` → emit 链中断（列表不刷新）+ 对话框残留报错。
- **对策**：通用 `request` 层先判 `res.status === 204` 直接 `return undefined`，再 `await res.json().catch(() => undefined)` + 空值短路；不能假定成功响应必有 JSON body。
- **完成信号**：删除类操作 = 「列表已刷新（emit 链走完）」+「无报错」二者同验才闭环（已在 ai-agent e2e 固化为回归断言）。

### 宿主前端 service 的 parseResponse 已解包（2026-09-22 踩坑）
- 前端 service 的 `parseResponse` 已解包 `json.data`（返回 T 本身）：新增 fetch 函数**直接按 T 收**，禁止再取 `.data`/`.stats`——fetchPluginVersion、fetchTraffic 各踩一次（恒返回 undefined → 空列表/空版本，页面无错但功能静默失效）。

### 收尾工作树的四桶分类 + 引用完整性校验（2026-09-09）
- **分类**：`git status` 把未提交项先分四桶——①源码 ②文档反同步 ③技能/工作日记 ④scratch，分别处置；源码改动才有门禁，文档/技能/日记走一致性核对即可提交。
- **不臆造引用**：`docs/` 引用的图/文/档案，提交前必须 `Test-Path` 确认存在（杜绝文档链到不存在的文件）。
- **会话工具产物**：`.playwright-mcp/`、`.serena/` 随跑随生，已入 `.gitignore`；scratch 一律移 `.trash/`（不永删），`.trash/` 本身已在 .gitignore。

### 前端测试与构建约定
- **单测后缀必须是 `.test.ts`**：vitest `exclude` 含 spec.ts 后缀（那是 Playwright e2e 的约定），写成 spec.ts 会报 "No test files found"。
- **代码注释里禁止出现 `*/` 序列**：例如写 `**/*.spec.ts` 会因含 `*/` 提前终止块注释 → esbuild "Unexpected *"。注释中改用文字描述。
- **前端 ElSelect 在 jsdom/vitest 的无限递归坑**：ElSelect 空值（undefined）归一化在 jsdom 下 `Maximum recursive updates exceeded`。规避：①页面用 `:model-value` + `@update:model-value` + `@change` 触发业务，**别用 v-model 绑 ref<number|undefined> + clearable**；②组件测试直接 stub `ElSelect/ElOption`。
- **ElForm validate 在 jsdom 不拦截**：EP 表单 rules 在 jsdom 测试环境下可能直接通过（required 未生效），关键业务校验须在 handleSubmit 内手动兜底，勿只依赖 EP validator。
- **全屏背景图**：用固定定位 `<img>` 元素，**不要**用 CSS `background-image: url(外链)`（本环境外链背景图不渲染）。
- **CSS 顶部禁止外链 `@import`**：浏览器须先 fetch 第三方域，离线/受限网络 → 整个文件悬挂，`:root` 变量与基础规则全部失效（渲染为无样式裸 HTML）。字体用系统 fallback 已足够。触发条件：写任何共享 CSS 入口时。

### 插件前端（web/）工程规则
- **前端产物** = Vite lib 模式 ESM，入口 `web/dist/index.js` + 同目录 `style.css`（宿主 `pluginViewLoader` 按约定注入 `<link>`）。
- **vue / vue-router / pinia / element-plus 必须 external**（宿主 import map 解析到同一份实例，防 Vue 双实例——双实例下 `ref`/`reactive` 响应式与组件通信全部失效）。
- 导出：必须 `export { XxxView as default }` 且命名导出名 = `plugin.json` `frontend.views[0]`，宿主按名字取、取不到回退 default。
- **共享依赖 = Import Map + 宿主共享桥 shim**：① `ForgeSelf.Web/src/shared/exposeSharedDeps.ts` 把真实模块挂到 `window.__FORGE_SHARED__`（main.ts 顶部调用，MUST 早于任何插件界面加载）；② `ForgeSelf.Web/public/shared/{vue,vue-router,pinia,element-plus}.js` 为 shim，从该全局**具名再导出**（ESM 不支持动态 `export * from window.xxx`，必须枚举）；③ `index.html` 注入 `<script type="importmap">` 映射裸模块名到 shim URL；④ 插件产物把共享依赖声明 external。
- **shim 导出清单必须脚本生成**：`ForgeSelf.Web/scripts/generate-shared-shims.mjs` 从真实包自动生成（vue 172 / vue-router 23 / pinia 16 个导出）。手工枚举必漏。
- **插件新增 EP 组件 = 一条宿主 shim 更新链，发布前必须整体走完**（单测/e2e 全绿不代表生产可用——e2e 每次全新 publish 宿主天然带最新 shim，测不出「增量发布后宿主静态资源陈旧」）：① `exposeSharedDeps.ts` 导出组件；② `shared/element-plus.js` shim 具名导出；③ `index.html` import map `?v=N` **递增**（浏览器缓存键，不 bump 则生产仍加载旧 shim → 插件白屏 "does not provide an export named 'ElXxx'"）；④ `pnpm run build` 重建宿主前端（输出到 `ForgeSelf.Api/wwwroot`）；⑤ 覆盖 `publish/wwwroot`；⑥ **删除 publish/wwwroot 下全部旧 `.br`/`.gz` 预压缩文件**（StaticFiles 对 Accept-Encoding 优先回旧压缩内容）；⑦ 走查前先 `curl http://host/shared/element-plus.js` 核对新导出存在。
- **@element-plus/icons-vue 在宿主共享桥下的 sizing 陷阱**：插件代码里写 `<Folder :size="14" />`，prop 转换依赖宿主打包器构建时处理，共享桥下不触发 → **必须给 SVG 添加 CSS 强制 `width: [N]px; height: [N]px;`**，否则图标被父容器撑大到默认尺寸（>20px）视觉失衡。
- **Vite lib 模式的 CSS 不会被产物 JS 引用**：约定 `assetFileNames:'style[extname]'` 固定输出 `style.css`，由宿主加载器按「入口同目录」注入 `<link>`（幂等，`data-plugin-style` 去重）。
- **plugin.json 序列化是 camelCase**：磁盘上写 `frontend.entry`（小写），反序列化到 C# 属性 `FrontendContributes.Entry`（PascalCase）。新插件 MUST 用 camelCase。
- **插件 web/ 构建（pnpm v11 esbuild 放行）**：`pnpm build` 报 `ERR_PNPM_IGNORED_BUILDS` → pnpm v11 把构建白名单移到 `pnpm-workspace.yaml` 的 `allowBuilds` / `onlyBuiltDependencies` 字段。标准样板：插件 `web/` 下建 `pnpm-workspace.yaml`（`allowBuilds.esbuild: true` + `onlyBuiltDependencies: [esbuild]`）并删掉 `package.json` 里失效的 `pnpm` 字段。触发：新建/构建任何 `Plugins/*/web` 前端时直接照搬。
- **sems 前端构建用 pnpm（非 npm）**：`Plugins/Sems/web` 所在仓库是 pnpm workspace（`link:` 协议），`npm install` 直接失败；命令 = `cd .../web && pnpm install && pnpm run build`。
- **前端路由接管约定**：插件清单 `frontend.route` **直接作为最终路径**（`/ai-agent`），不前缀化；仅当被宿主静态路由占用时回退 `/plugin-view/<id>`（`MANIFEST_ROUTE_PREFIX`）。判定靠 `isPathTaken`（精确匹配 + 参数化路由同段数比对），冲突先回退、回退仍冲突则跳过，**绝不覆盖宿主页面**。
- **宿主不再内置插件页**：插件界面一律由清单驱动远程加载，宿主不再手写插件页静态路由。
- **pluginViewLoader 缓存键 = 版本号**：入口 URL `/plugins/{id}/web/dist/index.js?v={version}`；版本未变时浏览器从磁盘缓存取旧 bundle，**同版本内的纯前端改动（如文案/icon）用户看不到，需硬刷**。对策：(a) 纯文案/样式改 → 文档化为已知限制；(b) 行为变更 → 升 patch 版本触发重抓；(c) 未来优化缓存键为 `version + hash`。
- **项目 logo/图标三处落点，换 logo 必须三处全更**：① web 前端源 `ForgeSelf.Web/public/`（favicon.ico + `logo/*.png`）；② 构建中间产物 `ForgeSelf.Api/wwwroot/`（陈旧则发布回旧图）；③ exe 图标源 `ForgeSelf.Api/Assets/ForgeSelf.ico`（**不会从 web logo 自动派生**，须显式重新生成 + 重编译 exe 才生效）。
- **块注释里不许出现通配路径（`*/` 会提前闭合注释）**（design-system 2026-09-29 两次踩）：注释写 `size.*/font.*`、`Plugins/*/web` 这类内容会让 esbuild/vite 报 `Unexpected "."`，症状像"代码语法错了"其实是注释被截断。对策：注释里写"xxx 系列"或改用行注释；一旦构建报 `Unexpected "."` 在注释行，先怀疑这个坑。
- **vue-tsc 把 `.vue` 当纯 TS 解析**（表现为 `</script>` 那行报 `'}' expected`、模板行报"未终止的正则字面量"）：模板 `{{ }}` 里的**多行嵌套三元**（尤其字符串内含双引号）会让 SFC 工具链错位。对策：模板只放简单插值，复杂文案/分支挪进 `<script>` 的 computed；模板里 `list.filter((x) => ...)` 这类回调推不出参数类型（TS7006）→ 先在 computed 里算好。
- **插件前端不装工具链**：`check`/`test` 一律委托宿主（`pnpm -C ../../../ForgeSelf.Web exec vue-tsc --noEmit -p ../Plugins/<X>/web/tsconfig.check.json`、`exec vitest run ../Plugins/<X>/web/src`）。注意 `-C` 之后 cwd 变宿主，相对路径必须以宿主为基准。宿主 vitest 的 include 已含 `../Plugins/*/web/src/**/*.test.ts`，插件单测无需自带 vitest。
- **CSS 变量别名只许引用"真存在的变量"**（design-system 2026-09-29）：`--ds-bg: var(--ds-semantic-surface-bg)` 若后者未定义，
  整条声明在 computed-value 阶段失效，`background: var(--ds-bg, fallback)` 也跟着失效 → 页面**全透明**（比不换肤更糟，且只有真浏览器能发现）。
  对策：从注入的 CSS 文本扫出已定义变量集合，别名按集合过滤；CSS 还没到位时两段样式都不注入。
- **给 `<input type="color">` 塞空串 = 每帧一条浏览器告警**：形状不合要**不渲染**该控件（`v-if`），
  而不是 `:value="''"`（Vue 对 input 的 `value` 走 DOM property 赋值，`null/undefined` 也会被 coerce 成空串，摘不掉）。
- **量"有没有滚动条"必须分轴，且不能拿"占位"当唯一判据**（2026-10-05 输入22 实测两处）：
  ① 轴向：横向滚动条吃的是**高度**（`offsetHeight - clientHeight`），纵向才吃宽度（`offsetWidth - clientWidth`）——
  拿 `offsetWidth-clientWidth` 判"横向有没有条"会把"量错轴"误读成"浏览器没画条"。
  ② 本项目 e2e 用本机 Chrome（`playwright.config.ts` 的 `channel: 'chrome'`）是**浮层滚动条**：
  连 `overflow:scroll` 的空白 div 都量出占位 0px；给元素写 `::-webkit-scrollbar { width: 40px; background: #f0f }`
  **一个像素都不画**，`scrollbar-width: thin` 同样 0px ⇒ 自定义滚动条样式被这台浏览器的滚动条策略忽略。
  ⇒ 判据只能钉"**溢出末端可达**（`scrollLeft` 能推到 `scrollWidth-clientWidth`，且被裁元素的右缘进入可见区）+ 界面上有一行说明"，
  不得钉"条占位 > 0"（那是浏览器策略，写死必然假红）；"条常驻可见"要如实写进 README 已知缺口，不许默默承诺。
- **等比缩放预览用 CSS `zoom`，不用 `transform: scale()`**（同批）：`zoom` 参与布局 ⇒ 盒子尺寸与滚动范围随缩放一起变，
  不必再测一次内容高去折算容器高（那要第二个 `ResizeObserver`，且和滚动条互相抖）；`getBoundingClientRect()` 在 `zoom` 下
  返回**缩放后的真实屏幕尺寸**，正好用来判"有没有被裁"。副作用要预防：观测器读的是容器 `clientWidth`（已扣滚动条），
  与 `max-height` 共存时存在"滚动条吃宽 → 重算"的回环，因 `k` 单调下降 + `min-width` 下界而收敛。
- **共享外壳的 `max-width` 会静默封死某个模式的可用宽**（同批）：`.ds-mode-pane { max-width:1240px }` 让展厅在 1920 窗口下
  与 1372 窗口下**一样宽**（放大窗口画布不变宽），排查方法是"改 `setViewportSize` 后量同一元素"。
  对策：只给需要宽画面的那一栏在**组件 scoped 样式**里覆写（`.ds-showroom[data-v-*]` 特异度 0-2-0 压过全局 0-1-0，不依赖注入顺序），
  其余模式保持原值；覆写值要能从"装下最宽的稿"推导出来，不许拍一个数。

## B4 后端工程规则
### 分层 / 鉴权 / 配置
- 分层：Controllers/Services/Entities；新插件走 `Plugins/` + `plugin.json` 注册；异步统一 `async/await`，不 `.Result` 阻塞。
- **管理面/CRUD 控制器必须类级 `[Authorize("ApiKeyPolicy")]`**（命名策略 = `ApiKeyAuthenticationHandler` Bearer 方案，注册于 AppBuilder.cs）：前端 `request.ts` 已全局注入 Authorization 头，勿因"前端会带 token"而漏加鉴权（踩坑：AIProviderController 曾无 [Authorize]；PluginController 2026-09-24 补课）。
- **鉴权 token 唯一真源 = `ForgeSetting.config` 的 `ApiToken`（AES-256-CBC 解密），绝非 appsettings 的 "ApiKey"**：`ApiKeyAuthenticationHandler` 比对的正是 `ForgeSetting.Current.ApiToken`（解密后）；appsettings 的 "ApiKey" 是 AI provider key，误用会 401。解密用 `AesSecretEncryptionService`（密钥 `Encryption:Key`/`FORGESELF_ENCRYPTION_KEY`/默认 `ForgeSelf-AIProvider-Default-Encryption-Key`）。
- **`ForgeConfig<T>` 路径覆盖触发铁律**：`ForgeConfig<TConfig> : Config<TConfig>` 基类在静态构造里把 `FileConfigProvider.FileName` 覆盖为绝对数据根路径（`数据根/Config/{Name}.config`）。该静态构造**只在 `TConfig` 实例被创建时触发**（经 `ForgeSetting.Current` 的 `new TConfig()` 路径），**只读 `ForgeSetting.Provider` 不触发**。断言用 `BeAssignableTo<FileConfigProvider>()`（勿 `BeOfType`，默认 XmlConfigProvider 继承 FileConfigProvider 但精确类型断言失败）。新增 `Config<T>` 子类须继承 `ForgeConfig<T>` 且至少经一次 `.Current` 访问。
- **启动后端必须用 `dotnet run`（不带 `--urls`）**：NewLife.Agent 宿主会**吞掉** `--urls` 参数并回退到默认 5000。正确命令：工作目录 `ForgeSelf.Api` 跑 `dotnet run`（端口取 `ForgeSetting.config` 的 `PortNumber`，默认 7102）。**改端口三选一（PILOT-050 起）**：① `FORGESELF_PORT` 环境变量（最优先，宿主自动覆盖并落盘 config）；② `--server-port <n>` 参数（次优先）；③ 直接改 `ForgeSetting.config` 的 `PortNumber`。勿用 `--urls`（被吞）。
- **宿主启动会写回 ForgeSetting.config（端口事故 2026-09-23）**：宿主启动流程会持久化自身运行端口（SettingsController/ApiServerController 的 ForgeSetting.Save 路径）。起宿主/发布前先读配置记录原值；冷启动后立即核对配置未被改写，被改写则恢复；发布脚本起宿主前加「备份配置 + 起后核对」防护（进 035 待办）。

### 运行时数据落盘铁律
- **唯一入口是 `IDataLocationService`**：开发态→程序目录 `Data/`，发布/服务态→`~/.forgeself`。任何写盘（SQLite 库、插件数据、配置 `*.config`、图片缓存）都必须经它，禁止再写 `AppContext.BaseDirectory` 字面量。
- **目录布局**：宿主数据/库 → 数据根（`~/.forgeself/OpenForgeSelf.db`）；插件数据与插件库 → `数据根/plugins/{插件Id}/`。**库文件名 = XCode 连接名（PascalCase）**，目录名仍是 kebab 运行时 Id。目录名常量是 `IDataLocationService.PluginDataRootName`，**禁止两侧各自写字面量**（漂移过一次：一侧 `plugins` 一侧 `Plugins`）。
- **appsettings 的 `ConnectionStrings:{连接名}` 只认绝对路径**：相对路径会被 NewLife 解析到程序目录、绕过数据根 → SQLite Error 14。`XCodeConfig.AddXCode` 已实现「相对路径忽略 + 告警，绝对路径才采纳」。新增库一律只在 `XCodeConfig.HostDbs`/`PluginDbs` 加一行。
- **插件禁止自注册 `DAL.AddConnStr`**（会覆盖宿主路径造出第二份互不可见的库，QuickLinks 踩过）；只做 `DAL.Create(ConnName)` + 探活。
- **SQLite 不自建父目录**：落在 `Plugins/{id}/` 子目录的库，必须在 `AddXCode`/`InitializeXCodeDatabase` 里先 `Directory.CreateDirectory`，否则 open 报 Error 14。
- **`ConfigUnifier.UnifyAllConfigFiles` 必须是进程里最早的 NewLife 调用**（已前置到 `Program.cs` 顶部）。否则框架先按默认相对路径初始化 `FileConfigProvider` 并对程序目录建 FileSystemWatcher → 发布目录无该目录，启动首行即报「FileSystemWatcher 创建失败」。
- **SQLite 探活统一用 `dal.Db.ServerVersion`，不要用 `dal.Session.Query("SELECT 1")`**：后者在库文件尚未创建时抛 `NullReferenceException`，导致每次启动误报「数据库初始化失败」。
- **发布产物要区分「运行时生成物」与「部署资产」**：`data/`、`log/` 是运行 exe 产生的遗留，构建脚本可排除；`Plugins/` 承载插件程序集属部署资产，**绝不能排除**。Web SDK 默认只把 `Plugins/**/plugin.json` 当内容发布，插件 DLL 靠 csproj 的 `StagePluginsToPublish`（`AfterTargets="Publish"`）补齐。

### XCode 实体加列/改字段流程
- 改实体字段的**唯一真源是 `Data/Model.xml`**（不是手改 `Entities/*.cs`）：在 xml 所在目录运行 `xcode Model.xml`，由 NewLife.XCode 工具重新生成 `Entities/*.cs`。
- 生成的实体文件由工具生成；**手写业务代码必须放在 `*.Biz.cs`**（如 `ChatRecord.Biz.cs`），重生成不会被覆盖。
- 数据库列随 XCode `Meta.CreateTable()` 自动同步，无需手写迁移脚本。
- **非主键列必须 `Nullable="True"`**：非主键列若不设 Nullable，XCode 生成的建表 SQL 会对非 INTEGER 主键列生成 `AUTOINCREMENT`，SQLite 报 `AUTOINCREMENT is only allowed on an INTEGER PRIMARY KEY`。非空约束靠业务层保证。
- **新增字段必须注册到 `this[name]` 索引器**：XCode 通过 `public override Object this[String name]` 读写列，不是 C# 属性。漏同步索引器 → ORM Insert 静默丢弃、读回 null。新增/修改字段后：① `FindById` 读回断言非 null；② 用 `PRAGMA table_info(...)` 核对物理列存在；③ 若表名与类名不同，勿用类名查。
- **插件必须自建表（XCode 建表职责边界）**：宿主 `XCodeConfig.EnsureTablesCreated` 只反射扫描**启动时已加载**的程序集，插件实体（ConnName=插件独立库）不在其列 → 全新库下 `FindAll` 报 `SQL logic error`/`no such table`。**修法**：插件 init 时自行 `DAL.Create(ConnName)` 先初始化连接，再反射取实体 `Meta` 静态属性调用 `Meta.Resolve()`（检查+创建，幂等）。注意时序——首个实体 `CreateTable` 在连接未就绪时只建空库不建表。`EnsureCreated()` 正确写法：先 `EntityFactory.InitConnection(ConnName)` 全量建表，再探活确认。
- **XCode 分页 TotalCount 铁律**：**`PageParameter` 不设 `RetrieveTotalCount = true` 时 `FindAll` 后 `pageParam.TotalCount` 恒为 0**——所有分页接口 total=0 的统一根因。读 `TotalCount` 的分页查询**必须**显式设该标志；例外：`UsageStatsService` 用 `FindCount(exp)` 显式 COUNT。新增分页优先沿用这两种写法之一。
- 扫描 `Plugins/**` 断言清单唯一性的测试必须**排除 `_` 前缀工具目录**（`_backups/`、`_published/`），否则发布流程产生的版本副本造成 Id「假性重复」断言失败。

### 后端 DI 作用域安全
- **root provider 不能解析 Scoped 服务**：宿主 `WebApplication` 在 Development 默认 `ValidateScopes=true`，从 root provider `GetService(Scoped契约)` 抛 `Cannot resolve scoped service ... from root provider`。**正解**：解析 scoped 一律走 `HttpContext.RequestServices`（request scope）或 `hostProvider.CreateScope()` 后 `scope.ServiceProvider`。
- **插件子 provider 手动 `BuildServiceProvider` 默认 `ValidateScopes=false`** → 插件内 `AddScoped` 服务解析为「俘获单例」，符合 Cordis「每上下文单例」设计（027-cordis-kernel），**不 500**；已显式锁定该契约，防未来误开校验重引入 500。
- **新增 Backend 插件报 CS0579「assemblyinfo 特性重复」** → 查 `ForgeSelf.Api.csproj` 的 `<Compile Remove>` 列表是否漏该插件（插件 .cs 被 Api 与插件自身 csproj 双重编译）；补 `<Compile Remove="Plugins\<Id>\**\*.cs" />` + bin/obj `<Content Remove>` 即解。**注意区分内嵌/拆分**：运行时依赖宿主程序集内嵌类型的插件禁止整目录 `Compile Remove`（类型会从宿主消失），只排除嵌套 `obj\**`/`bin\**` 止血；拆分插件才补全套 Remove+引用+staging。
- **解决方案文件 = `ForgeSelf.slnx`**（2026-09-21 由 .sln 迁移，19 项目全量保留）。解决方案级构建命令 = `dotnet build ForgeSelf.slnx`。
- **dotnet test 被 dev server 锁 exe 的规避**：运行中 dev server 持有 exe，`dotnet test`（Debug 构建）拷贝 apphost→exe 会 MSB3027/3021 失败。规避：`dotnet test -p:UseAppHost=false`；若 DLL 也被锁（加载中程序集），构建/测试一律加 `-p:OutDir=<临时目录>` 旁路验证。 **同一类锁不必等外部进程，自己就能造出来**（2026-10-04 实测踩到）：`dotnet test` 正在跑时再起一次 `dotnet build --no-incremental`，抢的是同一套 `bin/Debug` 输出树，直接报 48 条 MSB3021/MSB3027（`无法复制文件…另一个进程正在使用此文件`）。⇒ 规则：本仓任何 `dotnet build` / `dotnet test` **一律串行**，包括"只是想顺手数一下警告数"的 no-incremental 重建；并发跑出来的那份构建日志**整体作废**，不能拿来当"新增代码 0 warning"的证据（我那次就是靠它才误判过一次）。
- **git worktree 新检出目录首构失败 ≠ 代码事实**：全新目录无 obj/project.assets.json，restore/构建顺序问题所致（报 CS0246 但 HEAD 代码自洽）。判定「某提交能否编译/测试」不要用未先 restore 的 worktree；用 `git show HEAD:path` 核对代码事实，或先 `dotnet restore && dotnet build`。
- **"统计列"要么有人维护，要么读出时现算，否则就是假数字**（design-system 2026-09-29）：`DesignProject.TokenCount/ComponentCount` 建表时写了列，但没有任何写入路径更新它 → 界面显示"有效令牌 250 / 令牌数 0"，e2e 也直接判死。规则：出参一律现算（`FindCount` 直查库），缓存列只当历史兼容；要保留缓存列就必须写清"谁在什么时候更新它"。
- **复合令牌的 `ValueJson` 是真源，读值路径都要展开它**：DTCG 的 `shadow/typography/transition/cubicBezier` 的 `$value` 就是对象/数组，库里 `Value` 可能为空。任何投影（导出 CSS、换肤、发布快照）只读 `Value` 就会产出 `--ds-x: ;` 这类**空声明**（看似成功、下游全失效）。
- **`catch { return ""; }` 会把"修了但没修对"伪装成"修好了"**：JSON 数字被 `Num()` 写成字符串后 `GetValue<Double>()` 抛异常 → 整个展开函数返回空串，测试不细看就通过。对策：宽容解析（数字/字符串都吃）+ 保留兜底 + **断言写成"必须是非空真值"**而不是"有这一行"。
- **XCode `FindCount` 返回 `Int64`**：与 `Int32` 变量/断言混用会报 CS0266/CS1503，需要显式 `(int)`；写计数辅助方法时当场钉死返回类型。
- **令牌/规格"目录"必须从真实生成结果反推，不许写死清单**（design-system 2026-09-29）：`generate` 过去只写 `component.*` 令牌，
  组件目录表一直 0 行 → "组件库"页面对新项目永远空（读图才看得见）。补的 `SeedComponentCatalog` 规定：
  清单取本次真实生成的路径、这次没生成的组件就不建目录；测试断言"目录里每条 tokenRef 都能在库里查到"。
- **宿主 SQLite 并发读写会冒 `code = Busy (5) / database is locked` 并把接口打成 500**（design-system e2e 2026-09-29 三次命中，写与只读路径都中）：
  库虽已是 WAL（文件头 offset18/19 = `2 2`），但 `XCodeConfig` 派生的连接串原本**没有任何 Busy Timeout**（立即失败）。
  已补 `Busy Timeout=5000`（宿主级、影响全部插件库，需用户复核）；调用方一律**只给只读请求退避重试，写请求绝不重试**（重复落库比红屏更糟）。
  根因（写串行化/统一重试策略）仍在宿主 DAL，未闭合 → 不得叙述成"已修"。
- **同一套测试两个 filter 计数不一致时，禁止对外报"共 N 项"**：本轮 `~Plugins.DesignSystemTests`=85、`~DesignSystem`=154（均 0 失败），
  差 69 项未解释前只报"失败 0 + 所用命令原文"。
- **`dotnet test` 默认(quiet) logger 会假绿**（2026-09-29 定性）：测试主机中途崩溃时它把"已跑完的条数"当总数报，
  并且照样打印 `已通过! 失败: 0`（实测同一命令：quiet 报 85/154 且"崩溃"，`--logger "console;verbosity=normal"` 跑完 **164/164 无崩溃**）。
  规则：本仓跑 dotnet test **一律带 verbose console logger**，或先 `--list-tests` 拿发现数、再与执行数比对；
  两者不等就是事故，不能当通过。
- **全量 `dotnet test` 的环境前置（2026-10-01 plugin-dev-experience 实证）**：① 跑测前必须停掉本会话自起的 dev/调试宿主实例——运行中实例占住 `ConfigUnifier` 的 `.tmp` 文件名导致重建被拒，全量跑测会冒 496/535 假失败（TODO.md:86 已录此坑）；② 本机系统 `TEMP`/`TMP` 已对测试主机拒访，须在命令前 `TMP=<工作区>\temp\test-tmp TEMP=<同>` 重定向到工作区临时目录绕过（否则大量测试抛 `UnauthorizedAccessException`）。两项缺一都会把"环境劣化"误判为"代码回归"。

## B5 插件体系与发布

### 发布包「内容」才算发布验证（design-system 2026-09-29）
- 脚本 `ALL DONE` 不等于交付：必须解包核三件事 —— ① 目标插件目录在包内且时间戳是本次构建；② 新实现**在包里能探到**；
  ③ `SHA256SUMS.txt` 与实测哈希一致。探针一律用仓内正规入口 `scripts/probe-dll-string.cjs`，解包目录放仓库外 `$TEMP`，用完即删。
- **minified 前端产物探"实现指纹"，不要探函数名**：函数名会被压掉，但它携带的正则/常量/选择器字面量不会
  （例：探 `--ds-[\w-]+` 证明变量扫描逻辑进了 `dist/index.js`）。
- `SHA256SUMS.txt` 是 CRLF → `sha256sum -c` 报 "No such file or directory"，**不是包坏**；逐字比对或先 `tr -d '\r'`。
- 宿主 zip 版本号必须**高于当前运行版本**（页面才检测得到）；本地打包不打 tag、不触发 CI；**agent 不停/启/杀用户宿主**。

### 插件体系（ForgeSelf.Api/Plugins）
- **两层命名（刻意设计，非不一致）**：目录/程序集(.dll)/EntryType 用 PascalCase（代码身份）；`plugin.json` 的 `Id`/数据目录/`~/.forgeself/plugins/{id}`/路由/库文件用 kebab-case（运行时身份）。
- **实际 13 个插件**：ai-agent / dev-tools / file-tools / memory-system / proxy-capture / quick-links / sample / system-monitor / script-runner / scheduler / text-tools / todo-tracker / workflow-engine。
- 插件禁止自注册 `DAL.AddConnStr`，只 `DAL.Create` 探活（统一用 `dal.Db.ServerVersion`，禁用 `dal.Session.Query("SELECT 1")`）。
- **启动装配唯一路径 = `PluginManager.RegisterAllServices`**（AppBuilder Build 前调用，内部 Discover→拓扑排序→Resolve→MountPlugin→`fiber.Mount(Apply)` 一步到位，状态即 Running）；`LoadAndStartAllPlugins`/`InitializePlugin` 定义存在但**无外部调用者（未接线）**，勿误以为启动需另调；热启/热重载走 `EnablePlugin→LoadPlugin→InitializePlugin`。
- **宿主契约要分两批 seed 进插件上下文（时序铁律）**：`PluginManager.RegisterAllServices()` 在 `builder.Build()` **之前**执行并立刻触发插件 `Apply`，而 `ProvideHostServices(app.Services)` 必须等 Build 之后才有 DI 可解析。因此「Apply 期就要用」的契约（典型：`IDataLocationService`）必须用 `PluginManager.ProvideHostService(contract, instance)` 在 `RegisterAllServices` **之前**手工 seed 同一实例；其余契约仍走 Build 后的 `ProvideHostServices`。违反后果：插件抛「未注册到插件上下文」并整体注册失败，日志只表现为插件数变少、扩展点消失，不易定位。
- **跨插件解耦数据共享**：不互相注册服务/不直接调用，而是经 `IDataLocationService.GetHostDataDirectory()` + Abstractions 中公共常量落共享 JSON 文件，各自 `ctx.Get<IDataLocationService>()` 解析后读写。**插件服务注入宿主契约必须用构造函数注入 `IContext` + `ctx.Get<T>()`**，禁止直接构造注入宿主服务（子容器三无，直接注入必 500）。
- **ctx 与 IServiceCollection 是两套存储（根因级）**：插件 Apply 里 `ctx.Get<IServiceCollection>()` 拿到宿主 DI 容器，`services.AddSingleton<T>()` 只进宿主 DI（供控制器构造注入）；运行时 `ctx.Get<T>()` 查 **ctx 自有共享服务表**。两不相通。**正确模式**：`var inst = new T(); services?.AddSingleton<T>(inst); ctx.Register<T>(inst);` 同一实例双注册。
- **内核插件间服务互通（Cordis 共享服务表）**：`Context.Register<T>` = **全局服务**（root 共享服务表，兄弟 Fiber 可见，注册即走 `ctx.Effect` 逆序回滚自动摘除）；`Context.RegisterLocal<T>` = **本地值**（自身字典，不进共享表）；`Get<T>` 解析顺序 = **本地值 → 共享表**。宿主 seed 契约常驻 app 生命周期、不摘除。消费方 = `ctx.Get<T>()` 软依赖探测（null 走降级）、**禁止缓存实例为字段**（每次用每次 Get，防热重载悬空）。提供方 = **eager 单例**。坑：插件 `Apply` **先于**宿主 `ProvideHostServices` seed 契约执行，故服务对宿主契约须**构造时注入 `IContext` + 首次使用时 `ctx.Get<T>()` 懒解析**。
- **插件工具注册走「属性暴露 + ExtensionPointManager 自动发现」，禁止在 Apply 里手动 RegisterTool**：插件装配期 `ctx.Get<IToolRegistry>()` 为 null，`Apply` 里取注册表注册工具必然失败。正确姿势（AIAgent 同款）：工具实例持 `IContext`，`ExecuteAsync` 时运行期 `_ctx.GetService(typeof(IToolRegistry))` 解析；插件暴露 `public List<IToolFunctionExtension> ToolExtensions { get; }` 属性，宿主 `ExtensionPointManager.DiscoverExtensionsFromPlugin` 自动注册 + 热重载自动注销。
- **插件控制器构造注入宿主契约 = 激活 500（根因级）**：插件子容器（只含插件自身服务 + IContext）解析不到宿主契约 → `PluginAwareControllerActivator` 激活即 500「Unable to resolve service for type ...」。铁律：插件**控制器/服务**一律注入 `IContext`，宿主契约（IToolRegistry/IConfigurationService/ILogService…）运行期 `ctx.Get<T>()` 取。排障：`grep "public \w+Controller\(.*IToolRegistry"` 可扫出此类雷。
- **宿主契约解析不到的三步修法（ScriptRunner DI 实证）**：① `HostProvidedServiceContracts` 补齐契约清单；② 服务构造改 `IContext`，宿主契约走惰性属性 `_ctx.Get<T>() ?? throw`（带强文案）；③ **`ProvideHostServices` 给 `IServiceProvider` 特判——必须 seed 宿主根 provider 本体，不是本次 scope 的 provider**（scope 释放后取会抛 ObjectDisposedException）。
- **插件必须自建表**（见 B4 XCode 流程）；**EnsureCreated 必须 `EntityFactory.InitConnection(ConnName)` 全量建表再探活**（AgentHub 2026-09-23 教训：依赖「Migration=On 首次连接自动建表」是错误假设）。
- **插件内起服务勿用 IHostedService**（PluginServiceRegistry 按 ServiceType 单写覆盖，多插件只启一个）——用静态单例引擎 + `ctx.Effect` 卸载。
- **实体 ConnName 保留（插件改名迁移规律）**：插件改名时实体 `[BindTable(ConnName="X")]` 与宿主 `XCodeConfig.PluginDbs` 的 **key（连接名）保留**，只改 value（插件 Id）→ 库文件名/表不变，数据目录整目录 Move 即完成数据零迁移。

### 插件运行时显式更新（2026-09-24 起：无自动热重载）
> 目录结构与备份生命周期规则以 `docs/04-standards/packaging-upgrade-backup.md` 为唯一真源（§3-T4/§4-R4：去 `_backups` 已实施，2026-09-28 输入31 批次1；新版本直接 stage 到 `versions/`，插件多版本共存即回滚能力）。
- `PluginVersionService.UpdatePlugin`：版本比较（≤ 已加载版本直接跳过）+ 切 `current` 指针 + 卸载旧 ALC + 加载新 DLL + 端点动态刷新（`ApplicationPartManager` + `MvcActionDescriptorChangeProvider.NotifyChange`）。
- **触发方式（唯一）**：`POST /api/plugin/update/{id}` 版本化显式更新（`scripts/publish-plugin.ps1` 只做编译+staged 复制，不自动调 API）。`PluginHotReloadWatcher` 已一刀切移除（2026-09-24 输入 8 用户拍板，宿主不再自动监听插件目录）。冷启动宿主 = 兜底。
- **版本目录布局**：`Plugins/{id}/versions/<ver>/<entry>` + `Plugins/{id}/current` 文本指针；staged 入口 = `Plugins/{id}/versions/<ver>/`（2026-09-28 输入31 去 `_backups`：新版本直落 versions/，无备份目录）。
- **失败回退**：`ReloadPlugin` 异常自动回退 `current` 到上一可用版本；`POST /api/plugins/rollback/{id}` body `{"version":"x"}` 手动回滚。
- **DLL 锁处理**：`PluginAssemblyUnloader.ForceCollect`（两轮 GC + 终结器）+ `TryOpenExclusive`（`FileShare.None`）+ `TryDeleteDirectory`（占用时跳过下轮重试）。
- **发布脚本用法**：`./scripts/publish-plugin.ps1 -Plugin AIAgent`（`-Plugin` 是目录名 PascalCase，非 id）；幂等（staged 已存在则 skip，需 `-Force` 覆盖）；`-DryRun` 仅打印。技能侧载路径：`pwsh .agents/skills/plugin-publish-verify/scripts/run-plugin-publish-verify.ps1 -Plugin <PascalCase目录>`（`-BumpVersion` 自动升版本、`-SkipPublish`、`-Force`、`-ReloadWaitSec`）。**2026-09-27 起**：该脚本仅限「插件版本化侧载（宿主不重启）」可选用途且**必须先获用户同意**；脚本内「杀非 publish 实例重启宿主」的行为已废除。**主路径 = 打 tag 自动发布 + 页面自动更新**（见 B10）。
- **覆盖顺序：payload（DLL、web/dist）先，`plugin.json` 最后**。先写清单会在拷贝中途触发重载，后续 DLL 拷贝报 `Could not find file`。
- **更新成功判定（用户约定）**：取接口 `GET /api/plugin` 返回的该插件 `version`，与**当前活动目录 `plugin.json`（清单文件）**的 `Version` 比对，**一致即认为更新成功**。清单文件是版本号唯一真源。
- **端点前缀是单数 `api/plugin`**（`[Route("api/[controller]")]` + `PluginController`）。`publish-plugin.ps1` 结尾打印的 `/api/plugins/...` 是**错的**，实测 404。
- **假成功陷阱**：版本号来自 `plugin.json`，改 C# 代码时若入口 DLL 没真正替换，会出现「版本显示新值但跑旧二进制」。必须比对哈希：`publish/plugins/<Dir>/<Dir>.dll` vs `Plugins/<id>/versions/<ver>/<Dir>.dll`（脚本已内置该校验；2026-09-28 输入31 去 `_backups`）。
- **「版本号升了」≠「新代码生效」**：版本化布局（versions/ + current）只证明「切换动作完成」。**宿主代码（Middleware/Controller 等）改动 2026-09-27 起一律走 tag 发布 + 页面自动更新**（update-agent 自更新），agent 不手动停宿主；插件自身 DLL 生效判据 = 版本快照 DLL hash == staged hash。
- **web/dist 版本化读取**：`PluginFrontendFileMiddleware.ResolveFrontendRoot` 版本化优先（current 指针存在且 `versions/<current>/web` 存在 → 从版本快照读，否则回退扁平 `{插件目录}/web`）。
- **插件 config.json 手写键大小写**：插件 config.json 属用户可手改文件，`System.Text.Json` 默认大小写敏感——手写 `{"port":...}` 会被静默忽略、绑定回退默认端口。加载器必须 `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`。

### 发布坑（反复踩，全量 build 前必读）
- 🔴 **宿主进程锁致 build.ps1 发布漏更宿主 DLL**：`build.ps1` 覆盖 publish/ 用 `Copy-Item -ErrorAction SilentlyContinue` 或 `robocopy /E`——**运行中宿主锁定的文件（ForgeSelf.dll）被静默跳过**，publish 里宿主 DLL 保持旧版 → 与插件同路由控制器**歧义** → 界面/API 500 `AmbiguousMatchException`。**教训**：① 替换宿主自身二进制前先 `Stop-Process -Name ForgeSelf` 释放锁；② 发布后核对 `publish/ForgeSelf.dll` 时间戳与 `ForgeSelf.Api/bin/Release/.../ForgeSelf.dll` 一致；③ 排查「插件控制器 500 且无 action 日志」优先怀疑**路由歧义/控制器残留**。**⚠ 2026-09-27 起**：该「先停宿主」动作只适用于用户不在使用的隔离验证环境；**对用户运行中的宿主（含 51888 实例），禁止 agent 停/启/杀**，宿主二进制变更一律走 tag 发布 + 页面自动更新（见 B10）。
- 🔴 **PS 5.1 `Copy-Item 'dir\*' -Recurse` 通配符 bug 会静默漏拷**：`build.ps1` 第 3 步曾用 `Copy-Item (Join-Path $stagingDir '*') $publishDir -Recurse -ErrorAction SilentlyContinue` 在 PS 5.1 下**不拷全**（报错或 exit 0 假成功）。**修复**：改用 **`robocopy $stagingDir $publishDir /E`**（exit 0-7 均成功）+ 两处 robocopy 后 `$LASTEXITCODE = 0` 复位 + 脚本末尾 `exit 0`。**教训**：发布动作一律走发布技能主路径（打 tag 自动发布 / `release-local.ps1 -UpdateDir` + 页面自动更新），**禁止手动 Copy-Item / robocopy 进 publish/**。
- 🔴 **重复插件 id 目录致宿主启动崩溃**：`publish/plugins` 同时存在两个 plugin.json 的 `Id` 相同目录 → `TopologicalSort` 撞 key → **宿主启动即崩**。发布/归档后检查 `publish/plugins` 下 plugin.json `Id` 无重复；冗余目录移入 `.trash/`（勿删；`_backups` 已废弃，2026-09-28 输入31）。
- 🔴 **活动插件目录/版本快照只放插件自身程序集**：**绝不能**放入 `XCode.dll`/`NewLife.Core.dll`/`NewLife.Agent.dll`/`NewLife.Remoting.dll`/`ForgeSelf.Abstractions.dll`/`ForgeSelf.Core.dll`/`Stardust.dll`。否则 `PluginLoadContext` 再加载一份 → 类型标识分裂（「插件类型未实现 IPlugin 接口」）+ ALC 卸载中加载 → `FileLoadException`，宿主**启动即崩**。脚本已内置白名单过滤 + 防御性清理。
- 🔴 **宿主运行时入口 DLL 被独占锁，无法覆盖**：`plugin.json` 与 `web/dist` 可热覆盖；`<Dir>.dll` 被 ALC 锁定，独占 open 报「being used by another process」，而 `Copy-Item` **误报**成 `FileNotFoundException`（排查时勿被误导）。改 C# 代码靠 side-by-side 版本化更新或停宿主。
- ✅ **SQLite 驱动已是正式依赖（输入38，2026-09-29）**：`XCode.SQLite` 包（11.24.2026.302，依赖 NewLife.XCode ≥11.25 不升级主版本）把 `System.Data.SQLite.dll` 变成 csproj 包依赖 → dev-bin/FDD 发布随输出自然落盘；业务层单文件（PublishSingleFile）经 csproj Target `ExcludeSqliteFromSingleFile` 从 `FilesToBundle` 剔除 + `publish-host.ps1` 从 NuGet 缓存外置复制 → 探测本地命中、零下载、不再生成运行态 `Plugins/`（旧认知「外部放置运行时构件、全量 build 后需回补」已废除，仓库 `build/runtime/plugins` 已移 `.trash/`）。
- **验证已部署前端资产必须用 `/assets/` 前缀路径**：`vite.config.ts` 的 `build.assetsDir` 默认把 JS/CSS 产物输出到 `wwwroot/assets/`，宿主 `index.html` 引用 `/assets/index-xxxx.js`。直接 `curl http://host/index-xxxx.js` 会 **404 误判"修复未上线"**。确认线上确为修复后构建的最稳妥办法：`diff <(curl -s http://host/assets/<file>) <(cat publish/wwwroot/assets/<file>)` 应 `IDENTICAL`。
- **发布目录运行时遗留坑**：`publish/` 下的 `Data/`、`Log/`、`Config/` 是**运行 exe 时动态生成在程序目录**（非 dotnet publish 拷贝）。发布脚本清理发布目录应**整目录删除重建**而非仅删文件。safe-delete shim 拦 `Remove-Item`：build.ps1 清 publish/data|Log 被 fail-closed 拦退码 1（发布实质完成），并产生 `publish/publish/` 嵌套残留；规避 `-ErrorAction SilentlyContinue`。
- **PowerShell 数组 splat 是按位置传参**：`& $f @arr` 会把 `-PluginsRoot` 塞进前一个位置参数（实测撞上 `-Configuration` 的 ValidateSet）。跨脚本调用一律**显式具名传参**。
- **publish-plugin.ps1 子目录拷贝两处 bug（2026-09-23 修复）**：① `Split-Path -LiteralPath -Parent` 参数集冲突（-LiteralPath 不支持 -Parent）→ 改用 `[System.IO.Path]::GetDirectoryName`；② `FullName.IndexOf()` 在路径含 AppData 等包含 Data 的父路径时截错 → 改用 `Substring(.Length).TrimStart('\','/')` 按前缀长度精确截取相对路径。
- **运行宿主版本化布局判定**：插件热更新后 `versions/<v>/` + `current` 指针已切换、但活动根目录旧 DLL 被 ALC 占用 → 实际加载的仍是旧程序集。**判定**：`/api/plugin` 显示新版本 ≠ 加载新 DLL；须冷启动宿主后新 controller 才生效。
- **发布后必须冷启动验证**：插件整体改名（新 id 是新插件目录）或宿主核心改动后 watcher 不发现，必须冷启动（停 → 覆盖 → 起）。

### 宿主/插件运行形态（数据根与端口）
- **数据根二选一**：`ASPNETCORE_ENVIRONMENT=Development` → `程序目录/Data`，否则 → `~/.forgeself`；重启 51888 复用真实配置必须**不设** Development。NewLife 日志按 CWD 写（publish/log），与数据根无关。
- **活动代码目录 vs 数据目录**：`publish/plugins/<Dir>/`（dll + plugin.json + web/dist）= 宿主启动扫描 + 加载；`~/.forgeself/plugins/<id>/` = 插件自有 sqlite/缓存（运行时生成，**不**放代码）。不要即兴：猜目录、擅自重启宿主、绕开技能自创流程 → 几乎必踩坑（曾把插件代码误装进数据目录）。
- **本地代理拦截 localhost 致宿主访问 LM Studio 502**：本机 `HTTP_PROXY=127.0.0.1:10808` 时，从该 shell 起的宿主进程会用代理访问 `localhost:1234`（LM Studio）→ 502。**宿主须带 `NO_PROXY=localhost,127.0.0.1` 启动**（curl/Playwright 同理）。
- **插件新目录两条生效路**：① `POST /api/plugin/install` 上传 `.forgeself-plugin` 包触发 `DiscoverPlugins()`；② **冷启动宿主**。运行中更新**已加载**插件的入口 DLL 会被 ALC 锁死（`File.Copy` 覆盖报「找不到文件」实为锁）——走 `POST /api/plugin/update/{id}` 或冷启动。
- **MCP 中心整合兼容决策**：环境变量前缀**保留旧名** `FORGESELF_MCP_GATEWAY_*`、类名保留（McpGatewayConfig/Server/...），仅 namespace/日志前缀/`serverInfo.name` 改——兼容既有运维/e2e 环境变量写法，避免破坏性改名。

## B6 PowerShell 工程坑（本项目高频）

- **铁律（2026-10-04 输入12 定，AGENTS §2.3 同条）：执行脚本一律 `pwsh`（PowerShell 7，本机 7.6.6），禁止 `powershell` / `powershell.exe`（5.1）**——跑仓内 `*.ps1`、Git Bash 里调 PS、Node `spawn` 全部适用（CI `release.yml` 已是 `shell: pwsh`）。5.1 的三条实测代价：① 重定向日志与外部命令输出按控制台码页（GBK/936）处理 → 中文乱码且会被烤进交付物；② 从 Node spawn 无 `Cert:` 提供程序 → 签名必失败（见本节末条）；③ 靠 BOM 判定脚本编码 → UTF-8 无 BOM 中文脚本解析期崩（update-agent 事故）。**唯一例外**：为证明"无 BOM / 码页"这类 5.1 专属缺陷而做复现排查时必须用 `powershell`，用 Core 复现不出来 = 假绿（见下方"排查/验证"条）。**已登记偏差**：`scripts/hooks/pre-commit:24` 仍用 `powershell.exe` 调工件门禁——改它要考虑"没有 pwsh 的 clone 会不会提交不了"，属高风险，已入 TODO 待拍板，本条不擅自改。
- **捕获 `git` 等外部命令的 UTF-8 输出，必须先切 `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8`（用完还原）**：5.1 下不切码页直接 `& git log --pretty=...` 拿到的是**已解码损坏的字符串**，把乱码写进 RELEASE-NOTES 就是不可逆交付缺陷（2026-10-04 输入11 实测：更新说明整段乱码）。正规写法见 `scripts/release/make-release-notes.ps1` 的 `Invoke-GitUtf8` 助手。自动化守卫：`ForgeSelf.Api.Tests/RepositoryScriptTests.cs::GitLogCapturingScripts_MustSwitchConsoleToUtf8First`（扫 `scripts/**/*.ps1`，出现 `& git log` / `Invoke-GitUtf8 log` 的脚本必须在它之前设置 UTF-8 码页，并带"扫到文件数 > 0"阳性对照）。
- **铁律**：涉及中文的 PowerShell 脚本/写文件，绝不能直接用 `Get-Content`/`Add-Content`/here-string 管道。PS 5.1 默认按 ANSI 处理 → (1) 脚本自身解析失败（UTF-8 无 BOM 含中文 → 括号失配 → `ParserError`）；(2) 写入内容损坏。
- **修既有中文脚本首选「前置 UTF-8 BOM」**（字节级、**不重编码**、零内容损坏）：`$b=[System.IO.File]::ReadAllBytes($p); if(-not($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)){ $n=New-Object byte[]($b.Length+3); $n[0]=0xEF;$n[1]=0xBB;$n[2]=0xBF; [Array]::Copy($b,0,$n,3,$b.Length); [System.IO.File]::WriteAllBytes($p,$n) }`
- 新脚本内容全英文（最省事）；读写文件用 .NET API（`ReadAllText($p,[System.Text.Encoding]::UTF8)` / `WriteAllText($p,$s,(New-Object System.Text.UTF8Encoding($false)))`）。
- **排查/验证**：`[System.Management.Automation.Language.Parser]::ParseFile($p,[ref]$null,[ref]$errs)` 看 `$errs.Count`；**必须用 `powershell`（5.1）而非 `pwsh` 复现**——PS Core 解析无 BOM UTF-8 正常，用 Core 验证会漏判。
- **本坑的实弹代价（2026-09-26 spec 036）**：`update-agent.ps1`（随发布包分发、由宿主用 powershell 5.1 拉起）UTF-8 无 BOM → 解析期崩溃、**连日志都写不出**，宿主自停后无人换文件重启，51888 实例直接下线。自动化守卫：`ForgeSelf.Api.Tests/RepositoryScriptTests.cs`（scripts/ 下含非 ASCII 的 .ps1 必须带 BOM），新增中文脚本先跑它。
- **PowerShell here-string `$x=@'...'@` 等号后必须换行**：`@'` 必须独占一行，凡模板/多行替换一律拆行写。
- **构造 JSON 请求体一律 `ConvertTo-Json`，禁止手工拼字符串/正则 hack**（少一层闭合括号 → JSON-RPC parse error 返回 error 而非 result → `Invoke-RestMethod` 返回 null 数组才暴露）。发送前先核响应结构（有 `result` 还是 `error`），再取字段。
- **安全删除钩子**：`Remove-Item` 被包装成"移到回收站"，锁文件会失败；`cmd /c "rd/del ..."` 等价删除会被 **safe-delete fail-closed** 直接拒绝（明确 "Do not retry"）。Agent 不可绕过；需物理删除时请用户手动执行。
- **pnpm 的 ignored-builds 会拦截 esbuild**：`pnpm install` / `pnpm run build` 报 "Ignored build scripts: esbuild" 直接失败。可用绕法：直接执行 `node_modules\.bin\vite.cmd build` 跳过 pnpm 的 deps-check。
- **脚本内避免复杂管道**：`Get-ChildItem -LiteralPath X -File -Recurse | ForEach-Object { }` 在**脚本内**报 `AmbiguousParameterSet`（同样语句在 `-Command` 下正常）。递归复制一律用 `Copy-Item -Recurse`。
- **PowerShell 追加文件后若再用 WriteAllText 覆盖整个内容，会丢掉刚追加的文本**（AppendAllText 与 WriteAllText 混用顺序错误）——追加类/内容后用 ReadAllText 校验。
- **核验 .NET DLL 内字符串必须原始字节 hex（UTF-16LE）或 ildasm，禁止 shell 中文字面量**：PowerShell→`python -c` 传中文搜索词跨进程编码损坏、UTF-8 解码 #US 堆都会假阴性。定论手段：`ildasm /text` 看 IL（`ldstr bytearray`）+ Python `bytes.fromhex(...) in data` 精确比对 UTF-16LE 字节。
- **PS 5.1 读写中文路径/内容用 .NET API + `-Encoding UTF8` 显式**；`Get-Content` 默认 ANSI 解码 UTF-8 无 BOM 文件显示乱码 ≠ 文件损坏（两文件哈希一致即文件正常）。
- **Git-Bash 里给 `pwsh -File` 写 `*> <log>` 会被 bash 先展开通配符**（2026-10-07 实测）：bash 不认识 PowerShell 的全流重定向操作符 `*>`，于是把 `*` 当 glob 展开成**当前目录的文件名列表**，`CONTEXT.md` 之类被当作位置参数塞进脚本 → 报「找不到接受实际参数 'CONTEXT.md' 的位置形式参数」、exit 1，**形态完全像被调脚本自己坏了**（我当时因此去读了 `verify-pilot-artifacts.ps1` 源码，白绕一圈）。Git-Bash 侧一律写 `> log 2>&1`；要用 `*>` 就放进 `pwsh -Command "… *> log"` 里由 PS 自己解析。
- **`Edit` 工具反复报 "File has not been read yet"**：改用 PowerShell `[IO.File]::ReadAllText/WriteAllText(UTF8Encoding(false))` 精确替换；若用 .Replace，替换后先 `if($t -ne $o)` 判断再写，命中失败打印 'NO MATCH'。
- **从 Node（Playwright / 脚本链）spawn PowerShell 做证书 / 签名类操作必须用 `pwsh`（PowerShell 7），不要用 `powershell.exe`**（2026-10-01 e2e 实证）：同一 `powershell.exe` 在终端语境正常，但从 Node `spawn` 时（5.1）**没有 `Cert:` 提供程序**（`Get-PSDrive Cert`=False、代码签名证书查不到、`-CodeSigningCert` 参数不识别 → 签名必失败）；`pwsh` 在同语境完全正常（`drive=True certs=1`）。`e2e/global-setup.ts` 宿主签名已固定 `pwsh`；无签名环境用 `E2E_SIGN_EXE=false` 跳过。**取代此前任何"两个 shell 均可"的默认假设。**
- **批量改写文档/脚本正文的脚本必须保留原文件的 BOM**（2026-10-07 拆分 `agent-workflow.md` 时实测踩中）：`[IO.File]::WriteAllText($p,$t,(New-Object Text.UTF8Encoding($false)))` 会把**本来带 BOM** 的文件写成无 BOM——本仓多份入库文档首字节就是 `EF BB BF`（当场核实：`AGENTS.md`、`docs/04-standards/packaging-upgrade-backup.md` 的 HEAD 版都带）。后果不是编译问题，而是**归因污染**：`git diff` 把首行也算成改动，"我这次只改了指路句"的自述与 diff 读数对不上，后续会话据此判责必错。
  判据与补法：**改前 `head -c 3 <file>`、改后必须同值**；要补回用**字节级前置**（`ReadAllBytes` → 新建 `len+3` 数组写入 `EF BB BF` → `Array.Copy` → `WriteAllBytes`），不要用文本重写整份文件（那会连带改掉行尾与内容）。反向也成立：新建的 `.ps1` **必须**带 BOM（见上方铁律与 `RepositoryScriptTests` 守卫），新建的 `.md` 无此要求。

