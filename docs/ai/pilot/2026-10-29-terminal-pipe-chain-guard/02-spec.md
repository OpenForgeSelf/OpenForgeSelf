# Specification

> 阶段：Stage 2｜Task ID：PILOT-075
> 依据：00-repository-understanding（真实文件行号）+ 01-intent。不确定点显式记入文末 Unknown。

## Functional Requirements

**FR-1 引号感知分段（SplitSegments，两份守卫同构新增）**

- 输入：原始命令串。输出：段列表（保持段内原文，不重写引号）。
- 切分符（仅在**引号外**生效）：`&&`、`||`、`|`、`;`、换行（`\n`、`\r\n`、`\r` 视为一个换行分隔）。
- 引号：`"` `'` 各自开关引号态（单引号串内 `''` 成对出现 = 转义，与开关式扫描天然兼容；
  反引号整体拒绝，故不存在反引号转义歧义）。
- **整串仍拒绝**（语义同今天，消息不变或新增）：
  - 引号外的 `<`、`>`、反引号、单独 `&`（不构成 `&&`）→ `Error: chained/piped commands are not allowed (contains '<char>')`；
  - 引号不配对（扫描结束仍在引号内）→ 新消息 `Error: unbalanced quotes in command`；
  - 切分出空段（含 `git status &&` 尾分隔、`;;`、`| |`）→ 新消息 `Error: chained/piped commands are not allowed (empty segment)`。
- 命令为空/全空白 → 沿用 `Error: command is empty`。

**FR-2 逐段同一校验（CheckCore 重构，判定与今天逐条相等）**

- 每段独立跑「单命令核心」：`SplitExecutable` → 可执行名必须裸名 → 白名单（dotnet/pnpm/node/git/ssh/pwsh）→
  破坏性红线（位置参数精确匹配 / `-e -c -command -encodedcommand /c /k` 内联词边界扫描 / EncodedCommand base64 载荷解码扫描）。
- 段级拒绝原因 = 今天的原文（E5/E6/E7 三族）；当段数 > 1 时统一加前缀：
  `Error: segment {i}/{n}: {原原因}`（i 从 1 起）；段数 = 1 时原因**逐字节不变**（既有测试零改动通过）。
- 两份实现的判定与原因原文**逐条相同**（对账金样表扩展覆盖新形态）。

**FR-3 执行姿势（双执行器同构）**

- 段数 = 1 → 维持今天的直接 `Process` 路径（FileName=首 token，Arguments=rest，UseShellExecute=false），**行为零变化**。
- 段数 > 1 → `pwsh` 链式路径：`FileName="pwsh"`，
  `ArgumentList = ["-NoProfile", "-NonInteractive", "-Command", <完整命令串>]`
  （ArgumentList 由 .NET 负责转义，不做手工拼引号）。cwd / env / timeout(1-30 clamp) / 原始字节捕获 /
  `ProcessOutputText.Decode` / `Truncate` / 超时语义（timedOut=true、进程后台继续）/ 退出码透传全部与今天一致；
  退出码 = pwsh 返回值（链式取最后一段）；非 0 退出码仍是正常回传。
- 禁止在 pwsh 路径设置 StandardOutput/ErrorEncoding（PC-011 lens：不固定码页）。

**FR-4 模型可见文案同步（描述与实现同源）**

- mcp-center `RunTerminalCommandTool.Description` + `ParametersJsonSchema`（command 字段描述）：
  改述为「支持 | && || ; 换行，每段独立过同一套白名单+破坏性红线；重定向（> <）、反引号、后台单 & 仍拒绝；
  多段命令经 pwsh 执行，退出码取最后一段」。
- tool-bridge `ToolSpec.RunCommand` 条目描述与 command 字段描述、`PromptBuilder.Build()` 命令约定行
  同步改述；白名单句仍由 `CommandGuard.DefaultAllowlist` 常量派生。

**FR-5 版本与文档**

- `Plugins/McpCenter/plugin.json`：2.8.0 → **2.9.0**，Description 追加 v2.9.0 能力注记；
- `Plugins/ToolBridge/plugin.json`：1.0.4 → **1.1.0**，Description 追加注记；
- `docs/02-features/021-ai-agent.md` run_terminal_command 行：管道/链式描述更新（该行同时残留 cwd 旧描述
  「CWD 限登记项目根内」，与今日已生效的 cwd 必填化不符，属同一行的事实修复，一并更正）；
