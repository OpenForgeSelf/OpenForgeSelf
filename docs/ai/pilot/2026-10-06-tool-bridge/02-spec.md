# Specification

> 阶段：Stage 2｜必须从真实仓库内容与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：PILOT-053 ｜ 插件：`ToolBridge` / 运行时 id `tool-bridge`（命名依据见 03-plan §命名）
> 用户已拍板口径（2026-10-06 输入6 · AskUserQuestion 四问，全选推荐项）：① **多格式容错解析** ② **三件 + 列目录 + 生成初始提示词** ③ **照搬 TerminalCommandGuard 白名单+红线** ④ **插件专属沙箱目录**

## Functional Requirements

### FR-1 初始提示词生成（"出去"）

- FR-1.1 `GET /api/tool-bridge/prompt` 返回一段**纯文本**提示词，内容 = ①角色与任务说明 ②本工具面清单（四个工具名 + 每个工具的参数名/类型/必填 + JSON schema 原文）③要求 AI 的回复格式（把调用放进 json 代码围栏；一次可多条）④「结果会以 `[tool-bridge-result]` 段回给你，据此继续」的约定说明。
- FR-1.2 工具清单的**唯一真源**是插件内的 `ToolSpec` 常量表（名称/描述/schema/别名）；提示词、解析器、界面「支持的工具」三处**都由它派生**，不许出现第二份手写清单（依据记忆「不留第二份真相」）。
- FR-1.3 提示词文本里**不得**包含本机绝对路径、工作根位置、token、宿主端口（防把敏感信息带进外部聊天站点）。
- FR-1.4 界面提供「复制初始指令」按钮；复制失败回退为「已为你选中文本，请按 Ctrl+C」（照抄 `Plugins/DesignSystem/web/src/delivery/DeliveryMode.vue:238-266` 的已验证范式）。

### FR-2 工具调用解析（"识别"）

- FR-2.1 输入为**任意一段 AI 回复文本**（可含自然语言、代码围栏、多条调用）。解析器为**纯函数、零副作用**：不启动进程、不读写文件、不查库。
- FR-2.2 按优先级尝试四类格式，同一段文本可产出多条调用；每条调用记录它命中的格式档位（`via` 字段），用于回看时知道"AI 用的是哪种写法"：
  - **P1 围栏 JSON**：json 代码围栏内（或裸 JSON）的对象/数组。接受
    - 单条对象：键名可为 `tool` / `name` / `function` / `action` 之一（值=工具名），参数对象键名可为 `args` / `arguments` / `parameters` / `input` 之一；
    - OpenAI 风格：顶层 `tool_calls` 数组，每项含 `function.name` 与 `function.arguments`，其中 `arguments` **既接受对象也接受 JSON 字符串**（后者二次解析）；
    - 包装数组：顶层 `calls` / `actions` 数组，或顶层直接是数组。
  - **P2 标签式**：尖括号标签，标签名带 `name` 属性指明工具、子标签逐个给出参数（覆盖 Anthropic/Claude 与 OpenAI 两种社区常见文本协议形态）。实现须把标签名与参数名**结构化解析**，不用裸子串匹配。
  - **P3 行式 key=value**：一行一条，形如 `read_file path=a.txt`，支持值加引号、支持 `key=value` 任意顺序；工具名允许大小写与 `-`/`_` 差异。
  - **P4 箭头式**：形如 `> exec: git status` / `> write: a.txt` 的单动作行（当 P3 的分词歧义时按 P4 的固定映射表解释）。
  - **不实现**：从纯中文自然语言里"推断"意图（见 BC-1）。
- FR-2.3 工具名归一：先用受控**别名表**（`ToolSpec.Aliases`，如 `read` / `readfile` / `Read_File` → `read_file`）；别名表命中失败即判"未知工具"，**不做模糊/包含匹配**。
- FR-2.4 解析输出三段：`calls[]`（已识别，含 name/args/via/原文切片位置）、`unknown[]`（认出是调用但工具不在清单，带候选建议 = 清单里编辑距离最小者，仅提示不代跑）、`unparsed[]`（既非已识别也非可判定为调用，原样保留片段 + **具体原因**，如"JSON 缺工具名键"）。
- FR-2.5 `POST /api/tool-bridge/parse` 只解析不执行；`POST /api/tool-bridge/turn` 解析后立即执行（一次点击跑通一轮）。

