# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜载体：markdown 正文直发群消息（规范 §5；仅超单消息上限或用户明确要求时用附件，且附正文摘要）
> 状态只报事实，禁止模糊表述（对齐 AGENTS.md §10.4/§10.5）。
> Task ID：PILOT-053（`2026-10-06-tool-bridge`）｜日期：2026-10-06｜闸门1：用户「按推荐执行」

## 1. Repository Understanding

我确认了：后端 .NET 10 + SQLite/XCode 宿主 `ForgeSelf.Api/`，插件源码在仓库根 `Plugins/<PascalCase>/`（19 个），前端 Vue 3.5 + Vite 6，插件自带界面走 `web/dist/index.js` 远程加载；验证入口是 `dotnet build` / `dotnet test` / `pnpm run check|test` / Playwright 插件层 e2e；插件 csproj 一律只引 `ForgeSelf.Core` + `ForgeSelf.Abstractions`（实测 19/19），且**这两个共享层都没有引用 NewLife.Core**；宿主无全局鉴权中间件，策略在 `AppBuilder.cs:296-301`；全仓**不存在**"从 AI 自由文本解析工具调用"的代码（`tool_calls`/`function_call` 只在协议网关字段名与事件归一化里），`McpService.TestToolAsync` 实测是 `Thread.Sleep` 假端点。全部依据带 `文件:行号`，见 `00-repository-understanding.md`。

## 2. Selected Task

新增插件 `ToolBridge`（id `tool-bridge`）：复制含四工具 schema 的初始指令给网页 AI → 粘回 AI 回复 → 多格式解析（认不出必带原因、绝不猜）→ 在沙箱工作根内真实执行 `read_file`/`write_file`/`list_dir`/`run_command`（命令过白名单与破坏性红线）→ 结果拼成可一键复制的回粘文本 → 每轮落一条可回看记录。级别：**全量**（新功能 + 插件任务）。

## 3. Changed Files

- **新建**：`Plugins/ToolBridge/`（`plugin.json`、`ToolBridge.csproj`、`ToolBridgePlugin.cs`、`Services/`×10、`Models/`×3、`Controllers/ToolBridgeController.cs`、`web/`×7 源文件 + lock/workspace/vite 配置）；`ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/`×7；`ForgeSelf.Web/e2e/plugins/tool-bridge/tool-bridge.spec.ts`；`docs/02-features/039-tool-bridge.md`；本工件目录 00~07
- **修改**：`ForgeSelf.Api/ForgeSelf.Api.csproj`（+1 行登记）、`ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`（+1 行）、`.agents/skills/plugin-development/SKILL.md`（铁律 20 + §六 五条）、`.agents/skills/plugin-feasibility-study/SKILL.md`（§七 落点更正 + §九）、`docs/07-decisions/not-taken-decisions.md`（034~037）、`TODO.md`、`.forgeself/memory/2026-10-06.md`
- **未动**：`Plugins/AIAgent/**`、`ForgeSelf.Api/Plugins/**`、`XCodeConfig.cs`、`ForgeSelf.Web/src/**`、宿主 vite/playwright 配置、任何 `package.json`

## 4. Validation

Build: `dotnet build ForgeSelf.Api` → **0 错误**（`.temp/tb-build4.log`）；**AC13 宿主产物判据**：`ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/ToolBridge/ToolBridge.dll` 存在（98304 B）且 md5 与插件目录产物同串 `aafa0d08dd0d113935cd2045c7da048d`（Verified）。

Unit Test: `dotnet test --filter "FullyQualifiedName~ToolBridge"` → **失败 0 / 通过 140 / 总计 140**（`.temp/tb-test5.log`）。测试首跑抓到 **5 个实现缺陷**（引号误配、工作根回成绝对路径、相对路径被静默接受、plain 模式 CRLF 污染原文、未闭合标签留残片），全部改实现而非改期望。**四条反向探针**各自实红后还原（A 守卫漂移 6 红 / B 就近执行 6 红 / C 手抄白名单 1 红 / D 短路守卫 2 红），终态 `grep PROBE-` 0 命中。

