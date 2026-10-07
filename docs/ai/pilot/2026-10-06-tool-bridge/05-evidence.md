# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。
> Task ID：PILOT-053（`2026-10-06-tool-bridge`）｜日期：2026-10-06｜闸门1：用户「按推荐执行」（2026-10-06 输入7）

## Task

新增插件 `ToolBridge`（运行时 id `tool-bridge`）：把外部网页 AI 回复里的工具调用文本解析出来，在沙箱工作根内真实执行（读文件/写文件/列目录/执行命令），并把结果拼成可复制的回粘文本；含自带界面、后端单测、插件层 e2e、文档与技能回写。

## Changed Files

**新建（本批全部产物）**
- `Plugins/ToolBridge/plugin.json`、`ToolBridge.csproj`、`ToolBridgePlugin.cs`
- `Plugins/ToolBridge/Services/`：`ToolSpec.cs`、`PromptBuilder.cs`、`CallParser.cs`、`CommandGuard.cs`、`SandboxRoot.cs`、`FileExecutor.cs`、`CommandExecutor.cs`、`ToolDispatcher.cs`、`ResultFormatter.cs`、`TurnLedger.cs`
- `Plugins/ToolBridge/Models/`：`ToolCallDto.cs`、`ToolResultDto.cs`、`TurnDto.cs`
- `Plugins/ToolBridge/Controllers/ToolBridgeController.cs`（8 端点，类级 `ApiKeyPolicy`）
- `Plugins/ToolBridge/web/`：`package.json`、`pnpm-lock.yaml`、`pnpm-workspace.yaml`、`vite.config.ts`、`.gitignore`、`src/{index.ts,ToolBridgeView.vue,api.ts,http.ts,clipboard.ts,parseView.ts,types.ts}`（`dist/` 为构建产物，随 `.gitignore` 不入库）
- `ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/`：`PromptBuilderTests.cs`、`CallParserTests.cs`、`ToolBridgeGuardParityTests.cs`、`ExecutorTests.cs`、`ResultFormatterTests.cs`、`WorkspaceAndLedgerTests.cs`、`ToolBridgeAuthTests.cs`
- `ForgeSelf.Web/e2e/plugins/tool-bridge/tool-bridge.spec.ts`（5 条用例）
- `ForgeSelf.Web/screenshots/e2e/tool-bridge/alignment.png`
- `docs/02-features/039-tool-bridge.md`
- `docs/ai/pilot/2026-10-06-tool-bridge/{00..07}`

**修改（4 处生产/工程 + 4 处文档）**
- `ForgeSelf.Api/ForgeSelf.Api.csproj`：+1 行插件 `ProjectReference`（`ReferenceOutputAssembly="false"`）
- `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`：+1 行插件引用
- `.agents/skills/plugin-development/SKILL.md`：新增铁律 20（插件前端 lockfile）+ §六 五条新踩坑
- `.agents/skills/plugin-feasibility-study/SKILL.md`：§七 产物落点更正（`specs/` 已弃用）+ 新增 §九 三条实证经验
- `docs/07-decisions/not-taken-decisions.md`：新增 034~037
- `TODO.md`：新增 4 条待办（脚手架脚本路径、021 文档漂移、守卫共享 ADR、SettingsView flake）+ 1 条进行中
- `.forgeself/memory/2026-10-06.md`：输入 6/7 原文、调研结论、闸门1 批准记录、实施进度与探针读数

**未动（划界证据）**：`Plugins/AIAgent/**`、`ForgeSelf.Api/Plugins/**`（装载器）、`ForgeSelf.Api/Data/XCodeConfig.cs`、`ForgeSelf.Web/src/**`、宿主 `vite.config.ts` / `playwright.config.ts`、任何 `package.json`。

## Build

Command:

```bash
export TEMP=<repo>/.temp/tmp TMP=<repo>/.temp/tmp
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj          # 日志 .temp/tb-build3.log
```

Result: **PASS**（来源等级：Verified）

```text
    0 个错误
```

> 过程中两次真实编译错误（各一轮修好，非猜测）：
> ① `error CS0246: ParseResult/ToolResult/ParsedCall…` ×32 —— Services 未引 Models 命名空间 ⇒ csproj 补 `<Using Include="ForgeSelf.Api.Plugins.ToolBridge.Models" />`；
> ② `error CS0246: Win32Exception` ⇒ 补 `<Using Include="System.ComponentModel" />`。

