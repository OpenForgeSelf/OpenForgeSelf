# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门1 确认。
> Task ID：PILOT-053（`2026-10-06-tool-bridge`）｜级别：**全量**（新功能 + 插件任务 ⇒ 规范 §4 要求 00~06 七件齐备）

## Objective

在仓库新增插件 `Plugins/ToolBridge/`（运行时 id `tool-bridge`），使用户能在一屏内完成一轮**人工 agent 回合**：复制含四工具 schema 的初始指令 → 粘贴 AI 的回复文本 → 插件解析出全部工具调用（认不出的原样列出带原因）→ 在指定工作根内真实执行 `read_file`/`write_file`/`list_dir`/`run_command`（命令过守卫、路径防越界）→ 把结果拼成可一键复制的回粘文本 → 每轮落一条可回看记录；并完成插件五步维护闭环中当前可做的全部步骤。

## Scope

### Allowed

**新建（全部在 `Plugins/ToolBridge/` 内）**
- `plugin.json`、`ToolBridge.csproj`、`ToolBridgePlugin.cs`
- `Services/`：`ToolSpec.cs`、`PromptBuilder.cs`、`CallParser.cs`、`CommandGuard.cs`、`SandboxRoot.cs`、`FileExecutor.cs`、`CommandExecutor.cs`、`ToolDispatcher.cs`、`ResultFormatter.cs`、`TurnLedger.cs`
- `Models/`：`ToolCallDto.cs`、`ParseResultDto.cs`、`ToolResultDto.cs`、`TurnDto.cs`、`WorkspaceDto.cs`、`PromptDto.cs`
- `Controllers/ToolBridgeController.cs`
- `web/`：`package.json`、`vite.config.ts`、`tsconfig.json`、`src/{index.ts,ToolBridgeView.vue,api.ts,http.ts,clipboard.ts,parseView.ts}`、`dist/`（构建产物，入库口径跟既有插件一致）

**新建（测试与 e2e）**
- `ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/{PromptBuilderTests,CallParserTests,ToolBridgeGuardParityTests,ExecutorTests,ResultFormatterTests,WorkspaceAndLedgerTests,ToolBridgeAuthTests}.cs`
- `ForgeSelf.Web/e2e/plugins/tool-bridge/tool-bridge.spec.ts`
- `ForgeSelf.Web/screenshots/e2e/tool-bridge/*`（走查截图）

**修改（最小必要，逐条对应 03-plan）**
- `ForgeSelf.Api/ForgeSelf.Api.csproj`：插件 ItemGroup **+1 行** `ProjectReference`（`ReferenceOutputAssembly="false"`）——AC13 的唯一保障
- `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`：**+1 行** 插件引用——否则测试编译 CS0234
- `docs/02-features/039-tool-bridge.md`：**新建**功能档案
- `README.md`：功能模块清单同步（**只加本插件一行，不重构 README**）
- `docs/07-decisions/not-taken-decisions.md`：D3/D1 备选、P0 不做的删除端点
- `.agents/skills/plugin-development/SKILL.md`、`.agents/skills/plugin-feasibility-study/SKILL.md`：复盘回写（新坑：脚手架脚本路径过期；Core/Abstractions 无 NewLife 依赖 ⇒ 共享纯函数上移受阻）
- `.forgeself/memory/2026-10-06.md`、`TODO.md`：日记与队列
- `docs/ai/pilot/2026-10-06-tool-bridge/05,06,07`：Evidence / Review / Final Report

### Forbidden

- ⛔ **不修改数据库结构**：不新建 `Data/Model.xml`、不跑 `xcode`、不碰 `ForgeSelf.Api/Data/XCodeConfig.cs` 的 `PluginDbs`（D2）
- ⛔ **不改任何已有插件的业务代码**：尤其 `Plugins/AIAgent/`（守卫按 D1 复制而非搬移）、`Plugins/McpCenter/`（工作树另有他人未提交改动，避让）
- ⛔ **不放宽安全口径**：命令白名单/红线/`MaxOutputBytes`/`MaxTimeoutSeconds` 逐条沿用；不得为"测试方便"加 `python`/`curl`/`powershell`；不得关闭守卫；不得把路径校验写成 `TODO` 先放行
- ⛔ **不向宿主 `ToolRegistry` 注册工具扩展点**（D3）、不改 `AIAgentService.ToolScopePluginIds` 白名单、不动 `AIAgentToolScopeTests`
- ⛔ **不新增依赖**：不加 NuGet 包、不加 npm 包、不改 `ForgeSelf.Web/package.json`、不改 vite/playwright 配置
- ⛔ **不改 `.agents/skills/plugin-frontend-scaffold/scripts/scaffold-plugin-frontend.ps1`**（跨切面工具面，另立批次）
- ⛔ **不启停/杀任何宿主进程**（AGENTS.md §0 铁律1）；**不 commit / 不 push / 不打 tag**（等闸门2/3 授权）
- ⛔ **不做删除端点或台账清理自动化**（铁律 10；本插件也绝不删数据目录）
- ⛔ 不写 `temp/*.cjs` 一次性脚本作验证（§0 红线）；不顺手修基线红（`TerminalCommandGuardTests` 已知一条大小写断言红属既有问题，只归因不修）
- ⛔ 不改 `AGENTS.md` §2.4 之外的流程条款；若本批不新建技能则不动该表

