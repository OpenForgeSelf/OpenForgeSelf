# ADR-004：工具命名空间统一——插件工具名加 `<toolPrefix>_` 前缀，公共基础工具迁 mcp-center

- 状态：Accepted（2026-10-28，闸门1 用户拍板「按以上重新调整下各个工具归属」；随本轮 pilot 落地）
- 关联：`docs/ai/pilot/2026-10-28-tool-namespace-ownership/`（00–07）· `docs/02-features/021-ai-agent.md`（工具清单重写）· 被部分修订的 D10/ADR-003（枚举合并语义不变，工具归属变化见下）
- 背景Link：用户输入 2026-10-28「universal_tool / search_tools / list_tools 、request_help 、run_terminal_command 豁免，并且公共的工具迁移至mcp中心，比如文件读写、执行命令、万能工具、搜索工具等等跟插件主功能没多大关系的都迁出，这些，文件三件套也加入豁免，其他的保持上面的方案增加前缀。不用考虑存量引用。」

## 决策

1. **命名空间规则（唯一真源 `ForgeSelf.Abstractions/ToolNamespace.cs`）**：凡注册进宿主 `IToolRegistry` 的插件工具，名必须为 `<toolPrefix>_<语义名>` 或命中**平台保留名白名单**（11 个：universal_tool、search_tools、list_tools、run_terminal_command、read_file、write_file、list_files、finish、submit_plan、complete_step、request_help）。校验在 `ExtensionPointManager` 注册口单点执行：违规拒绝注册 + Error 日志，**永不阻塞宿主**。
2. **toolPrefix 声明双源一致**：`ToolNamespace.PrefixByPluginId`（16 个有工具插件）与各 `plugin.json` 新增 `toolPrefix` 字段须逐字一致；守卫测试 S1 扫描钉死。
3. **公共基础工具迁 mcp-center**（原 AIAgent 的通用能力与插件主功能无关）：万能工具 universal_tool、工具搜索 search_tools、工具枚举 list_tools、终端命令 run_terminal_command、文件三件套 read_file/write_file/list_files，以及 get_current_time/calculate。AIAgent 保留且仅保留协议词工具（finish/submit_plan/complete_step/request_help，豁免前缀）与 `aiagent_plan_workflow`（计划流入口，加前缀）。
4. **改名不做运行期别名**（用户拍板「不用考虑存量引用」）：81 个旧名即退役，改名映射 `RenameMap` 仅作**文档与守卫对账的唯一真源**，无兼容层、无数据迁移。
5. **文件工具绝对路径参数化（FR-5）**：read_file/write_file 的 `path` 必须为绝对路径（相对路径拒绝并给引导文案），不再隐式接项目根；list_files 的 `path` 可选——缺省回退当前项目根（`IProjectWorkspaceService`，契约上移 `ForgeSelf.Abstractions`，实现仍由 AIAgent 经 ctx 共享表提供）。依赖项目目录的会话状态改为参数传递，与「移除项目目录绑定」的 pilot 原目标一致。
6. **CWD 安全门语义不变**：run_terminal_command 迁属后仍经 `IProjectWorkspaceService` 做 cwd 越界门；守卫（`TerminalCommandGuard`）随迁 McpCenter，与 ToolBridge 自带 `CommandGuard` 的金样表对账判据保持（parity 测试改指向新命名空间）。

## 依据

- 用户拍板（背景Link 原文）：豁免清单 + 公共工具迁 MCP 中心 + 其余加前缀 + 不考虑存量引用，四点均为用户逐字指定；get_current_time/calculate 归入「等等」范畴迁 mcp-center（推定已按此执行，如需回迁属新决策）。
- 架构判据：工具名是跨插件契约面——裸语义名（`execute_workflow` 等）在任何聚合检索场景都会撞名；`<toolPrefix>_` 让名字自带归属。工具检索（search_tools）与权限名册（composerContext 权限三档）都以**工具名**为主键，前缀化后语义分组（dev_/text_/sys_…）可读性显著提升。
- 实施顺序（03-plan B1-B4）：契约层先立唯一真源（ToolNamespace + RenameMap）→ 守卫测试先行（S1-S5 + mutation 自检）→ 批量改名（81 行）→ 公共工具迁移与测试对齐。守卫采用「源码静态扫描 + 注释剥离 + DTO 行过滤」三层防误报（CostScope 注释误判、McpService 外部工具目录 DTO、ScriptTemplateService 脚本参数、ASP.NET 绑定特性四类实证）。

## 后果

- 正面：注册表内工具名全局唯一且自带归属；检索/权限/文档三类消费方按名分组不再有歧义；公共工具的维护与 AIAgent 插件版本解耦（mcp-center 独立演进）。
- 负面：所有既有调用方（内置 agent 提示词白名单、前端权限名册、e2e 断言、外部脚本）需一次性换名——本轮已同步（前端 1034 测试绿、后端过滤集 251 绿）；旧名不再可达（无别名），未同步的外部集成将收到「unknown tool」。
- 边界：本 ADR 只约束**宿主注册表工具**；外部 MCP 服务器工具名（`mcp.<服务器id>.<工具名>`）与本机脚本参数名不属命名空间管辖（守卫扫描器已显式排除这两类行）。
- 守卫形态：`ToolNamespaceGuardTests`（S1 前缀声明一致 / S2 全量合规 / S3 改名映射对账 / S4 零重名 / S5 mutation 自检）为常驻防复发判据；新增工具违反规则在 CI 即红。

## 修订（2026-10-28 17:29 用户拍板）

**mcp-center 整插件豁免前缀**（原文：「mcp中心的工具不用前缀，是特殊例外」）：

- mcp-center 是平台公共工具宿主，其工具名即**公共 API 面**，不加插件前缀——落地为 `ToolNamespace.ExemptPluginId`（Validate 对 mcp-center 直接放行）+ `PrefixByPluginId` 移除 mcp-center + 其 `plugin.json` 删除 `toolPrefix` 字段。
- 直接后果：get_current_time/calculate 用**原名**（推翻本 ADR 决策 3 初版的「加 mcp_ 前缀」推定）；其 7 个保留名工具不变。
- S5 mutation 自检追加豁免断言（豁免插件裸名合规 + 前缀表不含 mcp-center）防漂移。

## 修订（2026-10-28 22:47 用户拍板 · 归属修订轮 D-V1/D-V2）

**插件功能归属调整两项**（立法先行：`docs/04-standards/plugin-design-boundary.md` v1.0.0，本 ADR 只记命名/归属面）：

- **D-V1：text-tools 整插件并入 dev-tools**——`text_stats` 改名 `dev_text_stats`（按 dev-tools 前缀）；text_base64_encode/decode、text_hash、text_format_json 由 dev-tools 同功能工具承接**退役**（行为等价已核：dev_format_json 与 text_format_json 均支持 indentSize 默认 2）。text-tools 不进 RenameRows（退役插件无目录，S3 对账要求源目录存在），插件级对账见 pilot `2026-10-28-plugin-design-boundary`。宿主「文本工具」页面（TextToolsView，纯前端实现）不动，后端归 DevTools。
- **D-V2 方案 A：文件三件套实现迁 file-tools**——read_file/write_file/list_files 的 `PluginId`/`Id` 改 `file-tools`，**名字维持平台保留名不变**（名字稳定与归属解耦）；mcp-center 收窄为网关 + 发现 + 公共小工具面（6 工具）。S3 MovedTo 增三行（ai-agent:* → file-tools）。
- 工具总数 127 → 124（16 插件贡献）；mcp-center 2.8.0 / file-tools 1.2.0 / dev-tools 1.1.0。
