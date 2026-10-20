# AI-Native Pilot 结果汇报（复现批收口）

> 复现批（只复现不修复）轻量通道收口件；05/06 已于 2026-10-17 产出，本件为 2026-10-20 会话归档补齐（07 缺位即工件链不全，禁止裸提交关联测试文件）。

## 1. Repository Understanding
见 mini-task《根因定位》三节（三传输 initialize 缺 clientInfo / 无 initialized 通知与 session 回传 / 编辑回填硬编码空值 + 掩码往返覆盖密钥）。

## 2. Selected Task
把用户三现象钉成可重复证据：红灯测试 `McpExternalInitializeClientInfoTests` 锚定 initialize 必带 clientInfo；:51888 现场 /test 400 原文入 evidence；编辑回显缺陷以源码行号 + API 实测固化。

## 3. Changed Files
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalInitializeClientInfoTests.cs`（复现红灯锚）
- 本 pilot 目录工件。**业务代码零改动（复现批边界）**。

## 4. Validation
- 复现期（2026-10-17）：目标测试 FAIL=复现成功（红灯即验收），:51888 400 原文一致——见 05-evidence。
- 修复归宿：三传输 clientInfo/initialized/session 头 + 编辑回显 + 掩码不往返已由修复批 `2026-10-17-mcpcenter-external-mcp-fix` 落地（McpCenter 2.5.0），复现红灯随之转正；`McpClientProtocolGuardTests`/`McpStreamableSessionHeaderTests` 等守卫同批产出。

## 5. Evidence
05-evidence.md（复现期产物，Documented；转绿证据在修复批 05）。

## 6. Review
06-review.md Final Decision=APPROVED（复现批口径）；本收口件不改变其结论。

## 7. Risk
低——仅新增测试文件与文档。

## 8. Problems Found
候选新类「MCP 协议客户端侧合规缺失」已由修复批按 problem-class-closure 建档处理（PC 台账为准）；本批贡献=证据链与同类扫查命中表（三传输同缺）。

## 9. Process Evaluation
复现→修复→守卫三段闭环齐；缺口曾在本 07（复现批自身工件不全拖到归档日）——教训：复现批也要出口即八件/四件齐，别指望修复批代其补。

## 10. 问题类归档
归修复批所建 PC；本批不重复建档。

## 11. 重构提案
无（三传输骨架抽基类提案已登记 TODO P3，维持缓行理由）。

## 12. 最终教训清单
- 轻量通道 = mini-task + 05/06/07 四件，复现批不例外。
