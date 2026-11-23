# 05 Evidence — PILOT-087 sems 停止反馈（2026-11-23）

> 证据分级：**Verified**（亲自跑过拿到真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。只记实际发生。

## 1 红灯（复现，先于实现）

| # | 复现物 | 结果 | 等级 |
|---|--------|------|------|
| R1 | `Plugins/Sems/web/src/workbench/stop-feedback.spec.ts`（6 例，vitest，harness 同 regression-fixes.spec.ts） | 修复前 **5 红**：① 404+运行中不收敛（状态仍 running、无 /api/runs 核对）② already-stopped 被当「已停止」且不收敛 ③ failed 无留痕行且 stopTask 无返回形态 ④ 未运行仍发 POST 且无「无需停止」提示 ⑤ 停止留痕行是「■ 已请求停止」非「■ 已停止」；第 6 例（404+瞬态维持轮询）修复前后同绿（守卫型） | Verified |
| R2 | 后端行为差异无独立红灯文件——以「编译失败」为红灯：`IRunnerService.Stop` 签名改 `StopResult` 后旧测试 12 处 `Returns(true/false)` 编译错误即复现「后端无可区分结果」 | Verified（dotnet build 报错清单见构建日志） |

## 2 绿灯（实现后全部门禁）

| 门禁 | 命令 | 结果 | 等级 |
|------|------|------|------|
| 后端构建 | `dotnet build ForgeSelf.Api` | 0 错误（1656 警告全为存量） | Verified |
| 后端测试（过滤档） | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~Plugins.Sems"` | **152/152 通过**（1m01s），含新增：`Stop_SecondCall_AfterStopped_ReturnsAlreadyStopped`、`Stop_UnknownCommand_ReturnsAlreadyStopped`、`StopExternal_UnknownPid_ReturnsAlreadyStopped`、`StopExternal_InvalidPid_ReturnsFailed`、`Run_And_Stop_Endpoints_On_Launched_Session`（三态 200/200/409 + outcome 断言）、`Run_And_Stop_Failures_Are_Expressed_As_Tool_Errors`（F25 文案逐字守卫） | Verified |
| 插件前端测试 | `cd Plugins/Sems/web && pnpm run test` | **53/53 通过**（8 文件），含 stop-feedback.spec.ts 6/6 绿、confirmOps 10/10（含 data 透传）、regression-fixes 6/6（未受影响） | Verified |
| 插件前端构建 | `cd Plugins/Sems/web && pnpm run build` | dist/style.css 54.62 kB + dist/index.js 170.97 kB 构建成功（toast-host 修复后复建） | Verified |
| 设计门禁 | `pnpm run design:check`（根） | PASS：无新增硬编码色值（1768 < 基线 2501）、无新增设计规范违反 | Verified |
| 问题类机器校验 | `pwsh scripts/check-problem-classes.ps1` | **OK：41 个 PC 签名/allowlist/guards 全部通过**（PC-024 复发/守卫/台账齐） | Verified |
| 插件冒烟 e2e | `pnpm run test:e2e -- e2e/plugins/sems/sems.spec.ts --workers=1`（真实 publish 宿主，零 mock） | 见 §4（运行中/结果待回填） | 见 §4 |
| 起源码本地前后端按验收标准走查 | `scripts/dev-stack.ps1` 起源码整栈 → 按 AC-F09-1…7 逐项点停 | 见 §5（待回填） | 见 §5 |

说明：本任务未触碰宿主源码（`ForgeSelf.Api/Plugins/Sems/**` 为插件目录）与仓库级脚本，按 §5.6 跑**快档**（过滤后端测试 + 插件门禁 + 设计守卫 + 单插件冒烟 e2e），未跑全量后端/全量 e2e；worktree 基线红先对表原则不适用（过滤集内无基线红）。

## 3 F25 工具语义守卫（逐字核对）

`sems_stop_command`：成功仍为 `{stopped:true, commandId}`；「无存活会话」错误文案逐字 = `未找到本面板启动的存活会话：{id}（外部捕获进程请用 sems_stop_run 按 pid 停止）`（`SemsToolExtensionTests` 断言）。`sems_stop_run`：成功仍为 `{stopped:true, pid, note:…}`；「无法停止进程（不存在或无权限）：{pid}」逐字保留；新增 failed 文案仅当「进程仍在运行但无法终止」时出现。等级：Verified（测试断言逐字比对）。

## 4 冒烟 e2e（真实 publish 宿主，零 mock）

运行：`pnpm run test:e2e -- e2e/plugins/sems/sems.spec.ts --workers=1`（serial，一红即断）。

