# 01 Intent — 门禁违例修复批（PILOT-080）

> 阶段：Stage 1 · Intent｜任务日：2026-11-13｜Task ID：PILOT-080
> 用户原话（输入6，逐字）：「**跑一下项目门禁，解决有问题、违反的地方**」

## 1. 为什么做（Why）

2026-11-13 门禁首跑（`gate-logs/` 首轮 + pc.log）暴露一批违例与红：问题类扫描 PC-011 ×3 签名违例、后端全量 28 红（其中 PluginLoadDependencyGuardTests / JsonNonAsciiEscapingGuardTests 为新增非基线红）、`JsonNonAsciiEscapingGuardTests` 扫出 JSON 非 ASCII 转义 4 处、插件前端样式 token 化未完成（style-tokens 2501 基线待降）。这些既是**当下门禁的违反**，也多数命中既有问题类（PC-011/020/027），按问题类闭环规则应修「类」不只修「例」。

## 2. 做什么（What）

按 handoff §3 批次 A→F 修复，使本批门禁项回到绿（PC-033 属并行批，另案不修）：

| 批 | 内容 | 问题类 |
|----|------|--------|
| A（含 A1b/A2） | 插件测试工程 csproj 修复：A1 `SmartTerminal.Tests.csproj` 补 `CopyLocalLockFileAssemblies` + 编译剔除在途文件 `TerminalOpenParserTests.cs`（A1b，一行，按 DbAdminTests 先例）；A2 `DbAdminTests.csproj` 链接编译 `XCodeTestFixture.cs` | PC-020 |
| B | SmartTerminal 前端样式 token 化：新增 `web/src/styles/themes/smart-terminal-dark.css`（DESIGN.md 原型 v0.2 tokens），`styles.css` 顶部 `@import` 引入并去组件裸色值，**视觉等价** | 设计门禁（棘轮） |
| C | SmartTerminal 插件侧进程输出编码修复：`SessionShell.cs` / `TerminalSessionService.cs` 改读原始字节 → `ProcessOutputLineBuffer` 逐行裁决 → `SecretRedactor.Redact`，并履行非 UTF-8 解码 XTrace 日志义务 | PC-011 |
| D | PC-027 存量 2 处 JSON 写出加 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`（`AuditLogStore.cs`、`PermissionClassifier.cs`，随 2bde724 入库）；在途 2 处只归因不修改 | PC-027 |
| E | `JsonNonAsciiEscapingGuardTests` 扫描精度修正：赋值式才算写出站点，比较式为入站（仅改测试扫描逻辑） | PC-027 守卫 |
| F | `McpStdioWindowsShimTests` 环境红——**只记录不修**（handoff §3F 拍板） | 记录 |

## 3. 做到什么程度（Expected）

- 机器判据（对表 gate-logs 实测）：be-build 0 错误；st-tests 36/36；be-test-filtered2 仅剩 PC-027 守卫点名在途文件的 1 红（归因记录）；be-test-full 28 红全部对表（24 基线 + 4 非基线各有处置）；fe-check 0 error；fe-test 1209/1209；fe-design2 PASS（style-tokens 1761，较基线 2501 降 740，无新增违例）；pc-check2 除 PC-033（并行批）外全绿；st-web-build 绿。
- C 批行为判据：GBK 等非 UTF-8 输出不再乱码（逐行解码落块），且出现非 UTF-8 解码时有 XTrace 日志。
- B 批视觉判据：亮/暗两态走查与改前等价（暗色显式验证，`system` 不算）。
- 05/06/07 补齐后过闸门2；07 含问题类归档/重构提案/悬空教训三节。

## 4. 约束（Constraints）

- Gate 1：**用户直接指令预批**（输入6 原话即「解决违反的地方」，handoff 据此立批 A→F）。
- 写范围：业务代码改动仅限 handoff §3 列出的文件（A/B/C/D/E 六批）；在途批文件（LLM 代理批、smart-terminal-open 批、多模态批）**只归因不修改**；`gate-logs/` 只读；`docs/11-troubleshooting/` 本批只引用；不 git commit；不做决策项见 02「不做的事」。
- 验证纪律：日志正文即证据、exit code 不算证据；Verified/Inferred/Unknown 分级；禁语「应该可以」「理论上通过」。
- 分档：本批触 §5.6「中」档（宿主源码 + 共享测试基建）→ 全量 `dotnet test` 已跑（be-test-full.log）。

## 5. 成功判据（Success Criteria）

1. `pwsh scripts/check-problem-classes.ps1` 除 PC-033（并行批归属）外无本批违例。
2. be-test-full 28 红中**零新增**（24 基线对表 + 4 非基线均给出归属与处置）。
3. fe-design2 / st-web-build / st-tests / fe-check / fe-test 全绿（数字对表 05）。
4. 插件冒烟 e2e + 起源码走查证据（task-1/task-3）到位后，05/06/07 补齐，闸门2 交 lead 验收。
