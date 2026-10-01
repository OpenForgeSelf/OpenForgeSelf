# Specification

> 阶段：Stage 2｜从真实 Repository Understanding 与 Intent 推导。
> 规则：① 内容与实际项目一致；② 不发明不存在的接口/类/模块（下文凡"新增"均明确标注）；③ 不确定点记录在末尾 `Unknown`。
> Task ID：PILOT-ds-m1-agent-tools

## Functional Requirements

| #    | 需求                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | 依据                                                                   |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| FR1  | **工具注册**：`DesignSystemPlugin` 公开 `List<IToolFunctionExtension> ToolExtensions`，装配 8 个工具：`design_guide` / `design_context` / `design_lookup` / `design_review` / `design_audit` / `design_presets` / `design_create` / `design_edit`。`PluginId == "design-system"`（`ctx.Get<PluginMetadata>().Id`，缺省回落 `DesignSystemConstants.PluginId`）；`Id = "design-system.tool.<后缀>"`；由 `ExtensionPointManager` 自动注册进 `ToolRegistry`。                                                                                                              | Sems 范例；AIAgent 白名单按 PluginId 匹配                              |
| FR2  | **工具基座 `DesignToolBase`**：统一 `{success:true,data,next?}` / `{success:false,error,hint?}` 封套；参数解析与缺参错误带可执行提示；任何异常转 `{success:false,error}`；硬上限保护（序列化后 >120,000 字符 → 返回错误提示缩小范围）；读工具遇 SQLITE BUSY 退避重试 ≤3 次、写动作绝不重试；调用后 `RecordUsageAsync`（宿主 provider 在**调用时**才取）。                                                                                                                                                                                                              | `SemsToolFunctionBase`；技能 design-system-verify #15/#31；TODO 输入19 |
| FR3  | **写开关 `AgentAccess`（新增）**：`agent-access.json` 落插件数据目录；默认允许写；文件损坏按只读（fail-closed）；基类统一拦截写动作（工具用 `IsWrite(args)` 声明）；REST `GET/PUT agent-access`。                                                                                                                                                                                                                                                                                                                                                                      | 数据安全铁律 10（只写不删）                                            |
| FR4  | **设计说明书 `DesignBriefBuilder`（新增）**：以 `ExportService.Snapshot` 为输入的纯函数，章节 `identity / rules / colors / typography / scales / components / brand / checklist`，Markdown 与 JSON 两种形态，`maxChars` 预算（按序装配、放不下的章节记入 `omitted` 且继续尝试更小的后续章节），`contentHash`（16 位 hex，随令牌/组件/字体内容变化）。同时是导出格式 `brief`。                                                                                                                                                                                          | ExportService.Snapshot 纯记录                                          |
| FR5  | **`agent-rules` 导出格式（新增）**：一段可粘进目标项目 AGENTS.md / CLAUDE.md / .cursorrules 的 Markdown：唯一真源声明（项目 code/name/version）、开工前调用 `design_context`、写完调用 `design_review`、无 MCP 时的文件回退（DESIGN.md / tokens.css）、MUST/MUST NOT 摘要。                                                                                                                                                                                                                                                                                            | 用户目标"唯一真源"                                                     |
| FR6  | **最近令牌 `NearestTokenFinder`（新增）**：颜色（hex/rgb/hsl/oklch）按 OKLab 距离在 semantic+component 色令牌中取最近（含 `exact`）；长度按 `space.*`/`radius.*`/`size.*`/`border.*`；时长按 `duration.*`；阴影按 `shadow.elevation-*` 首层；字体族/字重按 `font.*`/`weight.*`；输出 `cssVar` 与 `replace`（可直接替换的文本）。                                                                                                                                                                                                                                       | 复用 `Oklch`                                                           |
| FR7  | **`design_lookup`**：`kind=token`（`prefix/q/tier/type/limit/offset`，返回有效值/hex/cssVar/别名）、`component`（省略 code 列出全部；给 code 返回解剖/状态/变体轴/令牌/可达性）、`nearest`（FR6）、`export`（任一导出格式文本，带 `maxChars`/`truncated`/REST url）、`icon`（关键字检索，`includeSvg`）。                                                                                                                                                                                                                                                              | `CatalogRepository`/`ExportService`                                    |
| FR8  | **审查引擎 `DesignReviewer`（新增）+ `DesignReviewService`**：输入内联 `files[{path,content}]` 或 `code+language`；识别 css/scss/less、vue（`<style>` 与模板内联样式）、html、tsx/jsx/ts/js（`css`/`styled` 模板、`style`/`sx` 对象）以及全文件 Tailwind 任意值；规则与严重级见 Business Rules；输出 findings(file,line,rule,severity,property,found,message,suggestion{token,cssVar,value,replace}) + summary(files,declarations,tokenized,hardcoded,tokenCoverage,errors,warnings,infos,passed) + `truncated`。`design_review` 与 `POST projects/{id}/review` 同源。 | 用户目标"开发完页面审查"                                               |
| FR9  | **审查清单**：`design_review mode=checklist` 按 `page`（dashboard/list/form/detail/login/settings/workbench/landing/mobile/any）返回 `{id,severity,check,how,tokens[]}`；`tokens[]` 只列本项目里真存在的路径。                                                                                                                                                                                                                                                                                                                                                         | 自查表 #22（引用必须可核对）                                           |
| FR10 | **`design_audit`**：只读返回 summary + 未通过项（严重级序、`kind`/`onlyFailed`/`limit`）；`run=true` 重跑并落库（写动作）。                                                                                                                                                                                                                                                                                                                                                                                                                                            | `AuditEngine`/`AuditRepository`                                        |
| FR11 | **预设与推荐（新增 `StylePresets`/`PresetRecommender`）**：首批 8 个预设（id、名称、一句话、性格词、适用 kind/industry、关键词、`GenerationRequest` 参数包，仅用现有可变参数）；`recommend` 依 `kind/industry/brief/tone/density/brandColor` 打分并给理由，`brandColor` 覆盖种子色。                                                                                                                                                                                                                                                                                   | 现有生成参数                                                           |
| FR12 | **快速创建 `QuickCreateService`（新增）**：预设/参数 → 建项目（code 自动分配：ASCII slug；中文名用稳定短哈希 `ds-xxxxxx`；冲突自增）→ 生成 → 组件/品牌种子 → 审计；`dryRun` 只预演不写库；生成失败时软归档已建项目。`design_create` 与 `POST projects/quick-create` 同源。                                                                                                                                                                                                                                                                                             | `DesignProjectService.Create`                                          |
| FR13 | **`design_edit`**：`set_token`（改值或别名，标 `manual`，走 `TokenRepository.UpsertBatch`）、`regenerate`（`apply=false` 预览；`apply=true` 走 `GenerationService`，`overwrite` 默认 false）、`publish`（`ReleaseService.Create`，critical 未清拒发）。                                                                                                                                                                                                                                                                                                                | 控制器既有端点语义                                                     |
| FR14 | **`GenerationService`（新增）**：把控制器 `Generate` 内联编排（`ApplyToProject`+`SeedComponentCatalog`+`SeedBrandCatalog`+`AuditEngine.Run`）收成一处，控制器、`design_create`、`design_edit` 共用；控制器响应形状不变。                                                                                                                                                                                                                                                                                                                                               | 工具/REST 同源                                                         |
| FR15 | **REST 对等端点（新增，均在类级 `ApiKeyPolicy` 下）**：`GET projects/{id}/brief`、`POST projects/{id}/review`、`GET presets`、`POST presets/recommend`、`POST projects/quick-create`、`GET/PUT agent-access`、`GET agent/tools`；`/meta.capabilities` 增 `brief,review,presets,quick-create,agent`，并出 `agentTools`。                                                                                                                                                                                                                                                | 自查表 #16                                                             |
| FR16 | **内置 agent 接入**：`AIAgentService.ResolveOwnToolDefinitions` 的 allowedPlugins 加入 `design-system`（抽成 `internal static` 便于单测）；不恢复 `universal_tool`。                                                                                                                                                                                                                                                                                                                                                                                                   | 用户选 A                                                               |
| FR17 | **版本与文档**：版本三元组 + `plugin.json` → 2.8.0；`ExportFormats.All` 增 `brief`/`agent-rules`（bundle 收录）；新技能 `design-system-consume`（登记 AGENTS §2.4）；更新 `design-system-verify`、README、ROADMAP、`docs/02-features/036-design-system.md`。                                                                                                                                                                                                                                                                                                           | AGENTS §0 出口清单                                                     |