### FR-3 四个工具的真实执行

- FR-3.1 `read_file(path)` → UTF-8 文本；FR-3.2 `write_file(path, content, append?)` → 建父目录、覆盖或追加；FR-3.3 `list_dir(path?)` → 目录优先、名字序，每项给 `name/isDir/size/relativePath`；FR-3.4 `run_command(command, cwd?, timeoutSeconds?)` → 真实起进程，回 `exitCode/stdout/stderr/durationMs`。
- FR-3.5 所有路径参数一律相对**工作根**解析，越界即拒（规则同 `Plugins/AIAgent/Services/ProjectWorkspaceService.cs:98-122`）。
- FR-3.6 `run_command` 执行前**必须**过命令守卫；被拒时**不得启动任何子进程**，且把守卫的拒绝原因原文回给 AI。白名单与红线词表以现有 `TerminalCommandGuard` 为准，**本任务不放宽任何一条**（落位方式见 03-plan 决策 D1，待用户拍板）。
- FR-3.7 进程实现沿用既有已验证范式（`Plugins/AIAgent/Services/ToolFunctions/RunTerminalCommandTool.cs:127-189`）：`UseShellExecute=false`、`CreateNoWindow=true`、stdout/stderr **显式 UTF-8**、**并发抽干双管道**后再等退出、超时 `exitCode=-1` 且**不 Kill** 进程。
- FR-3.8 非 0 退出码**不是错误**：`ok=true` 且照样回传 stdout/stderr（"失败输出同样是有效观察数据"，同 `:208` 注释语义）。只有"没能执行"才算 `ok=false`。

### FR-4 结果格式化（"原样返回给 AI"）

- FR-4.1 `ResultFormatter` 为纯函数，把一轮的执行结果拼成回粘文本，两种模式：
  - `json`（默认，AI 最好读）：单段 fenced JSON，形如 `{ tool_bridge_results: [ { tool, ok, args, result, error, truncated } ] }`；
  - `plain`：每条一个可读块，含工具名、成败、`exitCode`，以及 **stdout/stderr/文件内容原文**（不转义、不改行）。
- FR-4.2 两种模式都带稳定标记 `[tool-bridge-result]` 与结束标记，供 AI 定位；被截断的内容必须显式带 `truncated=true` 与"已截断，原长 N 字节"。
- FR-4.3 结果文本可一键复制；被拒/失败的调用**同样进结果文本**（否则 AI 不知道自己哪里错了，回合就断在人不发一言处）。

### FR-5 工作根（沙箱）管理

- FR-5.1 默认工作根 = `{数据根}/plugins/tool-bridge/workspace`（经 `ctx.EnsurePluginDataDirectory()` 派生，依据 `ForgeSelf.Abstractions/ContextExtensions.cs:23-28`；不放发布目录，升级会被覆盖）。
- FR-5.2 `GET /api/tool-bridge/workspace` → `{root, exists, defaultRoot, source}`；`PUT /api/tool-bridge/workspace {root}` 校验后落盘到插件数据目录的 `settings.json`，重启后仍生效。
- FR-5.3 校验规则：必须是**绝对路径**、不得是文件系统根（如盘符根）、若存在必须是目录；拒绝时返回具体原因并不落盘。
- FR-5.4 界面常驻显示当前工作根（截断+title 全文），并提供「打开工作根」说明文案；工作根切换属于改变用户可见状态 ⇒ 显示"当前工作根"而非静默换。

### FR-6 回合台账（可回看）