## 原子子任务（闸门1 批准后按序执行，每步改完即可验证）

| # | 子任务 | 落位 | 完成判据 |
| --- | --- | --- | --- |
| T001 | 插件骨架 + **两处 csproj 登记** + 空控制器 | `Plugins/ToolBridge/*`、`ForgeSelf.Api.csproj` | `dotnet build ForgeSelf.Api` 0 error 且宿主产物 `Plugins/ToolBridge/ToolBridge.dll` 与插件目录 md5 相同（AC13 先立门） |
| T002 | `ToolSpec` + `PromptBuilder`（纯函数） | Services、`PromptBuilderTests` | AC1/AC2；反向探针（改名 ⇒ AC1 红）实红记录 |
| T003 | `CallParser` 四档 + 三段输出 | Services、`CallParserTests` | AC3/AC4/AC5 + BC-1/2/3/12；反向探针（包含匹配 ⇒ AC5 红） |
| T004 | `CommandGuard` + **跨实现对账** | Services、`ToolBridgeGuardParityTests` | 40+ 金样逐条同判；删白名单项 ⇒ 对账红 |
| T005 | `SandboxRoot`（含 settings.json + BC-11 危险根） | Services、`WorkspaceAndLedgerTests` | AC6 越界三形态、AC10 非法根 400 且原值不变 |
| T006 | `FileExecutor` + `CommandExecutor` + `ToolDispatcher` | Services、`ExecutorTests` | AC6/AC7/AC8 + BC-4~7；"被拒零子进程"有 PID 预检证据 |
| T007 | `ResultFormatter` + `TurnLedger` | Services、`ResultFormatterTests` | AC9/AC11（含重启后文件证据、损坏文件不炸列表） |
| T008 | `ToolBridgeController`（7 端点）+ 鉴权 | Controllers、`ToolBridgeAuthTests` | AC12；匿名 401 实测读数 |
| T009 | 插件前端（四区 + 版本徽标 + 三空态） | `web/` | `pnpm run build` 出 `index.js`+`style.css`；裸导入自检命中 |
| T010 | 门禁四组 | — | 插件过滤集全绿、中档全量 + 基线对表、宿主 `check`/`test` 无新增红 |
| T011 | 插件层 e2e + 截图读图 + 隔离实例走查 | `e2e/plugins/tool-bridge/` | AC14/AC15；截图路径与读图结论入 05 |
| T012 | 文档与回写 | 039 档案、not-taken、两技能、05/06/07（**README 功能表经实测判定不加行**，理由见 not-taken 036） | 工件门禁 `verify-pilot-artifacts -TaskId 2026-10-06-tool-bridge` **PASS** |
| T013 | （等授权）发布 + 运行实例只读复验 | — | 依赖用户提交/打 tag 授权与升级动作；未做如实标 Unknown（U-6） |

### T001~T013 终态（2026-10-06 收口时填，读数一律指回 `05-evidence.md`）

| 子任务 | 状态 | 一句证据 |
| --- | --- | --- |
| T001 | ✅ Verified | `dotnet build` 0 错误；宿主产物与插件目录 md5 同串 `aafa0d08dd0d113935cd2045c7da048d`（98304 B） |
| T002 | ✅ Verified | `PromptBuilderTests` 9 条；探针 C 实红 1 后还原 |
| T003 | ✅ Verified | `CallParserTests` 22 条；探针 B 实红 6 后还原（第一次写成不可达代码 ⇒ 假绿，已纠） |
| T004 | ✅ Verified | 41 条金样对账；探针 A（删 `ssh`）实红 6 后还原 |
| T005 | ✅ Verified | 越界三形态 + BC-11 危险根；首跑抓到"相对路径被静默接受"真缺陷并修 |
| T006 | ✅ Verified | `git --version` 真起进程；`rm` 被拒且探针文件存留（含阳性对照）；探针 D 实红 2 |
| T007 | ✅ Verified | json/plain 两模式 + 台账落盘/重开实例回读/损坏不炸列表 |
| T008 | ✅ Verified | 8 端点（03-plan 偏差表）；反射守卫 + e2e 匿名 401 |
| T009 | ✅ Verified | `pnpm install --frozen-lockfile && pnpm build` 按 CI 同参数通过；产物仅 `index.js`+`style.css`；裸导入命中 |
| T010 | 🔄 部分 | 过滤集 **140/140**；宿主 `check` 0 error/81 warning；宿主 `test` 1 条**非本批**红；**中档全量未跑成**（`testhost` 文件锁，Unknown，见 05） |
| T011 | ✅ Verified | e2e **6 passed（4.3m）**；迭代中抓到真 UI 竞态（已修）与 2 条我自己的用例期望错；截图 21:15:57 重生成并读图（按钮宽度缺陷确认消失） |
| T012 | ✅ Verified | 039 档案 / not-taken 034~037 / 两技能回写 / 05~07 已出；工件门禁 `verify-pilot-artifacts` → **PASS** |
| T013 | ⏸ 未做 | 需用户提交与升级动作（U-6），不得当完成看 |