## Input

- 工具入参：JSON 对象，见 03-plan 的工具 schema 表（`project` 接受 code 或数字 id，仅一个非归档项目时可省略）。
- 审查入参：`files[{path,content}]`（≤200 个、总内容 ≤200KB）或 `code+language`；`path` 只是标签，**服务端不据此读盘**。
- REST 入参：同工具字段；`quick-create` 体含 `name/code?/kind?/description?/preset?/request?/dryRun?`。

## Output

- 工具：单行 JSON（camelCase、不转义中文）。`data` 因工具而异，稳定字段：`design_context` → `{project,theme,contentHash,sections,omitted,truncated,markdown|json}`；`design_review(code)` → `{summary,findings,truncated,notes}`；`design_create` → `{applied,project?,preview?,audit?,uiRoute?}`。
- 导出：`brief` → `BRIEF[-theme].md`；`agent-rules` → `agent-rules.md`；bundle 增 `agent-rules.md` 与 `brief/BRIEF.<theme>.md`。

## Business Rules

1. **同源**：brief/lookup/nearest/review 的数值一律来自 `ExportService.Load` 的解析结果（后端唯一算色/算尺度处），不另写第二套；工具与 REST 调同一服务方法。
2. **审查规则与严重级**（默认模式）：
   - error：`unknown-token-ref`（`var(--ds-*)` 不在项目 CSS 变量集）、`removed-token-ref`；
   - warning：`deprecated-token-ref`、`hardcoded-color`（与某语义/组件色 OKLab 距离 ≤0.05，给替换）、`off-palette-color`（>0.05，给最近项）、`hardcoded-length`（恰等于某档却写字面量）、`off-scale-length`、`hardcoded-font-family`、`hardcoded-shadow`、`hardcoded-duration`/`off-scale-duration`、`outline-removed`（`outline:none|0` 且同规则块无 `box-shadow`/`border`/`background` 等替代指示）、`tailwind-arbitrary-value`；
   - info：`hardcoded-font-weight`、`hardcoded-z-index`。
   - `strict=true`：warning 计为 error；`passed = (errors == 0)`。