- `docs/02-features/039-tool-bridge.md:48` 命令守卫描述更新。

## Input

| 参数 | 说明 | 变化 |
| --- | --- | --- |
| command | 完整命令串 | 语义扩展：可含 `|` `&&` `||` `;` 换行（引号外） |
| cwd | 绝对路径必填（今日已生效） | 不变 |
| timeoutSeconds | 1-30 | 不变；链式为整条命令的总墙钟 |
| env | 附加环境变量 | 不变（pwsh 路径同样生效） |

## Output

工具回包结构不变（mcp-center：`{success, command, cwd, exitCode, stdout, stderr, stdoutTruncated, stderrTruncated, durationMs}` /
超时形态 / 拒绝形态 `{success:false, error}`；tool-bridge ToolResult 字段不变）。`command` 字段回原始完整串。

## Business Rules

- **BR-1** 每段校验强度与单命令逐条相等：不放宽（白名单/破坏词/裸名/EncodedCommand 原样），也不加严。
- **BR-2** 拒绝路径零子进程：任何一段被拒 → 不启动任何进程。
- **BR-3** 整串级仍拒绝：`<` `>` 反引号、单独 `&`、空段、引号不配对、空命令。
- **BR-4** 引号内的 `|` `;` `&&` 不是分隔符（与 pwsh 解析一致）；引号态由 `"`/`'` 开关。
- **BR-5** 两份守卫逐条同判同因；金样表扩展后对账仍绿。
- **BR-6** 多段命令一律经 `pwsh -NoProfile -NonInteractive -Command` 执行；单段保持直接 Process。
- **BR-7** 超时按整条命令计；超时形态与今天一致（后台继续、timedOut=true、exitCode=-1）。
- **BR-8** 非 0 退出码不是错误（成功回传观察数据），链式取最后一段退出码。

## Boundary Conditions

- 尾/首分隔符（`git status &&`、`; git status`）→ 空段拒绝（E4）。
- 段内多空格/制表符按既有 Tokenize 语义处理；段按分隔符切出后 trim。
- `|&`：按 `|` 切分后下一段以 `&` 开头 → 撞单独 `&` 规则整串拒绝（E2）。
- `2>&1` 含 `>` → 整串拒绝（E2）——重定向不在本次放开范围（用户指令范围 = 管道与链式）。
- 超长命令/超多段：不做人工段数上限（输入体积本身受 DTO/传输限制）；破坏性扫描按段线性进行。

## Error Handling（拒绝消息清单，两实现逐字一致）

| 编号 | 触发 | 消息 |
| --- | --- | --- |
| E1 | 空命令 | `Error: command is empty`（不变） |
| E2 | 引号外 `<` `>` 反引号 单独 `&` | `Error: chained/piped commands are not allowed (contains '<char>')`（不变） |
| E3 | 引号不配对 | `Error: unbalanced quotes in command`（新） |
| E4 | 空段 | `Error: chained/piped commands are not allowed (empty segment)`（新） |
| E5 | 非裸名/解析不出 | 今天的原文（多段时加 segment 前缀） |
| E6 | 白名单未命中 | 今天的原文（多段时加 segment 前缀） |
| E7 | 破坏性红线命中 | 今天的原文（多段时加 segment 前缀） |

## Compatibility

- 单段命令：守卫判定、执行姿势、输出、日志全部逐字节不变 → 既有 4 个测试类（Guard/Cwd/Encoding/Executor 既有用例）零语义改动。
- 既有断言更新仅限「第 2 道门旧策略用例」（见 03-plan 偏差表——这些用例断言的是被本任务推翻的旧策略，更新属规格内动作）。
- 契约变化点 = 模型可见 Description/Schema/提示词文本 + 插件版本号；输出 schema 无变化。
- e2e `tool-gateway-031.spec.ts` 场景 4（git status 单段）不受影响。

## Non-functional Requirements