- FR-6.1 每次 `turn` 落一条记录：`{turnId, createdAt, promptText(粘贴原文), calls[], results[], resultText, stats:{识别/未知/未解析/执行成功/被拒}}`，写成插件数据目录下 `ledger/{turnId}.json` 一个文件。
- FR-6.2 `GET /api/tool-bridge/turns?take=20&skip=0` 倒序列出（默认 20，上限 200）；`GET /api/tool-bridge/turns/{turnId}` 取全文。
- FR-6.3 **只追加、不自动删、不做清理自动化**（plugin-development 铁律 10）。P0 也不提供删除端点（见 07 NOT-to-do）。

### FR-7 自带界面（`Plugins/ToolBridge/web/`）

- FR-7.1 单页四区：①初始指令（只读文本 + 复制）②粘贴区（多行输入 + 「解析」/「解析并执行」）③结果区（`json`/`plain` 切换 + 复制 + 每条折叠展开）④台账（列表 + 点开回看）。
- FR-7.2 根视图标题旁带**版本徽标**（铁律 13），数据从宿主 `GET /api/plugin` 解 `.data` 后按 id 过滤（响应是 `{code,data,message,success}` 包装，非裸数组）。
- FR-7.3 空态分级（§3.4 之 4）：未粘贴 → 「把 AI 的回复粘进来」；解析零命中 → 「这段里没认出工具调用，原因见下」+ 引导点「查看初始指令」；台账空 → 「还没有回合记录」。
- FR-7.4 交互状态一致：工作根改动**点即保存**并回读确认；执行中禁用按钮防重复提交并显示耗时；失败留区打印原因（§3.4 之 1/2/3）。
- FR-7.5 前端**只用原生 HTML + `--el-*` 变量 + Tailwind 布局原语**，不用 `<ElXxx>` 组件（依据 `Plugins/QuickLinks/web/vite.config.ts:13-16` 注释：预编译产物里不能依赖宿主编译期的 unplugin 自动引入 ⇒ 见决策 D5）。

### FR-8 鉴权与可发现

- FR-8.1 控制器类级 `[Authorize("ApiKeyPolicy")]`（铁律 17；策略声明实读 `ForgeSelf.Api/AppBuilder.cs:296-301`）。本插件的端点会在用户机器上写文件、起进程 ⇒ 属最高危的管理面。
- FR-8.2 无 token 访问任一端点必须 401（回归用例，写进单测或 e2e）。
- FR-8.3 工具清单可被自身发现：`GET /api/tool-bridge/prompt` 即清单载体；**P0 不向宿主 `ToolRegistry` 注册工具扩展点**（见决策 D3 与 `Unknown` U-4）。

## Input

| 输入 | 载体 | 约束 |
| --- | --- | --- |
| AI 回复文本 | `POST parse` / `POST turn` 请求体 `{ text }` | 非空；长度上限 200 KB（超出 ⇒ `ok=false, error="text_too_large"`） |
| 路径参数 | 各调用 `args.path`（可选 `args.cwd`） | 相对工作根；空 path 对 `list_dir` 视为根 |
| 写内容 | `args.content` | ≤ 1 MB；允许空串（= 建空文件）；保留原文（含中文/emoji/CRLF） |
| 命令 | `args.command` | 单行；`SplitExecutable` 后首个 token 必须命中白名单；超时 1..30s（默认 30） |
| 工作根 | `PUT workspace {root}` | 绝对路径，见 FR-5.3 |

## Output

统一宿主响应壳 `ApiResponse<T>`（`{code,data,message,success}`；对照 `Plugins/QuickLinks/Controllers/QuickLinksController.cs` 的返回形状）：

- `GET prompt` → `{ text, tools: [ { name, description, parametersSchema, aliases } ] }`
- `POST parse` → `{ calls[], unknown[], unparsed[], stats }`（无副作用）
- `POST execute` → `{ results:[ { tool, ok, args, result, error, reason, truncated, originalBytes, durationMs } ], resultTextJson, resultTextPlain, executed, rejected, durationMs }`（输入=已解析的 calls；**不落台账**，留档必须走 `turn`）
- `POST turn` → `{ turnId, calls[], results[], resultTextJson, resultTextPlain, stats }`
- `GET/PUT workspace` → `{ root, exists, defaultRoot, source }`
- `GET turns` → `{ items:[ { turnId, createdAt, stats, preview } ], total }`；`GET turns/{id}` → 全文记录
- 未识别/越界/被拒/超时 ⇒ **HTTP 仍 200**，业务失败在 `results[]` 里逐条表达（AI 场景需要"每条调用的观察"，不是整请求失败）。仅"请求本身不合法"（text 空/过大、JSON 坏、路径非法、401）走非 200。

