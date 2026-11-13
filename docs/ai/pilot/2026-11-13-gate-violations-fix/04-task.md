---
task_id: PILOT-080
feature: 门禁违例修复批（PC-011/020/027 闭环 + B 批 token 棘轮 + E 批守卫精度 + F 批环境红记录）
risk: { level: L2, triggers: ["ForgeSelf.Api/** 宿主源码（中档全量 dotnet test）", "scripts/** 仓库级脚本（verify-pilot-artifacts.ps1）", "ForgeSelf.Api.Tests/** 共享测试基建", "Plugins/SmartTerminal/** + Plugins/DbAdmin.Tests/** 插件层", "design 棘轮基线（06 §4 #2）"], raised_by_agent: false } # 本批如实定级：中（分档 中，AGENTS §5.6）→ L2
tier: 中
gate1:
  mode: pre-approved
  at: 2026-11-13
  ref: 用户直接指令（输入6）「跑一下项目门禁，解决有问题、违反的地方」
gate2:
  status: pending
  criteria: 05-evidence + 06-review 齐备（含 task-1 e2e 与 task-3 走查证据）→ lead 验收
gate3:
  status: pending
  criteria: 验收通过后归档提交（git commit 需用户同意，lead 域）
expected_files:
  - docs/ai/pilot/2026-11-13-gate-violations-fix/00-repository-understanding.md
  - docs/ai/pilot/2026-11-13-gate-violations-fix/01-intent.md
  - docs/ai/pilot/2026-11-13-gate-violations-fix/02-spec.md
  - docs/ai/pilot/2026-11-13-gate-violations-fix/03-plan.md
  - docs/ai/pilot/2026-11-13-gate-violations-fix/04-task.md
  - docs/ai/pilot/2026-11-13-gate-violations-fix/05-evidence.md
  - docs/ai/pilot/2026-11-13-gate-violations-fix/06-review.md
  - docs/ai/pilot/2026-11-13-gate-violations-fix/07-final-report.md
  - Plugins/SmartTerminal.Tests/SmartTerminal.Tests.csproj
  - Plugins/DbAdminTests/DbAdminTests.csproj
  - Plugins/SmartTerminal/web/src/styles/themes/smart-terminal-dark.css
  - Plugins/SmartTerminal/web/src/styles.css
  - Plugins/SmartTerminal/Services/SessionShell.cs
  - Plugins/SmartTerminal/Services/TerminalSessionService.cs
  - ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs
rollback: "逐批独立回滚（03 §6 回滚表）：提交前 git restore 对应文件，提交后按批 git revert；B 批回滚须联动 design 棘轮基线（06 §4 #2）"
forbidden:
  - ForgeSelf.Api/Services/AI/Proxy/**（在途 LLM 代理批，含 AnthropicProtocolBridge.cs）
  - ForgeSelf.Api/Services/Terminal/**（在途 smart-terminal-open 批）
  - WireStreamTranslator.cs / TerminalOpenParserTests.cs / SmartTerminalToolFunctions.cs / TerminalInboundSession.cs / TerminalOpenParser.cs / TerminalLaunchBootstrap.cs / LlmProxyContracts.cs / TerminalOpenInbox.cs
  - ForgeSelf.Core/ProcessOutputText.cs、ProcessOutputLineBuffer.cs（PILOT-073 既有）
  - docs/11-troubleshooting/**、problem-classes/README.md、gate-logs/**、TODO.md、.forgeself/memory/**（lead 域/只读）
  - PC-033（并行批归属）
---

# 04 Task — 门禁违例修复批（PILOT-080）

> 分档（§5.6）：**中**——触碰插件源码与共享测试基建，全量后端 `dotnet test` 为必跑门槛；汇报不得以快档结果报全绿。

## 1. 任务分解

| # | 任务 | 动作 | 验证命令（同 03 §5） | 状态 |
|---|------|------|----------------------|------|
| T1 | A/A1b | SmartTerminal.Tests.csproj 三处（L8-10 / L26-29，保留 L25 通配） | st-tests 36/36、be-build 0 错误 | 已落地 |
| T2 | A2 | DbAdminTests.csproj L29-31 链接 XCodeTestFixture；L12/L22 验证（2bde724） | 同上 | 已落地 |
| T3 | B | 新增 themes/smart-terminal-dark.css + styles.css:1-2 `@import` | st-web-build 绿、design:check 1761（基线 2501）、亮/暗走查（task-3） | 已落地；走查证据 pending |
| T4 | C | SessionShell.cs + TerminalSessionService.cs 接 byte 路径/逐行缓冲/Redact/XTrace | check-problem-classes.ps1（pc-check2 S1/S2 清零） | 已落地 |
| T5 | D | 验证 AuditLogStore.cs:20、PermissionClassifier.cs:213（2bde724）；在途 2 处归因 | be-test-filtered2 8/9（唯一红=在途点名） | 已入库 |
| T6 | E | OpenAIResponsesOutboundSchemaTests.cs:46-49（Regex 排除比较式） | filtered 通过 8 含 schema 组 | 已落地 |
| T7 | F | 05 观察记录（McpStdioWindowsShimTests / DesignAgentToolContractTests 当日缺席） | be-test-full.log 对表 | 记录项 |
| T8 | 工件收口 | 05/06/07 补齐（pending 槽位待 task-1/task-3 证据）→ CAS-complete task-2 | 05 AC-1..9、06 八问、07 三节 | ✅ 完成（task-2 rev 3 CAS-complete；20:12 lead 返工三项补齐后定稿） |

## 2. Allowed

- 仅写 `docs/ai/pilot/2026-11-13-gate-violations-fix/` 内 00–07 八件。
- 工件内引用（不修改）：gate-logs/*、problem-classes/README.md、docs/11-troubleshooting/*、DESIGN.md、PILOT-073/074 工件。

## 3. Forbidden（与 YAML 同源）

在途批文件清单见 YAML `forbidden`；另禁：改业务代码（实施态已冻结）、跑构建/长测试（执行主体=lead/task-1/task-3）、git commit、改 diary/TODO.md。

## 4. 回滚

逐批独立，动作与影响见 03 §6（B 批回滚须同步还原 style-tokens 棘轮基线，否则棘轮红）。

## 5. 回写（per-batch，执行主体标注）

| 批 | 回写目标 | 主体 |
|----|----------|------|
| A/A1b/A2 | problem-classes/README.md PC-020 状态行（→已守卫） | lead |
| B | 无独立真源回写（DESIGN.md 原型 v0.2 已是上游） | — |
| C | problem-classes/README.md PC-011 状态行 + 07 §10 归档草稿（本批产出） | lead 归档 / 本批起草 |
| D | PC-027 存量注记（2bde724） | lead |
| E | PC-027 守卫精度注记（read-side/write-side） | lead |
| F | 05 观察记录（无台账变更） | 本批 |

## 6. 完成判据

1. 02 §7 AC-1..9 全部有证据（AC-8 待 task-1/task-3 证据转发）。
2. 05/06/07 齐备；07 三节（问题类归档/重构提案/悬空教训）非空框架、悬空教训清单为空。
3. 闸门2：lead 验收通过 → CAS-complete task-2（先重读 expected_revision，claim 时为 2）。
4. git commit / 归档属闸门3（lead 域，需用户同意）。
