# 03 Plan — 门禁违例修复批（PILOT-080）

> 阶段：Stage 3 · Plan｜上游：01-intent + 02-spec。
> 本计划所有文件/行号均为 2026-11-13 实测（git status / 文件读取 / git diff），非 handoff 转述；与 handoff 冲突处以本表为准（偏差见 §4）。

## 1. 方案总览

| 批 | 一句话 | 落地状态 |
|----|--------|----------|
| A | 插件测试基建：SmartTerminal.Tests 补 `CopyLocalLockFileAssemblies`（PC-020）+ 保留通配链接编译 | 已落地（csproj 实测） |
| A1b | 同文件一行 `<Compile Remove>` 排除在途未跟踪测试文件（按 DbAdminTests 先例注释归属） | 已落地 |
| A2 | DbAdminTests 补 `XCodeTestFixture` 链接编译（PC-014 归化配套） | 已落地（csproj 实测） |
| B | SmartTerminal 前端 token 迁移：新增主题文件 + `@import`，去裸色值，视觉等价 | 已落地；亮/暗走查证据 pending（task-3） |
| C | 插件侧进程输出编码：两文件接 byte 路径 + 逐行缓冲解码 + Redact + XTrace duty（复用 ForgeSelf.Core 既有解码器） | 已落地（实测行号） |
| D | PC-027 存量 2 处 `UnsafeRelaxedJsonEscaping`（2bde724 已入库）；在途 2 处归因不修 | 已入库/已归因 |
| E | 守卫扫描精度：出站构造点 vs 入站比较式区分（测试 +5 行 Regex 排除） | 已落地（diff 实测） |
| F | McpStdioWindowsShimTests 环境红：仅记录（当日全量未点名，见偏差 #5） | 记录项 |

## 2. Files To Change（实测 file:line）

### A / A1b — `Plugins/SmartTerminal.Tests/SmartTerminal.Tests.csproj`
| 行 | 内容 |
|----|------|
| L8-9 | 注释：PC-020 修复说明（按 DbAdminTests.csproj L11-12 先例） |
| L10 | `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` |
| L25 | 保留既有 `<Compile Include="..\..\ForgeSelf.Api.Tests\Plugins\SmartTerminalTests\*.cs" />` 通配 |
| L26-28 | 注释：A1b 归属说明（在途批文件，本体禁改） |
| L29 | `<Compile Remove="..\..\ForgeSelf.Api.Tests\Plugins\SmartTerminalTests\TerminalOpenParserTests.cs" />` |

### A2 — `Plugins/DbAdminTests/DbAdminTests.csproj`
| 行 | 内容 |
|----|------|
| L29-30 | 注释：XCodeTestFixture 链接原因（PC-014 归化后 DbAdminTestFixture 依赖） |
| L31 | `<Compile Include="..\..\ForgeSelf.Api.Tests\XCodeTestFixture.cs" Link="XCodeTestFixture.cs" />` |
| L12 | `CopyLocalLockFileAssemblies`（2bde724 已入库，只验证） |
| L22 | `XCode.SQLite 11.24.2026.302`（2bde724 已入库，只验证） |

### B — SmartTerminal 插件前端
| 文件 | 内容 |
|------|------|
| `web/src/styles/themes/smart-terminal-dark.css` | **新增**（untracked，已存在）：DESIGN.md 原型 v0.2 tokens 全量；文件名命中 check-style-tokens EXCLUDE_SUFFIXES / check-design-compliance THEME_SUFFIX 豁免 |
| `web/src/styles.css:1` | `@import './styles/themes/smart-terminal-dark.css';` |
| `web/src/styles.css:2` | 注释「tokens moved to styles/themes/smart-terminal-dark.css (DESIGN.md 原型 v0.2)」 |

### C — 插件侧进程输出编码（PC-011）
| 文件 | 行 | 内容 |
|------|----|------|
| `Plugins/SmartTerminal/Services/SessionShell.cs` | L17-18 | `_stdoutLines/_stderrLines` 两个 `ProcessOutputLineBuffer` 字段 |
| 同上 | L37 | PC-011 修复注释（byte 路径说明） |
| 同上 | L43 | `Pump(Stream, ProcessOutputLineBuffer, StringBuilder)`：读原始字节、逐行解码、写 sink |
| 同上 | L58 | `UsedNonUtf8`/`NonUtf8EncodingUsed` 非空 → XTrace 日志 |
| `Plugins/SmartTerminal/Services/TerminalSessionService.cs` | L795-796 | PC-011 修复注释 |
| 同上 | L797-798 | `new ProcessOutputLineBuffer()` |
| 同上 | L800/L802 | `AppendChunk(s, "stdout"/"stderr", SecretRedactor.Redact(text))` |
| 同上 | L920-922 | `foreach (var line in lineBuffer.Flush()) onLine(line);` |
| 同上 | L927-931 | `LogNonUtf8Decoding` → `XTrace.Info`（PILOT-073 FR-3 duty） |

