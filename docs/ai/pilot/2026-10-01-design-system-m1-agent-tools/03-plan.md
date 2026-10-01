# Plan

> 阶段：Stage 3｜具体到真实文件路径。「新增」= 仓库里目前不存在（已核实）；「改」= 已存在。
> Task ID：PILOT-ds-m1-agent-tools

## Files To Change

**后端（`Plugins/DesignSystem/`）**

- file: `Plugins/DesignSystem/Services/GenerationService.cs`（新增）
  reason: 把控制器 `Generate` 内联的 `ApplyToProject → SeedComponentCatalog → SeedBrandCatalog → AuditEngine.Run` 收成一处，控制器/`design_create`/`design_edit` 共用（FR14）。
- file: `Plugins/DesignSystem/Services/AgentAccess.cs`（新增）
  reason: 写开关（FR3），`agent-access.json` 落插件数据目录，fail-closed。
- file: `Plugins/DesignSystem/Services/TokenIndex.cs`、`Services/NearestTokenFinder.cs`（新增）
  reason: 从 `ExportService.Snapshot` 建令牌索引（色/长度/时长/阴影/字体/字重/已定义 CSS 变量集）+ 最近令牌（FR6，审查引擎与 `design_lookup nearest` 共用）。
- file: `Plugins/DesignSystem/Services/DesignBriefBuilder.cs`、`Services/AgentRulesBuilder.cs`（新增）
  reason: 设计说明书（FR4）与 `agent-rules` 文本（FR5），均为 `Snapshot` 上的纯函数。
- file: `Plugins/DesignSystem/Services/DesignReviewer.cs`、`Services/DesignReviewService.cs`（新增）
  reason: 审查引擎（纯函数：扫描器 + 规则）与服务层（加载 Snapshot/生命周期/主题回落，FR8/FR9）。
- file: `Plugins/DesignSystem/Services/StylePresets.cs`（新增，含 `PresetRecommender`）
  reason: 8 个预设与推荐（FR11）。
- file: `Plugins/DesignSystem/Services/QuickCreateService.cs`（新增）
  reason: 预设/参数 → 项目 + 生成 + 审计（FR12）。
- file: `Plugins/DesignSystem/Agent/DesignToolKit.cs`、`Agent/DesignToolBase.cs`、`Agent/DesignToolIndex.cs`（新增）
  reason: 工具基座与服务束、工具索引单一真源（`design_guide`/REST `agent/tools`/`meta.agentTools` 共用）（FR1/FR2/FR15）。
- file: `Plugins/DesignSystem/Agent/DesignReadTools.cs`、`Agent/DesignReviewTool.cs`、`Agent/DesignWriteTools.cs`（新增）
  reason: 8 个工具：`design_guide`/`design_context`/`design_lookup`/`design_audit`/`design_presets`（读）、`design_review`、`design_create`/`design_edit`（写）。
- file: `Plugins/DesignSystem/DesignSystemPlugin.cs`（改）
  reason: `Apply` 里 new 出服务单例并同实例注册 DI，装配 `ToolExtensions`（PluginId 取 metadata，缺省回落常量）。
- file: `Plugins/DesignSystem/Controllers/DesignSystemController.cs`（改）
  reason: 构造函数增服务；`Generate` 改调 `GenerationService`（响应形状不变）；新增 `brief/review/presets/recommend/quick-create/agent-access/agent-tools` 端点；`meta.capabilities` 增 5 项 + `agentTools`。
- file: `Plugins/DesignSystem/Services/ExportService.cs`（改）
  reason: `ExportFormats` 增 `brief`/`agent-rules`；`Produce`/`Bundle`/`Manifest` 收录；把 `CssVarName`、`ShouldSkipCss`、组件规格解析辅助（`AxisSummary`/`ParseStringArray`/`AnatomyJson`）提升为 `public static` 供 brief/reviewer 复用（不改行为；`DesignSystem.csproj` 无 `InternalsVisibleTo`，不为此改工程文件）。
- file: `Plugins/DesignSystem/Services/DesignGenerator.cs`（改）
  reason: 增 `public static InferIndustry(String? brief)` 与 `KnownIndustries`（推荐器复用既有行业线索表，不另抄一份）。
- file: `Plugins/DesignSystem/Services/DesignSystemConstants.cs`、`Plugins/DesignSystem/plugin.json`（改）
  reason: 版本三元组 + 清单版本 → 2.8.0（同源契约，`DesignSystemAuthTests` 断言）。

**内置 agent**

- file: `Plugins/AIAgent/Services/AIAgentService.cs`（改）
  reason: `ResolveOwnToolDefinitions` 的白名单抽成 `internal static ToolScopePluginIds(string ownPluginId)` 并加入 `design-system`（FR16）。
- file: `Plugins/AIAgent/plugin.json`（改）
  reason: 1.7.2 → 1.7.3（行为变更须升版，随包发布）。

**测试（`ForgeSelf.Api.Tests/`）**

- file: `Plugins/DesignSystemTests/DesignAgentToolTests.cs`、`McpGatewayDesignToolsTests.cs`、`DesignBriefTests.cs`、`NearestTokenFinderTests.cs`、`DesignReviewerTests.cs`、`PresetTests.cs`、`QuickCreateTests.cs`、`AgentAccessTests.cs`（新增）
  reason: AC1–AC21/AC24 的自动化判据（TDD：先红后绿）。
- file: `Plugins/DesignSystemTests/ExportProjectionTests.cs`（改）
  reason: 增 `brief`/`agent-rules` 内容与 bundle 收录断言（AC21）。
- file: `Plugins/AIAgent/AIAgentToolScopeTests.cs`（新增）
  reason: AC23。

**e2e / 文档 / 技能**

- file: `ForgeSelf.Web/e2e/plugins/design-system/design-system-agent.spec.ts`（新增）
  reason: AC26（不往既有巨型用例里塞；沿用 `mcp-center.spec.ts` 的网关直连写法与 `injectRealApiKey`）。
- file: `.agents/skills/design-system-consume/SKILL.md`（新增）、`.agents/skills/design-system-verify/SKILL.md`（改）、`AGENTS.md`（改 §2.4 一行）
  reason: AC27，技能不登记=不存在。
- file: `Plugins/DesignSystem/README.md`、`ROADMAP.md`、`docs/02-features/036-design-system.md`（改）
  reason: 事实同步（工具/REST/导出格式/版本/边界）。
- file: `docs/ai/pilot/2026-10-01-design-system-m1-agent-tools/*`、`.forgeself/memory/2026-10-01.md`、`TODO.md`
  reason: 工件链与日志、队列。

## 工具契约（8 个；schema 只列必要字段，描述 ≤400 字符）

| 工具             | 读/写                                    | 关键入参                                                                                       | 关键出参                                                                   |
| ---------------- | ---------------------------------------- | ---------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `design_guide`   | 读                                       | 无                                                                                             | `tools[]`、`workflows[]`、`projects[]`、`presets[]`、`access`、`discovery` |
| `design_context` | 读                                       | `project`、`theme`、`sections[]`、`format`(markdown\|json)、`maxChars`                         | `contentHash`、`sections`、`omitted`、`truncated`、`markdown`/`json`       |
| `design_lookup`  | 读                                       | `kind`(token\|component\|nearest\|export\|icon) + 各 kind 字段                                 | 各 kind 专属；export 含 `truncated`/`restUrl`                              |
| `design_review`  | 读                                       | `mode`(code\|checklist)、`files[]`/`code`+`language`、`theme`、`strict`、`maxFindings`、`page` | `summary`、`findings[]`、`truncated`、`notes[]` / `items[]`                |
| `design_audit`   | 读；`run=true` 写                        | `project`、`run`、`kind`、`onlyFailed`、`limit`                                                | `summary`、`blocking`、`items[]`                                           |
| `design_presets` | 读                                       | `action`(list\|recommend)、`brief`、`kind`、`industry`、`tone[]`、`brandColor`、`density`      | `presets[]` / `matches[]`（score、reasons、request）                       |
| `design_create`  | `apply=true` 写                          | `name`、`code?`、`kind?`、`preset?`、生成参数、`themes[]`、`apply`                             | `applied`、`preview`/`project`、`audit`、`uiRoute`                         |
| `design_edit`    | 写（`regenerate` 且 `apply=false` 除外） | `project`、`action`(set_token\|regenerate\|publish)、各 action 字段                            | 各 action 专属（含 `diagnostics`）                                         |

## 实现契约（零自决详表）

> **使用规则**：本节是实现方的**唯一设计依据**。表内的工具名、参数名、枚举、默认值、阈值、关键文案即验收判据（验收方会逐项对表）。
> 实现时发现与仓库现状冲突 → 先写入文末「Plan 偏差记录」再改，**不得自行重新设计**；表里没写的细节才由实现方决定（并在 05-evidence 登记）。
> 通用风格：C# 用 NewLife 类型别名（`String`/`Int64`/`Boolean`），服务类 `sealed`，注释用中文只写"为什么"；新文件无 BOM；JSON 一律 camelCase + `UnsafeRelaxedJsonEscaping`（中文不转义）。

### §A 通用约定（8 个工具共用，由 `DesignToolBase` 一处实现）

