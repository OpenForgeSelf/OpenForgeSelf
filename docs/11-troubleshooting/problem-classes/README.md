# 问题类台账（Problem Classes）

> **本目录是「这类事为什么会再犯、怎么防」的唯一真源索引**；pilot 工件回答「任务做到哪了」，此处回答「问题被归类了吗、守卫在守吗」。
> 模板与 front-matter schema 单一来源 = `docs/18-templates/postmortem/problem-class.tpl.md`；流程（何时建档、如何扫查/上浮）= 技能 `.agents/skills/problem-class-closure/`。
> 机器校验：`ForgeSelf.Api.Tests/ProblemClassRegistryTests.cs`（守卫的守卫：台账完整性/悬空引用/路径存在性）+ `scripts/check-problem-classes.ps1`（签名扫描，pre-commit/CI 触发）。

## 按 tags 检索（设计/修复前先查这张表）

| 触碰区域 | 必查 PC |
|---|---|
| 插件间契约 / ctx.Register / ctx.Get / Abstractions 接口 | PC-001, PC-005 |
| `/api/**` 鉴权、前端请求代码 | PC-002 |
| 插件持久库 / XCode 连接名 / 升级迁移 | PC-003 |
| Agent 委派 / 状态回报 / 进程生命周期 | PC-004 |
| 写/改 *.ps1 脚本、发布链路 | PC-006 |
| 本地起前后端 / 端口 / dev 环境 | PC-007 |
| 对外协议客户端（MCP 传输/第三方 API 封装）/ 端点响应 data 与前端契约 | PC-008 |
| fetch 封装/DELETE 类 204 端点/前端 JSON 解包 | PC-002, PC-009 |
| stdio 拉起外部进程（npx/uvx 等 shim）/消息帧型/服务器沉默不回 | PC-010 |
| 起子进程消费 stdout/stderr（终端命令/CLI 集成/服务探活）/中文乱码 U+FFFD | PC-011 |
| 设置页 AI 提供方编辑表单受容器裁切 / 编辑未以视口级弹窗显示 | PC-012 |
| AIAgent 执行入口 / 模型解析 / Run 恢复 / IChatCompletion（IM 网关）/ 上游空响应 | PC-013 |
| XCode 测试夹具 / 测试 ctor 连接串 / 全量联跑缺表吞行 | PC-014 |
| 流式工具调用分片消费 / UnifiedStreamChunk.DeltaToolCall / 会话历史空名调用 | PC-015 |
| 出站 `/v1/responses` 流式事件 / `response.output_text.delta` / item_id / `text part … not found` | PC-016 |
| 工具 ParametersJsonSchema / 工具注册契约 / 万能工具网关按名调用 | PC-017 |
| 工具/端点报 success 但副作用未落定（写盘/终态/写库） | PC-018 |
| flex/grid 子项尺寸约束双面：shrink 面压缩裁切/残行 + min-width 面长文本撑破挤压分栏 | PC-019 |
| 插件新增第三方 NuGet / 插件控制器构造函数 | PC-020 |
| 智能体顶栏旧名 / 主区域空注释 / 兜底路由把插件路径判成冲突 | PC-021 |
| XCode 实体直插时间戳 / CreatedAt / UpdatedAt / 0001-01-01 | PC-022 |
| 执行记录文件清单 / ToolCallsJson 提取 / 双编码 JSON / 留痕参数面 | PC-023 |

## 台账总表