3. **"宁少勿假"**：不报 `0 / 1px / 2px（间距）/ 百分比 / auto / inherit / initial / unset / none / transparent / currentColor / calc()/clamp()/min()/max()` 与 `var()` 回退值、`url()` 内容；无法确定的一律不报。`tokenCoverage = tokenized ÷ (tokenized + hardcoded)`，分母为 0 时为 `null`（不编造 1.0）。
4. **令牌建议只推荐 semantic/component 层**（不推荐直接引用 primitive 色阶，见 DESIGN.md 规则）；同距离时 semantic 优先。
5. **写动作集合**：`design_create(apply=true)`、`design_audit(run=true)`、`design_edit(set_token | regenerate(apply=true) | publish)`。其余为只读。写开关关闭时写动作一律拒绝。
6. **只写不删**：任何工具不删除数据；`design_create` 失败时软归档（`Status=archived`）已建项目；不提供物理删除。
7. **手改保护**：`set_token` 写入标 `Generator=manual`；`regenerate` 默认 `overwrite=false`，受保护行跳过并回报 `skippedProtected/conflicts`。
8. **预设只用现有参数**：8 个预设均通过"生成后审计无 critical"的测试；风格轴扩展属 M3。
9. **说明书章节只含真有的东西**：`components`/`brand` 目录为空时不开该节（空标题比没标题更像假交付）。

## Boundary Conditions