| 项       | 规定                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 封套     | 成功 `{"success":true,"data":{…},"next":["…"]}`（`next` 可省，≤3 条纯文本"下一步"）；失败 `{"success":false,"error":"…","hint":"…"}`（`hint` 可省）。序列化单行、无缩进。                                                                                                                                                                                                                                                                                                                                                      |
| 参数     | 空串/空白 → 视为 `{}`；非法 JSON → `error="参数 JSON 非法：…"`。字符串先 Trim，空串=缺失；枚举**大小写不敏感**，输出规范化为小写；`string[]` 参数同时接受 JSON 数组与逗号分隔字符串；类型不符 → `error="参数 x 类型不符（期望 …）"`；数值越界按各表**夹取**而非报错（夹取后在 `data.notes` 说明）。                                                                                                                                                                                                                            |
| 项目解析 | 参数 `project`：先按 `code` 精确匹配（Trim + 忽略大小写），纯数字再试 `id`。缺省时：非归档项目**恰好 1 个**→用它；0 个/多个 → `error="请指定 project（可选：a, b, c）"`（最多列 20 个 code；0 个时 `hint` 指向 `design_presets action=recommend` 与 `design_create`）。归档项目可被只读工具显式指定；**写动作拒绝归档项目**：`error="项目 x 已归档（软删），不能写入；先在界面把状态改回 draft"`。                                                                                                                             |
| 写动作   | 判定函数 `IsWrite(args)`：`design_create` 的 `apply=true`；`design_audit` 的 `run=true`；`design_edit` 的 `action=set_token`/`publish`，以及 `action=regenerate` 且 `apply=true`；其余全部只读。基类在调用 handler 前**统一**用 `AgentAccess` 拦截（子类不得各自判断）。关闭时：`error="外部写入已被关闭（设计系统 › 接入 › 写入开关，或 PUT api/design-system/agent-access）。只读能力不受影响。"`，`hint="可先用 apply=false 或只读动作预演。"`。**写开关只约束 `design_*` 工具（agent/MCP 路径），不约束人用界面与 REST。** |
| 重试     | 只读：异常消息含 `database is locked` / `code = Busy` / `SQLITE_BUSY` 时退避重试最多 3 次（150ms / 400ms / 900ms）；**写动作绝不重试**，失败原样报 `error="数据库繁忙，未写入：<原文>"`（写路径本身是整批事务，未提交即无半写）。                                                                                                                                                                                                                                                                                              |
| 体积     | 序列化结果 > 120,000 字符 → `{"success":false,"error":"结果过大（N 字符，上限 120000），请缩小范围（sections/limit/maxChars/prefix）"}`。`design_context`/`design_lookup export` 默认 `maxChars=16000` 自行截断，正常不会触发该上限。                                                                                                                                                                                                                                                                                          |
| 异常     | 任何异常转 `{"success":false,"error":"执行失败：<异常类型>: <消息>"}`，**不得抛给 `universal_tool` 转发层**；`XTrace.Log.Error("[DesignSystem.Tool] {0} 失败: {1}", name, ex)` 记全量。输出文本过一遍 `Sanitize`：形如 `X:\…` / `X:/…` 的绝对路径与 `/home                                                                                                                                                                                                                                                                     | Users | var | tmp/…` 路径替换为 `<path>`（AC19：结果 JSON 不含绝对路径）。 |
| 用量     | 每次调用结束（成败皆报）`RecordUsageAsync(hostProvider, toolName, elapsedMs)`；`hostProvider` 在**调用时**用 `ctx.Get<IServiceProvider>()` 取（Apply 时拿不到宿主根 provider，见 00 §DI/服务解析陷阱）；取不到静默跳过。参照 `Plugins/Sems/ToolExtensions.cs:16-110` 的基类形状，但**服务不走 DI 解析**：`DesignToolKit` 直接持有单例引用。                                                                                                                                                                                    |
| 元数据   | `Id = "design-system.tool.<后缀>"`（后缀 = 工具名去掉 `design_`），`PluginId = ctx.Get<PluginMetadata>()?.Id ?? DesignSystemConstants.PluginId`（值必须等于 `design-system`，AIAgent 白名单按它匹配）；`Description` ≤ 400 字符；`ParametersJsonSchema` 是合法 `{"type":"object","properties":{…},"required":[…]}`，`required ⊆ properties`，**不使用 `oneOf/anyOf`**（条件必填由 handler 校验并回可执行错误）。每个 schema 的 `properties` 键集合必须与 §B 表**逐名一致**（测试会对表）。                                     |
| 确定性   | 同输入同输出：不引入随机/时钟参与结果（`generatedAt` 只作元数据且不进 hash）。                                                                                                                                                                                                                                                                                                                                                                                                                                                 |

### §B 工具参数与出参（逐工具）

> 表头：参数 | 类型 | 必填 | 默认 | 取值/范围。「出参」只列 `data` 内**稳定字段**（可多不可少，字段名即契约）。

#### B1 `design_guide`（读）

- 入参：无（`properties` 为空对象）。
- 出参：`version`（插件版本）、`purpose`（一句话：设计系统是前端 UI/UX 唯一真源）、`tools[]`（`{name,kind:"read"|"write",summary,when}`，恰 8 项，来自 `DesignToolIndex`）、`workflows[]`（三条：`consume` 开发前读真源→写完审查 / `create` 为新项目建设计系统 / `maintain` 调整并发布；每条 `{id,title,steps[]}`，steps 里写具体工具名与关键参数）、`projects[]`（非归档，≤20，`{code,name,kind,version,status,tokenCount,componentCount}`，超出写 `projectsTotal`）、`presets[]`（`{id,name,tagline}`，8 项）、`access`（`{allowWrite}`）、`discovery`（固定含子串 `universal_tool` 与 `"tool":"list_tools"` 与 `keyword`，说明外部 MCP 客户端须先枚举再调用）。
- `next`：无项目 → `["design_presets action=recommend","design_create"]`；有项目 → `["design_context"]`。

#### B2 `design_context`（读）— 设计说明书（唯一真源）

| 参数     | 类型     | 必填 | 默认                        | 取值/范围                                                                             |
| -------- | -------- | ---- | --------------------------- | ------------------------------------------------------------------------------------- |
| project  | string   | 条件 | 唯一非归档项目              | code 或 id                                                                            |
| theme    | string   | 否   | 项目默认主题 → 回落 `light` | 项目内的色向主题 code；不存在则回落默认主题并在 `notes` 说明                          |
| sections | string[] | 否   | 全部（有内容者）            | `identity,rules,colors,typography,scales,components,brand,checklist`；`identity` 恒含 |
| format   | string   | 否   | `markdown`                  | `markdown` \| `json`                                                                  |
| maxChars | integer  | 否   | 16000                       | 2000–60000（夹取）                                                                    |

出参：`project{code,name,kind,version,status}`、`theme`、`contentHash`、`sections[]`（实际包含）、`omitted[]`（因预算放不下的章节）、`truncated`（`omitted` 非空即 true）、`markdown`（format=markdown）或 `json`（format=json，结构化对象，章节名为键）、`notes[]`。无令牌项目：只含 `identity`，`notes` 含"尚未生成令牌"并指向 `design_create`/`design_edit regenerate`，**不抛错**。

#### B3 `design_lookup`（读）— `kind` 决定其余字段

| 参数                    | 类型             | 必填           | 默认                           | 取值/范围                                                                                                                                                                  |
| ----------------------- | ---------------- | -------------- | ------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| kind                    | string           | **是**         | —                              | `token` \| `component` \| `nearest` \| `export` \| `icon`                                                                                                                  |
| project                 | string           | 条件           | 同 B2                          | `icon` 可省（省则只查内置库）                                                                                                                                              |
| theme                   | string           | 否             | 同 B2                          |                                                                                                                                                                            |
| prefix / q              | string           | 否             | —                              | `token`：路径前缀 / 路径子串（不区分大小写）                                                                                                                               |
| tier / type             | string           | 否             | —                              | `token`：`primitive\|semantic\|component` / `TokenTypes.All` 之一                                                                                                          |
| limit                   | integer          | 否             | token 50 / icon 30 / nearest 3 | token 1–200；icon 1–100；nearest 1–5                                                                                                                                       |
| offset                  | integer          | 否             | 0                              | `token`：≥0                                                                                                                                                                |
| code                    | string           | 否             | —                              | `component`：缺省 = 列出全部；给了 = 详情，不存在回候选 code 列表                                                                                                          |
| value                   | string           | `nearest` 必填 | —                              | 颜色（hex/rgb/hsl/oklch）、长度（`13px`/`0.8rem`）、时长（`180ms`/`0.2s`）、阴影、字体族、字重                                                                             |
| property                | string           | 否             | —                              | `nearest` 的类别线索（如 `color` `background` `padding` `border-radius` `font-size` `font-family` `font-weight` `transition-duration` `box-shadow`），无线索则按值形状推断 |
| format                  | string           | `export` 必填  | —                              | `ExportFormats.All`（含新增 `brief` `agent-rules`）                                                                                                                        |
| maxChars                | integer          | 否             | 16000                          | `export`：2000–60000                                                                                                                                                       |
| collection / includeSvg | string / boolean | 否             | — / false                      | `icon`                                                                                                                                                                     |

