# 02 · Spec — 工具命名空间统一与归属重估（v2，2026-10-28 用户拍板修订）

> **v2 修订（用户拍板 2026-10-28 00:33）**：① 豁免清单扩大（run_terminal_command、文件三件套加入）；② 公共工具迁 **McpCenter**（非 FileTools）；③ **不做存量兼容**（删别名层与数据迁移，直接换名）。

## 1. Functional Requirements

### FR-1 前缀命名规则
- **FR-1.1** `plugin.json` 新增可选字段 `"toolPrefix": "<snake>"`；**有工具注册的插件必填**。
- **FR-1.2** 工具正式名 = `<toolPrefix>_<语义名>`。**保留名豁免清单（不加前缀，用户拍板 2026-10-28）**：

| 保留名 | 归属插件 | 性质 |
|--------|---------|------|
| `universal_tool` | mcp-center（迁入） | 万能网关 |
| `search_tools` | mcp-center（迁入） | 工具检索 |
| `list_tools` | mcp-center | 工具枚举 |
| `run_terminal_command` | mcp-center（迁入） | 命令执行 |
| `read_file` / `write_file` / `list_files` | mcp-center（迁入） | 文件三件套（裸名豁免） |
| `finish` | ai-agent | 循环控制（上轮白名单成员，维持） |
| `submit_plan` / `complete_step` / `request_help` | ai-agent | 计划流协议（request_help 用户明确豁免；submit_plan/complete_step 同为协议三件套，维持豁免保持一致性） |

外部 MCP 工具名空间 `mcp.<服务器id>.<工具名>`（决策 D10/003）不变。
- **FR-1.3** 注册守卫：宿主 `ExtensionPointManager` 注册时校验——`Name` 必须以「本插件 toolPrefix + `_`」开头或 ∈ 保留名清单，否则**拒绝注册** + Error 日志（含插件 id、工具名、期望前缀）。重名注册失败 + Error（沿用 TryAdd 行为，守卫测试保证零重名）。
- **FR-1.4** 前缀声明表（与 FR-2 映射表同源）：

| 插件 | toolPrefix | 改动面 |
|------|-----------|--------|
| ai-agent | `aiagent` | plan_workflow→`aiagent_plan_workflow`；universal_tool/search_tools/run_terminal_command 迁出；文件三件套迁出；删重复 3 件；协议/finish 豁免 |
| workflow-engine | `workflow` | 5 个改名 |
| memory-system | `memory` | 5 个改名 |
| design-system | `design` | ✅ 8 个已合规零改 |
| dev-tools | `dev` | 26 个改名 |
| text-tools | `text` | 5 个改名 |
| file-tools | `file` | 5 个已合规保留；`folder_stats`→`file_dir_stats` |
| system-monitor | `sys` | 5 个改名 |
| script-runner | `script` | 11 个改名 |
| quick-links | `link` | 4 个改名 |
| scheduler | `schedule` | 3 个改名 |
| todo-tracker | `todo` | 8 个改名 |
| sems | `sems` | ✅ 22 个已合规零改 |
| mcp-center | `mcp` | 迁入 8 个（6 裸名豁免 + 2 加前缀）+ 原 list_tools |
| agent-hub | `hub` | 5 个改名 |
| sample | `sample` | 1 个改名 |

### FR-2 改名映射表（全量、唯一真源；正式名 ← 旧名）

**ai-agent**：`aiagent_plan_workflow`←plan_workflow。**迁出**（→mcp-center，见 FR-3）：universal_tool、search_tools、run_terminal_command（裸名保留）、list_files/read_file/write_file（裸名保留）、get_current_time/`mcp_get_current_time`、calculate/`mcp_calculate`。**删除**（归属 WorkflowEngine，不迁移）：execute_workflow、get_workflow_status、list_workflows。豁免不变：finish、submit_plan、complete_step、request_help。
**mcp-center 迁入**：`universal_tool`、`search_tools`、`run_terminal_command`、`list_files`、`read_file`、`write_file`、`get_current_time`（原名）、`calculate`（原名）（+原 list_tools）。

