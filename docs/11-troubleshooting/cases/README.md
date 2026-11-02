# 案例档案（Cases）— 时间线索引

> Case 只讲「这一次发生了什么」（一事一档）；规则/修法/守卫一律引用对应 PC，不复述。模板 = `docs/18-templates/postmortem/case.tpl.md`，新建走 `scripts/new-problem-case.ps1` 或按技能 `problem-class-closure` P6 手工建。

| case id | 日期 | 现象一句话 | PC | 证据 | 档案 |
|---------|------|------------|----|------|------|
| 2026-09-24-plugin-auth-frontend-bare-fetch | 2026-09-24 | 鉴权后插件管理页裸 fetch 全 401 空态 | [PC-002](../problem-classes/PC-002-auth-frontend-bare-fetch.md) | Verified | [链接](2026-09-24-plugin-auth-frontend-bare-fetch.md) |
| 2026-10-09-agenthub-db-unregistered-data-loss | 2026-10-09 | 未登记 PluginDbs，升级后登记数据"全丢" | [PC-003](../problem-classes/PC-003-plugin-db-unregistered-data-loss.md) | Verified | [链接](2026-10-09-agenthub-db-unregistered-data-loss.md) |
| 2026-10-10-workflow-engine-contract-not-registered | 2026-10-10 | 工作流工具「服务没激活」= 契约没进共享表 | [PC-001](../problem-classes/PC-001-cross-plugin-contract-shared-table.md) | Verified | [链接](2026-10-10-workflow-engine-contract-not-registered.md) |
| 2026-10-17-gateway-list-todos-scope | 2026-10-17 | 通用网关 list_todos 报 IServiceScopeFactory 未注册 | [PC-005](../problem-classes/PC-005-plugin-eager-provider-resolve.md) | Verified | [链接](2026-10-17-gateway-list-todos-scope.md) |
| 2026-10-17-mcp-external-client-protocol | 2026-10-17 | 外部 MCP 握手 -32602（缺 clientInfo）+ 编辑不回显 + /test 契约脱节 | [PC-008](../problem-classes/PC-008-mcp-external-client-protocol.md) | Verified | [链接](2026-10-17-mcp-external-client-protocol.md) |
| 2026-10-18-host-request-204-empty-body | 2026-10-18 | 删除 AI 提供方后端已删 204，UI 卡片不消失无提示（request.ts 裸 res.json()） | [PC-009](../problem-classes/PC-009-http-204-empty-body-json-parse.md) | Verified | [链接](2026-10-18-host-request-204-empty-body.md) |
| 2026-10-18-mcp-stdio-npx-windows-shim | 2026-10-18 | npx 配外部 MCP 报 400「系统找不到指定的文件」，修复后又暴露帧型沉默与 null params 沉默（三层） | [PC-010](../problem-classes/PC-010-mcp-stdio-launch-and-framing.md) | Verified | [链接](2026-10-18-mcp-stdio-npx-windows-shim.md) |
| 2026-10-22-process-output-fixed-codepage-decode | 2026-10-22 | run_terminal_command 执行 pwsh 读中文文件返回全 U+FFFD 乱码（同类扫查 8 站点） | [PC-011](../problem-classes/PC-011-process-output-fixed-codepage-decode.md) | Verified | [链接](2026-10-22-process-output-fixed-codepage-decode.md) |
| 2026-10-24-settings-provider-edit-container-clipping | 2026-10-24 | AI 提供方编辑表单受设置容器约束而看不全 | [PC-012](../problem-classes/PC-012-settings-provider-edit-container-clipping.md) | Verified | [链接](2026-10-24-settings-provider-edit-container-clipping.md) |
| 2026-10-25-builtin-agent-silent-model-fallback | 2026-10-25 | 待办一键交给内置 Agent 后 Run 立即 Stuck（硬编码模型不存在 + 空响应当成功） | [PC-013](../problem-classes/PC-013-silent-model-fallback-empty-response.md) | Verified | [链接](2026-10-25-builtin-agent-silent-model-fallback.md) |
| 2026-10-30-im-gateway-unbound-model-404 | 2026-10-30 | IM 网关未绑定模型 → 陈旧 AiConfig.ModelName（qwythos-9b-v2）直发上游 404，企微回复全失败（PC-013 入口级逃逸二次复发） | [PC-013](../problem-classes/PC-013-silent-model-fallback-empty-response.md) | Verified | [链接](2026-10-30-im-gateway-unbound-model-404.md) |
| 2026-10-29-test-ctor-rebind-connstr | 2026-10-29 | 测试 ctor 手搓 AddConnStr 换库 → 全量联跑吞行/吞表/写走别处（PILOT-051 74 红归因，净修复 22 红） | [PC-014](../problem-classes/PC-014-test-ctor-rebind-connstr.md) | Verified | [链接](2026-10-29-test-ctor-rebind-connstr.md) |
| 2026-10-29-aiagent-maf-empty-function-name | 2026-10-29 | 2.4.18 MAF 聊天路径流式工具调用分片逐片成调用，空名空壳毒化会话（同会话每条消息 1~2ms 必败） | [PC-015](../problem-classes/PC-015-maf-stream-toolcall-fragment.md) | Verified | [链接](2026-10-29-aiagent-maf-empty-function-name.md) |
| 2026-10-30-responses-outbound-delta-item-id | 2026-10-30 | Open Responses 流首条空 delta 无 item_id，客户端 zod 拒绝整段 | [PC-016](../problem-classes/PC-016-responses-outbound-delta-item-id.md) | Verified | [链接](2026-10-30-responses-outbound-delta-item-id.md) |
| 2026-10-30-responses-text-part-not-registered | 2026-10-30 | item_id 已发出，delta 前没有 output_item.added，客户端报 text part not found | [PC-016](../problem-classes/PC-016-responses-outbound-delta-item-id.md) | Verified | [链接](2026-10-30-responses-text-part-not-registered.md) |
| 2026-10-30-tool-schema-json-duplicate-key | 2026-10-30 | 万能工具 write_file 三连挂（schema 重复键少闭合括号，`Expected depth to be zero…Line 26`），任何参数都一样 | [PC-017](../problem-classes/PC-017-tool-schema-json-duplicate-key.md) | Verified | [链接](2026-10-30-tool-schema-json-duplicate-key.md) |
| 2026-10-30-tool-success-without-side-effect | 2026-10-30 | write_file 报 success 但磁盘无文件（坏合并修复批弄丢 WriteAllText，被 PC-017 网关错误遮蔽） | [PC-018](../problem-classes/PC-018-tool-success-without-side-effect.md) | Verified | [链接](2026-10-30-tool-success-without-side-effect.md) |
| 2026-10-31-aiagent-flex-shrink-compression | 2026-10-31 | AIAgent 执行记录步骤时间线被 flex 压缩裁切成 43px 残行（shrink 面） | [PC-019](../problem-classes/PC-019-flex-shrink-column-compression.md) | Verified | [链接](2026-10-31-aiagent-flex-shrink-compression.md) |
| 2026-10-31-imgateway-minwidth-overflow | 2026-10-31 | IM 网关 300px 侧栏被 60 字预览 min-content 撑到 ~788px，主栏挤到 ~460px（min-width 面复发，PC-019 扩面） | [PC-019](../problem-classes/PC-019-flex-shrink-column-compression.md) | Verified | [链接](2026-10-31-imgateway-minwidth-overflow.md) |
| 2026-10-09-mcpcenter-nuget-missing | 2026-10-09 | 升级包缺 ModelContextProtocol DLL，宿主启动即崩 | [PC-020](../problem-classes/PC-020-plugin-dependency-unresolved.md) | Verified | [链接](2026-10-09-mcpcenter-nuget-missing.md) |
| 2026-11-02-browseruse-and-approval-di | 2026-11-02 | 浏览器插件缺 Playwright；审批/项目/成本控制器构造注入宿主契约 | [PC-020](../problem-classes/PC-020-plugin-dependency-unresolved.md) | Verified | [链接](2026-11-02-browseruse-and-approval-di.md) |
| 2026-11-02-agent-page-blank-old-name | 2026-11-02 | 智能体页顶栏仍是旧名，主区域空注释；兜底路由曾把声明路径判成冲突 | [PC-021](../problem-classes/PC-021-agent-entry-blank-or-old-name.md) | Verified | [链接](2026-11-02-agent-page-blank-old-name.md) |

**登记规则**：新 case 文件名 = `YYYY-MM-DD-<slug>.md`（id 与文件名逐字一致）；必须同时出现在本表 + 对应 PC §5 案例列表；PC-004（候选）定性后补档。未建 PC 的孤立缺陷事件不得只写 case——先归类（Registry 测试拒绝悬空 case）。
