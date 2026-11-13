# 02 Spec — 门禁违例修复批（PILOT-080）

> 阶段：Stage 2 · Spec｜任务日：2026-11-13｜Task ID：PILOT-080
> 输入：01-intent + handoff §3 批次 A→F + PC 台账（PC-011/020/027）。

## 1. 背景

门禁首跑暴露三类问题类违例（PC-011/020/027）与设计棘轮未达标（style-tokens 2501），分布在插件测试基建（A）、插件前端样式（B）、插件进程管线（C）、宿主 JSON 序列化存量（D）、守卫精度（E）、环境红记录（F）。其中 D 存量 2 处已随 2bde724 入库；本批是这些修复的**工件链与剩余接线收口**。背景事实链见 00 §9。

## 2. 目标

1. 六个批次（A→F）各自达到「机器门禁可判绿」状态（数字判据见 §7 验收标准）。
2. 命中的三个问题类均按闭环要求落到：同类扫查（00 §9 表）+ 守卫生效（pc-check2）+ 07 归档三节。
3. 05/06/07 补齐后过闸门2，交 lead 验收。

## 3. 非目标

- 不修 PC-033（并行批归属，见 §9）。
- 不改任何在途批文件（只归因）。
- 不动 PILOT-073 既有解码器实现（ForgeSelf.Core 两文件保持干净）。

## 4. 功能需求

### FR-A1 SmartTerminal.Tests.csproj（PC-020）
- `Plugins/SmartTerminal.Tests/SmartTerminal.Tests.csproj` 设置 `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>`（按 `Plugins/DbAdminTests/DbAdminTests.csproj:11-12` 先例，注释引 PC-020）。
- 保留既有通配链接编译 `..\..\ForgeSelf.Api.Tests\Plugins\SmartTerminalTests\*.cs`（该 csproj L25）。
- **A1b**：`<Compile Remove>` 在途未跟踪测试文件 `TerminalOpenParserTests.cs`（一行，注释注明归属在途批；**禁止改该测试文件本体**）。文件存在时才需要此行（无则编译错误/重复定义风险；已验证文件存在）。

### FR-A2 DbAdminTests.csproj
- `Plugins/DbAdminTests/DbAdminTests.csproj` 增加 `<Compile Include="..\..\ForgeSelf.Api.Tests\XCodeTestFixture.cs" Link="XCodeTestFixture.cs" />`（PC-014 归化后 `DbAdminTestFixture` 依赖 `XCodeTestFixture`）。
- 随 2bde724 已含 `CopyLocalLockFileAssemblies`（L12）与 XCode.SQLite 11.24.2026.302（L22）——本批**只验证不改**。

### FR-B SmartTerminal 前端 token 化
- 新增 `Plugins/SmartTerminal/web/src/styles/themes/smart-terminal-dark.css`：DESIGN.md 原型 v0.2 tokens 迁入（主题文件名命中 `check-style-tokens.mjs` EXCLUDE_SUFFIXES 与 `check-design-compliance.mjs` THEME_SUFFIX 豁免名单）。
- `Plugins/SmartTerminal/web/src/styles.css` 顶部 `@import './styles/themes/smart-terminal-dark.css';` + 注释「tokens moved to styles/themes/smart-terminal-dark.css (DESIGN.md 原型 v0.2)」。
- 组件样式去裸色值、只引 `var(--*)`；**视觉等价**（不改布局/交互/状态机）。

### FR-C SmartTerminal 插件侧进程输出编码（PC-011）
- `SessionShell.cs`：stdout/stderr 各持一个 `ProcessOutputLineBuffer`，`Pump` 读原始字节流（不读 `Process.StandardOutput` 事件流默认解码、不设 `StandardOutput/StandardErrorEncoding`），逐行解码后写 sink。
- `TerminalSessionService.cs`：同法接线，行文本过 `SecretRedactor.Redact` 后落块；`UsedNonUtf8`/`NonUtf8EncodingUsed` 非空时履行 XTrace 日志（PILOT-073 FR-3）。
- 复用 `ForgeSelf.Core/ProcessOutputText.cs` + `ForgeSelf.Core/ProcessOutputLineBuffer.cs`（既有，本批不修改）。

### FR-D PC-027 存量 JSON 转义（已入库，验证为主）
- `AuditLogStore.cs:20`、`PermissionClassifier.cs:213` 写出 `JsonSerializerOptions` 注入 `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`（2bde724）。
- 在途 2 处（`SmartTerminalToolFunctions.cs:11`、`TerminalInstanceForwarder.cs:23`）**不修改**，守卫点名走归因记录（03 Forbidden）。

### FR-E PC-027 守卫扫描精度
- 扫描判定收窄：**赋值式**（`options.Encoder = …`）才算「写出站点」，**比较式**（读 Encoder 判等）为入站不点名。
- 精确落点（已定稿）：`ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs:46-49`（+5 行 diff：`Regex.IsMatch` 排除 `[!=]= "response.output_text.delta"` 比较式行，注释引 PC-027 同类教训「字面量匹配须区分读侧与写侧」）；守卫本体 `JsonNonAsciiEscapingGuardTests.cs` 保持 committed 干净（其扫描本就基于初始化器块判定，L179/L182/L204）。

### FR-F McpStdioWindowsShimTests 环境红
- 只记录不修（handoff §3F 拍板）。当日全量（be-test-full.log）未点名该组——在 05 观察记录，不作为本批修复项。

## 5. 交互设计（Interaction Design — 插件任务必写节）