> ⚠️ **修订（2026-10-28 17:29 用户拍板）**：原文两处 `mcp_get_current_time`/`mcp_calculate`（FR-3.2 初版按规则加前缀）已推翻——**mcp-center 整插件豁免前缀**（「mcp中心的工具不用前缀，是特殊例外」），两工具用原名，见 ADR-004 修订节。上文保留原表述仅为对账可追溯。
**workflow-engine**：`workflow_create`←create_workflow；`workflow_get`←get_workflow；`workflow_execute`←execute_workflow；`workflow_get_status`←get_workflow_status；`workflow_list`←list_workflows。
**memory-system**：`memory_relevant`←get_relevant_memories；`memory_add`←add_memory；`memory_search`←search_memories；`memory_update`←update_memory；`memory_stats`←get_memory_stats。
**dev-tools**：`dev_format_json`←format_json；`dev_validate_json`←validate_json；`dev_convert_json_yaml`←convert_json_yaml；`dev_jsonpath_query`←jsonpath_query；`dev_format_xml`←format_xml；`dev_base64_encode`←base64_encode；`dev_base64_decode`←base64_decode；`dev_url_encode`←url_encode；`dev_url_decode`←url_decode；`dev_hash`←compute_hash；`dev_hmac`←compute_hmac；`dev_aes_encrypt`←aes_encrypt；`dev_aes_decrypt`←aes_decrypt；`dev_regex_test`←test_regex；`dev_regex_generate`←generate_regex；`dev_regex_replace`←regex_replace；`dev_timestamp`←convert_timestamp；`dev_color_convert`←convert_color；`dev_color_palette`←generate_color_palette；`dev_color_contrast`←check_color_contrast；`dev_jwt_decode`←decode_jwt；`dev_jwt_validate`←validate_jwt；`dev_jwt_generate`←generate_jwt；`dev_uuid`←generate_uuid；`dev_snowflake_id`←generate_snowflake_id；`dev_qrcode`←generate_qrcode。
**text-tools**：`text_format_json`←format_json；`text_base64_encode`←encode_base64；`text_base64_decode`←decode_base64；`text_hash`←compute_hash；`text_stats` 原名保留（合规特例，记入表）。
**file-tools**：`file_rename`/`file_cleanup`/`file_compress`/`file_stats`/`file_extract` 原名保留（合规特例）；`file_dir_stats`←folder_stats。
**system-monitor**：`sys_cpu`←get_cpu_usage；`sys_memory`←get_memory_usage；`sys_disk`←get_disk_info；`sys_processes`←get_process_list；`sys_kill_process`←kill_process。
**script-runner**：`script_run`←run_script；`script_list`←list_scripts；`script_exec_code`←execute_code；`script_generate`←generate_script；`script_analyze_error`←analyze_script_error；`script_suggest_fix`←suggest_script_fix；`script_templates`←list_script_templates；`script_snippet_list`←list_code_snippets；`script_snippet_create`←create_code_snippet；`script_personal_stats`←get_personal_stats；`script_recommend`←get_recommendations。
**quick-links**：`link_search`←search_links；`link_open`←open_link；`link_categories`←get_categories；`link_create`←create_link。
**scheduler**：`schedule_create`←create_scheduled_task；`schedule_list`←list_scheduled_tasks；`schedule_pause`←pause_scheduled_task。
**todo-tracker**：`todo_create`←create_todo；`todo_list`←list_todos；`todo_complete`←complete_todo；`todo_task_get`←get_agent_task；`todo_task_claim`←claim_agent_task；`todo_execution_append`←append_task_execution；`todo_stage_update`←update_task_stage；`todo_dispatch`←dispatch_task。
**agent-hub**：`hub_list_agents`←list_agents；`hub_delegate_task`←delegate_task；`hub_task_result`←get_task_result；`hub_cancel_task`←cancel_task；`hub_ask_agent`←ask_agent。
**sample**：`sample_greet`←示例问候。
**design-system / sems**：零改名。