- 守卫保持纯逻辑零副作用、可单测（不启动子进程）；分段扫描 O(n) 单趟。
- 不新增 NuGet/npm 依赖；不改 csproj。
- 日志：拒绝日志沿用现有 `[McpCenter]/[tool-bridge] … 拒绝` 行；执行日志多段时 exe 记为 `pwsh(chain)`。

## Interaction Design（交互设计）

**本批次无用户可见 UI 改动**（不触任何 web/ 视图）。模型/外部 AI 的「界面」是工具描述文本，其变化由 FR-4 锁定，
并以既有同源探针测试（PromptBuilderTests 的白名单句派生断言 + schema 文本断言）作为验收锚。故交互规格表、
交互验收标准、走查符合性均不适用（N/A）；替代性人工验证 = 03-plan 的 dev-stack 测试台真实发命令走查（无 UI 断言）。

## Failure Modes（PC lens 预填）

| 命中的问题类 | 本任务如何满足 |
| --- | --- |
| PC-011（子进程输出固定码页解码） | pwsh 链式路径不设任何 Encoding；沿用原始字节捕获 + `ProcessOutputText.Decode` 裁决；链式真实执行测试断言输出无 U+FFFD |
| 守卫双实现漂移（PILOT-053 已知风险） | 金样表对账扩展新形态后全绿作为常驻判据；新增公共逻辑（分段器）在两份实现逐行同构 |
| 「描述与实现自相矛盾」（PromptBuilder 反向探针教训） | 白名单句继续由常量派生；新文案与守卫行为由测试互锁 |

## Acceptance Criteria

- **AC1** 放开且真实可执行：`git --version; git --version`（`;` 链）、`git --version && git --version`（`&&` 链）、
  `node -e "console.log(6*7)" | node -e "<stdin 读入并回显 ANSWER>"`（跨段管道，stdout 含 `ANSWER:42`）、
  含换行的两段命令——经 `run_terminal_command` 真实执行成功并断言输出（真实子进程测试）。
- **AC2** 逐段拦截·破坏性：`git status && rm -rf x` → 拒绝，原因含 `destructive` 与 `segment 2/2`，且零子进程。
- **AC3** 逐段拦截·白名单：`git log | select-string foo` → 拒绝，原因含 `allowlist` 与 `segment 2/2`。
- **AC4** 引号保护：`git commit -m "a && b"` 放行（引号内 `&&` 非分隔符）。
- **AC5** 仍拒绝：`git log > out.txt`、`cat a < b`、`` echo `id` ``、`git status & git log`、`git status &&`、
  `git commit -m "unterminated`——原因按 E2/E3/E4 原文。
- **AC6** 单段回归：既有 TerminalCommandGuardTests / RunTerminalCommandCwdRequiredTests / RunTerminalCommandEncodingTests /
  ExecutorTests 既有用例全绿（第 2 道门旧策略断言按本 Spec 更新为 AC1~AC5 语义）。
- **AC7** 对账：`ToolBridgeGuardParityTests` 金样表扩展（新形态正反例 ≥ 12 条）后两实现判定与原因逐条相同，全绿。
- **AC8** 文案同步：PromptBuilderTests / ToolSpec 描述断言更新后绿；两插件 plugin.json 版本号已升（2.9.0 / 1.1.0）。
- **AC9** 构建 + 过滤测试集绿（快档）：`dotnet build` 0 error；McpCenter + ToolBridge + 守卫相关过滤集全绿。
- **AC10** 冒烟 e2e：`e2e/plugins/mcp-center/` 增改 1 条用例，经万能工具转发 `run_terminal_command` 链式命令
  （不依赖 LLM），断言逐段校验与真实输出；实跑通过。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 目标机器 pwsh 7 缺失时链式命令的行为 | 链式命令报 `failed to start process 'pwsh'` | 保守假设可接受：白名单本就含 pwsh；错误路径已有明确文案（AC1 本机实证） |
| pwsh `-Command` 对特殊参数（`%`、caret 等）的再解析与直接 Process 的差异 | 仅影响多段命令（今天根本执行不了，无回归面） | 集成测试钉住常规形态；文档描述注明「多段经 pwsh 执行」 |
| `e2e/plugins/mcp-center` 现有用例形态 | AC10 落点 | 实现期首步读取，若已有冒烟结构则扩展一条，不铺全流程（铁律 24） |
