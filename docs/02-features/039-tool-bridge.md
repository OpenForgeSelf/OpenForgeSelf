# 039 · 工具桥（ToolBridge）— 测试 AI 工具调用能力的人工回合台

> 插件 id：`tool-bridge`｜目录：`Plugins/ToolBridge/`｜路由：`/tool-bridge`｜菜单：工具桥
> 版本：1.0.1（`plugin.json`）｜协议世代：`toolbridge-spec v1`
> 立项与规格：`docs/ai/pilot/2026-10-06-tool-bridge/`（PILOT-053，00~07 工件链）

## 这个插件解决什么

用户在**网页版 AI 聊天**里测试"模型会不会用工具"时，网页 AI 没有函数调用通道，也碰不到本机文件与命令。
工具桥承接其中"人肉搬运"的那一段：

1. 复制一段**初始指令**（内含本机支持的工具清单与参数 JSON Schema）发给网页 AI；
2. 把 AI 的回复**整段粘回**插件；
3. 插件解析出其中的工具调用，在**沙箱工作根**内真实执行；
4. 结果拼成一段可复制的文本，粘回给 AI 就是它能读懂的观察数据；
5. 每一轮（原文 + 调用 + 结果）留档，便于对比模型在多轮里的表现。

与既有插件的边界：`AgentHub` 是**委派本机已装的 agent 进程**；`McpCenter` 是 MCP **协议网关**（其 `tools/{id}/test` 端点实测为 `Thread.Sleep` 假实现）；`FileTools`/`ScriptRunner` 是给人用的批量运维与脚本面。
本插件的独特之处是**从 AI 的自由文本里解析调用**——这是全仓此前不存在的能力（调研 Verified）。

## 四个工具（唯一真源 = `Services/ToolSpec.cs`）

| 工具 | 参数 | 返回 |
|------|------|------|
| `read_file` | `path`（必填，相对工作根） | `{path, content, bytes, encoding}`，二进制可疑时标 `binarySuspect`，超 1 MB 截断并标 `truncated` |
| `write_file` | `path`、`content`（必填，可为空串）、`append?` | `{path, bytesWritten, totalBytes, append, created}`；父目录自动创建；UTF-8 **不带 BOM** |
| `list_dir` | `path?`（省略=根） | `{path, count, entries:[{name,isDir,size,relativePath}]}`，目录优先、名字序，500 条上限 |
| `run_command` | `command`（必填）、`cwd?`、`timeoutSeconds?` | `{command, cwd, exitCode, stdout, stderr, stdoutTruncated, stderrTruncated}`；**非 0 退出码属正常回传** |

工具名/说明/Schema/别名只写在这一处：初始提示词、解析别名、界面清单三处全部由它派生。
（反向探针实测：手抄第二份白名单到提示词里，`PromptBuilderTests` 即红。）

## 能认出哪四种写法

解析按**逐段扫描、每段独立定档**处理，同一段文本里混排也能全收：