**禁触（本批不改）**：`ForgeSelf.Core/ProcessOutputText.cs`、`ForgeSelf.Core/ProcessOutputLineBuffer.cs`（PILOT-073 既有实现，`git status --porcelain -- ForgeSelf.Core` 为空）。

### D — PC-027 存量（2bde724 已入库，只验证）
| 文件 | 行 | 内容 |
|------|----|------|
| `Plugins/SmartTerminal/Services/AuditLogStore.cs` | L20 | 写出 `JsonSerializerOptions` 注入 `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` |
| `Plugins/SmartTerminal/Services/PermissionClassifier.cs` | L213 | 同上 |

在途 2 处（**归因不修**）：`SmartTerminalToolFunctions.cs:11`、`TerminalInstanceForwarder.cs:23`（`ForgeSelf.Api/Services/Terminal/`）。

### E — 守卫扫描精度（+5 行 diff，实测）
| 文件 | 行 | 内容 |
|------|----|------|
| `ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs` | L46-49 | 新增 `using System.Text.RegularExpressions;` + `Regex.IsMatch(lines[i], "[!=]=\s*\"response\.output_text\.delta\"")` 则 `continue`；注释 L46-47 引「PC-027 同类教训：字面量匹配须区分读侧与写侧」 |

守卫本体 `ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs`：committed 干净，无本批修改（其扫描基于初始化器块判定 L179/L182/L204）。

### F — 仅记录
`McpStdioWindowsShimTests`（handoff §3F 环境红定性）→ 05 观察记录，不作修复项。

## 3. 实施步骤（顺序）

1. **A/A1b/A2**（csproj 三处）→ 佐证：st-tests 36/36、be-build 0 错误、filtered 通过 8。
2. **B**（主题文件 + `@import`）→ 佐证：st-web-build 绿、fe-design2 style-tokens 1761 无新增违例；亮/暗走查证据 = task-3（pending）。
3. **C**（两插件文件接线）→ 佐证：pc-check2 S1/S2 清零 + XTrace duty 落地。
4. **D/E**（验证态核验：D 2bde724、E diff）→ 佐证：filtered 8/9，唯一红=守卫点名在途 2 文件（归因）。
5. **F** → 05 观察记录。
6. **05/06/07** → 待 lead 转发 task-1（e2e）+ task-3（走查）证据后填齐，CAS-complete task-2。

> 实施态说明：A–E 的代码变更已在工作树中完成并经 gate-logs 首跑验证；本批（pilot-writer）职责 = 工件链 00–07 记录与收口，不再重复改动业务文件。

## 4. 偏差记录表（handoff vs 实测）

| # | handoff 说法 | 实测 | 处置 |
|---|--------------|------|------|
| 1 | D 批 `AnthropicProtocolBridge.cs` 在 `ForgeSelf.Api/Controllers/AI/Proxy/`，L151 | 实为 `ForgeSelf.Api/Services/AI/Proxy/`，L152 | 02/03 按实测路径行号记录；文件属在途 LLM 代理批，仅归因 |
| 2 | C 批「新增 helper 文件」 | helper（ProcessOutputText/ProcessOutputLineBuffer）早已在 `ForgeSelf.Core/`（PILOT-073，git 干净）；本批 C 仅接线两个插件文件 | FR-C 按「复用」表述（02 §4）；偏差入本表 |
| 3 | C 批行号未给 | SessionShell.cs L17-18/37/43/58；TerminalSessionService.cs L795-802/920-931（L927-931 LogNonUtf8Decoding） | §2 按实测行号记录 |
| 4 | E 批「仅改测试扫描逻辑」未指名文件 | `OpenAIResponsesOutboundSchemaTests.cs:46-49`（+5 行 Regex 排除比较式）；守卫本体干净 | 02 U-2 已定稿回写；偏差入本表 |
| 5 | F 批 McpStdioWindowsShimTests 列入当日 28 红 | be-test-full.log 未点名该组（当日绿）；DesignAgentToolContractTests（11-11 基线）亦缺席 | F 降级为观察记录（05），02 §8 U-3/U-4 |
| 6 | handoff 提及在途批目录名 `2026-11-12-llm-proxy-thin-forward` | 名称未验证 | 00/01 不落具体名，写「在途 LLM 代理批」 |
| 7 | handoff §3A 写 `DbAdmin.Tests.csproj` | 实为 `Plugins/DbAdminTests/DbAdminTests.csproj` | §2 按实测路径记录 |
| 8 | st-walkthrough 证据 | gate-logs/st-walkthrough.log（403 字节，记录时点 task-1 尚未完成；task-3 blockedBy=[task-1]） | 05/06/07 留 pending 槽位，待 lead 转发 |

