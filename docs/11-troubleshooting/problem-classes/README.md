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

## 悬空检查（每次新增/更新 PC 后核对；Registry 测试自动兜底）

- [ ] 每个 PC 的 `guards` 至少一项 `status: active`，或 `path: none` 时带 `todo`（逐字可在 `TODO.md` 检索到）+ `expires`；
- [ ] 每条 case 的 `pc:` 指向存在的 PC；PC `cases:`/`recurrences:` 反向指向存在的 case 文件；
- [ ] PC `signature`/`guards` 引用的 `path` 文件真实存在；
- [ ] 各 pilot 07 报告「悬空教训清单」为空（新模板节），历史 07 的散文式「下一步建议」不强制迁移、新增必查。

## 状态语义

`open`（未守卫）→ `guarded-partial`（有实例守卫缺类级）→ `guarded`（类级守卫 active）→ `structural`（重构使错误不可表达，G4）。复发计数 = `recurrences` 长度：**≥2 强制 G2，≥3 强制评估 G4**（规则本体见技能 SKILL.md 决策逻辑，此处不复述）。
