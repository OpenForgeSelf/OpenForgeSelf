# Specification

> Task ID：`2026-11-19-oar-runtime-p1`

## Functional Requirements

| ID | 要求 |
| --- | --- |
| FR1 | 新增 `IAgentRuntime` 与配套 DTO：`AgentDescriptor`、`AgentTaskRequest`、`AgentTaskHandle`、`AgentTaskSnapshot`、`AgentTaskEvent`、`AgentInput`、`AgentControlResult`（命名对齐架构真源 §5；可小幅对齐仓库词表） |
| FR2 | 契约与 DTO **零**引用 `@botiverse/oar` / Worker TS 类型 |
| FR3 | `OarWorkerClient`：可配置 Node 可执行文件与 Worker 入口；延迟 Start；JSON-RPC 2.0 JSONL；请求 id；超时；通知 `session.event` 回调 |
| FR4 | Client 至少暴露：Hello、ListRuntimes、OpenSession、Prompt、ControlAbort、DisposeSession、Dispose（杀进程树） |
| FR5 | Node&lt;24 / 入口缺失 / 启动失败 → `IsAvailable=false` + 可解释原因；不抛未文档化导致宿主崩溃 |
| FR6 | 本批不 Register `IAgentRuntime`；不改 CliTransport / IAgentDelegation |

## Input / Output

- In：P0 Worker `dist/index.js` 或 `src/index.ts`+tsx；Node≥24  
- Out：Abstractions 契约源码；AgentHub `OarWorkerClient`；过滤测试；032 回写

## Business Rules

1. stdout 仅协议；stderr 日志（Client 可读但不当协议）。  
2. 控制结果形状与 Worker `controlOutcomeSafe` 对齐：Accepted/Code/Reason 映射到 `AgentControlResult`。  
3. SessionKey 为 Worker 返回的宿主侧 key（P2 再持久化映射）。

## Boundary Conditions

| 边界 | 行为 |
| --- | --- |
| 无 Node | Available=false |
| Worker 崩溃 | 待处理请求失败；Dispose 幂等 |
| 并发请求 | 多路 id 匹配；单 stdin 写锁 |

## Error Handling

- JSON-RPC error → 抛/返回带 code+message 的明确异常类型（Client 层）；契约层（P3）再映射 Outcome。  
- Serialize 使用 `ForgeJson.Default`。

## Compatibility

- 不修改 `IAgentDelegation` 签名。  
- 不修改现有 Cli 测试预期。

## Non-functional

- Dispose 必须终止进程树（对齐 ProcessRunner 安全铁律：ArgumentList 数组、无 shell 拼接）。

## Failure Modes

| 模式 | 应对 |
| --- | --- |
| 契约泄漏 OAR 类型 | 源码守卫：Abstractions 禁止 botiverse/oar 字符串（测试 Grep 或简单 assert） |
| JSON `\uXXXX` 中文 | ForgeJson |
| 测试在 Node22 假红 | 无 Node24 则 Skip，并写明原因 |

## Interaction Design

**无界面交互**。本批经 stdio / 契约操作：

| 通道 | 入参 | 成功 | 错误 |
| --- | --- | --- | --- |
| `OarWorkerClient.HelloAsync` | — | protocolVersion=1 | 不可用原因 |
| `ListRuntimesAsync` | — | 含 scripted | RPC error |
| `OpenSessionAsync` | runtimeId=scripted, cwd | sessionKey | not installed / unknown |
| `PromptAsync` | sessionKey, input | outcome | session missing |

## Acceptance Criteria

- [x] AC1：`IAgentRuntime` 等类型在 Abstractions 可编译，无 OAR 依赖  
- [x] AC2：`OarWorkerClient` 对真实 Worker：hello + list 含 scripted  
- [x] AC3：scripted open → prompt → dispose session → Dispose client（进程结束）  
- [x] AC4：未改 `IAgentDelegation` / CliTransport 源行为路径  
- [x] AC5：032 §6 有 P1 进度句；pilot 05–07 齐

## Unknown

- 生产环境 Node 路径发现策略细节 → P1 用显式 Options；P4 打包文档固化。
