# AI-Native Pilot Result（最终汇报）

> 任务：MCP 中心（McpCenter）升级支持 MCP 2.0 协议（2025-11-25）｜worktree `wt-mcp2`（分支 mcp2-support）｜状态：实现+验证完成，待闸门2 用户验收（不 commit/push）

## 1. Repository Understanding

- McpCenter 插件（对外 MCP 网关 + 外部 MCP 客户端双半身），手写最小 JSON-RPC 2.0 + Streamable HTTP，零 MCP SDK。
- 协议协商白名单：`McpJsonRpcHandler.SupportedProtocolVersions`（服务端）/ `McpClientSession.SupportedProtocolVersions`（客户端），原最高 `2025-06-18`。
- 官方 changelog（modelcontextprotocol.io/specification/2025-11-25/changelog）核实：MCP 2.0 的 Major 变更（OAuth/OpenID、tasks、icons、elicitation、sampling）对不声明 capabilities 的服务器均**可选**；与本网关相关仅 minor（`Implementation.description` 可选字段 + JSON Schema 2020-12 向后兼容）。
- 版本双源不一致存量问题：plugin.json=2.1.1 / csproj=2.1.0。

## 2. Selected Task

PILOT-mcp2-protocol：协商白名单加 `2025-11-25`（服务端+客户端，置首）、initialize 响应补可选 `description`、版本统一 2.2.0、同步单测/e2e/夹具/功能文档。D1（SEP-1303 错误语义）经闸门1 决策不纳入。

## 3. Changed Files

9 个源码/测试/夹具/e2e/文档文件 + 3 个新增工件（05/06/07），详见 05-evidence.md Changed Files。

## 4. Validation

Build: `dotnet build` 0 error（1190 既有警告）。
Unit Test: `--filter "FullyQualifiedName~McpCenter"` **96/96 通过**（含新增 2.0 协商用例）。
E2E: `playwright test e2e/plugins/mcp-center` **3 passed**（initialize 2025-11-25 回显 + description 断言；外部客户端对外部 2.0 服务器协商 2025-11-25；1.x 路径回归通过）。
Static: `pnpm run check` 0 errors / vitest **568/568 通过**。

## 5. Evidence

见 `docs/ai/pilot/2026-10-01-mcp2-protocol/05-evidence.md`（全部 Verified，含命令原文）。

## 6. Review

见 `06-review.md`：八问全过，Final Decision = **APPROVED**，Risk L0。

## 7. Risk

L0。MCP 2.0 对服务器无强制能力项；协商为追加式，1.x 客户端零影响；唯一环境依赖（esbuild 写入被按 worktree 路径拦截）已绕行并记录，非代码缺陷。

## 8. Problems Found

- 环境（非代码）：①新 worktree 跑前端 e2e 的前置缺口——esbuild 写入被环境按 worktree 路径拦截（.vite junction 绕行）、插件 web/dist 不入库（复制等价产物）；②`e2e/global-setup.ts` 第 1.5 步 SQLite 检查陈旧（项目已装 XCode.SQLite 正式依赖、publish 自带 DLL，该步仍强制从 REPO_ROOT 候选复制，新 worktree 无 publish 目录即报错，本次复制 build/runtime/Plugins 兜底；根因修复已记 TODO）。均已记 TODO（建议沉淀为「新 worktree e2e 前置检查」SOP）。
- 存量：`list_todos` 工具缺陷（输入19）不在范围。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（源码+官方规范+changelog 核实） |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（严格按 Files To Change） |
| Code → Test | PASS（单测/e2e 全绿） |
| Test → Evidence | PASS（全部 Verified + 原文） |
| Evidence → Review | PASS（八问 + L0） |

## 10. 最重要的问题

worktree 化开发的环境前置未被工程化：新检出跑前端 e2e 依赖三个未入库运行时产物（SQLite provider、插件 dist、vite 缓存），且 esbuild 写入被环境按 worktree 路径拦截。本次逐个手工补齐，下次应固化。

## 11. 下一步建议

用户验收（闸门2）后，按用户指示执行提交/推送/打 tag 发布（闸门3）；发布走页面「自动更新」，不触碰运行实例。
