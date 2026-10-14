# mini-task：万能工具增加搜索工具功能（输入2 · 2026-10-13）

> 级别：轻量（≤3 文件核心改动 + 测试）。Intent/Spec/Plan/Task 合并本文件（五要素）；05-evidence/06-review 执行后单独产出。
> 闸门1：用户 2026-10-13 输入2 直接下达实现指令（「开始做万能工具增加搜索工具功能，避免只有列举工具，搜索不到工具再用列举所有工具」）= 即批开工（沿用 2026-10-12 输入4 先例）。

## Intent（为什么 / 做什么 / 到什么程度）
- **Problem**：工具发现问题现在只有两条路——`universal_tool {tool:'list_tools'}` **全量枚举**（112 个工具，名字+说明全回，token 大、还要模型自己肉眼找），或直接猜名字调用。模型侧没有一个「给我关键词，返回最可能的目标工具」的入口；搜不到/猜错名时，错误封套只说「unknown tool（已注册 N 个）」，不帮模型找正确工具名—— 下一步只能退化为全量列举（恰恰是用户想避免的：**搜不到才允许列举，而不是默认列举**）。
- **Why**：031/034 建立的万能工具网关是「先发现、再按名调用」模式，但发现手段只有枚举；工具数量已过百，枚举的成本随工具数线性上涨。行业内（MCP/ToolSearch 方向）通行做法是「关键词搜索 → 命中即调，未命中才枚举」。
- **Expected Outcome**：
  1. 新工具 `search_tools`：按关键词对「工具名 + 描述」打分检索，返回 top-N（默认 8）带名称/说明/插件来源/匹配理由（matched name/description）；
  2. `universal_tool`（AIAgent 插件）与 MCP 转发器（McpCenter）在「unknown tool」错误封套附带 top 搜索建议，模型无须二次枚举即可纠正工具名；
  3. `list_tools` 保留不变（兜底路径仍在，只是不再是默认第一动作）。