| 用例 | 结果 | 等级与归属 |
|------|------|-----------|
| `管理面鉴权 401`（:539，断言含本次新增三端点 `commands/stop`/`runs/stop`/`runs/check` 未带 token 均 401） | 单独复跑 `--grep "管理面鉴权"` → **1 passed (26.6s)** | Verified（本批新增端点被鉴权面覆盖） |
| :150 / :261（旧用例选择器 `.sems` 找不到元素） | 红：`TimeoutError: locator('.sems …') not found` | **基线红，非本批**：两用例最后改动 c85f537（2026-10-30），workbench 重构 ca1ccc3（2026-11-19）已把 `.sems` 改 `.sems-root`；本批 diff 不含 e2e 与 workbench 骨架类名。已记 TODO（补选择器/或删过时用例） |
| :450（MCP 链路） | 红：首次只读 `tools/list`（id=1）即 `SyntaxError: Unexpected token 'e', "event: mes"… is not valid JSON` | **基线红，非本批**：网关以 SSE 回包而用例 `res.json()` 解析（`mcpCall` :402-410）；`ModelContextProtocol.AspNetCore` 2.2.0 自 30b076f（2026-10-09）已在 csproj，早于该用例最后更新；本批 diff 仅 `Plugins/Sems/**`。失败发生在**首个只读调用**，全链零写操作。已记 TODO（用例改 SSE 解析） |
| :567（后续用例） | 未运行（serial 模式在 :450 断后跳过） | Unknown（无结论，不臆断） |

端口隔离佐证：`playwright.config.ts:39` e2e MCP 网关端口 = `19000 + (wtHash % 900)`，**永不落在 18890**（用户运行实例 PID 75416 所占），e2e 不会误伤用户实例。

## 5 起源码本地走查（dev-stack，后端 :7301 / 前端 :7399，逐 AC）

演示数据：登记项目「停止反馈走查」（根目录 `.temp/walkthrough-stop-demo`）+ 命令「长驻 ping」= `ping -n 600 127.0.0.1`；截图存 `ForgeSelf.Web/screenshots/dev-stack/sems-walkthrough-*.png`（已读图核对）。

| AC | 验证方式与观察 | 结论 | 等级 |
|----|----------------|------|------|
| AC-F09-1 空闲任务停止可点、缺日志一次性提示 | 选中已完成任务：停止按钮可用；日志区一次 `! 读取日志失败：请求失败(404): 暂无该命令的输出日志…`，此后长时间轮询 **console error 恒为 1 条**，无重复风暴 | 满足 | Verified |
| AC-F09-2 运行中停止 → toast + 日志留痕 + 状态收敛 | 三次实测（PID 37860 / 76848 / 42884）：toast「已停止：长驻 ping」、日志行「■ 已停止：已停止进程树（PID …）」、状态→已完成（起/止时间闭合）、右上「运行中 0」。截图 `…ac-f09-2-stopped-toast.png`（toast+留痕+收敛同屏）、`…ac-f09-2-stopped-log.png` | 满足 | Verified |
| AC-F09-3 进程已死再点停（already-stopped 分支） | 单测 Verified：会话在但进程死 → 200 `outcome:"already-stopped"`，前端 toast「X 已不在运行，无需停止」（stop-feedback.spec.ts 断言）。实测无法命中：后端日志流 EOF 在 ~1s 内收割死进程会话，远程工具往返（秒级）追不上该窗口；实测「外部杀后点停」落到 not-running 分支（同样完整反馈） | 单测满足；实况窗口过窄 | Verified（单测）/ Unknown（实况） |
| AC-F09-5 未运行点停 → 不发请求 + 提示 | 空闲任务点停止：网络记录**无任何 stop POST**，toast「「长驻 ping」当前没有运行中的进程，无需停止」。截图 `…ac-f09-5-notrunning-toast.png` | 满足 | Verified |
| AC-F09-7 外部杀进程不点停 → 自动收敛 + 留痕 | `taskkill` 16:27:00.374 杀演示 ping 树 → 不点任何按钮 → 日志出现「[16:27:00] 进程已退出（PID 67444）」→ 界面自动收敛：已完成/已结束 + 运行中 0，无轮询风暴。截图 `…ac-f09-7-poll-converged.png`。宿主重启后会话消失变体（404+运行中 → 一次性核对收敛）由单测覆盖 + 16:09 实况「恰 1 条 404」佐证 | 满足 | Verified |
| failed 分支（进程仍在但无法终止） | 单测 Verified：控制器 409 + 工具文案逐字 + 前端 toast「停止失败：…」分支断言；实况无法有机制造（需真实拒绝 kill 的进程） | 单测满足 | Verified（单测）/ Unknown（实况） |

**走查发现（伴生缺陷，随批修复）**：`.wb__toast-host` 原覆盖 `position:absolute;bottom:48px` 锚在 `min-height:100vh` 的 `.wb` 文档底部 → 长页面里 **toast 落在首屏之外**（实测 rect y=936 > vh=716）＝"有 toast 但看不见"，恰好复刻用户「点了没反应」体感。02-spec 交互设计（行 60/68）承诺 toast 为任务详情路径的反馈通道，故随批修复：改 `position:sticky;bottom:48px`（与 statusbar 同机制，`workbench.css` :421-429），重建后 toast 实测 y=610 可见（截图为证）。判定依据：完成已批交互规格所依赖的通道 ≠ 扩范围；根因（布局锚定）与停止逻辑缺陷（终态分支不收敛）不同，已在 case 文件分记。

**清测试数据**：项目经 UI「移除→确认移除」删除（档案级联，共 0 个）；演示目录 `.temp/walkthrough-stop-demo` 已删；遗留演示 ping 树已按父链核对后 taskkill（仅杀本 dev-stack 实例子进程）。