- 空库/无项目：`design_guide` 返回空项目列表 + 引导（先 `design_create`）；其余需项目的工具报错并列出候选。
- 项目无令牌：`design_context` 返回 identity + 明确提示"尚未生成，先 design_create/regenerate"，不抛。
- 项目存在但请求的主题不存在：回落项目默认主题并在结果 `notes` 说明。
- `maxChars` 越界：夹到 [2000, 60000]；`limit` 夹到 [1, 200]。
- 审查输入：空文件、超大文件、二进制/非文本内容、未知扩展名无 `language`：跳过并记入 `notes/skipped`，不抛。
- 正则：全部带匹配超时（500ms）；超时该文件记 `skipped`。
- 并发：读工具可与界面并行；写动作沿用既有串行/事务纪律（发布走 `ReleaseService.ProjectGates`）。
- 中文/emoji/全角项目名：`code` 自动分配走稳定哈希，不产出非法字符。

## Error Handling

| 情形                                         | 行为                                                                                                              |
| -------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| 缺必填参数/枚举越界                          | `{success:false,error:"参数 x 必填…",hint}`，`ToolRegistry.ValidateParameters` 先拦一层                           |
| 项目不存在/未指定且不唯一                    | 错误里列出可用项目（code 列表）                                                                                   |
| 写开关关闭                                   | `error` = "外部写入已被关闭（设计系统 › 接入 › 写入开关 / PUT api/design-system/agent-access）"，读类不受影响     |
| 令牌校验失败（别名成环/悬空/分层违规）       | 整批回滚零行落库，`error` + `diagnostics[]`                                                                       |
| 发布被门禁拦（critical 未清 / 版本内容不同） | 保留原始中文原因                                                                                                  |
| SQLITE BUSY                                  | 读：退避重试；写：原样报错不重试                                                                                  |
| 结果超硬上限                                 | `success:false` + 缩小范围建议                                                                                    |
| 未预期异常                                   | `success:false,error:"执行失败：<类型>: <消息>"`，`XTrace.Log.Error` 记详情；绝不向 `universal_tool` 转发层抛异常 |

## Compatibility

- **REST 契约**：现有端点/响应形状不变（含 `Generate`）；新增端点为纯增量；`ExportFormats.All` 增 2 项（消费方按 `export/formats` 现读，无写死列表；界面导出台自动出现两张新卡片）。
- **前端**：`Plugins/DesignSystem/web/**` M1 零改动；`plugin.json` frontend 四项契约不变。
- **数据库**：纯增量的**零表变更**（`Model.xml` 无 diff）；`agent-access.json` 是插件数据目录内的新文件。
- **宿主/其他插件**：仅 AIAgent 一处白名单集合（`internal static` 抽取 + 加 1 项）；McpCenter 不改。
- **版本**：2.7.1 → 2.8.0（`ModelVersion/GeneratorVersion/ProjectionVersion` 与 `plugin.json.Version` 同步；`DesignSystemAuthTests` 断言）。同版本产物变了必须升版（复现契约）。

## Non-functional Requirements

- **确定性**：brief/review/nearest/presets 无随机、无时钟参与结果（`generatedAt` 只作元数据且不进 hash）。
- **性能**：`design_context` 默认参数在 ~700 令牌项目上 <500ms（实测记录）；审查 200KB 输入 <2s。
- **安全**：不读服务器磁盘、不执行传入代码、正则超时、体积上限、写开关、REST 沿用 `ApiKeyPolicy`；工具结果不含绝对路径/密钥。
- **prompt 预算**：每个工具描述 ≤400 字符，schema 只列必要字段（内置 agent 会一并挂载 8 个）。
- **可测性**：核心（brief/nearest/reviewer/presets）为纯函数，可脱离 DB 单测；DB 相关走 `[Collection("XCode")]` 隔离目录。
- **零 mock**：e2e 直连真实宿主网关。
- **无警告**：新增代码 `dotnet build` 0 warning（生成的 Entities 存量 warning 不计）。

## Acceptance Criteria

> 闸门1 依据：用户在审阅计划后下达「Start implementation」；本清单与计划一致。逐条可测。