## Business Rules

- BR-1 **不猜**：解析不出来的绝不合成调用；别名表之外的工具名一律未知。宁可让用户看到"没认出来 + 原因"。
- BR-2 **纯函数优先**：`PromptBuilder` / `CallParser` / `ResultFormatter` 零 IO 零静态状态 ⇒ 可完全单测；`SandboxRoot` / `CommandExecutor` / `FileExecutor` / `TurnLedger` 才碰文件系统与进程。
- BR-3 **守卫先于进程**：任何命令在被拒路径上不得 `Process.Start`（照 `TerminalCommandGuard.cs:12` 注释「拒绝路径不得启动任何子进程」）。
- BR-4 **本任务不放宽安全口径**：白名单/红线/上限值逐条沿用，若需扩展属另立批次。
- BR-5 **camelCase 契约**：DTO 用 PascalCase 属性 + 反序列化开 `PropertyNameCaseInsensitive`（`RunTerminalCommandTool.cs:23-32` 记录了不加就**静默丢参**的实测教训）。
- BR-6 **单写者**：工作根与设置读改写经插件数据目录内 `settings.json`，写文件用临时文件+改名（避免半截 JSON）。
- BR-7 **台账 turnId 唯一可判**：`{yyyyMMddHHmmss}-{8位随机}`；文件名只含安全字符（防路径穿越，参照 `ForgeSelf.Api/DataLocationService.cs:72` 的清洗思路）。

## Boundary Conditions

- BC-1 纯自然语言（无 JSON/无标签/无 key=value）⇒ 进 `unparsed[]`，原因写"未找到结构化调用字段"，**不做意图推断**。
- BC-2 一段里既有自然语言又有 JSON ⇒ JSON 正常识别，自然语言不进 `calls`；若整段一个调用都没认出且段内出现工具名裸词 ⇒ `unparsed` 原因里额外提示"疑似工具名 X 但缺参数结构"。
- BC-3 `arguments` 是被字符串化的 JSON（OpenAI 常见）⇒ 二次解析成功则采纳；解析失败 ⇒ 该条进 `unknown[]` 并回原始字符串片段（前 200 字符）。
- BC-4 读二进制文件 ⇒ 不猜编码，按 UTF-8 读并在检出大量替换字符/控制符时标 `binarySuspect=true` + 截断到 1 MB。
- BC-5 路径指向目录而工具要读文件 ⇒ `ok=false, error="is_a_directory"`；文件不存在 ⇒ `"not_found"`（不静默返回空串，AI 会把空串误读成"文件是空的"）。
- BC-6 `run_command` 里 `cmd`/`powershell` 形态的复合命令 ⇒ 守卫按既有规则拒（管道/分号/换行）。命令**不存在**（`Win32Exception`）⇒ `ok=false, error="executable_not_found: <exe>"`。
- BC-7 超时 ⇒ `exitCode=-1, timedOut=true`，不 Kill 进程（与既有工具语义一致，AI 需知道进程仍在跑）。
- BC-8 台账读取遇损坏 JSON 文件 ⇒ 跳过该条并在 `items` 里标 `corrupt=true`，**不抛不炸列表**。
- BC-9 并发多个 `turn` ⇒ 各自独立 turnId 与结果，工作根读取用当次快照值（避免执行中途被 PUT 改根导致"读在 A 根、写在 B 根"）。
- BC-10 界面窄屏（1280×720 起，Playwright 默认）⇒ 不得横向溢出、不得 flex 压扁裁字（铁律 8：滚动容器直接子区块 `flex-shrink:0`），e2e 带回归断言。

## Error Handling