E2E: `bash node_modules/.bin/playwright test e2e/plugins/tool-bridge --workers=1` → **6 passed（4.3m）**（`.temp/tb-e2e6.log`）。迭代 6 轮：前几轮抓到 1 个真 UI 竞态（已修视图）+ 2 条我自己的用例期望错 + 1 次我用错 Playwright API + 1 轮被并行会话的 `CostScope` 编译错挡住 globalSetup（未改对方文件，重试通过）。

Static: 宿主 `pnpm run check` **0 error / 81 warning**（基线同数）；宿主 `pnpm run test` **755/756**（1 红为 `SettingsView.test.ts` 计时 flake，**非本批**：`git status --porcelain ForgeSelf.Web/src` 空输出）；插件前端 `pnpm install --frozen-lockfile && pnpm build` 按 CI 同参数复现通过，产物只有 `index.js` + `style.css`；裸导入自检命中 `from "vue"`。

中档全量后端（§5.6，因碰了 `ForgeSelf.Api.csproj`）：读数见 §5。

## 5. Evidence

全量证据在 `05-evidence.md`（每项标 Verified/Inferred/Unknown + 日志路径）。核心事实：
- 一轮真实回合（e2e 日志原文）：`write_file` 写 38 字节 → `read_file` 读回 `"# 工具桥 e2e 第二行\n追加一行"`；`git --version` → `exitCode 0` + `git version 2.49.0.windows.1`；`rm -f important.txt` → `command_rejected` + 守卫原文；`delete_file` → `unrecognized`；散文 → `unparsed` 带「疑似工具名「write_file」但缺参数结构」。
- e2e 终态：**6 passed（4.3m）**（`.temp/tb-e2e6.log`，`E2E_EXIT=0`）——六条含"完整一轮识别 4/执行 3/被拒 1/未知 1/未解析 2"、"execute 不落档"、"匿名 401×4"、"1280×720 无溢出 + 四卡片不被 flex 压扁"；截图 `ForgeSelf.Web/screenshots/e2e/tool-bridge/alignment.png`（21:15:57）已读图核对（按钮宽度缺陷确认消失）。
- 中档全量终态：**未跑成（Unknown）**。`dotnet test` 在构建阶段即失败：`error MSB3027/MSB3021 … 文件被 "testhost (34664)" 锁定`；`tasklist` 显示该 testhost 存活且另有 8 个 `dotnet.exe` 在跑 ⇒ 并行会话正在使用同一台机器与同一输出目录。**未杀他人进程**（AGENTS.md §0 铁律 1 + 并行避让），两次复探同果。等价证据：Release 全图 `dotnet publish` 成功并起住宿主（6 条 e2e 全绿）、Debug 全图 `dotnet build` 0 错误、本插件过滤集 140/140。

## 6. Review

`06-review.md` Final Decision = **APPROVED_WITH_PENDING**（本会话自审，非独立验收方）；Requirement/Scope/Architecture Check 均 PASS，Test Check 为 PASS_WITH_NOTE（宿主一条非本批红）。Risk **L2**。

## 7. Risk

**L2**：新增面全在插件目录内、无 DB 变更、无新依赖；真实风险是"会在用户机器上写文件与起进程"，由三层约束兜住（命令白名单+58 条红线、沙箱工作根 + 危险根显式确认、管理面类级鉴权 + e2e 真 401）。次生风险：守卫两份实现的漂移（已由 41 条金样对账钉住，探针 A 证明能红）。

## 8. Problems Found