出参（按 kind）：
- `token`：`theme`、`total`、`offset`、`limit`、`items[]`（`{path,tier,type,value(有效值),hex?,cssVar,alias?,lifecycle}`）。
- `component`（列表）：`components[]`（`{code,name,category,interactive,status,tokenCount,variantCount}`）；（详情）：`code,name,category,anatomy[],states[],axes{轴:[值]},tokens[],a11yNotes,variantCount`。
- `nearest`：见 §F。
- `export`：文本格式 `{format,theme,fileName,contentType,length,truncated,content,restUrl}`；`bundle` 是二进制 → `{format,fileName,contentType:"application/zip",restUrl,note:"二进制，请经 REST 下载"}`（**不回 content**）。`restUrl` 形如 `api/design-system/projects/{id}/export?format=…&theme=…`。
- `icon`：`total`、`items[]`（`{code,name,collection,license,tags,svg?}`，`svg` 仅 `includeSvg=true`）。

#### B4 `design_review`（读）— 开发后审查

| 参数            | 类型     | 必填      | 默认   | 取值/范围                                                                                               |
| --------------- | -------- | --------- | ------ | ------------------------------------------------------------------------------------------------------- |
| mode            | string   | 否        | `code` | `code` \| `checklist`                                                                                   |
| project / theme | string   | 条件 / 否 | 同 B2  |                                                                                                         |
| files           | object[] | 条件      | —      | `[{path:string, content:string}]`，≤200 个、总内容 ≤200KB(UTF-8)；`path` 只是标签，**服务端不据此读盘** |
| code / language | string   | 条件      | —      | `code` 与 `files` 二选一；`language ∈ css,scss,less,vue,html,tsx,jsx,ts,js`（`code` 模式必填）          |
| strict          | boolean  | 否        | false  | true：warning 计为 error                                                                                |
| maxFindings     | integer  | 否        | 100    | 1–500                                                                                                   |
| page            | string   | 否        | `any`  | `checklist` 用：`dashboard,list,form,detail,login,settings,workbench,landing,mobile,any`                |

出参（code）：`project`、`theme`、`summary{files,declarations,tokenized,hardcoded,tokenCoverage(null=无可判),errors,warnings,infos,passed,strict}`、`findings[]`（`{file,line,column,rule,severity,property,found,message,suggestion?{token,cssVar,value,replace}}`）、`truncated`、`notes[]`、`skipped[]`（`{file,reason}`）。出参（checklist）：`page`、`items[]`（`{id,severity,check,how,tokens[]}`，`tokens[]` 只含**本项目里真存在**的路径）。

#### B5 `design_audit`（读；`run=true` 写）

| 参数       | 类型    | 必填 | 默认  | 取值/范围                   |
| ---------- | ------- | ---- | ----- | --------------------------- |
| project    | string  | 条件 | 同 B2 |                             |
| run        | boolean | 否   | false | true = 重跑并落库（写动作） |
| kind       | string  | 否   | —     | `AuditKinds.All` 之一       |
| onlyFailed | boolean | 否   | true  |                             |
| limit      | integer | 否   | 50    | 1–200                       |

出参：`ran`、`summary{total,passed,critical,warning,info,blocking}`、`blocking`、`items[]`（严重级序 critical→warning→info，同级按 targetPath；`{kind,rule,severity,target,passed,expected,actual,message,suggestion}`）。

#### B6 `design_presets`（读）

| 参数                | 类型                       | 必填 | 默认   | 取值/范围                                                    |
| ------------------- | -------------------------- | ---- | ------ | ------------------------------------------------------------ |
| action              | string                     | 否   | `list` | `list` \| `recommend`                                        |
| brief / kind / tone | string / string / string[] | 否   | —      | `kind ∈ product,console,brand,marketing,system`              |
| industry            | string                     | 否   | —      | `DesignGenerator.KnownIndustries`（7 个）                    |
| brandColor          | string                     | 否   | —      | hex 或 `oklch(...)`；仅用于结果里的 `request.seedColor` 覆盖 |
| density             | string                     | 否   | —      | `default,compact,comfortable`                                |
| limit               | integer                    | 否   | 3      | 1–8                                                          |

出参：`list` → `presets[]`（完整字段见 §D）；`recommend` → `inferredIndustry`（brief 推断，可空）、`matches[]`（`{id,name,tagline,score,reasons[],request}`）。

#### B7 `design_create`（`apply=true` 写）

| 参数                                                                                                                                 | 类型                   | 必填   | 默认                | 取值/范围                                      |
| ------------------------------------------------------------------------------------------------------------------------------------ | ---------------------- | ------ | ------------------- | ---------------------------------------------- |
| name                                                                                                                                 | string                 | **是** | —                   | 显示名（中文可）                               |
| code                                                                                                                                 | string                 | 否     | 自动分配（§G）      | `^[a-z0-9][a-z0-9-]{0,39}$`；已存在 → 冲突错误 |
| kind / description                                                                                                                   | string                 | 否     | `product` / ""      | `kind` 同 B6                                   |
| preset                                                                                                                               | string                 | 否     | —                   | §D 的 id；未知 → 错误并列出全部 id             |
| brief, seedColor(别名 brandColor), hue, chroma, density, typeRatio, typeBasePx, radiusBase, motionScale, brandName, industry, themes | 同 `GenerationRequest` | 否     | 预设值 → 生成器默认 | **显式参数覆盖预设，预设覆盖默认**             |
| apply                                                                                                                                | boolean                | 否     | **false**           | false = 干跑，不写库                           |

出参：干跑 `{applied:false,preview{code,codeAvailable,name,seed,industry,hue,tokens,themes[],notes[]},request{有效请求},uiRoute}`；落库 `{applied:true,project{id,code,name,version,status},generation{seed,industry,hue,tokens,components,variants,fonts,screens,assets,themes[],notes[],skippedProtected},audit{total,passed,critical,warning,info,blocking},warnings[],uiRoute}`。`uiRoute` = `/design-system`（插件 `frontend.route`，从 `plugin.json` 常量取，不手写第二份）。

#### B8 `design_edit`（写；`regenerate` 且 `apply=false` 为只读预览）

| 参数                           | 类型   | 必填                    | 默认                          | 取值/范围                                                                                     |
| ------------------------------ | ------ | ----------------------- | ----------------------------- | --------------------------------------------------------------------------------------------- |
| project                        | string | 条件                    | 同 B2                         |                                                                                               |
| action                         | string | **是**                  | —                             | `set_token` \| `regenerate` \| `publish`                                                      |
| path                           | string | `set_token` 必填        | —                             | 令牌路径（点分 kebab）                                                                        |
| value / alias                  | string | `set_token` 二选一      | —                             | 字面值 / 别名目标路径（不含花括号）                                                           |
| theme                          | string | 否                      | 令牌现居层                    | 不填：令牌已存在则写它所在层；不存在则：primitive/component 写共享层，semantic **必填** theme |
| tier / type                    | string | 新建令牌时必填          | —                             | `TokenTiers.All` / `TokenTypes.All`                                                           |
| description                    | string | 否                      | —                             |                                                                                               |
| (生成参数) / overwrite / apply | 同 B7  | `regenerate`            | overwrite=false / apply=false | `overwrite=true` 才覆盖手改行                                                                 |
| version / notes                | string | `publish`：version 必填 | —                             | `version` 非空；`notes` 可选                                                                  |

出参：`set_token` → `{path,theme,created,before{value,alias},after{value,hex?},generator:"manual"}`，失败时 `error` + `diagnostics[]{path,status,message}`（别名成环/悬空/分层违规 = **整批回滚、零行落库**）；`regenerate` 预览 → `{applied:false,preview{…同 B7}}`，`apply=true` → `{applied:true,generation{…},skippedProtected,conflicts[],audit{…}}`；`publish` → `{release{id,version,tokensHash,tokenCount,auditPassed,snapshotAvailable}}`，被门禁拦（critical 未清 / 版本内容不同）→ `error` 保留 `ReleaseService` 的中文原文。

### §C 审查引擎（`DesignReviewer`）规范

**C0 输入归一与限额**
- 输入 `files[]`（`code`+`language` 等价于单文件 `path="snippet.<ext>"`）。文件数 >200 或总字节（UTF-8）>204,800 → **整次请求拒绝**：`error="审查输入过大（N 个文件 / M KB），上限 200 个 / 200 KB，请分批"`。
- 文件级跳过（记入 `skipped[]`，不抛）：`content` 空/空白 → `empty`；含 `\0` → `binary`；扩展名未知且无 `language` → `unknown-language`；单文件正则超时（500ms）→ `regex-timeout`。
- 语言推断：`.css .scss .less .vue .html .htm .tsx .jsx .ts .js .mjs`；`.sass/.styl` 等 → `unknown-language`。

**C1 抽取器**（把文件变成 `Declaration{file,line,column,property,value,selector?}` 序列）