| 档 | 形态 | 说明 |
|----|------|------|
| P1 `json` / `openai` | json 代码围栏里的对象或数组，**以及裸发（无 ``` 围栏、前后带散文）**——从网页聊天复制代码块常常只带内容不带围栏，两种都必须认；散文里"像结构却不是调用"的内容（如本插件工具目录那种带 `description`/`parametersSchema` 的对象）一律不猜成调用 | 键名宽容：工具名 `tool`/`name`/`function`/`action`；参数 `args`/`arguments`/`parameters`/`input`；`tool_calls`/`calls`/`actions` 包装；`arguments` 允许是被字符串化的 JSON |
| P2 `tag` | 尖括号标签式（`tool`/`invoke` 带 `name` 属性，子标签逐个给参数） | Anthropic/Claude 与社区文本协议两种形态 |
| P3 `kv` | 一行一条 `read_file path=a.txt content="含 空格"` | 引号必须成对且恰好包住整个值，否则判"不可解析"而不是猜 |
| P4 `arrow` | `> exec: git status` 这类单动作行 | 裸值绑到该工具的**第一个必填参数**（固定映射，不是推断） |

**不猜原则**：认不出的一律进 `unparsed` 并带具体原因（"未找到结构化调用字段"/"疑似工具名 X 但缺参数结构"/"标签未闭合"…）；清单外的工具名进 `unknown`（附编辑距离 ≤3 的候选提示），**绝不就近替 AI 挑一个执行**。散文里出现的工具名不会被解析成调用。

## 安全边界（设计如此，不是缺陷）

- **命令守卫**：与内置 agent 同规则——换行/管道/分号/重定向即拒；可执行名必须是裸名且命中白名单
  `dotnet / pnpm / node / git / ssh / pwsh`；**58 条**破坏性词红线（逐条数自 `CommandGuard.DestructiveTokens`，与
  `TerminalCommandGuard` 同值）；`-EncodedCommand` 载荷 base64→UTF-16LE 解码后扫描，不可验证即拒。
  **被拒路径不启动任何子进程**，并把守卫拒绝原因原文回给 AI。
  两份实现（`Plugins/AIAgent/Services/TerminalCommandGuard.cs` 与本插件 `Services/CommandGuard.cs`）由常驻判据
  `ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/ToolBridgeGuardParityTests.cs` 用同一张 40+ 命令金样表逐条对账钉死漂移。
  为什么不做成一份：`ForgeSelf.Core` / `ForgeSelf.Abstractions` 均未引用 NewLife.Core，上移即需给内核层加依赖（依赖结构变更，须另立批次出 ADR）。
- **沙箱工作根**：默认 `{数据根}/plugins/tool-bridge/workspace`，可改成任意**绝对路径**（相对路径一律拒——`Path.GetFullPath` 会把它悄悄补成当前目录下的路径）。
  越界（含 `..`、绝对路径）拒绝；工作根若落在盘符根 / 用户目录根 / **Git 仓库树内**，默认拒绝，需界面显式勾选"确认使用危险根"。
- **管理面鉴权**：控制器类级 `[Authorize("ApiKeyPolicy")]`（宿主无全局鉴权中间件）。端点能在本机写文件、起进程，匿名访问必须 401。
- **不外泄本机信息**：初始指令与回粘文本里不出现绝对路径、token、端口（路径一律回写成相对工作根的形式）。
- **数据只增不删**：台账 `ledger/{turnId}.json` 只追加，插件没有任何删除/清理自动化。

## 端点（前缀 `api/tool-bridge`，响应壳 `{code,message,success,data}`）

| 方法与路径 | 用途 |
|-----------|------|
| `GET prompt` | 初始指令文本 + 机器可读工具清单 |
| `POST parse {text}` | **只解析、零副作用**，返回 `calls/unknown/unparsed/stats` |
| `POST execute {calls}` | 执行已解析出的调用（界面「只执行已解析（不落档）」按钮）；同样返回两种回粘文本，但**不写台账** |
| `POST turn {text}` | 一轮完整回合：自己再解析一次 → 执行 → 回粘文本 → 落台账 |
| `GET workspace` / `PUT workspace {root,confirmUnsafe}` | 工作根读数 / 点即保存（落 `settings.json`，重启后仍生效） |
| `GET turns?take&skip` / `GET turns/{turnId}` | 台账倒序列表（默认 20、上限 200）/ 单轮全文 |

业务层失败（未识别 / 越界 / 被拒 / 超时 / 缺参）**HTTP 仍 200**，在 `results[]` 逐条表达；只有请求本身不合法（text 空或 >200KB、路径非法、401）才 400/401。

## 回粘文本

`[tool-bridge-result] toolbridge-spec v1 mode=json|plain` … `[/tool-bridge-result]`，两种模式：
`json`（标记之间单行 JSON，含 `tool_bridge_results` / `unrecognized` / `unparsed`）、
`plain`（每条一个可读块，`content`/`stdout` 原文单独成段、逐字节不改、行尾统一 `\n`）。
被拒与未解析条目也在里面——否则 AI 收到空白会原地重复同一条调用。

## 怎么用（一轮）

1. 打开「工具桥」，先看工作根是不是你想落的地方（不是就改，点即保存并回读）；
2. 点「复制初始指令」→ 粘给网页 AI；
3. AI 回复后整段粘回本页 → 先点「只解析」看清识别/未知/未解析三段；
4. 点「解析并执行」→ 结果区切 `json`/`plain` → 「复制结果」→ 粘回 AI；
5. 第 4 区「回合记录」点任一条可回看原文与结果（不会自动重跑）。

## 已知限制

- 命令白名单不可配置（与内置 agent 同现状）；要测 `python`/`curl` 类场景会被拒，扩展属独立批次。
- 台账只追加、无删除端点，磁盘占用随回合缓慢增长（单轮上限 200 KB 量级）。
- 不从散文推断意图：模型若只用自然语言描述"我要写个文件"而不给结构，本轮就是"未解析"。
- 不与内置 AIAgent 共享工具注册表（决策 D3：避免 `read_file` 等同名并存并撑大本地小模型 prompt）。
- 跨轮上下文/会话树、AI API 直连：均未做（`02-spec` U-3 / 见 `docs/07-decisions/not-taken-decisions.md`）。