| 场景 | 行为 | HTTP |
| --- | --- | --- |
| 无 token 访问 | 401 | 401 |
| `text` 空 / > 200 KB / 请求体非 JSON | `success=false` + 明确 message | 400 |
| 工具名不在清单 | `unknown[]` 项 + 候选提示 | 200 |
| 缺必填参数 | 该条 `ok=false, error="missing_argument: <key>"` | 200 |
| 路径越界 | 该条 `ok=false, error="outside_workspace"` + 带 root 与目标路径 | 200 |
| 命令被守卫拒 | 该条 `ok=false, error="command_rejected", reason=<守卫原文>`，零子进程 | 200 |
| 文件 not_found / is_a_directory | 该条 `ok=false` + 对应 error | 200 |
| 命令非 0 退出 | `ok=true` + `exitCode` + stdout/stderr 原文 | 200 |
| 工作根 PUT 非法 | `success=false` + 原因，不落盘 | 400 |
| 台账写盘失败 | 回合结果照常返回 + `ledgerError` 字段说明没记上 | 200 |
| 未捕获异常 | 不在 `Apply()` 内抛（插件注册会整体失败）；控制器层转成 `success=false` | 500 |

## Compatibility

- 后端 `net10.0` 插件；宿主 TFM `net10.0-windows` ⇒ 构建日志出 MSB3271 告警属既有形态（铁律 12b），**不为它改 TFM**。
- 插件只引用 `ForgeSelf.Core` + `ForgeSelf.Abstractions`（19/19 既有形态）。
- 前端产物 `web/dist/index.js` + `style.css`，入口导出名 = `plugin.json` 的 `views[0]` = `ToolBridgeView`。
- **无 DB 结构变更**（不引入实体）⇒ 不碰 `Data/Model.xml`、不碰 `XCodeConfig.PluginDbs`、不需要建表（铁律 12 的对象是"有实体的插件"）。
- e2e `menu-route-consistency.spec.ts` 为 manifest 驱动 ⇒ 新插件自动纳入，无需改（若 route 后续改名才需同步，铁律 19③）。
- `features.ts` / `dynamicPlugins.ts` / 打包脚本 **无需**为新插件改动（调研 Verified）。

## Non-functional Requirements

- NFR-1 文件类调用 p95 < 500 ms（1 MB 内）；`run_command` 上限由超时决定（≤30s）。
- NFR-2 输出截断阈值：stdout/stderr 各 50 KB（沿用 `MaxOutputBytes`）；读文件 1 MB；写内容 1 MB。
- NFR-3 零新依赖（不新增 NuGet 包、不新增 npm 依赖，不改构建配置）。
- NFR-4 台账目录单轮 < 400 KB；不做自动清理 ⇒ 用户可见提示「记录只增不删，占位于插件数据目录」。
- NFR-5 可观测：命令被拒/越界/超时经 `XTrace.Log.Warn` 带插件前缀落日志，便于现场定位。

## Acceptance Criteria

> 闸门1 用户确认的就是这份清单。每条都有对应的测试或实测读数，不依赖主观判断。