| 文件类型            | 抽取规则                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| css / scss / less   | 状态机逐字符：跳过 `/* */` 注释（scss/less 另跳 `//`）、字符串、`url(...)` 内部；识别 `selector { decl; … }` 与嵌套；`@font-face` 块整块跳过；其他 at-rule 的前导文本跳过、其花括号内按规则块解析；scss `$x:` / less `@x:` 视为定义跳过；**自定义属性声明 `--x: …` 是定义**：不做任何字面量规则，只扫其值里的 `var(--ds-*)` 引用。                                                                                                                                                                                         |
| vue                 | 提取全部 `<style>` 块（含 `scoped`、`lang=scss\|less\|css`）按上行解析，行号加块起始偏移；模板里的 `style="…"` 与 `:style="{…}"` 做内联抽取；整文件再跑 Tailwind 任意值。                                                                                                                                                                                                                                                                                                                                                  |
| html                | `<style>` 块 + `style="…"` 属性 + Tailwind 任意值。                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| tsx / jsx / ts / js | 标签模板 `` css`…` `` / `` styled.x`…` `` / `` styled(X)`…` `` / `` createGlobalStyle`…` `` / `` keyframes`…` ``：内容按 css 解析，`${…}` 插值先替换为占位 `__expr__`，**值含占位的声明跳过**；JSX `style={{…}}` / `sx={{…}}`：括号配对取出对象体，仅识别 `key: '字符串字面量'` 与 `key: 数字` 两种形态，驼峰转 kebab，数字仅对 `padding*/margin*/gap/rowGap/columnGap/borderRadius*/borderWidth/fontSize` 视为 px；其余形态（变量、三元、展开）**不识别**（宁漏勿误，覆盖边界写进 `notes`）；整文件再跑 Tailwind 任意值。 |
| Tailwind 任意值     | 对 vue/html/tsx/jsx/ts/js 全文匹配 `(?<![\w-])(?:[a-z0-9-]+:)*(bg\|text\|border\|ring\|fill\|stroke\|p[xytrbl]?\|m[xytrbl]?\|gap\|rounded(?:-[a-z]+)?\|shadow)-\[([^\]\s]+)\](?![\w-])`：括号内是 `var(--ds-…)` → 记 tokenized 并核对引用是否存在；是颜色字面量或 px/rem 长度 → `tailwind-arbitrary-value`；其余（`calc()`、`theme()`、百分比…）忽略。                                                                                                                                                                     |

**C2 统一跳过名单**（任何规则都不报）：`0`；`1px`、`2px`（仅间距类）；`1px`（边框宽）；百分比；`auto`；`inherit|initial|unset|revert|none|normal`；`transparent`；`currentColor`；含 `calc(` / `clamp(` / `min(` / `max(` 的整个值；`var()` 的**回退值**部分（`var(--x, 2px)` 的 `2px` 不报）；`url()` 内容；负值长度；`@keyframes` 名；alpha=0 的颜色；自定义属性声明的值（仅扫 `var(--ds-*)` 引用）。

**C3 规则表**（`severity` 为默认模式；`strict=true` 时 warning→error，info 不变）

| 规则 id                    | 级      | 触发条件                                                                                                                                                                                                                                                                                                                                                                                                                                                       | `suggestion`                                                                | 测试必造的反例                          |
| -------------------------- | ------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- | --------------------------------------- |
| `unknown-token-ref`        | error   | 任意位置 `var(--ds-…)` 的变量名不在**本项目该主题导出 CSS 所定义的变量集**（= 对 `ExportService.ToCss(snapshot)` 扫 `--ds-[\w-]+\s*:`，与前端 `definedVars` 同口径）且不属 `removed` 令牌                                                                                                                                                                                                                                                                      | 编辑距离 ≤2 或同前缀的最近已定义变量；`deprecated` 令牌的 `replacedBy` 优先 | `color: var(--ds-semantic-brnd)`        |
| `removed-token-ref`        | error   | 引用的变量对应令牌 `lifecycle=removed`（用 路径→变量名 反查）                                                                                                                                                                                                                                                                                                                                                                                                  | `replacedBy` 的变量                                                         | 先把某令牌标 removed 再引用             |
| `deprecated-token-ref`     | warning | 引用的变量对应令牌 `lifecycle=deprecated`                                                                                                                                                                                                                                                                                                                                                                                                                      | `replacedBy`                                                                | 同上标 deprecated                       |
| `hardcoded-color`          | warning | 颜色属性（`color`、`background(-color)`、`border(-*)-color`、`border` 简写里的颜色、`outline(-color)`、`fill`、`stroke`、`caret-color`、`accent-color`、`text-decoration-color`、渐变函数内的颜色）里的颜色字面量（hex3/4/6/8、`rgb(a)`、`hsl(a)`、`oklch()`、17 个基础命名色 `aqua black blue fuchsia gray green lime maroon navy olive orange purple red silver teal white yellow`），与 semantic/component 色令牌的 **OKLab 欧氏距离 ≤ 0.05**（忽略 alpha） | 距离最近的令牌（规则见 §F）                                                 | `color: #7c3aed`（正好等于品牌色）      |
| `off-palette-color`        | warning | 同上但最近距离 **> 0.05**                                                                                                                                                                                                                                                                                                                                                                                                                                      | 最近令牌，**仅当距离 ≤ 0.25** 才给                                          | `color: #123456`                        |
| `hardcoded-length`         | warning | 间距类（`padding*` `margin*` `gap` `row-gap` `column-gap`）等于某 `space.*`；圆角类（`border-radius` 及四角）等于某 `radius.*`（`pill/full` 不参与）；边框宽（`border-width`、`border-*-width`、`border` 简写宽度）等于 `border.thick`；`font-size` 等于某 `size.*`（rem/em 按 16px 换算，容差 0.01px）                                                                                                                                                        | 该令牌                                                                      | `padding: 16px`（= `space.4`@default）  |
| `off-scale-length`         | warning | 同属性集，值不等于该类任何档位且 ≥3px                                                                                                                                                                                                                                                                                                                                                                                                                          | 最近档位，**仅当 \|Δ\| ≤ max(2px, 25%)** 才给                               | `padding: 13px`                         |
| `hardcoded-font-family`    | warning | `font-family` 值不含 `var(` 且**不是纯通用族**（`serif sans-serif monospace system-ui ui-*`）                                                                                                                                                                                                                                                                                                                                                                  | 含 `mono` → `var(--ds-font-mono)`，否则 `var(--ds-font-sans)`               | `font-family: "Helvetica Neue", Arial`  |
| `hardcoded-font-weight`    | info    | `font-weight` 数值或关键词（`normal`→400、`bold`→700）等于某 `weight.*`                                                                                                                                                                                                                                                                                                                                                                                        | 该令牌                                                                      | `font-weight: 600`                      |
| `hardcoded-shadow`         | warning | `box-shadow` 值不含 `var(` 且非 `none`（`text-shadow` 不检）                                                                                                                                                                                                                                                                                                                                                                                                   | 第一层（offsetY/blur/spread/inset）与 `shadow.elevation-*` 第一层距离最近者 | `box-shadow: 0 4px 12px rgba(0,0,0,.2)` |
| `hardcoded-duration`       | warning | `transition-duration` / `animation-duration` / `transition` / `animation` 里**第一个**时间值等于某 `duration.*`（不含 `-reduced`）                                                                                                                                                                                                                                                                                                                             | 该令牌                                                                      | `transition: all 200ms ease`            |
| `off-scale-duration`       | warning | 同上但不等于任何档位且 ≥50ms（`0` 与 `*-delay` 不检）                                                                                                                                                                                                                                                                                                                                                                                                          | 最近档位（\|Δ\| ≤ 100ms 才给）                                              | `transition: all 250ms ease`            |
| `outline-removed`          | warning | `outline: none\|0` 或 `outline-style: none`，且**同一规则块**没有 `box-shadow` / `border*` / `background*` 声明，且选择器不含 `:not(:focus-visible)`                                                                                                                                                                                                                                                                                                           | 焦点令牌 `var(--ds-component-focus-outline-width)`（若存在）                | `button:focus { outline: none }`        |
| `tailwind-arbitrary-value` | warning | 见 C1                                                                                                                                                                                                                                                                                                                                                                                                                                                          | `bg-[var(--ds-semantic-brand)]` 形式                                        | `class="bg-[#7c3aed] p-[13px]"`         |
| `hardcoded-z-index`        | info    | `z-index` 整数**恰好等于**某 `z-index.*` 令牌值且 ≥10                                                                                                                                                                                                                                                                                                                                                                                                          | 该令牌                                                                      | `z-index: 1000`                         |

**C4 汇总口径**（不给自拟分数）
- `declarations` = 被 C1 抽取且属于 C3 任一属性集的声明数；`tokenized` = 这些声明里使用 `var(--ds-*)` 的**值位**个数；`hardcoded` = 触发 `hardcoded-color / off-palette-color / hardcoded-length / off-scale-length / hardcoded-font-family / hardcoded-font-weight / hardcoded-shadow / hardcoded-duration / off-scale-duration / hardcoded-z-index / tailwind-arbitrary-value` 的条数（`unknown/removed/deprecated/outline-removed` 不计）。
- `tokenCoverage = tokenized ÷ (tokenized + hardcoded)`，保留 4 位小数；分母为 0 → `null`（**不编造 1.0**）。
- `errors/warnings/infos` 按最终级别（strict 升级后）计；`passed = errors == 0`。
- `findings` 排序 `file`（序数）→ `line` → `column` → `rule`；截断发生在排序之后（`truncated=true`）。`line/column` 以**输入原文**为准（Vue `<style>` 块与模板内联要加偏移），行号从 1 起。