| id | 标题 | status | 守卫 | 复发计数 | 档案 |
|----|------|--------|------|----------|------|
| PC-001 | 跨插件契约只进 IServiceCollection、未进共享表 | guarded | G1+G2 active（G4 提案） | 1 | [PC-001](PC-001-cross-plugin-contract-shared-table.md) |
| PC-002 | 后端加鉴权、前端裸 fetch 未同步带 token | guarded | G3×2 active + e2e 实例 | 1 | [PC-002](PC-002-auth-frontend-bare-fetch.md) |
| PC-003 | 插件库连接名未登记 PluginDbs → 升级丢数据 | guarded | G2 active | 1 | [PC-003](PC-003-plugin-db-unregistered-data-loss.md) |
| PC-004 | Agent 成功回报 ≠ 进程真实退出（**候选，待定性**） | open | none（TODO+期限 2026-12-31） | 0 | [PC-004](PC-004-agent-report-vs-process-terminal-state.md) |
| PC-005 | 插件执行体构造期 eager 解析宿主 provider | guarded | G1+G2 active（PILOT-062 修复批） | 1（逃逸：10-08 同族未建 PC） | [PC-005](PC-005-plugin-eager-provider-resolve.md) |
| PC-006 | PowerShell 5.1 编码/BOM/码页（**范例 PC**） | guarded | G2 active（RepositoryScriptTests） | 0（守卫后未复发） | [PC-006](PC-006-powershell-51-encoding.md) |
| PC-007 | 手敲命令起前后端静默陷阱（**范例 PC**） | structural | G4 active（dev-stack.ps1） | 0 | [PC-007](PC-007-manual-dev-launch-silent-trap.md) |
| PC-008 | 外部 MCP 协议客户端侧合规缺失（clientInfo/initialized/会话头/端点 data 契约） | guarded | G1+G2×3+G3 active（修复批 2026-10-17） | 0 | [PC-008](PC-008-mcp-external-client-protocol.md) |
| PC-009 | HTTP 封装成功路径无条件 res.json()，204/空体解析崩溃被 catch 吞成无操作 | guarded-partial | G1+G3 active，G2 planned（TODO，expires 2026-12-31） | 1 | [PC-009](PC-009-http-204-empty-body-json-parse.md) |
| PC-010 | stdio 外部进程启动与消息帧协商脱节（Windows shim/双帧协商/null params 沉默） | guarded | G1+G2×3+G3 active + e2e 零容忍用例（修复批 2026-10-18） | 0 | [PC-010](PC-010-mcp-stdio-launch-and-framing.md) |
| PC-011 | 子进程输出固定码页解码（GBK↔UTF-8 双向毁；裁决器单点 ProcessOutputText） | guarded | G1×2+G2+G3 active（修复批 2026-10-22，PILOT-073） | 0 | [PC-011](PC-011-process-output-fixed-codepage-decode.md) |
| PC-012 | 设置容器内的提供方编辑未使用视口级弹窗 | guarded | G1 active | 0 | [PC-012](PC-012-settings-provider-edit-container-clipping.md) |
| PC-013 | 模型选择静默降级 + 上游空响应当成功 | guarded | G1+G2×2 active | 1（复发：10-30 IM 网关入口未接链；逃逸：041 门禁未覆盖空流） | [PC-013](PC-013-silent-model-fallback-empty-response.md) |
| PC-014 | 测试 ctor 手搓 DAL.AddConnStr 换库（Reset 换 Database + 会话残留 → 吞行/吞表） | guarded | G2 active（XCodeTestFixtureIronRuleTests） | 0 | [PC-014](PC-014-test-ctor-rebind-connstr.md) |
| PC-015 | 流式工具调用分片被逐片当完整调用（空名调用毒化会话） | guarded | G1+G3 active（修复批 2026-10-29） | 0 | [PC-015](PC-015-maf-stream-toolcall-fragment.md) |
| PC-016 | 出站 Open Responses 文本增量缺 item_id / 未先登记 text part | guarded | G2 active | 1 | [PC-016](PC-016-responses-outbound-delta-item-id.md) |
| PC-017 | 工具 ParametersJsonSchema 重复键少闭合括号，该工具经网关任何调用全挂 | guarded | G2 active（ToolSchemaJsonGuardTests） | 0 | [PC-017](PC-017-tool-schema-json-duplicate-key.md) |
| PC-018 | 执行体报 success 但真实副作用未落定（write_file 零落盘） | guarded | G2 active（FileToolsWritePersistTests） | 0 | [PC-018](PC-018-tool-success-without-side-effect.md) |
| PC-019 | flex/grid 子项尺寸约束双面：shrink 面（缺 flex-shrink:0 压扁/残行；max-height 无 overflow 叠画）+ min-width 面（超宽内容缺 min-width:0 撑破分栏，短种子走查不可见） | guarded | G2 active ×2（ai-agent AC-9 几何锁 + im-gateway 压力几何锁） | 1（10-31 IM 网关 min-width 面复发；先发例 10-26 Home 建档前） | [PC-019](PC-019-flex-shrink-column-compression.md) |
| PC-020 | 插件依赖在宿主加载期解析不到（NuGet 未进 bin，或控制器构造注入宿主契约） | guarded | G2 active（PluginLoadDependencyGuardTests） | 2 | [PC-020](PC-020-plugin-dependency-unresolved.md) |
| PC-021 | 智能体入口顶栏仍用旧名，或主区域因兜底路由被判冲突而空白 | guarded | G1×2 active | 1 | [PC-021](PC-021-agent-entry-blank-or-old-name.md) |
| PC-022 | XCode 实体直插漏赋时间戳字段，落库为 0001-01-01 缺省值 | guarded | G1 active（MemoryTimestampGuardTests） | 0 | [PC-022](PC-022-xcode-entity-timestamp-default.md) |
| PC-023 | 执行记录 filesChanged 断链：自动留痕无参数 + 双编码 JSON 裸正则永不命中 | guarded | G1 active（BuiltInFilesChangedPersistenceTests） | 0 | [PC-023](PC-023-todo-fileschanged-persistence-loss.md) |
| PC-024 | 前端轮询面错误处理缺失：静默吞 404（终态错误继续轮询）/ 无 catch 裸抛反复 toast | guarded | G2 active（e2e dead-session restored：banner 一次 + 3s 零新增 404） | 0 | [PC-024](PC-024-frontend-poll-error-handling.md) |

## 悬空检查（每次新增/更新 PC 后核对；Registry 测试自动兜底）

- [ ] 每个 PC 的 `guards` 至少一项 `status: active`，或 `path: none` 时带 `todo`（逐字可在 `TODO.md` 检索到）+ `expires`；
- [ ] 每条 case 的 `pc:` 指向存在的 PC；PC `cases:`/`recurrences:` 反向指向存在的 case 文件；
- [ ] PC `signature`/`guards` 引用的 `path` 文件真实存在；
- [ ] 各 pilot 07 报告「悬空教训清单」为空（新模板节），历史 07 的散文式「下一步建议」不强制迁移、新增必查。

## 状态语义

`open`（未守卫）→ `guarded-partial`（有实例守卫缺类级）→ `guarded`（类级守卫 active）→ `structural`（重构使错误不可表达，G4）。复发计数 = `recurrences` 长度：**≥2 强制 G2，≥3 强制评估 G4**（规则本体见技能 SKILL.md 决策逻辑，此处不复述）。