| #    | 判据                                                                                                                                                                                                           | 验证方式                                                    |
| ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| AC1  | 工具集**精确**等于 8 个（名见 FR1）；`Id` 全局唯一且以 `design-system.tool.` 开头；`PluginId=="design-system"`；每个 schema 是合法 object，`required ⊆ properties`                                             | xUnit `DesignAgentToolTests`（仿 `SemsToolExtensionTests`） |
| AC2  | 注册进真实 `ToolRegistry` 后 `GetToolDefinitions().Count==8`；缺必填参数被 `ValidateParameters` 拦下                                                                                                           | xUnit                                                       |
| AC3  | `McpJsonRpcHandler`+`UniversalToolForwarder`+真实 `ToolRegistry`：`tools/call universal_tool {tool:"design_guide"}` 得 `success:true`；`list_tools {keyword:"design"}` 枚举出 8 个                             | xUnit + e2e                                                 |
| AC4  | `design_guide` 返回：工具索引 8 项、三条工作流、非归档项目摘要、预设摘要、写开关状态、含 `list_tools` 的发现提示                                                                                               | xUnit                                                       |
| AC5  | `design_context`：默认 8 章；`sections` 过滤生效；`maxChars` 生效并把放不下的章记入 `omitted`；`format=json` 结构化；`contentHash` 同输入稳定、改令牌后变化；无令牌项目给引导不抛                              | xUnit `DesignBriefTests`                                    |
| AC6  | **同源**：说明书里每个 `semantic.*` 颜色 hex == `ExportService.Load` 的 `ColorHex`；变量名 == `--ds-` + 路径 `.`→`-`（`ExportService.CssVarName`）                                                             | xUnit                                                       |
| AC7  | `design_lookup`：token（过滤/分页/hex/cssVar）、component（列表/详情/不存在给候选）、export（文本+`truncated`+url）、icon（检索）行为正确                                                                      | xUnit                                                       |
| AC8  | `nearest`：颜色/长度/时长/阴影/字体/字重各自返回正确最近项与 `exact`；输入不可解析给明确错误                                                                                                                   | xUnit `NearestTokenFinderTests`                             |
| AC9  | 审查零假警报：仅用 `var(--ds-*)` 与允许值的 CSS/Vue/TSX/HTML 样例 → 0 error 0 warning，`tokenCoverage==1`；**设计系统自己的 CSS 导出被当作源码送审也 0 error**（生成产物自己必须先过）                         | xUnit `DesignReviewerTests`                                 |
| AC10 | 审查反例必响：FR8/Business Rules 中列出的每条规则各有反例触发（含 `unknown-token-ref` 的近似名建议、`removed`/`deprecated` 生命周期、Tailwind 任意值、`outline:none`）；`strict` 升级；`passed` 语义           | xUnit                                                       |
| AC11 | 审查建议正确：`hardcoded-color` 的建议令牌 == OKLab 距离最近的 semantic/component 令牌；`replace` 可直接替换；`line` 与源文件行一致；结果排序稳定                                                              | xUnit                                                       |
| AC12 | 审查输入上限（>200 文件或 >200KB）→ 拒绝且提示；工具 schema 无"读盘"语义；二进制/空文件跳过不抛                                                                                                                | xUnit                                                       |
| AC13 | `mode=checklist`：`any` ≥8 条；`tokens[]` 中每个路径都存在于项目（自查 #22）                                                                                                                                   | xUnit                                                       |
| AC14 | `design_audit`：只读返回 summary + 未通过项排序；`run=true` 后 summary 与 `GET audit` 一致；写开关关闭时 `run=true` 被拒                                                                                       | xUnit                                                       |
| AC15 | 预设：8 个、id 唯一、**每个预设生成后审计无 critical**；`recommend(industry=finance)` 首位为 finance 适用预设；`brandColor` 覆盖种子色；无参数不报错                                                           | xUnit `PresetTests`                                         |
| AC16 | `design_create`：`apply=false` 不落库（项目数与 `DesignToken` 行数不变）并返回预演摘要；`apply=true` 落库（令牌>100、组件≥10、审计无 critical）；中文名自动得合法唯一 code；重名 code 明确冲突；生成失败软归档 | xUnit `QuickCreateTests`                                    |
| AC17 | `design_edit`：`set_token` 写后回读有效值 == 期望且标 manual；成环/悬空整批拒绝零行落库并回诊断；`regenerate(apply=false)` 只预览；`publish` 遇 critical 拒绝并回原因                                          | xUnit                                                       |
| AC18 | 写开关关闭：全部写动作被拒且信息可执行，读类不受影响；REST PUT 立即生效；文件损坏 → 只读；重启（新实例）后状态保持                                                                                             | xUnit `AgentAccessTests`                                    |
| AC19 | 任何工具异常都转 `{success:false,error}`；硬上限保护生效；结果 JSON 不含绝对路径                                                                                                                               | xUnit                                                       |
| AC20 | REST 端点全部在类级 `ApiKeyPolicy` 下（`DesignSystemAuthTests` 通过）；与对应工具同输入关键字段一致（brief/review/presets/quick-create）                                                                       | xUnit + e2e                                                 |
| AC21 | `ExportFormats.All` 含 `brief`/`agent-rules`；每格式×每主题可导出非空；bundle 含 `agent-rules.md` 与 `brief/BRIEF.<theme>.md`；`Manifest` 列出                                                                 | xUnit（扩展 `ExportProjectionTests`）                       |
| AC22 | 控制器 `Generate` 响应形状不变；存量 DesignSystem 用例 0 回归                                                                                                                                                  | 基线 208 用例全绿 + 对比                                    |
| AC23 | AIAgent 工具范围含 `design-system`、`memory-system` 与自身；不含 `file-tools`/`sems` 等其他插件                                                                                                                | xUnit `AIAgentToolScopeTests`                               |
| AC24 | plugin.json / 三个版本常量均为 2.8.0；`meta.capabilities` 含新增五项；`meta.agentTools` == 工具集                                                                                                              | xUnit（既有 `清单契约四项未变` + 新断言）                   |
| AC25 | `Plugins/DesignSystem/web/**`、`Data/Model.xml`、`ForgeSelf.Api/**` 无 diff                                                                                                                                    | `git diff --stat`                                           |
| AC26 | e2e（真实宿主直连网关）：`tools/list` 恒 1 个；`list_tools` 枚举 8；`design_context` 数值 == REST `tokens/effective`；`design_review` 反例抓到 error；`design_create` 干跑不落库/落库可读；关写开关后拒绝      | Playwright 新 spec + 证据日志                               |
| AC27 | 技能 `design-system-consume` 存在且登记 AGENTS §2.4；`design-system-verify` 增补；README/ROADMAP/036 更新                                                                                                      | 文件核对                                                    |
| AC28 | `dotnet build` 0 error（新增代码 0 warning）；DesignSystem 过滤集全绿且**报告总数 == `--list-tests` 发现数**；插件 web `check/test/build` 全绿；AIAgent 相关测试全绿                                           | 命令输出                                                    |