**C5 零假警报语料**（`DesignReviewerTests` 必含，均须 0 error / 0 warning，`tokenCoverage==1` 或 `null`）：① 仅用 `var(--ds-*)` 与 C2 跳过名单值的 CSS / SCSS / Vue SFC / TSX（styled 与 style 对象）/ HTML 各一份；② **本项目 `export?format=css` 在每个主题下的产物原文**当作输入（自己的产物先过自己的门禁，自查表 #24）；③ `@font-face`、`@media (prefers-reduced-motion)`、`:where(…):focus-visible{outline:var(--ds-component-focus-outline-width,2px) solid currentColor}`、`calc(var(--ds-space-2)*2)`、`clamp(1rem,2vw,2rem)`、`transparent`、`rgba(0,0,0,0)`。**反向探针**：在语料 ① 里临时插入 `color:#7c3aed`，必须出现 `hardcoded-color`（记入 Evidence）。

### §D 预设目录（`StylePresets`，8 个；id / 名称 / 性格词 / 适用范围 / 关键词为契约，数值可按下述规则微调）

`request` 只用 `GenerationRequest` 现有字段；`themes` 默认 `["light","dark","high-contrast","compact"]`（与 `DesignProjectService.DefaultThemes` 一致，免得审计出 `theme-empty` 警告）。**数值微调规则**：某预设"生成→审计"出现 critical 时，先把 `chroma` 在 ±0.03 内调整；仍不过，再从该预设的 `themes` 去掉 `high-contrast` 并在 05-evidence 登记；**不得改 id、名称、性格词、适用范围、关键词**。

| id                   | 名称        | 一句话                                          | 性格词                          | 适用 kind          | 适用 industry     | hue | chroma | density     | typeRatio | typeBasePx | radiusBase | motionScale | request.industry | 关键词                                         |
| -------------------- | ----------- | ----------------------------------------------- | ------------------------------- | ------------------ | ----------------- | --- | ------ | ----------- | --------- | ---------- | ---------- | ----------- | ---------------- | ---------------------------------------------- |
| `admin-calm`         | 后台·沉稳   | 后台/中台管理系统的沉稳蓝，信息密度适中         | calm, professional, reliable    | console, system    | general, devtools | 255 | 0.15   | default     | 1.2       | 14         | 6          | 0.9         | general          | 后台, 管理, 中台, 运营, 报表, admin, dashboard |
| `workbench-focus`    | 工作台·专注 | 工具/工作台的低彩度紧凑风格，让内容而非界面说话 | focused, minimal, efficient     | console, product   | devtools, general | 275 | 0.09   | compact     | 1.15      | 14         | 4          | 0.8         | devtools         | 工具, 工作台, 编辑器, ide, 开发, 效率, 控制台  |
| `finance-trust`      | 金融·稳健   | 金融/支付/风控场景的稳健蓝，小圆角、克制动效    | trustworthy, serious, precise   | console, product   | finance           | 240 | 0.12   | default     | 1.15      | 14         | 4          | 0.8         | finance          | 金融, 银行, 支付, 账单, 风控, 保险, 证券       |
| `healthcare-gentle`  | 医疗·柔和   | 医疗健康的青绿柔和风格，大圆角更亲切            | gentle, clean, reassuring       | product, console   | healthcare        | 190 | 0.11   | default     | 1.25      | 16         | 10         | 1.0         | healthcare       | 医疗, 医院, 患者, 健康, 护理                   |
| `commerce-vivid`     | 电商·活力   | 电商/促销的暖橙活力风格                         | energetic, friendly, bold       | marketing, product | commerce          | 35  | 0.19   | default     | 1.25      | 16         | 8          | 1.0         | commerce         | 电商, 商城, 商品, 购物, 促销, 订单             |
| `media-bold`         | 内容·大胆   | 官网/媒体/品牌站的大胆玫红与强字阶              | bold, editorial, expressive     | brand, marketing   | media             | 350 | 0.22   | default     | 1.4       | 16         | 4          | 1.2         | media            | 媒体, 资讯, 视频, 直播, 内容, 官网, 品牌       |
| `education-friendly` | 教育·亲和   | 教育/学习产品的亲和绿，宽松密度与大圆角         | friendly, playful, approachable | product, marketing | education         | 150 | 0.17   | comfortable | 1.3       | 16         | 12         | 1.15        | education        | 教育, 学习, 课程, 学生, 儿童, 课堂             |
| `mobile-fresh`       | 移动·清新   | 移动端/H5 的清新天蓝，宽松触控密度              | fresh, light, friendly          | product, marketing | general           | 205 | 0.14   | comfortable | 1.25      | 16         | 12         | 1.0         | general          | 移动, H5, 小程序, app, 手机                    |

`StylePresets.All` 为只读 `IReadOnlyList<StylePreset>`，每项含 `Id, Name, Tagline, Tones, Kinds, Industries, Keywords, Request`（`GenerationRequest` 的深拷贝，调用方改它不得影响目录）；`Find(id)` 大小写不敏感。

**推荐打分** `PresetRecommender.Recommend(input)`（纯函数、确定性）

| 加分项   | 分值             | 条件                                                                            |
| -------- | ---------------- | ------------------------------------------------------------------------------- |
| 行业命中 | +40              | `industry`（显式）或 `DesignGenerator.InferIndustry(brief)` ∈ 预设 `Industries` |
| 用途命中 | +30              | `kind` ∈ 预设 `Kinds`                                                           |
| 关键词   | +10/个，封顶 +40 | `brief` 含预设关键词（不区分大小写子串）                                        |
| 性格词   | +10/个，封顶 +30 | `tone[]` 与预设 `Tones` 有交集（不区分大小写、完全相等）                        |
| 密度     | +15              | `density` == 预设 `request.density`                                             |

排序：分数降序，同分按 `StylePresets.All` 声明序；全部 0 分 → 按声明序取前 N，`reasons=["无匹配线索，按通用推荐排序"]`；`reasons[]` 用中文逐条说明加分来源（如 `行业匹配：金融`、`关键词：后台、管理`）。`brandColor` 给了 → 返回的 `request.seedColor` 覆盖该预设的 hue/chroma 种子（`hue` 置空，避免两种种子并存）。`InferIndustry` 由 `DesignGenerator.ResolveIndustry` 里的行业线索推断抽成 `public static String? InferIndustry(String? brief)`（无线索回 null），并公开 `KnownIndustries`（取既有 `Industries` 字典的键，**不另抄一份**）。

### §E 设计说明书 `DesignBriefBuilder` 与 `agent-rules`

**E1 章节与顺序**（固定；`identity` 恒含）：`identity → rules → colors → typography → scales → components → brand → checklist`。`components` / `brand` 在目录为空时**不开该节**（空标题比没标题更像假交付）。

| 章节         | 内容（Markdown 形态；JSON 形态为同数据的结构化对象，章节名作键）                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `identity`   | 标题 `# {name}（{code}）设计说明书 · v{version}`；引用块声明"唯一真源：设计系统「{code}」v{version}（theme={theme}，hash={contentHash}）。写 UI 前先读本文，写完用 `design_review` 对照"；要点列表：用途类型、状态、主题（含可选主题清单）、`SeedText` 前 120 字（有则列）。                                                                                                                                                                                                                                                                                                                  |
| `rules`      | 「使用规则（MUST / MUST NOT）」固定条款：① MUST 颜色一律 `var(--ds-semantic-*)` 或 `--ds-component-*`，禁字面色值，禁直接引用原语色阶 `--ds-color-*`；② MUST 间距/圆角/描边/字号/阴影/时长一律取 `--ds-space-* / --ds-radius-* / --ds-border-* / --ds-size-* / --ds-shadow-elevation-* / --ds-duration-*`；③ MUST 保持焦点可见（`--ds-component-focus-*`），不得 `outline:none` 而无替代；④ MUST 动效尊重 `prefers-reduced-motion`；⑤ MUST NOT 为单页新增令牌或自定义色值，缺令牌用 `design_edit set_token` 或提给设计维护者；⑥ 不知道令牌名时用 `design_lookup kind=nearest`，不凭感觉写值。 |
| `colors`     | 表：`令牌 \| CSS 变量 \| 值(hex) \| 对 surface-bg 对比度 \| 用途`，列出全部 `semantic.*` 颜色（对比度用 `ContrastMath.Ratio` 现算，缺 `surface-bg` 则省该列）；再一行汇总 component 色令牌条数与原语色阶规模（"7 族 × 11 阶，不直接引用"）。                                                                                                                                                                                                                                                                                                                                                  |
| `typography` | `font.sans/font.mono` 栈；`type.*` 角色表：`角色 \| 字号 \| 行高 \| 字重 \| 字距`（取 `type.*` 复合令牌与 `size.*`）。                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `scales`     | `space` / `radius` / `border` 档位表（令牌→值）、阴影 elevation 1..5（首层摘要）、`duration`（含 `-reduced`）与 `ease` 名、`breakpoint`、`z-index`。                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `components` | 每个组件一条：名称/code、分类、状态序（读 `VariantAxes`）、变体轴（`ExportService.AxisSummary`）、关键令牌数、`A11yNotes`。                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `brand`      | 字体登记（family/role/weight/license）、资产（code/kind）、页面清单（code/route）。                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `checklist`  | 交付前清单 8 条以上，每条引用本项目真存在的令牌路径（与 `design_review mode=checklist page=any` 同源）。                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |

