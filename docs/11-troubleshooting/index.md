# 11-troubleshooting — 故障知识库

> 状态：在役（2026-10-17 升级为「问题类闭环」知识库）
> 最后更新：2026-10-17

本目录三层分工（同一事实只放一处）：
- **[problem-classes/](problem-classes/README.md)** — 问题类台账：不变量 + 特征签名 + canonical fix + 守卫（「这类事为什么再犯、怎么防」的唯一真源，设计/修复前先查tags检索表）
- **[cases/](cases/README.md)** — 事件档案：一事一档，只讲发生了什么
- 本页 **现象速查表** — 报错关键字入口，每行必须指向 case/PC 或标注散例，不再独立承载根因

## 现象速查（报错 → 去哪查）

| 现象 / 报错关键字 | 去处 |
|------|------|
| 插件页面/远程视图空态、`/api/**` 全 401 | case [2026-09-24 裸 fetch](cases/2026-09-24-plugin-auth-frontend-bare-fetch.md) → [PC-002](problem-classes/PC-002-auth-frontend-bare-fetch.md) |
| 宿主升级后插件数据「全丢」、新版本目录出现小空库 | case [2026-10-09 PluginDbs](cases/2026-10-09-agenthub-db-unregistered-data-loss.md) → [PC-003](problem-classes/PC-003-plugin-db-unregistered-data-loss.md) |
| 「工作流服务没激活」、兄弟插件 `ctx.Get<T>()` 返回 null | case [2026-10-10 契约共享表](cases/2026-10-10-workflow-engine-contract-not-registered.md) → [PC-001](problem-classes/PC-001-cross-plugin-contract-shared-table.md) |
| `No service for type 'IServiceScopeFactory' has been registered`、工具恒空 | case [2026-10-17 list_todos](cases/2026-10-17-gateway-list-todos-scope.md) → [PC-005](problem-classes/PC-005-plugin-eager-provider-resolve.md) |
| RELEASE-NOTES/日志中文乱码、脚本解析期崩溃、签名必失败 | [PC-006](problem-classes/PC-006-powershell-51-encoding.md)（真源 AGENTS §2.3 + §B6） |
| 手敲命令起宿主 exit 0 无端口 / vite 崩 | [PC-007](problem-classes/PC-007-manual-dev-launch-silent-trap.md)（用 `scripts/dev-stack.ps1`） |
| Agent 委派「成功」但进程未退出/状态不一致 | [PC-004](problem-classes/PC-004-agent-report-vs-process-terminal-state.md)（候选，待定性） |
| 外部 MCP 握手 `-32602 Invalid request parameters`、新增连接/测试失败、握手成功仍报「测试失败」、编辑不回显 | case [2026-10-17 外部 MCP 客户端合规](cases/2026-10-17-mcp-external-client-protocol.md) → [PC-008](problem-classes/PC-008-mcp-external-client-protocol.md) |
| 外部 MCP stdio 配 `npx` 报 400「无法启动外部 MCP 进程…系统找不到指定的文件」，或进程起了但 tools/list 永远超时（服务器沉默） | case [2026-10-18 npx 三层全灭](cases/2026-10-18-mcp-stdio-npx-windows-shim.md) → [PC-010](problem-classes/PC-010-mcp-stdio-launch-and-framing.md) |
| 子进程命令输出中文乱码（全 `?`/U+FFFD）、`run_terminal_command` 返回坏文本而 `read_file` 正常 | case [2026-10-22 固定码页解码](cases/2026-10-22-process-output-fixed-codepage-decode.md) → [PC-011](problem-classes/PC-011-process-output-fixed-codepage-decode.md) |
| 设置页 AI 提供方编辑内容在容器内显示、表单看不全 | case [2026-10-24 提供方编辑容器裁切](cases/2026-10-24-settings-provider-edit-container-clipping.md) → [PC-012](problem-classes/PC-012-settings-provider-edit-container-clipping.md) |
| `SocketException (10013)`（`HttpRequestException …以一种访问权限不允许的方式…(host:80)`，直连偶发失败重试即好） | 散例：本机**动态端口段与保留端口段重叠**——`netsh int ipv4 show dynamicport tcp` 实测 10000–64999，而 `show excludedportrange protocol=tcp` 有 38755–39773、50000–50059（Hyper-V/WinNAT）；OS 随机选中保留端口发起 connect 即本地拒绝，**不是服务器/代理/防火墙返回**。处置：管理员 `net stop winnat && net start winnat` 或重启；反复发则 `netsh int ipv4 set dynamicport tcp start=40000 num=9900` 挪开重叠段（2026-10-18 实测 gpustack:80 场景定位） |
| `dotnet build` CS2012（文件被占用） | 散例：运行实例占用 DLL → 构建到独立输出目录（无守卫需求） |
| 前端 build 预存类型错误（T032） | 散例：非本轮回归，独立修复 |
| `navigator.clipboard` 只读致 vitest 偶发 | 散例：环境缺陷，`Object.defineProperty`/`vi.stubGlobal` |
| `ChatRecordRealLLMTests` 偶发红 | 散例：测试隔离性，TODO 已知 |

新报错排查后：能归入既有 PC 的补 case；成类的走技能 `problem-class-closure`（P2 归类）；确属一次性环境杂音才允许留在本表当散例。