## Unknown

| 不确定点                                                                                          | 影响                   | 处理方式                                                                                                              |
| ------------------------------------------------------------------------------------------------- | ---------------------- | --------------------------------------------------------------------------------------------------------------------- |
| 内置 agent 一次挂 22 个工具（既有 14 + 新 8）对小模型 prompt 预算的影响                           | 可能上游 400           | 实测（AIAgent 对话 e2e / 手测）；出现问题再降级为默认只挂 4 个只读工具，不预设                                        |
| e2e 中 MCP 网关端口：默认 18889 可能被用户实例占用（`mcp-center.spec:158` 有存量红）              | 新 e2e 起不来          | 读 `FORGESELF_MCP_GATEWAY_PORT`（playwright.config 已按 worktree 派生 19000–19899）；仍不可用则标 Unknown 并如实记录  |
| JSX/TSX 样式对象与 CSS-in-JS 的识别是启发式                                                       | 漏报（原则是宁漏勿误） | 结果 `notes` 写明覆盖边界；文档写"不在范围内"清单                                                                     |
| Tailwind 项目是否已引入导出的 `@theme`（`bg-brand` 等命名）                                       | 建议文本是否直接可用   | 建议同时给 `bg-[var(--ds-…)]` 形式（任何配置下都成立），`@theme` 命名作为备选说明                                     |
| 本环境测试主机无法在系统 Temp 下建目录（`UnauthorizedAccessException`，基线 208 中 101 例受影响） | 验证命令需绕过         | 验证时把 `TMP/TEMP` 指到 `.temp/ds-m1/tmp`（已实证 `ReleaseSnapshotTests` 17/17 通过）；非代码问题，如实写入 Evidence |