**E2 预算算法**：每章节单独渲染成字符串；`budget = maxChars`；按章节顺序装配：`identity` 先入；其余若 `used + len ≤ budget` 则入，否则记入 `omitted` **并继续尝试后续更小的章节**；Markdown 以 `\n\n` 连接；`truncated = omitted.Count > 0`。JSON 形态按各章节序列化长度同样计预算。

**E3 `contentHash`**：SHA-256 取前 16 位小写 hex。输入 = 以下规范文本逐行拼接：`project.Code`、`project.Version`、`theme`、`DesignSystemConstants.ProjectionVersion`、令牌（按快照序）`path|tier|type|value|alias`、组件（按 code 序）`code|status|tokenRefs`、字体 `family|role|weight|license`、资产 `code|kind|svgBody`、页面 `code|route`。**不含任何时间戳**。同输入恒同值；改任一令牌值/别名必变。

**E4 同源硬约束**（AC6）：说明书里每个 `semantic.*` 颜色的 hex == `ExportService.Load` 解析出的 `Snap.ColorHex`；变量名 == `ExportService.CssVarName(path)`；**不另写第二套算色/算尺度**。

**E5 `agent-rules`**（`ExportFormats.AgentRules`，文件名 `agent-rules.md`，`text/markdown`）：可粘贴进目标项目 `AGENTS.md` / `CLAUDE.md` / `.cursorrules` 的 Markdown，章节固定为：`# 设计系统接入规则（{name} · {code} v{version}）` → 一段"唯一真源"声明 → `## 开工前`（① 有 MCP：`universal_tool {"tool":"design_context","parameters":{"project":"{code}"}}`；② 无 MCP：读随包 `DESIGN.md` 与 `tokens.css`）→ `## 写代码时`（只用 `var(--ds-*)`；不知令牌名用 `design_lookup {"kind":"nearest","value":"…"}`）→ `## 写完后`（`design_review {"files":[…],"strict":true}`，`summary.passed` 必须为 true、`tokenCoverage` 不得低于改动前；无 MCP 时人工核对 DESIGN.md「使用规则」）→ `## MUST / MUST NOT`（同 `rules` 摘要）→ `## 令牌速查`（前 12 个 `semantic.*` 颜色：变量与 hex）。验收关键子串：项目 code、版本号、`design_context`、`design_review`、`MUST NOT`、`list_tools`。

### §F 最近令牌 `NearestTokenFinder`（`design_lookup kind=nearest` 与审查建议共用）

纯函数：输入 `TokenIndex`（由 `ExportService.Snapshot` 构建，只含 `lifecycle != removed` 的**有效值**）+ `(value, propertyHint, limit)`。`TokenIndex` 同时提供「已定义 CSS 变量集」（对 `ToCss(snapshot)` 扫描）与 路径→变量名 反查（`ExportService.CssVarName`）。

| 类别        | 值解析                                                                                                                                                                                | 候选集                                                                                                                                           | `distance`                                                                     | 并列决胜                                               |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------ | ------------------------------------------------------ |
| color       | hex（`#rgb #rgba #rrggbb #rrggbbaa`）、`rgb()/rgba()`（逗号或空格语法）、`hsl()/hsla()`（先转 sRGB）、`oklch()`（`Oklch.ParseOklch`）、17 个基础命名色；`normalized` = 小写 `#rrggbb` | `semantic` + `component` 层 `type=color` 令牌（**不推荐 primitive**）                                                                            | OKLab 欧氏距离（`Oklch.OklchToOklab`），保留 4 位小数；`exact` = 距离 < 0.0005 | semantic 先于 component → `deprecated` 排后 → 路径序数 |
| length      | `^-?\d+(\.\d+)?(px\|rem\|em)$`（rem/em ×16）；无单位数字按 px                                                                                                                         | 有 `property` 线索：间距类→`space.*`、圆角类→`radius.*`、边框宽→`border.*`、`font-size`→`size.*`；无线索：四类全搜并在每条 `match` 标 `category` | \|Δpx\|                                                                        | `space` → `radius` → `size` → `border`，再路径序       |
| duration    | `^\d+(\.\d+)?(ms\|s)$`                                                                                                                                                                | `duration.*`（不含 `-reduced`）                                                                                                                  | \|Δms\|                                                                        | 路径序                                                 |
| shadow      | 取第一层 `[inset] x y blur [spread] color`                                                                                                                                            | `shadow.elevation-*`（取其 `ValueJson` 第一层）                                                                                                  | \|Δy\|+\|Δblur\|+\|Δspread\|（`inset` 不一致 +1000）                           | 路径序；`note="只比较第一层"`                          |
| font-family | 取首个族名（去引号、小写）；含 `mono` 视为等宽                                                                                                                                        | `font.sans` / `font.mono`                                                                                                                        | 首族相等 0 否则 1                                                              | 等宽优先匹配 `font.mono`                               |
| font-weight | 数值或 `normal`/`bold`                                                                                                                                                                | `weight.*`                                                                                                                                       | \|Δ\|                                                                          | 路径序                                                 |

每个 `match`：`{path,tier,cssVar,value,distance,exact,replace,category?,deprecated?}`；`replace` = `var(--ds-…)`（可直接替换原值的文本）。值解析失败 → `error="无法识别的值 x，支持：颜色（hex/rgb/hsl/oklch）、长度、时长、阴影、字体族、字重"`（不抛）。无候选 → `matches=[]` 且 `note` 说明原因（如"该项目没有 duration 令牌"）。

### §G 服务层与装配

- **`GenerationService`**（`Services/GenerationService.cs`）：`Run(Int64 projectId, GenerationRequest req, Boolean overwrite)` 把控制器 `Generate` 里内联的 `DesignGenerator.ApplyToProject → SeedComponentCatalog → SeedBrandCatalog → AuditEngine.Run` 收成一处，返回 `GenerationOutcome{Result, Components, Brand, Audit}`；控制器 `Generate` 改调它，**响应匿名对象的键与类型一字不变**（`seed industry hue tokens components variants fonts screens assets themes notes skippedProtected conflicts audit`）。重构**之前**先把现有 `Generate` 响应的 JSON 键序列记入 Evidence 作为"形状基线"，重构后用单测钉住。
- **`AgentAccess`**（`Services/AgentAccess.cs`）：文件 `{DataDirectory}/agent-access.json`，内容 `{"allowWrite":true,"updatedAt":"<ISO8601>"}`；`Get()` → `(Boolean AllowWrite, String Source, Boolean Corrupt, DateTime? UpdatedAt)`：文件不存在 → `AllowWrite=true, Source="default"`；JSON 无法解析或缺 `allowWrite`/类型不是布尔 → **`AllowWrite=false, Corrupt=true`**（fail-closed，`GET agent-access` 附 `message="agent-access.json 损坏，按只读处理；PUT 一次即可修复"`）；`Set(Boolean)` 先写临时文件再原子替换；每次 `Get()` 现读文件（不缓存，REST PUT 立即生效、重启后保持）。`DesignSystemPaths` 增 `AgentAccessFile` 属性。**只写不删**：不提供删除该文件的能力。
- **`DesignReviewService`**：`Review(projectId, theme, files|code, strict, maxFindings)`：`ExportService.Load(projectId, theme)`（主题缺失回落默认色向主题并记 `notes`）→ `TokenIndex` → `DesignReviewer`；生命周期映射来自 `TokenRepository.LoadGraph` 的节点；`Checklist(projectId, page)` 同源产出（清单条目的 `tokens[]` 逐项在 `TokenIndex` 里校验存在，不存在的条目要么省略令牌要么整条省略，**不得列出不存在的路径**，AC13）。
- **`QuickCreateService`**（`Services/QuickCreateService.cs`）：`Execute(QuickCreateInput, Boolean apply)`：① 解析 `preset`（未知 → 错误并列出全部 id）；② 有效请求 = 预设 `Request` 深拷贝 ← 显式参数非空字段覆盖；`brief` 缺省取 `description ?? name`；`themes` 缺省取预设值；③ `code` 显式给了先校验 `^[a-z0-9][a-z0-9-]{0,39}$`（服务端目前**没有**这条校验，只有前端有，必须补在这里），已存在 → `DesignConflictException("项目标识 x 已存在")`；未给则 `AllocateCode(name)`；④ `apply=false`：只 `DesignGenerator.Generate(request)`，**零写库**（项目数与 `DesignToken` 行数不变），回 `preview`；⑤ `apply=true`：`DesignProjectService.Create` → `GenerationService.Run(project.Id, request, overwrite:false)`；生成阶段抛异常 → 把刚建的项目 `Archive`（软删）并抛 `error="生成失败，已将刚建的项目 {code} 归档（软删）：{原因}"`；生成成功但 `audit.blocking=true` → 项目保留（draft），出参 `warnings=["审计存在 N 条 critical，发布前需处理"]`。
- **`AllocateCode(name)`**：`lower = name.Trim().ToLowerInvariant()` → 非 `[a-z0-9]` 一律换成 `-`、连续 `-` 合并、去首尾 `-`；结果为空或不含任何字母数字（如纯中文名）→ `ds-` + `SHA-256(UTF8(name))` 前 3 字节的 6 位小写 hex；截断到 40 字符后再去尾 `-`；直查库（`DesignProject.FindCount`，铁律 11）判重，冲突依次试 `-2 … -99`（基底按需截短保证总长 ≤40）；全部占用 → 错误。**同名同库同时刻必得同一结果（确定性）**。
- **装配**（`DesignSystemPlugin.Apply`）：按依赖序**手工 new** 出全部服务（既有 7 个 + 新增 `GenerationService`/`AgentAccess`/`DesignReviewService`/`QuickCreateService`/`DesignBriefBuilder` 等），`services.AddSingleton(instance)` 注册**同一实例**（既有的 `AddSingleton<T>()` 改成实例注册，行为不变；控制器与工具由此用同一批对象）；`DesignToolKit` 打包这些引用供工具持有；新增公开属性 `public List<IToolFunctionExtension> ToolExtensions { get; } = new();` 并在 `Apply` 里装配 8 个工具（`PluginId = ctx.Get<PluginMetadata>()?.Id ?? DesignSystemConstants.PluginId`）。**不要**注册 `UniversalTool`，**不要**改 McpCenter。
- **`ExportService` 成员可见性**：`CssVarName`、`ShouldSkipCss`、`AxisSummary`、`ParseStringArray`、`AnatomyJson`（现为私有 `static`，`CssVarName` 在 `ExportService.cs:366`、`ShouldSkipCss` 在 `:485`）提升为 `public static`（供 brief/reviewer 复用，**不改行为**）。`DesignSystem.csproj` 现**没有** `InternalsVisibleTo`，本任务**不为此改工程文件**；新增服务类一律 `public sealed`（与既有 `ExportService`/`DesignGenerator` 同风格，测试直接调用）。`AIAgent.csproj` 已带 `InternalsVisibleTo ForgeSelf.Api.Tests`，故 `ToolScopePluginIds` 可用 `internal static`。