### FR-3 归属迁移（用户拍板：公共工具迁 MCP 中心）
- **FR-3.1** 归属原则：与插件主功能无直接关系的**平台公共工具**迁 McpCenter；能力工具归能力插件；编排插件（AIAgent）只留 AI 编排专属与协议工具。
- **FR-3.2** 迁 AIAgent → McpCenter（8 个）：`UniversalTool`（universal_tool）、`SearchToolsToolFunction`+`ToolSearchScorer`（search_tools）、`RunTerminalCommandTool`+`TerminalCommandGuard`（run_terminal_command）、`ListFilesToolFunction`/`ReadFileToolFunction`/`WriteFileToolFunction`+`FileToolLineOps`（文件三件套裸名）、`GetCurrentTimeToolFunction`（→`mcp_get_current_time`）、`CalculateToolFunction`（→`mcp_calculate`）。McpCenter `ToolExtensions` 属性注册（既有模式，McpCenterPlugin.cs:30/47）。
- **FR-3.3** 契约上移（架构铁律 2）：`IProjectWorkspaceService`（现定义于 AIAgent `ProjectWorkspaceService.cs:19`，已 `ctx.Register` 共享 AIAgentPlugin.cs:33）**接口定义上移 `ForgeSelf.Abstractions`**，实现留在 AIAgent；McpCenter 消费经 `ctx.Get<IProjectWorkspaceService>()`（run_terminal_command CWD 门、list_files 项目根回退、resolve 行为不变）。
- **FR-3.4** 删除：AIAgent 的 ExecuteWorkflow/GetWorkflowStatus/ListWorkflowsToolFunction 三类（AIAgentPlugin.cs:470-855 对应段）；内置「程序员」Agent 工具清单改 workflow_* 新名。
- **FR-3.5** AIAgent 保留：`aiagent_plan_workflow`、finish、submit_plan、complete_step、request_help。

### FR-4 注册守卫测试（防复发）
- **FR-4.1** `ForgeSelf.Api.Tests/Architecture/ToolNamespaceGuardTests.cs` 四断言：① 有工具的插件必声明 toolPrefix 且格式 `^[a-z][a-z0-9_]*$`；② 全部 IToolFunctionExtension 的 Name 前缀合规或 ∈ 保留名；③ 测试内嵌映射表与实际注册名集合一致（防漂移）；④ 模拟全注册零重名。
- **FR-4.2** ToolRegistry 单测：重名第二个注册失败 + Error 日志（行为锚定）。

### FR-5 文件工具参数化（迁 McpCenter 时同步）
- **FR-5.1** `list_files`：`path` 改**绝对路径**（可选，省略=回退当前项目根；未选项目且无 path → 报错引导传绝对路径）。
- **FR-5.2** `read_file`：`path` 绝对路径**必填**（不再隐式接项目根）；start_line/end_line 语义不变。
- **FR-5.3** `write_file`：`path` 绝对路径**必填**；行区间替换/先读后写/父目录自动创建语义不变。
- **FR-5.4** 安全边界：读/写任意绝对路径（与 FileTools file_* 家族同治理，FileToolsController.cs:11 先例）；保留 UTF-8 文本约束。不新增根白名单（not-taken）。
- **FR-5.5** 响应封套：保留 `success/root/usedRoot` 字段；`usedRoot`=规范化绝对路径；错误文案不再要求「先选择项目目录」（read/write path 必填后前提消失）。
- **FR-5.6** run_terminal_command 三道门不变（白名单/链式拒绝/CWD 限项目根，CWD 经 IProjectWorkspaceService）。

### FR-6 兼容性（用户拍板：不考虑存量引用）
- **FR-6.1** **无别名层、无数据迁移、无过渡期**。旧名直接消失；外部 MCP 客户端、存量 Agent 配置（ConfigJson.Tools）、历史 PlanJson/TrustedScopes 中的旧名引用**不迁移不兜底**（用户明示）。
- **FR-6.2** 源码内字面量全量换新名：AIAgentService 记忆注入（`get_relevant_memories`→`memory_relevant`，:1263/:1287）、ReactLoopAgent（:721）、ResolveToolDefinitions 排除清单（:611，submit_plan/complete_step/request_help 豁免不变）、BuiltInAgentDefinitions（:170）、BuiltInAgentExecutionService（:212 `write_file`→`read_file`？不——该判定改 `write_file` 保留？`write_file` 是裸名豁免不变，**无需改**）、前端 composerContext 三张表、McpCenter/AIAgent 工具描述举例。
- **FR-6.3** 前端 DANGER_RE 不动（词根匹配，`write_file`/`run_terminal_command`/`sys_kill_process` 仍命中）。