## 5. 验证计划（§5.6 档位：**中**）

本批触碰插件源码 + 共享测试基建（`ForgeSelf.Api.Tests` 夹具/链接编译）→ 按中档执行；全量后端 `dotnet test` 为必跑门槛。**执行主体 = lead / task-1 / task-3 / 并行批日志（gate-logs 只读）**；pilot-writer 不重复跑构建与长测试，05 只记录日志事实（日志正文为证据，exit code 不算）。

| 门禁 | 命令 | 证据位 |
|------|------|--------|
| 环境前置 | `NO_PROXY='localhost,127.0.0.1,::1'`（小写一并）、`TEMP=TMP=<repo>\.temp\tmp`；一律 `pwsh`（禁 5.1） | — |
| 问题类扫描 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1` | pc-check2.log |
| 后端构建 | `dotnet build ForgeSelf.slnx` | be-build.log |
| 插件单测 | SmartTerminal.Tests `dotnet test` | st-tests.log |
| 过滤复跑 | `dotnet test --filter`（PC-027 守卫 + schema 组，总计 9） | be-test-filtered2.log |
| 后端全量（中档） | `dotnet test`（ForgeSelf.Api.Tests） | be-test-full.log |
| 前端 check/test | `cd ForgeSelf.Web && pnpm run check` / `pnpm run test` | fe-check2.log / fe-test2.log |
| 设计棘轮 | `pnpm run design:check` | fe-design2.log |
| 插件前端构建 | `cd Plugins/SmartTerminal/web && pnpm run build` | st-web-build.log |
| 插件冒烟 e2e | `e2e/plugins/smart-terminal/` 1 条主链路（task-1） | st-e2e.log（pending lead 确认） |
| 起源码走查 | `pwsh scripts/dev-stack.ps1`，亮/暗两态显式切 `html.dark`（task-3） | st-walkthrough.log（pending） |

## 6. 回滚（逐批独立）

| 批 | 回滚动作 | 影响 |
|----|----------|------|
| A/A1b/A2 | 还原两个 csproj（`git checkout -- <csproj>`）；A1b 回滚需在在途测试文件存在时重加 Remove | st-tests/be-build 回到基线红 |
| B | 删主题文件 + 去 `@import`；**注意棘轮**：style-tokens 基线已 2501→1761，仅回滚代码不改基线会红，须同步让 lead 还原基线值 | 样式回到 2501 基线态 |
| C | 还原两个插件文件至事件流默认解码 | pc-check 红回归（预期） |
| D | `git revert 2bde724`（lead 域，需用户同意） | PC-027 存量红回归 |
| E | 还原 +5 行 hunk | 守卫误报在途文件回归（归因形态） |

## 7. Forbidden（在途批文件，只归因不修改）

`WireStreamTranslator.cs`、`TerminalOpenParserTests.cs`、`SmartTerminalToolFunctions.cs`、`TerminalInstanceForwarder.cs`、`TerminalInboundSession.cs`、`TerminalOpenParser.cs`、`TerminalLaunchBootstrap.cs`、`LlmProxyContracts.cs`、`TerminalOpenInbox.cs`、`ForgeSelf.Api/Services/AI/Proxy/**`（含 `AnthropicProtocolBridge.cs`）——属并行 LLM 代理 / smart-terminal-open / 多模态批。另：不修 PC-033（并行批归属）；不动 diary/TODO.md、gate-logs、docs/11-troubleshooting（lead 域）；不 git commit。