**AC13 宿主产物判据（不能拿"插件目录自建通过"代替）**

```bash
ls -la ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/ToolBridge/ToolBridge.dll
md5sum ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/ToolBridge/ToolBridge.dll \
       Plugins/ToolBridge/bin/Debug/net10.0/ToolBridge.dll
```

Result：**PASS**（来源等级：Verified，21:09 实测）

```text
-rwxr-xr-x 98304 Oct  6 21:09 ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/ToolBridge/ToolBridge.dll
-rwxr-xr-x 98304 Oct  6 21:09 Plugins/ToolBridge/bin/Debug/net10.0/ToolBridge.dll
aafa0d08dd0d113935cd2045c7da048d  <宿主产物>
aafa0d08dd0d113935cd2045c7da048d  <插件目录>      # 同一串 ⇒ 插件确实进了宿主构建图
```

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~ToolBridge"
```

Result: **PASS**（来源等级：Verified）

```text
已通过! - 失败: 0，通过: 140，已跳过: 0，总计: 140，持续时间: 9 s - ForgeSelf.Api.Tests.dll (net10.0)
```

日志：`.temp/tb-test4.log`（还原四条反向探针之后的终态跑）。

**测试首跑抓到 5 个实现缺陷**（`.temp/tb-test2.log` 当时 8 红，逐条归因如下——都是实现错，不是用例错）：

| # | 缺陷 | 症状（真实读数） | 修法 |
| --- | --- | --- | --- |
| 1 | 行式引号误配 | `write_file path="broken content="x` 被解析出 `path = "broken content="x` 一条假调用 | 值里的引号必须成对且恰好包住整值，否则整行进 `unparsed` |
| 2 | 工作根自身回写成绝对路径 | `ToRelative(root)` 返回 `D:\…\workspace` ⇒ `list_dir` 的 `relativePath` 全泄漏本机路径（违反 FR-1.3 同一口径） | 等于根时返回空串 |
| 3 | 相对路径被静默接受 | `TrySetRoot("relative/dir")` 返回 true（`GetFullPath` 按宿主 CWD 补成绝对路径） | 在 `GetFullPath` **之前**判 `IsPathRooted` |
| 4 | plain 模式行尾污染 | `AppendLine()` 在 Windows 是 CRLF ⇒ AC9「stdout 逐字节相等」红（`---- stdout ----` 段找不到 / 多 `\r`） | 全部改显式 `\n` |
| 5 | 未闭合标签留残片 | 一条未闭合标签产出 **2 条** unparsed（行首散文被再记一次） | 整行消费 |

## 反向探针（四条，每条先实红再还原；`grep PROBE-` 终态 0 命中）

| 探针 | 改动 | 结果（Verified，日志原文） |
| --- | --- | --- |
| A 守卫漂移 | `CommandGuard.DefaultAllowlist` 删掉 `ssh` | **失败 6 / 通过 44 / 总计 50**（`.temp/tb-probe-a.log`）——`两份守卫逐条同判` 抓到原因原文差异（`仅放行 …git/ssh/pwsh` vs `…git/pwsh`） |
| B 解析"就近执行" | `ToolSpec.Normalize` 未命中时返回 `All[0].Name` | **失败 6 / 通过 16 / 总计 22**（`.temp/tb-probe-b2.log`），含 `未知工具名_归unknown_绝不就近执行_AC5`、`纯自然语言_零调用_带原因_BC1` |
| C 提示词手抄白名单 | 把 `ssh` 从提示词那句里删掉（改硬编码） | **失败 1 / 通过 8 / 总计 9**（`.temp/tb-probe-c.log`）——`提示词声明命令白名单与拒绝口径` 红；**顺带暴露真缺陷**：`ToolSpec.RunCommand` 的 Description 也手抄了一份 ⇒ 改为从 `CommandGuard.DefaultAllowlist` 派生 |
| D 短路守卫 | `CommandExecutor` 里把 guard 结果强制改成 Allow | **失败 2 / 通过 23 / 总计 25**（`.temp/tb-probe-d.log`）：`白名单外命令被拒_并且未启动任何子进程_AC7`、`内联破坏命令被拒_原因原文回给AI_AC7` |

> ⚠️ 探针 B 第一次**写成死代码**（放在 `return null` 之后），跑出来 22/22 绿——那是假探针，挪到生效位置才见红。
> 教训已回写技能：「反向探针的判定标准是改动前后测试结果变化，不是代码写上了」。

**AC7「被拒不启动子进程」的可观测证据**（不是注释推断）：`ExecutorTests.白名单外命令被拒_并且未启动任何子进程_AC7` 先在沙箱里写真探针文件，再发 `rm -f probe-must-survive.txt`，断言 ①`error=command_rejected` ②**探针文件仍存在且内容未变**；同用例内跑一条 `git --version` 作阳性对照（证明确实会起进程，"文件还在"不是假绿）。该用例在探针 D 下变红 ⇒ 判据有效。

## Integration Test

Result: **N/A（依据：本批未新增 WebApplicationFactory 级集成用例）**。
端点级鉴权由两层覆盖：① 反射守卫 `ToolBridgeAuthTests.程序集内每个控制器都带ApiKeyPolicy_AC12`（Verified，随 140 条绿）；② e2e 用真实 HTTP 断匿名请求 401（见下）。
仓内同层先例（`SemsControllerTests.cs:104-119`）存在，本批按 04-task 未强制要求处理，记为后续可加项。

## E2E

Command:

```bash
cd ForgeSelf.Web
NO_PROXY=localhost,127.0.0.1,::1 TEMP=<repo>/.temp/tmp \
  bash node_modules/.bin/playwright test --config=playwright.config.ts \
  e2e/plugins/tool-bridge --workers=1