- [ ] AC1 `GET prompt` 文本逐字含四个工具名与各自参数名；且不含任何绝对路径 / token / 端口（守卫用例）。
- [ ] AC2 prompt 文本、解析别名、界面工具清单三处同源于 `ToolSpec`（守卫：改 `ToolSpec` 一处 ⇒ AC1 用例跟着变红，反向探针）。
- [ ] AC3 解析器四种格式各有 ≥2 条金样用例（含 OpenAI `arguments` 字符串形态）；解析零副作用由断言证明（无文件系统写入 + 无进程启动的可观测证据）。
- [ ] AC4 未结构化文本 ⇒ `unparsed[]` 带具体原因且 `calls` 为空；**不产生任何调用**（BC-1 用例）。
- [ ] AC5 别名表外工具名 ⇒ `unknown[]`，绝不"就近执行"（反向探针：塞入包含匹配 ⇒ 用例红）。
- [ ] AC6 四个工具各有成功 + 失败用例；越界路径三形态（`../`、绝对路径外指、盘符根）全部被拒。
- [ ] AC7 命令守卫：白名单外 exe 被拒 ⇒ 断言"未启动子进程"；`git --version` 成功 ⇒ 断 `exitCode=0` + stdout 原文；破坏性内联（如 `pwsh -Command Remove-Item x`）被拒（沿用既有测试口径）。
- [ ] AC8 非 0 退出码 ⇒ `ok=true` 且带 `exitCode`（BR/FR-3.8 用例）。
- [ ] AC9 `ResultFormatter` 两模式：`json` 可被 `JsonDocument.Parse` 解析；`plain` 的 stdout 段与原文逐字节相等；截断必带 `truncated=true` + 原长。
- [ ] AC10 工作根 PUT 非法 ⇒ 400 且 `GET` 回原值；合法 ⇒ 落盘且**重启插件后仍读到**（文件证据）。
- [ ] AC11 `turn` 产生 `ledger/{turnId}.json`；`GET turns` 倒序可读；损坏文件 ⇒ 标 corrupt 不炸列表（BC-8）。
- [ ] AC12 控制器全部带类级 `ApiKeyPolicy`（反射守卫用例，照 `DesignSystemAuthTests.cs:18-35`）；匿名 curl 任一写端点 ⇒ 401（实测）。
- [ ] AC13 宿主构建判据：`dotnet build ForgeSelf.Api` 后 `ForgeSelf.Api/bin/.../Plugins/ToolBridge/ToolBridge.dll` 存在且与插件目录 md5 相同（铁律 12b；**不接受"插件目录自建通过"**）。
- [ ] AC14 插件层 e2e（`e2e/plugins/tool-bridge/tool-bridge.spec.ts`，零 mock）跑通完整一轮：复制初始指令 → 粘贴含 write_file/read_file/`git --version` 的 AI 风格文本 → 执行 → 结果区含原文 → 台账新增一条；截图落 `ForgeSelf.Web/screenshots/e2e/tool-bridge/` 并按 Level 3 读图核对；含 1280×720 无横向溢出与区块不被裁字断言（BC-10）。
- [ ] AC15 界面：版本徽标显示插件版本；三种空态文案各有用例或走查证据；工作根"点即保存 + 回读一致"。
- [ ] AC16 门禁档位如实申报：碰 `ForgeSelf.Api.csproj` ⇒ 后端**中档全量** `dotnet test` 已跑，并按 §5.6 与基线对表（只报新增红）。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| U-1 用户手上真实的 AI 回复文本长什么样（四档协议覆盖后仍可能有第 5 种写法，如带自定义前缀的 DSL） | 解析器可能要加一档 | 已按"多格式容错 + 不猜"实现；界面保留原文与诊断可回看，拿到真实样例后只加金样用例，不改架构。**待用户提供样例** |
| U-2 命令白名单是否够用（用户测试场景若要跑 `python`/`curl` 会被拒） | 现场可能"插件没用" | 不擅自放宽（BR-4）。被拒原因原样回显即自解释；扩展属独立批次（守卫无可配置入口是既有事实 `TerminalCommandGuard.cs:25`） |
| U-3 台账是否需要"跨轮上下文/导出给 AI 的完整对话" | 影响是否要会话树 | P0 只做单轮记录（FR-6）；多轮串联记为后续项 |
| U-4 是否要把四个工具同时注册进宿主 `ToolRegistry`（让内置 agent 也能用） | 会与 `aiagent.read_file`/`write_file`/`list_files` **同名冲突**，且撑大本地小模型 prompt（`AIAgentService.cs:566` 注释实证） | P0 不做（决策 D3）；若要，须走 `architecture-design` 出 ADR 定命名与白名单 |
| U-5 决策 D1（命令守卫上移 `ForgeSelf.Core`）是否获批 | 影响是否碰 AIAgent | **本闸门待用户拍板**；未批则退化为"插件自带一份守卫实现 + 登记双真相风险"（见 03-plan 风险表） |
| U-6 运行实例只读复验（维护闭环第⑤步）依赖用户先把宿主更新到含本插件的版本 | 无法由我单方完成 | 如实标 Unknown，不伪造（AGENTS.md §10.1） |
