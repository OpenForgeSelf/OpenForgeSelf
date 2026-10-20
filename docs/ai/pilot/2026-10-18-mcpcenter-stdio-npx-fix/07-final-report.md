# AI-Native Pilot Result（收尾汇报 · stdio 外部 MCP npx 三层修复批，2026-10-18）

## 1. Repository Understanding

见 00。关键先验：PC-008（同插件协议层）格局、store 逐字段拷贝链、mock 与客户端同构风险面。

## 2. Selected Task

用户报障修复（最高优先级指令）：`npx @playwright/mcp@latest` 外部 MCP 连接 400；流程 = 红灯复现 → 修复 → 回归（problem-class-closure + plugin-development 六步门禁）。

## 3. Changed Files

见 05「Changed Files」。核心：StdioMcpTransport（S1+S2）、McpClientProtocol（S3）、framing 贯穿链、6 个测试文件（2 新 4 改）、e2e 新用例、版本 2.5.1、PC-010 档案、034 文档。

## 4. Validation

定向 dotnet test 32/32；插件 e2e 6/6 + UI 段复跑绿；check-problem-classes 10 PC 绿；RegistryTests 5/5；门禁③用户原文配置 API 实测 connected=True toolCount=25 /test success 3851ms；截图两张读图核验（v2.5.1 徽标 + 已连接 13 工具 + 「握手成功」绿 toast）。

## 5. Evidence

05-evidence 全量；红灯=用户报错逐字同款是本批最强锚。

## 6. Review

06 结论 PASS（Critical/Major 零，Minor 三项有归档去向）。

## 7. Risk

auto 探测 5s 经验预算（显式 framing 逃生）；ComSpec 中介进程树（断开路径已覆盖）；均记录于 06 Risk。

## 8. Problems Found

- 三层缺陷本体（①进程启动 ②帧协商 ③null params）——全修；
- 衍生面：store ValidateAndNormalize 吞新字段 Framing——修+教训入 PC-010 §3；
- 流程面：旧应用层 e2e「优雅降级」分支=宽松断言掩盖第三次命中——机制封堵（零容忍用例+靶子去同构）；
- 编号冲突：本批拟用 PC-009 已被并行会话占用 → 改 PC-010，代码注释全量订正。

## 9. Process Evaluation

- 有效：红灯先行拿到"报错逐字同款"锚，后续三层剥洋葱每一步都有实测事实支撑（探针 A/B 用完即删、结论沉淀进测试）；
- 低效：dev-stack 栈在会话间隙被并行活动停掉，恢复时二次踩"后台静默失败"坑（前台+超时转后台解）；
- 改进：跨会话并行时 PC 编号在**建档瞬间**才申领（写文件前再 grep 一次），不预占。

## 10. 问题类归档

- PC-010（stdio 外部进程启动与消息帧协商脱节）+ case `2026-10-18-mcp-stdio-npx-windows-shim` 已建档；
- 三索引（problem-classes/README tags 速查+总表、cases/README、index.md 症状速查）已回写；
- 机器校验绿：check-problem-classes（S1 grep 签名）+ ProblemClassRegistryTests。

## 11. 重构提案

- R1（已做）：请求序列化收敛 McpClientProtocol.SerializeRequest 单点（与 PC-008 BuildInitializeParams 同处收口）；
- R2（提案未做）：三传输 pending/超时/去重骨架抽 `JsonRpcChannelBase`——Stdio 的 RequestAsync 已因帧协商特殊化，为两例提前造基类收益不足；**待第三条新传输或第二次骨架重复时兑现**（已登记 TODO.md）；
- R3（不做）：换官方 C# MCP SDK 客户端——沿 PC-008 结论，需 architecture-design 单独立项。

## 12. 沉淀教训清单（不得为空）

1. **Windows 拉起用户配置命令必按 PATHEXT 探测实体形态**（npx/npm/uvx 皆 .cmd shim；CreateProcess UseShellExecute=false 不解析批处理）——已入 PC-010 lens G1；
2. **协议客户端合规结论只能下给"生态实况"，不能下给规范 SHOULD 段落或自家同构 mock**——官方 TS SDK 只答 NDJSON 且对错误帧型沉默；mock 必须至少一个非同构变体（stdio-ndjson 模式）；
3. **可选字段发 null 还是省键是兼容性分水岭**（FastMCP 收 null 沉默；沉默≠慢，A/B 探针是唯一裁判）；
4. **e2e 降级/优雅分支=缺陷温床**（第三次命中）：真实世界依赖的用例一律零容忍断言；
5. **store 逐字段拷贝的 ValidateAndNormalize/merge 链，加 config 字段必同步透传**（否则静默吞掉）；
6. **Playwright 证据截图前 scrollIntoViewIfNeeded**（断言不滚动，图与标注可能名实不符——读图环节兜住）。