```

Result: **PASS —— 6 passed（4.3m）**（来源等级：Verified，日志 `.temp/tb-e2e6.log`，`E2E_EXIT=0`）

```text
6 passed (4.3m)
```

六条用例（全部零 mock、真实 publish 宿主 + 真实前端 dev server）：
1. 页面装配：标题 / 版本徽标 / 工作根读数 / 初始指令含四工具名 / 未粘贴空态引导；
2. 完整一轮：识别 4 · 执行 3 · 被拒 1 · 未知 1 · 未解析 2，回粘文本含 `git version` 与写后读回的原文；
3. **只解析 → 单独执行不落档**（`POST execute` 的真实消费者；断 `turnsBefore == turnsAfter`）；
4. 工作根点即保存：相对路径拒 → 危险根未确认拒 → 确认后保存并回读一致 → 复原默认根；
5. 鉴权：匿名 `GET prompt|workspace|turns` 与 `POST turn` 全部 401；
6. 视觉：1280×720 无横向溢出 + 四张卡片 `scrollHeight ≤ 盒子高`（铁律 8 守卫）+ 截图产出。

**e2e 抓到的两个真问题（都不是"用例写错"就完事）**
1. **UI 竞态（真缺陷，已修）**：`gotoToolBridge` 等到 `[data-testid=tb-paste]` 可见即开始操作，但那是首屏空壳——四个请求还没回，`loadAll()` 随后把用户已填的工作根输入框**覆盖回默认值**（真实读数：`expect(inputValue).toBe('relative/should-be-rejected')` 收到 `"D:\…\publish\data\plugins\tool-bridge\workspace"`，`.temp/tb-e2e2.log`）。修法＝视图加 `workspaceTouched` 守卫（用户改过就不覆盖）+ e2e 等"数据真回来"（`code` 不再是 `加载中…`、版本徽标不含 `加载中`）。
2. **BC-11 与 e2e 宿主数据根同树的现实**：默认工作根位于 `…\OpenForgeSelf\.temp\e2e\wt-b26d4625\publish\data\plugins\tool-bridge\workspace` ⇒ 它本身就在 Git 树内，切到其兄弟目录会被判危险根。用例因此改成三步：相对路径拒（`绝对路径`）→ 危险根未确认拒（`危险工作根`）→ 勾选确认后保存并回读一致。

**一轮完整回合并行验证过的真实数据**（`.temp/tb-e2e2.log` 里 json 回粘原文，Verified）：
`write_file` 写 `e2e-run.md` 38 字节 → `read_file` 读回 `"# 工具桥 e2e 第二行\n追加一行"`（真写盘 + 真读回）；
`run_command git --version` → `exitCode:0`、`stdout:"git version 2.49.0.windows.1\n"`；
`run_command rm -f important.txt` → `ok:false`、`error:"command_rejected"`、reason 为守卫原文（含白名单清单）；
`delete_file` → 进 `unrecognized`（`suggestion:null`，绝不就近执行）；
两句散文 → 进 `unparsed`，分别带「未找到结构化调用字段」与「疑似工具名「write_file」但缺参数结构」。

## Static Analysis

```bash
cd ForgeSelf.Web && pnpm run check     # 日志 .temp/tb-web-check.log
cd ForgeSelf.Web && pnpm run test      # 日志 .temp/tb-web-test.log
cd Plugins/ToolBridge/web && pnpm install --frozen-lockfile && pnpm build   # CI 同参数复现
grep 'from "vue"' Plugins/ToolBridge/web/dist/index.js                      # 铁律 4 裸导入自检
```

Result：**PASS（宿主 check 0 error）** / **宿主 vitest 有 1 条非本批红** / 插件前端构建 PASS / 裸导入自检命中（来源等级：Verified）

```text
check:  ✖ 81 problems (0 errors, 81 warnings)      # 与基线同数（本批零宿主前端源码改动）
test:   Test Files 1 failed | 66 passed (67)
        Tests      1 failed | 755 passed (756)     # 红在 src/__tests__/SettingsView.test.ts「默认渲染通用设置面板」Error: Test timed out in 5000ms