### §H REST 契约（新增，均在控制器类级 `[Authorize("ApiKeyPolicy")]` 下；沿用 `Guard/Data/ErrorBody/RequireProject`，响应 camelCase）

| 方法 路由                        | 请求                                                               | 响应 `data`                                                                                                                                                   | 状态码                                                                                                                                                                         |
| -------------------------------- | ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `GET projects/{id:long}/brief`   | query：`theme` `format` `maxChars` `sections`（逗号分隔）          | 同 B2 出参                                                                                                                                                    | 200；项目不存在 404；`format` 非法 400                                                                                                                                         |
| `POST projects/{id:long}/review` | body：`{files?,code?,language?,theme?,strict?,maxFindings?}`       | 同 B4（code 模式）出参                                                                                                                                        | 200；输入过大/参数非法 400                                                                                                                                                     |
| `GET presets`                    | —                                                                  | `{presets:[§D 全字段]}`                                                                                                                                       | 200                                                                                                                                                                            |
| `POST presets/recommend`         | body：`{brief?,kind?,industry?,tone?,brandColor?,density?,limit?}` | 同 B6 recommend 出参                                                                                                                                          | 200                                                                                                                                                                            |
| `POST projects/quick-create`     | body：`{name,code?,kind?,description?,preset?,request?,dryRun?}`   | 同 B7 出参                                                                                                                                                    | 200；`code` 已存在 409；非法参数 400。**REST 的 `dryRun` 默认 false（人用界面直接落库）；工具的 `apply` 默认 false（保护 agent）——两者默认值方向相反是有意的**，须写进接口注释 |
| `GET agent-access`               | —                                                                  | `{allowWrite,source,corrupt,updatedAt,message?}`                                                                                                              | 200                                                                                                                                                                            |
| `PUT agent-access`               | body：`{allowWrite:boolean}`                                       | 同上（新状态）                                                                                                                                                | 200；缺字段/类型错 400                                                                                                                                                         |
| `GET agent/tools`                | —                                                                  | `{tools:[{name,kind,description,parametersSchema}],writeSwitch:{allowWrite,source,corrupt},discovery}`（`parametersSchema` 为解析后的 JSON 对象，不是字符串） | 200                                                                                                                                                                            |

`GET meta` 增量：`capabilities` 追加 `brief` `review` `presets` `quick-create` `agent`（五项）；新增 `agentTools`（8 个工具名数组，取自 `DesignToolIndex`，与 `agent/tools` 同源）。`export/formats` 因 `ExportFormats.All` 增项自动多两张卡片（前端零改动）。

### §I 版本、清单与导出格式改动明细

- **版本**：`DesignSystemConstants.ModelVersion` / `GeneratorVersion` / `ProjectionVersion` 与 `Plugins/DesignSystem/plugin.json` 的 `Version` 全部 `2.7.1 → 2.8.0`（`DesignSystemAuthTests` 与 e2e 断言三者一致）。先 `grep "2.7.1"` 区分"当前版本引用"（要改）与"历史记录"（不改，如 README 历史节、ROADMAP 已完成项）。`Plugins/AIAgent/plugin.json`：`1.7.2 → 1.7.3`。
- **`ExportFormats`**：新增常量 `Brief = "brief"`、`AgentRules = "agent-rules"`；`All` 顺序为 `[…, StardustSql, Brief, AgentRules, Bundle]`（`Bundle` 仍居末）；`Produce`：`Brief → Text($"BRIEF{suffix}.md","text/markdown",…)`、`AgentRules → Text("agent-rules.md","text/markdown",…)`；`Bundle` 追加根目录 `agent-rules.md` 与每个**非密度**主题的 `brief/BRIEF.<theme>.md`；包内 `README.md`（`Manifest`）列出二者。
- **密度主题**：`brief` 在 `compact` 这类密度主题下**不得抛错**——没有语义色就省略 `colors` 节（与 `components/brand` 空则不开节同一规则）；`agent-rules` 与主题无关。（既有 e2e 门禁"每种声明格式 × 每个主题都真能导出"会覆盖到。）
- **AIAgent**：`AIAgentService.ResolveOwnToolDefinitions` 的 `allowedPlugins` 抽成 `internal static IReadOnlyCollection<String> ToolScopePluginIds(String ownPluginId)`，返回 `{ownPluginId, "memory-system", "design-system"}`（沿用原有的字符串比较方式）；**不恢复 `UniversalTool`**；`ToolAllowlist` 裁剪语义不变。

### §J 测试清单（类 → 必含用例 → 对应 AC）；用例名沿用仓库既有的中文命名风格，实现方在 05-evidence 登记真实名字