1. **发布链缺件（本批修掉）**：新建带界面插件若缺 `pnpm-lock.yaml`，`scripts/release/build-frontend.ps1:39` 的 `pnpm install --frozen-lockfile` 在 CI 必红——本地出树构建能过，所以 Plan 期调研"打包脚本无需登记"的结论漏了这条。已补 lock/workspace 并回写为 plugin-development 铁律 20。
2. **UI 竞态（e2e 抓到并修）**：首屏渲染早于四个请求返回，迟到的 `loadAll()` 会把用户已填的工作根输入框覆盖回默认值（真实读数：断言 `inputValue` 收到默认绝对路径）。修法在视图侧（`workspaceTouched` 守卫），不是给用例加等待了事。
3. **假探针一次**：反向探针 B 第一次写在 `return null` 之后（不可达代码），跑出 22/22 绿——那是假证据；挪到生效位置才见红。已把"探针必须体现为结果变化"写进技能。
4. **第二份真相两处**：提示词与 `ToolSpec.RunCommand` 描述里各手抄了一份命令白名单 ⇒ 改为从 `CommandGuard.DefaultAllowlist` 派生（探针 C 的副产品）。
5. **并行会话互扰一次**：21:00:41 对方把 `Plugins/CostScope/Controllers/CostController.cs:403` 改出 `error CS0120` ⇒ 我这一轮 e2e 的 publish 直接失败。未改对方文件，重试后通过。
6. **计划自身一处错判**：04-task 要求"README 功能表加一行"，实读证明该表派生自 `features.ts` 且插件功能不入表 ⇒ 改为 not-taken 036 记录，未污染 README。
7. **中档全量未跑成（诚实记账）**：`dotnet test` 在构建阶段被并行会话的 `testhost (34664)` 文件锁挡住（MSB3027/3021，两次复探同果）。我没杀别人的进程，也没因此把"快档 + e2e"报成全量绿；这条仍是 Unknown，等机器空时补跑。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（两路子代理 + 自查，全部带 文件:行号；一处漏项见下） |
| Intent → Spec | PASS（四问口径由用户拍板后才写 Spec；Unknown 表 6 条如实留白） |
| Spec → Plan | PASS（决策 D1~D6 带代价与证据；5 张流程图查出 BC-11/BC-12 两条原设计无下落的分支） |
| Plan → Code | PARTIAL（Plan 漏了"插件前端必须有 lockfile"这一发布链条件；偏差已记 03-plan 表） |
| Code → Test | PASS（TDD 顺序：先写用例见红→改实现→绿；5 个真缺陷由测试抓出） |
| Test → Evidence | PASS（探针四条 + 每轮日志路径；一条假探针被自己发现并纠正） |
| Evidence → Review | PASS（06 八问逐条回答，未做项显式列出） |

## 10. 最重要的问题

**"新建插件要登记几处"这类清单不能只查源码树，必须把发布链跑一遍。**
本批调研阶段逐条 Verified 了 `ForgeSelf.Api.csproj` / `Api.Tests.csproj` / `XCodeConfig.PluginDbs` / `features.ts` / `dynamicPlugins.ts` / 打包脚本名单，结论"scripts 无需改动"——但 `build-frontend.ps1` 是**按 glob 自动发现插件 web 并要求 lockfile**：不登记任何名单，却要求每个插件前端自带依赖锁文件。这类"无名单但有硬前提"的集成点，静态 grep 是查不全的，只有把 CI 的那条命令按同参数本地跑一次才会现形（这与 AC13「判据必须是宿主产物，不是插件目录自建」是同一族教训）。

## 11. 下一步建议

把"**新建/改动插件后按 CI 同参数跑一遍发布前端链**"固化成一个可重复动作：在 `scripts/release/build-frontend.ps1` 加 `-PluginsOnly -Plugin <X>` 的快速路径已存在，因此下一步应是一条门禁命令 + 一条仓库级守卫测试（断言每个含 `package.json` 的 `Plugins/*/web` 都带 `pnpm-lock.yaml` 与 `pnpm-workspace.yaml`），把这条踩坑变成机器判据，而不是留在技能文字里。