### FR-7 描述与文档同步
- **FR-7.1** 描述文案举例同步：AIAgent UniversalTool/SearchTools 迁移后描述、McpCenter UniversalToolForwarder/ListToolsToolFunction/McpUniversalTool 描述（read_file 等举例按新名/新归属校对）。
- **FR-7.2** 文档：`021-ai-agent.md` 工具清单节重写（新归属+新名）、`034-mcp-center.md`（公共工具迁入）、根目录 `fs工具集HTTP-MCP调用文档-1.9.2.md`、`design-system-consume` 技能、ADR `docs/07-decisions/004-tool-namespace.md`（新）、not-taken 台账增补。
- **FR-7.3** DesignSystem `DesignBriefBuilder.cs:401` 指引文案核对（universal_tool/list_tools 裸名不变，预计零改，逐条核过）。

## 2. Input / Output
- 文件三工具新入参：`path` 绝对路径（list_files 可选）；其余工具参数不变。

## 3. Business Rules
- 保留名仅限 FR-1.2 清单 11 个；新工具申请保留名 = 守卫红。
- 迁移表（FR-2）唯一改名真源：契约常量 + 守卫测试 + 文档三处同源。

## 4. Boundary Conditions
- 未声明 toolPrefix 注册工具 → 拒绝注册 + Error（不阻断宿主启动，其他插件不受影响）。
- file_read 对不存在路径 → 既有错误封套；file_write 自动创建父目录。
- list_files 省略 path 且未选项目 → 错误文案含「传绝对路径」引导。

## 5. Error Handling
- 注册期错误：Error 日志（插件 id + 工具名 + 期望前缀），不抛断宿主。
- 模型调旧名：走既有 unknown 提示（含注册数量 + search_tools 引导）——不做别名，属预期行为（用户拍板）。

## 6. Compatibility
- 不考虑存量（FR-6）。前端 composer 列表即时反映新名；已勾选旧名的会话配置需用户重新勾选（接受）。

## 7. Non-functional Requirements
- 契约上移后 AIAgent/McpCenter 仍只引 ForgeSelf.Core + ForgeSelf.Abstractions（铁律，构建验证）。
- 守卫测试 < 5s。

## 8. Failure Modes（PC lens）
无命中 PC（00 §2）。预填风险：
- **契约上移遗漏**：McpCenter 引用 IProjectWorkspaceService 编译失败即暴露（构建立判，低风险）。
- **豁免清单与映射表漂移**：守卫 ③ 拦截。
- **AIAgent 删类后残留引用**：编译期暴露（grep + build 双查）。

## 9. Interaction Design（交互设计）
无新增交互流程；用户可见变化：composer 🔧 列表显示新名/新归属（公共工具 meta 变 `mcp-center`），工具卡名称随之；「写操作」标行为不变（DANGER_RE）。验收并入 AC-6/AC-7；走查项：composer 列表渲染、勾选→发送→工具卡一致。

## 10. Acceptance Criteria
- [ ] AC-1 守卫测试 4 断言绿 + mutation（去前缀）变红验证。
- [ ] AC-2 全注册表零重名；映射表与实际一致。
- [ ] AC-3 read_file/write_file 绝对路径必填可用、list_files 省略 path 回退项目根、**未选项目根时 read/write 照常工作**（单测锚定）。
- [ ] AC-4 AIAgent 注册表贡献 = aiagent_plan_workflow + 4 个豁免协议/控制工具；8 个公共工具在 McpCenter；workflow_* 三件替代 AIAgent 删除的三件。
- [ ] AC-5 `IProjectWorkspaceService` 接口位于 ForgeSelf.Abstractions；AIAgent/McpCenter csproj 均不新增插件间引用（构建 + csproj 检查）。
- [ ] AC-6 composer 三档白名单行为等价（表驱动换新名）。
- [ ] AC-7 ai-agent tool-gateway-031 e2e（4 场景换新归属后语义不变：universal_tool 仍在注册表、calculate→mcp_calculate、list_files 裸名、run_terminal_command 裸名）+ 涉改插件冒烟绿。
- [ ] AC-8 中档门禁：宿主全量 `dotnet test` 无新增红 + ForgeSelf.Web check/test + 涉改插件 build 绿。
- [ ] AC-9 文档 + ADR + not-taken 同步；021 工具清单与新归属一致。