本批唯一用户可见改动面 = **FR-B**（SmartTerminal 插件前端样式 token 迁移），**行为零变化**：

- **点什么出现什么**：无新增/删除交互；既有交互（会话列表、终端面板、命令输入、审计视图等）触发路径与响应不变。
- **五态**（加载/空/出错/无权限/正常）：状态机不变；五态视觉与改前等价（走查逐态核对）。
- **亮/暗两态**：显式切 `html.dark` 验证暗色（`system` 不算暗色验证）；选中行/标签/横幅对比度 ≥4.5:1（PC-025 精神）。
- **验证手段**：task-1 冒烟 e2e（`e2e/plugins/smart-terminal/` 1 条主链路）+ task-3 起源码走查（`dev-stack.ps1`，亮/暗各一遍，截图读图）——证据槽位在 05，待 lead 转发。

**无 UI 改动面说明**（FR-A/C/D/E/F）：SmartTerminal 插件后端经 HTTP 端点/事件流交互（terminal open/write/close、事件流 chunk、审计落库），入参与错误提示不变；C 的用户可见效应 = 非 UTF-8 输出不再乱码（功能质量修复，非交互变化）。

## 6. Failure Modes

| # | 失败形态 | 守卫/发现手段 |
|---|----------|----------------|
| FM-1 | C 批退回事件流默认解码 → GBK 等输出乱码（PC-011 复发） | `check-problem-classes.ps1` S1/S2 签名扫描红 |
| FM-2 | A 批缺 `CopyLocalLockFileAssemblies` → 测试 DLL 不随包发布 | `PluginLoadDependencyGuardTests` 红 |
| FM-3 | B 批新增裸色值 / 漏 `@import` | `design:check` 棘轮红；st-web-build/走查暴露样式丢失 |
| FM-4 | 顺手修改在途批文件 → 破坏并行批归属 | 03 Forbidden 清单 + 提交前 git status 复核 |
| FM-5 | E 批把比较式也当写出站点 → 守卫误报 | filtered 复跑红点名应只剩在途 2 文件 |
| FM-6 | 逐行缓冲跨读取边界丢字节 → 行不完整 | 设计上字节先入缓冲、凑整行才解码（ProcessOutputLineBuffer.cs L7-10 实测依据）；回归用例属 PILOT-073 范围，本批复用 |

## 7. 验收标准

| # | 判据 | 证据位 |
|---|------|--------|
| AC-1 | pc-check2：除 PC-033（并行批）外全绿（status=guarded 完成） | pc-check2.log |
| AC-2 | be-build 0 错误（501 警告为既有） | be-build.log |
| AC-3 | st-tests 36/36；be-test-filtered2 总计9/通过8，唯一红=守卫点名在途 2 文件（归因） | st-tests.log / be-test-filtered2.log |
| AC-4 | be-test-full 28 红 = 24 基线对表 + 4 非基线（各有处置），零新增 | be-test-full.log |
| AC-5 | fe-check 0 error（165 警告既有）+ component-conventions PASS；fe-test 116 文件/1209 用例全绿 | fe-check2.log / fe-test2.log |
| AC-6 | fe-design2：style-tokens 1761（基线 2501，−740）无新增违例；compliance a=209 b=0 c=0 d=0 PASS | fe-design2.log |
| AC-7 | st-web-build 绿（vite 6.4.5，11 modules，dist 产出） | st-web-build.log |
| AC-8 | 插件冒烟 e2e 1 条通过 + 亮/暗两态走查证据 | st-e2e.log / st-walkthrough.log（task-1/task-3，pending lead 转发） |
| AC-9 | 05/06/07 齐备；07 含问题类归档/重构提案/悬空教训三节且悬空教训清单为空 | 05/06/07 文件 |

## 8. 不确定点（Unknown）

| # | 不确定点 | 等级 | 处置 |
|---|----------|------|------|
| U-1 | st-e2e.log / st-walkthrough.log 的最终任务状态（task-1/task-3） | Unknown | 待 lead 转发后填 05/06/07；05/06/07 先留 pending 槽位 |
| U-2 | FR-E 精确落点文件 | ✅ 已定稿 | 实测 = OpenAIResponsesOutboundSchemaTests.cs:46-49（Regex 排除比较式；守卫本体干净），详见 03 §4 偏差 #4 |
| U-3 | McpStdioWindowsShimTests 当日全量未点名（handoff 定性环境红） | Inferred | 05 观察记录，不作修复项 |
| U-4 | DesignAgentToolContractTests ×1（11-11 基线红）当日未复现 | Inferred | 05 对表记录「当日缺席」，不计入 |

## 9. 不做的事（NOT-to-do）

1. 不修 PC-033（S3 未知 kind 'source'，并行批 toolchoice-without-tools 归属）。
2. 不改在途批文件（LLM 代理批 `ForgeSelf.Api/Services/AI/Proxy/*` 与 `SmartTerminalToolFunctions.cs`/`TerminalInstanceForwarder.cs`；smart-terminal-open 批 `Terminal*` 系列文件）。
3. 不动 `ForgeSelf.Core/ProcessOutputText.cs`、`ProcessOutputLineBuffer.cs`（PILOT-073 既有实现，git 干净）。
4. 不给 TimeoutKill 抖动「修复」（2 例为负载抖动，非缺陷）。
5. 不改 `docs/11-troubleshooting/` 台账（07 只引用）；不改 diary/TODO.md（lead 专属）；不 git commit。
