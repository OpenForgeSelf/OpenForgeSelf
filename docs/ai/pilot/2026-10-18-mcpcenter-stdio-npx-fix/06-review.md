# Review（stdio 外部 MCP npx 三层修复批，2026-10-18）

## 审查对象

本批全部改动（05-evidence「Changed Files」清单），对照 02-spec S1-S4 与 01-intent 成功判据。

## Requirement Check

- S1 启动层：ResolveLaunch exe>cmd>bat 探测、ComSpec /d /s /c 包装、可行动报错、env.PATH 覆盖 —— 均有单测（含包装器参数逐字形状断言）✓
- S2 帧协商：读侧双帧嗅探、auto NDJSON 首发 + 5s 探测 + LSP 重发一次（_probeResent 防循环、同 id 去重）、写帧跟随、framing 贯穿链（store 透传补齐）—— FramingTests 4 组 + 真实 npx 集成 ✓
- S3 序列化：SerializeRequest null 省略键、三传输同改、arguments {} 封堵 —— 直测 + 源码扫描守卫 ✓
- S4 靶子去同构 + e2e 零容忍 —— mock stdio-ndjson 模式、新用例无降级分支、旧 stdio mock 用例锁 lsp ✓

## Scope Check

未触碰：宿主版本线、前端 UI 控件、白名单安全边界、并行会话文件。全量 60 红经 git status 归因他批（零重叠），登记不修——范围纪律 ✓。发现过的衍生面（store 吞新字段）属同根因链已修。

## Test Check

红灯先行且报错与用户原文逐字同款（复现有效性最高标准）；定向 32/32；e2e 6/6 + 单用例复跑；门禁③按用户**原文配置**（无 -y 的 @playwright/mcp@latest 真服务器）实测——测试证据链覆盖"报障原文→最小靶→真实生态"三级。

## Architecture Check

canonical 单点延续 PC-008 格局（McpClientProtocol 同处收口 initialize 与 request 序列化）；帧协商状态内聚于 StdioMcpTransport（其他两传输无帧问题，不抽象）；config 字段扩展走既有 DTO/merge/透传链，无旁路。

## Risk

- auto 探测 5s 预算为经验值：极慢冷启动的 NDJSON 服务器可能被误判换帧重发——重发同 id 无害（TryRemove 去重），且显式 framing 可逃生；
- ComSpec 包装改变 stdio 子进程树（cmd.exe 中介）：KillTree 语义已由既有断开路径覆盖（e2e 清理无残留进程观察）。

## Findings

### Critical

无。

### Major

无（流程级教训「宽松/同构靶子 + 宽松断言」双重掩盖已在 S4 以机制封堵：靶子去同构 + 零容忍用例 + G3 扫描，非仅口头）。

### Minor

1. 前端未暴露 framing 选择 UI——拍板留待需要时加（config 已预留），登记 07 悬空教训；
2. Windows 分支 CI 不可真跑，靠注入式单测兜形状——若 CI 未来加 windows runner 应去掉 skip；
3. e2e 证据图首版无条目（断言不滚动）——已修并沉淀 case §9。

## Final Decision

**PASS** —— S1-S4 全达成、证据链闭合、范围纪律守住；遗留三项均为 Minor 且有归档去向（07 §11/§12）。