- **Scope（不改）**：六闸门分发核、宿主 ForgeSelf.Api/**、其他插件、web/、外部 MCP 转发（mcp.*）路由、鉴权。
- **Success Criteria**：
  1. `search_tools {keyword:"文件"}` / `{keyword:"file"}` 对注册表实时快照检索，命中工具按相关度排序返回（含自身以外的全部工具，排除 search_tools 自身不必要）；
  2. 名字精确子串命中 > 描述命中，同分按名称稳定排序；空/缺 keyword → 返回友好错误（引导传 keyword），**不静默全量列举**；
  3. unknown tool 封套升级：`unknown tool 'xxx'（已注册 N 个…）` + `hint`（含 top≤3 建议，形如 `did you mean: a / b / c？可 search_tools 检索，或 list_tools 全量枚举`）；无建议时只给引导语；
  4. 既有行为零回归：透传/防自引用/字符串参数/unknown 拒绝路径零分发不变（既有 10 例 UniversalToolTests 全数通过）。

## Spec（具体规格）
- **Functional Requirements**：
  - FR-1 `SearchToolsToolFunction`（新类，`Plugins/AIAgent/Services/ToolFunctions/SearchToolsToolFunction.cs`，对齐 ListToolsToolFunction 范式：IToolFunctionExtension + 运行期 `ctx.Get<IToolRegistry>()` 软依赖）：
    - 入参 schema：`{ keyword: string(必填语义), limit?: number(默认 8, 1..20 截断) }`；
    - 打分：名称 OrdinalIgnoreCase 包含 keyword=3 分（前缀命中再加 1 分）、TokenEquals（全词等于）=5 分；描述包含=1 分；0 分不返回；
    - 出参：`{ success:true, keyword, total, tools:[{name, description, pluginId, score, matchedIn:["name"/"description"]}] }` 按 score 降序、再按名称稳定序；
    - 注册：AIAgentPlugin.RegisterToolFunctionExtensions 追加 `ToolExtensions.Add(new SearchToolsToolFunction(pluginId, ctx))`。
  - FR-2 `UniversalTool`（AIAgent）unknown 分支：GetTool 为 null 时调用与 search 相同的打分逻辑（内部方法复用，不硬编新搜索器依赖），封套加 `hint` 字段（top≤3 + 引导语）。已有错误措辞保留（兼容既有断言 `unknown tool`、`已注册 N 个`）。
  - FR-3 `UniversalToolForwarder`（McpCenter）unknown 分支同样加 `hint`（同一打分逻辑以轻量静态助手复用；不改 mcp.* 外部路由）。
  - FR-4 `list_tools` 行为不变；`SearchTools` 自身的 Description/Schema 写清「先搜索、搜不到再 list_tools」。
- **复用决策**：打分逻辑落在 AIAgent 插件内（`ToolSearchExtensions` 静态类 targeting `IToolFunctionExtension`），McpCenter 通过 `ForgeSelf.Core` 已引用的公共工程？——**Unknown：两插件无公共工程可放**（插件只依赖 Abstractions/Core）。为避免为一个小函数新建共享工程（C3 反对引第三套机制），McpCenter 侧以**自包含静态方法**实现同样打分（<40 行，两处注释互指），不抽象插件间新契约。此为记录在案的实施取舍，如后续第三处出现同类需求再上移缓存池（触发条件式上移先例）。
- **Boundary**：不改 list_tools 输出结构；不动 McpController 面板接口；不建 e2e（纯工具层能力，冒烟随下次插件批次）；无 UI 变更 → 免 Interaction Design 节（规范 §2 的 UI 触发条件不满足）。
- **Error Handling**：registry 未挂 → `{success:false, error:"IToolRegistry 未注册到插件上下文"}`（与 UniversalTool 同文案）；keyword 空 → `{success:false, error:"keyword 必填：请给出要搜索的工具能力关键词（如 file/记忆/工作流/终端）", hint:"如 'file'、'记忆'、'工作流'"}`；limit 非法 → 截断，不报错。
- **Compatibility**：unknown 封套新增 hint 字段为**加字段不破断言**（既有测试 Contains("unknown tool")/Contains("已注册") 仍命中）。
- **Acceptance Criteria**：Intent·Success Criteria 1-4。

## Plan（具体到真实文件）
1. **新增** `Plugins/AIAgent/Services/ToolFunctions/SearchToolsToolFunction.cs`（FR-1，打分逻辑以 internal static 助手暴露给 UniversalTool 复用）。
2. **修改** `Plugins/AIAgent/Services/ToolFunctions/UniversalTool.cs`（FR-2：unknown 分支接 hint → 复用 SearchTools 内部打分助手）。
3. **修改** `Plugins/AIAgent/AIAgentPlugin.cs`（FR-1 注册一行）。
4. **修改** `Plugins/McpCenter/Services/UniversalToolForwarder.cs`（FR-3：unknown 分支自包含打分 + hint）。
5. **新增** `ForgeSelf.Api.Tests/Plugins/AIAgentTests/SearchToolsToolFunctionTests.cs`（搜中/排序/空 keyword/limit 截断/registry 未挂/名称vs描述打分）；**修改** `ForgeSelf.Api.Tests/Plugins/UniversalToolTests.cs`（unknown 封套含 hint 且不破坏原断言；新增 top 建议用例）。
6. **禁改**：web/、宿主 ForgeSelf.Api/**、其他插件、既有 list_tools 实现与测试断言。

## Task（工作单元）
- **Objective**：模型/外部客户端可 `search_tools` 关键词检索目标工具；猜错名字时封套直接给 top 建议；`list_tools` 退居兜底。
- **Scope Allowed**：上述 3 新增 + 3 修改 + 测试；`cd Plugins/AIAgent && dotnet build` + ForgeSelf.Api.Tests 过滤集（`SearchTools|UniversalTool`）。
- **Scope Forbidden**：删测试放水；动 web/；动宿主；停/启/杀用户运行中的宿主（PID 锁则用 `-p:OutDir`/`-o` 旁路，仓内既有规避）；未授权 git commit/push；发布动作（②-⑥ 闭环按输入2 范围不含，注明未做）。
- **Acceptance Criteria（checkbox）**：
  - [x] `search_tools` 新工具落地并注册（名称/id/描述/schema 完整）
  - [x] 打分与排序正确（名称优先于描述；同分稳定序；0 分不返回）
  - [x] 空 keyword 友好错误不静默全量列举
  - [x] unknown 封套带 top≤3 hint（AIAgent + McpCenter 两条链路）
  - [x] 新测试全绿 + 既有 UniversalToolTests 10 例零回归（42/42 + McpCenter 108/108）
  - [x] 构建门禁 0 error（日志正文判据，不拿 exit code）
- **Verification Commands**：
  - `cd Plugins/AIAgent && dotnet build --nologo -v q` → 判据：日志正文 `0 个错误`
  - `cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~SearchTools|FullyQualifiedName~UniversalTool" --nologo` → 判据：`失败: 0` 且通过数 ≥ 既有 10 例 + 新增数
  - 环境前置（§5.0）：TEMP 指到 `<repo>\.temp\tmp`；锁文件时 `-p:OutDir` 旁路；读日志正文判。