plugin-web build: ✓ built in 560ms  → dist/index.js 22477 B + dist/style.css 6173 B（只有这两个产物）
bare import:      from "vue" 命中 1 处
```

**基线对表（§5.6：新增的红才是我的）**：`git status --porcelain ForgeSelf.Web/src` 输出为空 ⇒ 宿主前端源码零 diff，该红非本批引入；单跑该文件反而 **3 failed / 15 passed**（同文件两次读数 1↔3 摆动，mount 耗时 4~10s 压 5s 默认超时）⇒ 判为负载/计时型 flake，已记 TODO（含 stderr 里 `[ApiKeys] 主密钥信息加载失败 TypeError: Failed to parse URL from /api/api-server/status` 这条疑似真因）。

**发布链隐患（本批发现并已补）**：`scripts/release/build-frontend.ps1:39` 对每个 `Plugins/*/web` 跑 `pnpm install --frozen-lockfile`；新建插件的 web 起初**没有 lockfile** ⇒ CI frontend 段必红。已补 `pnpm-lock.yaml` + `pnpm-workspace.yaml`（`allowBuilds.esbuild: true` + `onlyBuiltDependencies: [esbuild]`），并按 CI 同参数本地复现通过（`Lockfile is up to date` + `esbuild postinstall Done` + `✓ built`）。

## 中档全量后端测试（§5.6：碰了 `ForgeSelf.Api.csproj` ⇒ 必跑）

Command:

```bash
cd ForgeSelf.Api.Tests && dotnet test            # 日志 .temp/tb-full-test.log
```

Result: **BLOCKED（未跑成，来源等级：Unknown）** —— 失败在**构建阶段**，不是测试红：

```text
error MSB3027: 无法将 "…\ForgeSelf.Api\bin\Debug\net10.0-windows\ForgeSelf.dll"
  复制到 "bin\Debug\net10.0-windows\ForgeSelf.dll"。超出了重试计数 10。失败。
  文件被 "testhost (34664)" 锁定。
error MSB3021: 同上（The process cannot access the file … being used by another process）
```

已尝试：① 与 e2e 串行跑（e2e 结束后才起）→ 仍锁；② 隔约 10 分钟后单独 `dotnet build ForgeSelf.Api.Tests` 复探 → 同样 4 条 MSB3026/3027；③ `tasklist` 读数：`testhost.exe 34664` 存活，另有 **8 个 `dotnet.exe`** 在跑 ⇒ 判定为**并行会话正在跑测试**（本 worktree 同机共享）。
**未采用的做法**：不杀他人 `testhost`/`dotnet` 进程（AGENTS.md §0 铁律 1 与并行会话避让）；也不改用例或降档蒙过去。

**为什么现在跑也不干净**：即便抢到锁，与另一会话的测试并发跑会产出**受污染的计时红**（同族先例：2026-10-06 输入4 的 A3b 全量里 8 条 `UpdateServiceTests.ApplyUpdateAsync_*` 隔离复跑 28/28 绿）。所以这条要么等机器空时跑，要么由用户授权处置那个 testhost 后再跑。

**本批已拿到的等价证据（不能替代全量，如实分级）**：
- Release 全图构建通过：e2e globalSetup 的 `dotnet publish ForgeSelf.Api -c Release` 成功并起住宿主（6 条 e2e 全绿 ⇒ 整个构建图 + 插件装配可用）；
- Debug 全图构建通过：`dotnet build ForgeSelf.Api` 0 错误（AC13 产物 md5 同串）；
- 本插件过滤集 140/140；`TerminalCommandGuard`（AIAgent 既有守卫套件）在探针期间多次随构建编译通过，未见新增红。

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/tool-bridge/alignment.png`（21:15:57 重生成，1280×720 fullPage；由「视觉」用例在第 6 轮跑出）

读图结论（Level 3 逐项核对，Verified——看图核对，不是推断）：
- **按钮宽度缺陷已修好**：三个动作按钮（`只解析（不执行）` / `只执行已解析（不落档）` / `解析并执行`）现在按内容宽排在一行右侧，主按钮不再拉满整行 ✓
- **统计行读数与用例断言一致**：截图里就是 `识别 4 · 执行 3 · 被拒 1 · 未知 1 · 未解析 2 · 耗时 343ms` ✓
- **识别列表**：`write_file` 条目带 `via openai` 标记 + 参数 JSON 展开 + 绿色左边框（成功态）✓
- **对齐/留白**：卡片标题与按钮同一基线；`支持的工具（4 个，与解析器同源）` 折叠行左对齐 ✓
- **溢出**：无横向滚动条（同用例断言）；长路径按 `break-all` 折行不撑破卡片 ✓
- **颜色**：全部走 `--el-*` 变量（成功绿边 / 主色按钮 / 次级灰字），未自造色值 ✓
- 前一轮（20:40 的旧图）读出的「保存工作根按钮占满整行」已在本图中确认消失

## Known Limitations

1. 命令白名单**不可配置**（与内置 agent 同现状，`TerminalCommandGuard.cs:25` 的"预留配置扩展位"至今无入口）；要测 `python`/`curl` 类场景会被拒。
2. 台账只追加、无删除端点、无容量裁剪（铁律 10）；磁盘占用随回合数增长。
3. 守卫是**两份实现 + 机器对账**（决策 D1），不是一份共享代码；对账表未覆盖的破坏性变体仍可能两侧同时漏。
4. 解析不覆盖"跨行的行式值"（`content=` 后换行写多行文本）——AI 必须走 JSON 或 `\n` 转义，提示词已明写。
5. 未向宿主 `ToolRegistry` 注册工具（决策 D3），内置 agent 用不到这四个能力。
6. 端点级 WebApplicationFactory 集成用例未加（靠反射守卫 + e2e 真 401 两层）。
7. 发布（打 tag / 本地更新源）与运行实例 :51888 只读复验**未做**——需用户提交与升级动作（U-6）。

## Unresolved Issues

| # | 事项 | 状态 |
| --- | --- | --- |
| 1 | 中档全量后端 `dotnet test` 终态读数 | **未跑成（Unknown）**：`ForgeSelf.Api.Tests` 输出 DLL 被并行会话的 `testhost (34664)` 锁住（MSB3027/3021），两次复探同果；未杀他人进程。等机器空时或由用户授权处置后补跑 |
| 2 | e2e 终态读数与 CSS 修复后读图 | ✅ 已完成：**6 passed（4.3m）**，截图 21:15:57 重生成并读图核对 |
| 3 | 宿主 vitest 的 `SettingsView.test.ts` 计时红 | **非本批**，已记 TODO（P2 测试确定化），本批不修 |
| 4 | 插件发布（打 tag / 本地更新源）与运行实例只读复验（闭环 ③⑤） | **未做**，等用户提交授权 + 升级动作；不以"完成"表述 |
| 5 | 规格 U-1：用户手上真实的 AI 回复文本样例 | Unknown；四档协议 + 金样已就位，拿到样例只加用例不改架构 |
| 6 | 新增的 `POST execute` 消费者（界面按钮）是否要再补一条后端单测 | 现由 e2e 第 3 条覆盖（真后端真执行 + 不落档断言）；单测层未加，属可选加固 |

## 补记（2026-10-06 23:5x）：U-1 到位后暴露的 Critical 缺口——本工件原结论被推翻一处

**触发**：用户把网页 AI 的真实回复粘进界面点「解析」，得到"这段里没认出工具调用"（输入14）。**上面第 5 行写的
"Unknown；四档协议 + 金样已就位，拿到样例只加用例不改架构"是错的**——真实样例带来的不是"加一条用例"，而是暴露
`CallParser` 的**结构性缺口**（见下）。本补记优先于上文相应行，原行保留以留痕。

| # | 项 | 读数与结论 |
| --- | --- | --- |
| 7 | 用逐字原文验后端 | `CallParserTests` 类过滤 **27/27 通过**（`.temp/tb-parser-test2.log`）⇒ 带围栏 + OpenAI 嵌套 + `path:""` 后端本来就支持，问题不在该形状 |
| 8 | 界面提示的成因定位 | 文案出自 `web/src/parseView.ts:67`（`no-call` 空态）⇒ 后端确实回 0 条 ⇒ 实际粘贴内容与原文不同 |
| 9 | **真因（Critical）** | `CallParser.cs:52-68`：**JSON 只在 ``` 围栏内试**（`ParseFence`），正文区只走标签式/key=value；而"从网页聊天复制代码块"常只带内容不带围栏 ⇒ **裸 JSON 整段判未解析**。这是最常见输入形态，主力流程因此跑不通 |
| 10 | Red（先让测试复现） | 新增 3 条（散文包裹裸 JSON 单条 / 裸 JSON 数组多条 / 裸花括号不是调用仍归未解析）→ `--filter CallParserTests` **失败 2**（正是前两条），`.temp/tb-red.log` |
| 11 | Green + 反向护栏 | 增 `ExtractBareJson`（字符串感知的括号配对，跳过串内括号/转义，配不上即当散文）与 `LooksLikeInvocation`（**宁可少认**：有工具名键且无 `description`/`aliases`/`parametersSchema`/`schema`/`inputSchema` 目录特征键，或 `tool_calls`/`calls`/`actions` 包装）。加第 4 条用例 `裸JSON工具目录_不当成调用_不猜原则`（防把初始指令整段凭空造出 4 条调用）→ **27/27** |
| 12 | 全量回归（本插件层） | `--filter ToolBridge` **失败 0 / 通过 145 / 总计 145**（`.temp/tb-full3.log`）。⚠️ 同一命令先前报 34 红、后报 7 红，TRX 正文＝`UnauthorizedAccessException: …AppData\Local\Temp\forgeself-toolbridge-tests\ExecutorTests_<guid> is denied` ⇒ **TEMP 必须在 pwsh 内部赋值**，Git-Bash `export TEMP` 不传进 testhost（AGENTS §5.0 同族），非代码红 |
| 13 | 交付形态改为「只发插件」 | 完整发布链第六轮断在 `dotnet publish` 段——**并行会话**的 `Plugins/McpCenter`（`DshMcpConfigWriter.cs` 引用 `DshMcpConfigDto` 已被删的 `EntryId/ServerName/Transport/Url/EntryExists`）编译不过；该文件非本批范围且对方在写，**未代改**。故走 027 号「插件本地包目录更新源」+ 侧载三件套：<br>· `publish-plugin.ps1 -PluginsRoot ~\.forgeself\plugins -Configuration Release -Force` → `tool-bridge/versions/1.0.2/`<br>· 顶层扁平活动件 + `current=1.0.2`（照现场 `FileTools` 形态；未碰只读版本快照、未删任何数据，原 `settings.json` 144B 保留）<br>· 单插件包 `updates/plugins/tool-bridge-1.0.2.forgeself-plugin`，SHA256 `492F926859DCE5FB92D49D2F0590347CCFECD6D6AB73175F9B4705E04F8B856A`<br>· 包内容验真：`web/dist/index.js` md5 `7B4D93781080` 与磁盘同串；**零宿主共享 DLL** |
| 14 | 版本 1.0.2 的理由 | 已装 `2610062027` 内为 1.0.0；中途外发的 1.0.1 包**不含**本修复 ⇒ 不同名换内容（那会造出"同一版本号两份产物"的第二份真相），直接抬 1.0.2，1.0.1 包移出更新源到 `.temp/retired-plugin-pkgs/`（不删） |

**仍未做（不得当完成看，替代原表第 4 行）**：宿主重启/插件切换由用户执行后，本会话才做 `:51888` 只读复验（闭环第⑤步）；
中档全量后端 `dotnet test` 因 McpCenter 半迁移编译不过仍未跑成（本批用 `-p:BuildProjectReferences=false` 绕行，绕行即未验证项）；
完整发布链（含签名段端到端）未复验。