| 测试类（`ForgeSelf.Api.Tests/Plugins/…`）                                                             | 必含用例                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  | AC                                   |
| ----------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------ |
| `DesignSystemTests/DesignAgentToolTests`（`[Collection("XCode")]`，仿 `Sems/SemsToolExtensionTests`） | 工具集**精确** 8 个名；`Id` 唯一且前缀 `design-system.tool.`；`PluginId=="design-system"`；schema 合法、`required ⊆ properties`、`properties` 键集 == §B 表；描述 ≤400；缺参错误可执行；干跑零落库（项目数、`DesignToken` 行数不变）；落库后令牌>100/组件≥10/审计无 critical；`set_token` 回读 == 期望且 `Generator=manual`；成环/悬空整批拒绝零行落库并回 `diagnostics`；`regenerate` 预览不写、`apply=true` 才写、`overwrite=false` 保护手改行；`publish` 遇 critical 拒绝且原文保留；写开关关闭拒全部写动作且读不受影响；超大结果 → 缩小范围错误；任何异常转 `{success:false}`；结果 JSON 不含绝对路径 | AC1 AC2 AC4 AC14 AC16 AC17 AC18 AC19 |
| `DesignSystemTests/McpGatewayDesignToolsTests`                                                        | 真实 `ToolRegistry` 注册 8 工具后 `GetToolDefinitions().Count==8`；缺必填被 `ValidateParameters` 拦；`McpJsonRpcHandler`+`UniversalToolForwarder` 调 `design_guide` 得 `success:true`；`list_tools {keyword:"design"}` 枚举 8 个                                                                                                                                                                                                                                                                                                                                                                          | AC2 AC3                              |
| `DesignSystemTests/DesignBriefTests`                                                                  | 默认章节齐全；`sections` 过滤；`maxChars` 生效且 `omitted` 记放不下的章节、继续尝试更小章节；`format=json` 结构化；`contentHash` 同输入稳定/改令牌后变化；无令牌项目只给 identity+引导不抛；**同源**：每个 `semantic.*` hex == `ExportService.Load` 的 `ColorHex`、变量名 == `CssVarName`；`compact` 密度主题不抛且省略 `colors`；`agent-rules` 含 §E5 全部关键子串                                                                                                                                                                                                                                       | AC5 AC6 AC21                         |
| `DesignSystemTests/NearestTokenFinderTests`                                                           | 六类各一条（色/长/时/影/族/重）；`exact`；并列决胜（semantic 先于 component、deprecated 靠后）；primitive 不被推荐；不可解析给明确错误；无候选回空 + `note`                                                                                                                                                                                                                                                                                                                                                                                                                                               | AC8                                  |
| `DesignSystemTests/DesignReviewerTests`                                                               | §C5 零假警报全语料（含**自产 CSS 全主题**）；§C3 **每条规则**反例必响（含 `unknown-token-ref` 近似名建议、removed/deprecated、Tailwind、`outline:none`）；alpha=0 与自定义属性定义不报；`strict` 升级；`tokenCoverage` 算式与 `null`；排序稳定；行号偏移（Vue `<style>` 块、模板内联、TSX 模板字面量）；超限整次拒绝；binary/empty 跳过；病态输入触发 500ms 超时 → `skipped` 不挂；checklist `any` ≥8 条且 `tokens[]` 全部真存在；**反向探针**（临时插入 `color:#7c3aed` 必响）                                                                                                                           | AC9 AC10 AC11 AC12 AC13              |
| `DesignSystemTests/PresetTests`                                                                       | 恰 8 个且 id 集合 == §D；`Request` 深拷贝不污染目录；**每个预设「生成→审计」无 critical**（4 个默认主题）；`recommend(industry=finance)` 首位 `finance-trust`；`brandColor` 覆盖种子且 `hue` 置空；无参数不报错；`InferIndustry` 对 §D 关键词命中                                                                                                                                                                                                                                                                                                                                                         | AC15                                 |
| `DesignSystemTests/QuickCreateTests`（`[Collection("XCode")]`）                                       | 干跑零写库；落库后项目/令牌/组件/审计符合 AC16；纯中文名 → 合法唯一 `ds-xxxxxx`；同名冲突自增 `-2…`；显式重名 code → 冲突；非法 code → 拒绝；生成阶段失败（注入）→ 项目被软归档且错误含 code；审计有 critical 时项目保留并带 `warnings`                                                                                                                                                                                                                                                                                                                                                                   | AC16                                 |
| `DesignSystemTests/AgentAccessTests`                                                                  | 默认允许；`Set(false)` 立即生效；**新实例**读到同状态（模拟重启）；损坏文件 → 只读 + `Corrupt`；写入不留临时文件                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          | AC18                                 |
| `DesignSystemTests/ExportProjectionTests`（改）                                                       | `ExportFormats.All` 含两项且 `Bundle` 居末；每格式×每主题（含密度主题）可导出非空；bundle 含 `agent-rules.md` 与 `brief/BRIEF.<theme>.md`；`Manifest` 列出                                                                                                                                                                                                                                                                                                                                                                                                                                                | AC21                                 |
| `DesignSystemTests/DesignSystemAuthTests`（既有，随之更新断言）                                       | 新端点类级鉴权（反射自动覆盖）；版本 2.8.0 三处一致；`meta.capabilities` 含五项新增、`meta.agentTools` == 8 个工具名                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | AC20 AC24                            |
| `DesignSystemTests/GenerateShapeTests`（新）                                                          | `Generate` 响应键集与重构前基线一字不差；存量 208 例 0 回归                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | AC22                                 |
| `AIAgent/AIAgentToolScopeTests`（新）                                                                 | `ToolScopePluginIds("ai-agent")` == {`ai-agent`,`memory-system`,`design-system`}；不含 `file-tools`/`sems` 等                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             | AC23                                 |
| e2e `design-system-agent.spec.ts`（新）                                                               | ① 直连网关 `tools/list` 恒 1 个；`list_tools keyword=design` 枚举 8 个；② `design_context` 的 `semantic.surface-bg` hex == REST `tokens/effective`；③ `design_review` 对反例报 error、对自产 CSS 0 error；④ `design_create` 干跑项目数不变 → `apply=true` 落库可读 → 审计无 critical；⑤ `PUT agent-access` 关写后 `design_edit` 被拒、读仍可，**收尾改回**；⑥ `POST projects/{id}/review` 与 `design_review` 同输入关键字段一致。数据隔离：唯一项目码；收尾只做**软归档**，不删任何数据                                                                                                                   | AC3 AC20 AC26                        |

## Implementation Steps

1. **基线**（已完成）：`--list-tests` 发现 208；系统 Temp 在测试主机下被拒（`UnauthorizedAccessException`），改用 `TMP/TEMP=.temp/ds-m1/tmp` 后 `ReleaseSnapshotTests` 17/17 通过；DesignSystem 过滤集基线 **208 总数 / 208 通过**（4.69 分钟，TMP 重定向后；规划会话实测），原始输出摘要见 05-evidence「基线」节。
2. **同源编排**：新增 `GenerationService`，控制器 `Generate` 改调它（形状不变）→ 跑存量 DesignSystem 过滤集确认 0 回归。
3. **AgentAccess**（TDD）：默认允许/持久化/损坏只读。
4. **TokenIndex + NearestTokenFinder**（TDD，纯函数，DB 只用于造快照）。
5. **DesignBriefBuilder + AgentRulesBuilder + 导出格式**（TDD）：先写"同源"断言（说明书 hex == `Load` 的 `ColorHex`）与 `maxChars`/`omitted`/hash 用例，再实现；`ExportFormats.All` 增两项并补 bundle。
6. **DesignReviewer + DesignReviewService**（TDD）：先写"生成产物自己必须过"与"每条规则反例必响"，再实现扫描器（CSS 状态机、Vue/HTML/JSX 抽取、Tailwind 任意值）与规则；结果排序稳定。
7. **StylePresets + PresetRecommender**（TDD）：8 个预设逐个"生成→审计无 critical"。
8. **QuickCreateService**（TDD）：干跑不落库、code 分配、冲突、失败软归档。
9. **工具层**：`DesignToolKit`/`DesignToolBase`/`DesignToolIndex` → 8 个工具 → `DesignSystemPlugin.Apply` 装配；写开关统一拦截；`ToolRegistry`/网关往返测试。
10. **REST 与契约**：控制器新增端点与 `meta`；版本 2.8.0（常量 + plugin.json）；`DesignSystemAuthTests` 反射断言自动覆盖。
11. **AIAgent 白名单**：抽 `ToolScopePluginIds` + 加 `design-system` + 单测；plugin.json 升 1.7.3。
12. **e2e**：`design-system-agent.spec.ts`（真实宿主；网关端口读 `FORGESELF_MCP_GATEWAY_PORT`）。
13. **文档与技能**：`design-system-consume`、`design-system-verify` 增补、README/ROADMAP/036、AGENTS §2.4 登记。
14. **验证与收口**：Build/后端过滤集（总数==发现数）/插件 web 三件/e2e/AIAgent 相关；写 05-evidence、06-review、07-final-report；日志与 TODO 收尾。**未获授权不提交 git、不推 tag、不停启用户宿主**。

## Test Plan

1. **纯函数单测**（无 DB）：`NearestTokenFinderTests`（颜色/长度/时长/阴影/字体/字重/不可解析）、`DesignBriefTests`（章节/`sections`/`maxChars`/`omitted`/JSON/hash 稳定与敏感）、`DesignReviewerTests`（零假警报样例 × 4 种语言、每条规则反例、strict、覆盖率算式、排序、上限、跳过）、`PresetTests`（目录完整性 + 推荐排序）。
2. **DB 集成单测**（`[Collection("XCode")]`、每类独立目录）：`QuickCreateTests`、`AgentAccessTests`、`DesignAgentToolTests`（8 工具往返：干跑不落库/落库/写开关/set_token/发布门禁/体积上限）、扩展 `ExportProjectionTests`。
3. **网关往返**：`McpGatewayDesignToolsTests`——真实 `ToolRegistry` + `ExtensionPointManager` 风格注册 + `McpJsonRpcHandler`/`UniversalToolForwarder`/`ListToolsToolFunction`。
4. **契约反射**：`DesignSystemAuthTests`（既有）自动覆盖新端点的类级鉴权；`清单契约四项未变` 断言版本同步。
5. **AIAgent**：`AIAgentToolScopeTests`。
6. **e2e（零 mock）**：新 spec（见 AC26），日志留证（不适用截图的部分写明理由）。
7. **反向探针**：写好"生成产物自己 0 error"后，临时往样例塞一条字面色值确认审查会响（自查表 #24），记入 Evidence。

## Verification

### Build

```bash
dotnet build Plugins/DesignSystem/DesignSystem.csproj
dotnet build Plugins/AIAgent/AIAgent.csproj
```

### Unit Test

```bash
# 本环境测试主机无法在系统 Temp 建目录 → 先指到工作区 .temp（已实证）
$env:TMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'; $env:TEMP=$env:TMP
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem" --list-tests   # 总数须 == 报告总数
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AIAgent|FullyQualifiedName~McpCenter|FullyQualifiedName~Sems" --logger "console;verbosity=normal"
```

### Integration Test

```bash
# 网关往返在 McpGatewayDesignToolsTests（进程内，真实 ToolRegistry + JSON-RPC 处理器），随上面的过滤集运行
```

### E2E

```bash
cd ForgeSelf.Web
pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system/design-system-agent.spec.ts
pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system   # 含既有巨型用例，回归
```

### Other Checks

```bash
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build   # 前端零改动，回归门禁
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-01-design-system-m1-agent-tools
git diff --stat -- Plugins/DesignSystem/web Plugins/DesignSystem/Data/Model.xml ForgeSelf.Api   # 期望为空（AC25）
```

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan。

| 时间       | 偏差点                                                                                      | 原 Plan            | 修正后                                                                                             |
| ---------- | ------------------------------------------------------------------------------------------- | ------------------ | -------------------------------------------------------------------------------------------------- |
| 2026-10-01 | 系统 Temp 目录在测试主机下被拒（`UnauthorizedAccessException`），基线 208 例中 101 例受影响 | 直接 `dotnet test` | 验证命令统一前置 `TMP/TEMP=.temp/ds-m1/tmp`（`.temp/` 已 gitignore）；非代码问题，不改任何测试夹具 |