## Acceptance Criteria

见 `02-spec.md` §Acceptance Criteria **AC1~AC16**（本任务不得裁剪其中任何一条；AC13 与 AC16 是历史上最常被漏的两条，单列为 T001 与 T010 的出口条件）。

补充三条流程性判据（AGENTS.md §0 出口清单）：
- [ ] AC17 五个维护闭环步骤逐条有结论：①门禁 ②插件层 e2e ③发布 ④隔离实例走查 ⑤运行实例只读复验——做了的给读数，没做的**显式标注未做与原因**，不得以"无关"带过。
- [ ] AC18 汇报写明档位（快/中/深）与覆盖范围；未跑项写 Unknown，不报"门禁绿"。
- [ ] AC19 日记逐回合原文 + TODO 待办清位；工件 05/06/07 随任务补齐。

## Expected Files

新建（24）：`Plugins/ToolBridge/` 下 `plugin.json`、`ToolBridge.csproj`、`ToolBridgePlugin.cs`、`Services/`(10)、`Models/`(6)、`Controllers/ToolBridgeController.cs`、`web/`(6 源文件 + 构建配置 3)；`ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/`(7)；`ForgeSelf.Web/e2e/plugins/tool-bridge/tool-bridge.spec.ts`；`docs/02-features/039-tool-bridge.md`；`docs/ai/pilot/2026-10-06-tool-bridge/{05,06,07}*.md`
修改（6~8）：`ForgeSelf.Api/ForgeSelf.Api.csproj`、`ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`、`README.md`、`docs/07-decisions/not-taken-decisions.md`、`.agents/skills/plugin-development/SKILL.md`、`.agents/skills/plugin-feasibility-study/SKILL.md`、`.forgeself/memory/2026-10-06.md`、`TODO.md`
**不动**：`Plugins/AIAgent/**`、`ForgeSelf.Api/Plugins/**`（装载器）、`ForgeSelf.Api/Data/XCodeConfig.cs`、`ForgeSelf.Web/src/**`、`vite.config.ts`(宿主)、`playwright.config.ts`、任何 `package.json`

## Verification Commands

```bash
# T001 起每步都要跑（环境前置见 AGENTS.md §5.0）
$env:TEMP=$env:TMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\tmp'
$env:NO_PROXY='localhost,127.0.0.1,::1'

cd ForgeSelf.Api && dotnet build                       # 判据看日志正文「0 个错误」，不拿 exit code 当证据
# AC13 宿主产物判据（两条都要）
ls ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/ToolBridge/ToolBridge.dll
certutil -hashfile ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/ToolBridge/ToolBridge.dll MD5
certutil -hashfile Plugins/ToolBridge/bin/Debug/net10.0/ToolBridge.dll MD5   # 必须相同

cd ForgeSelf.Api.Tests
dotnet test --filter "FullyQualifiedName~ToolBridge"           # T002-T008 逐档
dotnet test --filter "FullyQualifiedName~TerminalCommandGuard" # D1：既有守卫仍绿（一条基线红需如实归因）
dotnet test                                                    # 中档全量 + 基线对表

cd ForgeSelf.Web && pnpm run check && pnpm run test             # 宿主前端门禁
cd Plugins/ToolBridge/web && pnpm run build                     # 或 03-plan D6 出树兜底
grep 'from "vue-router"' Plugins/ToolBridge/web/dist/index.js   # 铁律4 裸导入自检，须命中

cd ForgeSelf.Web
bash node_modules/.bin/playwright test e2e/plugins/tool-bridge --workers=1   # AC14；地址取自 e2e-env.ts

# AC12 匿名 401 实测（同一 bash 调用内完成，不留驻进程）
curl -s -o /dev/null -w "%{http_code}" http://localhost:<port>/api/tool-bridge/prompt      # 期望 401

# T012 工件门禁
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\verify-pilot-artifacts.ps1 -TaskId 2026-10-06-tool-bridge
```

## 闸门状态

- **闸门1（规格确认）**：⏸ **待用户批准** —— 批准对象 = `01-intent` 的 Expected Outcome/Success Criteria、`02-spec` 的 AC1~AC16 与 Unknown 表、`03-plan` 的决策清单 D1~D6（其中 **D1、D3、D4 需用户点头或改选**）、本文件 Allowed/Forbidden。
- 闸门2（成果验收）：Evidence + Review 齐备后交付；通过前不提交代码。
- 闸门3（提交归档）：仅由用户授权后执行。
- ⚠️ 依据记忆条目「闸门 ✅ 要有出处」：用户"一次性配置好"的表述 = 授权推进，**不等于**逐项批准本规格；本工件需落字批准。
