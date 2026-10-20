---
id: PC-008
title: 外部 MCP 协议客户端侧合规缺失：initialize 缺必填 clientInfo、不发 initialized 通知、会话头不回传、/test 响应契约与前端脱节
tags: [mcp,protocol,client-contract,api-contract]
invariant: 作为协议客户端对外发起请求时，载荷与后续行为必须满足规范强制项——MCP initialize 必带 clientInfo{name,version}、收到 result 后必发 notifications/initialized、服务器协商出的会话标识（Mcp-Session-Id/协议版本）必须在后续请求回传；且端点响应 data 信封必须覆盖前端类型契约消费的全部字段（apiPost 只解包 data，信封 message 到不了页面）
status: guarded
first_seen: 2026-10-17
recurrences: []
cases: [2026-10-17-mcp-external-client-protocol]
signature:
  - {id: S1, kind: grep, glob: "Plugins/McpCenter/Services/McpClient/*.cs", regex: "new\\s*\\{\\s*protocolVersion", expect: no-match}
  - {id: S2, kind: test, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientProtocolGuardTests.cs}
  - {id: S3, kind: test, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalInitializeClientInfoTests.cs}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalInitializeClientInfoTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientProtocolGuardTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpStreamableSessionHeaderTests.cs, status: active}
  - {level: G2, path: ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpClientIntegrationTests.cs, status: active}
  - {level: G3, path: scripts/check-problem-classes.ps1, status: active}
lens:
  - 以协议客户端身份对外发请求时，是否逐字段核对了规范**必填项**（如 MCP initialize 的 clientInfo），而不是"对着宽松 mock 能跑通就抄最小对象"？
  - 握手之后规范要求的 MUST 通知（notifications/initialized）与服务器协商出的会话标识（Mcp-Session-Id / Mcp-Protocol-Version）是否在后续每个请求回传？
  - 端点响应 data 是否覆盖前端类型契约消费的全部字段？经 http.ts 解包后前端还拿得到 message/success 吗（信封字段 ≠ data 字段）？
  - 多条传输/多个 handler 是否共用同一个 canonical 构造入口，还是各自手搓载荷？
---
# PC-008 外部 MCP 协议客户端侧合规缺失

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

协议客户端的对外兼容性由**规范强制项**决定，不由"某个宽松服务器能跑通"决定。MCP 规范 2025-06-18 把 `clientInfo{name,version}` 列为 initialize params 必填、把 `notifications/initialized` 列为客户端收到 result 后的 MUST 通知；严格服务器（DeepWiki 等）直接回 `-32602` 拒绝握手。

认知缺口（为什么 Agent/人会犯）：
- **宽松靶子诱导**：项目自带 mock MCP 服务器不校验 clientInfo，单测/集成/e2e 全绿仍不合规——"测试通过"给了虚假安全感；
- **抄实现不抄规范**：写传输层时参照了能跑通的最小载荷，没有回到 spec 的 Required 字段清单；
- **信封/载荷两层脱节**：`http.ts` 统一解包 `data`，端点把结论放信封 `message`、data 只放 `{id,ok}`，前端类型声明 `{success,message,durationMs}` 无人核对——vite build 不查运行时形状，vue-tsc 只查声明侧。

## 2 特征签名与扫描方法

- **S1（grep，G3）**：三传输源文件内出现手写 initialize 载荷 `new { protocolVersion... }` 即违规——initialize 参数唯一合法构造入口是 `McpClientProtocol.BuildInitializeParams`；由 `scripts/check-problem-classes.ps1`（pre-commit 挂接）执行。
- **S2/S3（test）**：守卫与红灯回归文件存在性检查；实际断言由 `dotnet test` 承担。
- 已知盲区：grep 只覆盖 `Services/McpClient/*.cs` 传输层；若未来在别处新写 MCP 客户端（如网关转发侧），glob 需扩面——lens 第 4 问负责在 Spec 阶段兜住。掩码往返（前端把脱敏展示串回填提交）属同 case 衍生面，由 `RejectMaskedValue` + 前端基线守卫，未单列 grep 签名（形态多样，靠 G2 测试枚举）。

## 3 Canonical fix

**单点构造 + 尽力而为通知 + 会话头回传**，参考实现：
- `Plugins/McpCenter/Services/McpClient/McpClientProtocol.cs` —— `BuildInitializeParams(protocolVersion)` 一处定义 clientInfo（名称/版本），三传输一律引用，禁止回退手搓（S1 封死）；
- 各传输 `InitializeAsync`：拿到 result 后发 `NotifyAsync("notifications/initialized")`（无 id JSON-RPC 通知）；通知失败仅 Warn 不阻断（宽松服务器兼容）；
- Streamable HTTP：`CaptureSessionId` 记响应头 `Mcp-Session-Id` + 协商版本，`ApplyProtocolHeaders` 在后续请求（含通知与 SSE GET）回传；无状态服务器（无该头）自然不携带；
- 端点契约：响应 data 必须自带前端消费字段（`/test` 的 `{success,message,durationMs}` 与信封冗余同发），失败分支保持 4xx + 真实 message（前端 throw → error toast 路径已验证可用）。

被否方案：① 前端改读信封 message——需动 `http.ts` 统一解包架构，影响所有端点，不成比例；② 不发 initialized 通知"看服务器是否强制"——规范 MUST，且严格服务器会挂半开会话。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalInitializeClientInfoTests.cs` | dotnet test | 单实例：initialize 首发载荷 + 后续通知（node mock 捕获全部请求体） |
| G2 | `.../McpClientProtocolGuardTests.cs` | dotnet test | 形状直测（BuildInitializeParams 输出）+ 三传输源码扫描 + 掩码拒绝枚举 |
| G2 | `.../McpStreamableSessionHeaderTests.cs` | dotnet test | 会话头回传/无状态不携带两分支 |
| G2 | `.../McpClientIntegrationTests.cs`（TestEndpoint_* 两测） | dotnet test | /test 端点成功/失败两分支 data 形状锁 |
| G3 | `scripts/check-problem-classes.ps1` S1 签名（pre-commit） | git commit | 手写 initialize 载荷回退 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| [2026-10-17-mcp-external-client-protocol](../cases/2026-10-17-mcp-external-client-protocol.md) | 2026-10-17 | 真实服务器新增连接/测试全挂（-32602）+ 编辑不回显 + 掩码险覆盖密钥 + /test 成功也误报「测试失败」 | N/A 首次（本类建类案） |
