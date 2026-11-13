# 00 Repository Understanding — 门禁违例修复批（PILOT-080）

> 阶段：Stage 0 · Repository Understanding｜任务日：2026-11-13｜Task ID：PILOT-080
> 来源指令（输入6，lead 转达）：「跑一下项目门禁，解决有问题、违反的地方」
> 本文自含，依据 = 仓库现状直读 + `gate-logs/` 实测日志（只读）+ `.forgeself/memory/2026-11-13-gate-handoff.md`。

## 1. 项目结构

单仓库双端 + 插件架构（Git 管理，本批只读业务代码、只写 `docs/ai/pilot/2026-11-13-gate-violations-fix/`）：

```
OpenForgeSelf/
├── ForgeSelf.Api/            # ASP.NET Core 宿主（.NET 10 + SQLite + NewLife.XCode）
│   ├── Controllers/          # REST 控制器（含 AI 网关、设置）
│   ├── Services/             # 宿主服务（AI Providers、Terminal、AI/Proxy 网关桥）
│   ├── Plugins/PluginManager.cs
│   └── AppBuilder.cs / Program.cs
├── ForgeSelf.Core/           # 共享核心库（进程输出解码器 ProcessOutputText/ProcessOutputLineBuffer 在此）
├── ForgeSelf.Abstractions/   # 宿主↔插件契约（LlmProxyContracts、TerminalOpenInbox）
├── ForgeSelf.Api.Tests/      # xUnit 后端测试（含 Architecture/ 守卫测试、Plugins/<X>/ 真源测试源码）
├── Plugins/
│   ├── SmartTerminal/        # 终端插件（Services/ + web/ 前端）
│   │   ├── Services/         # SessionShell、TerminalSessionService、AuditLogStore、PermissionClassifier
│   │   └── web/src/          # Vue3 插件前端（styles.css + styles/themes/smart-terminal-dark.css）
│   ├── SmartTerminal.Tests/  # 插件单测工程（链接编译宿主测试源码）
│   ├── DbAdmin/DbAdminTests/ # 数据库管理插件及其单测工程（A2/A1b 的先例工程）
│   └── AIAgent/              # MAFF 循环插件
├── ForgeSelf.Web/            # Vue 3.5 + Vite 6 + TS 5.7 主前端（e2e/ 为 Playwright 用例）
├── scripts/                  # 门禁脚本（check-style-tokens.mjs、check-design-compliance.mjs、check-problem-classes.ps1、verify-pilot-artifacts.ps1）
├── gate-logs/                # 2026-11-13 门禁批实测日志（只读证据，append 模式 *>>）
├── docs/11-troubleshooting/  # 问题类/案例归档（本批只引用不修改）
└── docs/ai/pilot/            # 本九阶段工件链
```

## 2. 技术栈表

| 层 | 技术 | 依据 |
|----|------|------|
| 宿主后端 | .NET 10 + ASP.NET Core + NewLife.XCode（唯一 ORM）+ SQLite | `ForgeSelf.Api/*.csproj`（net10.0）、AGENTS.md §2.1 |
| 共享核心 | .NET 10 类库（ForgeSelf.Core，零 UI 依赖） | `ForgeSelf.Core/ProcessOutputLineBuffer.cs` namespace 声明 |
| 宿主测试 | xUnit + FluentAssertions + Moq + Coverlet | `ForgeSelf.Api.Tests.csproj`、be-test-full.log `ForgeSelf.Api.Tests.dll (net10.0)` |
| 插件后端 | `Plugins/<X>/` + `plugin.json` 清单注册，类库 DLL 隔离 | PluginManager、AGENTS.md §2.3 |
| 插件单测 | xUnit 工程（`Plugins/<X>.Tests/`），跨工程 `<Compile Include>` 链接宿主测试源码 | `Plugins/SmartTerminal.Tests/SmartTerminal.Tests.csproj:25`、`Plugins/DbAdminTests/DbAdminTests.csproj:28` |
| 插件前端 | Vue 3.5 + Vite 6 + TS + pnpm，产物 `web/dist/index.js` | st-web-build.log（vite v6.4.5，11 modules，dist/index.js） |
| 主前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia + pnpm | AGENTS.md §2.1 |
| e2e | Playwright（单配置 globalSetup，零 mock，真实宿主+前端） | AGENTS.md §5.3、e2e-testing 技能 |
| 门禁脚本 | Node ESM（*.mjs）+ PowerShell 7（*.ps1，禁 5.1） | scripts/check-style-tokens.mjs、check-problem-classes.ps1、AGENTS.md §2.3 |

## 3. 架构特点

- **插件隔离**：插件本体 DLL 只依赖 `ForgeSelf.Abstractions` 契约；插件单测工程把宿主测试目录里的真源测试源码 `<Compile Include>` 进来，同时保留 `PluginLoadDependencyGuardTests` 守卫「测试 DLL 必须随包发布」（PC-020）。
- **AI 网关分层**：`ForgeSelf.Api/Services/AI/Proxy/` 目录整体为在途 LLM 代理批的未跟踪/修改文件区，`AnthropicProtocolBridge`/`WireStreamTranslator` 等归属该批；本批 D/E 只在守卫视角引用，不改其文件（归因与偏差记录见 03）。
- **进程输出管线**（PC-011 核心）：子进程 stdout/stderr → 原始字节流 → `ProcessOutputLineBuffer.Append(ReadOnlySpan<byte>)` 按 0x0A 切行（GBK 安全，见该文件头注释实测依据）→ `ProcessOutputText.Decode` 逐行裁决（utf8 / utf8-bom / ansi-<codepage> / utf8-lossy）→ 行文本再过 `SecretRedactor.Redact` → 事件/落块；`UsedNonUtf8`/`NonUtf8EncodingUsed` 履行 PILOT-073 FR-3 的 XTrace 日志义务（`TerminalSessionService.cs:927-931`、`SessionShell.cs:58`）。
- **设计 token 单源**：插件前端主题 token 收敛 `web/styles/themes/*.css`（文件名命中 `check-style-tokens.mjs` EXCLUDE_SUFFIXES 与 `check-design-compliance.mjs` THEME_SUFFIX 豁免名单），组件样式只引 `var(--*)`，禁止裸色值（DESIGN.md 棘轮门禁）。

## 4. 测试方式

| 门禁 | 入口 | 判据（看日志正文，不看 exit code） |
|------|------|------|
| 后端构建 | `dotnet build ForgeSelf.slnx` | 0 错误（be-build.log 尾部「0 个错误」） |
| 后端全量测试 | `dotnet test`（ForgeSelf.Api.Tests） | 与基线红对表，新增红才是本批的 |
| 插件单测 | `dotnet test`（SmartTerminal.Tests / DbAdminTests） | 「已! - 失败: 0」 |
| 前端 check/test | `pnpm run check` / `pnpm run test`（ForgeSelf.Web） | 0 error；`Tests 1209 passed (1209)` |
| 设计门禁 | `pnpm run design:check` | `[style-tokens] PASS` + `[design-compliance] PASS`（棘轮基线不新增违例） |
| 问题类守卫 | `pwsh scripts/check-problem-classes.ps1` | 无新增违例（PC-033 为并行批遗留，另案） |
| 插件 e2e | `e2e/plugins/smart-terminal/` 冒烟 1 条 | 用例通过 + 截图读图 |
| 走查 | `pwsh scripts/dev-stack.ps1` 起源码前后端，亮/暗两态 | 验收标准逐项核对 |

环境前置（AGENTS.md §5.0）：`NO_PROXY='localhost,127.0.0.1,::1'`、`TEMP=TMP=<repo>\.temp\tmp`、一律 `pwsh`。

## 5. 构建命令

- 后端：`dotnet build ForgeSelf.slnx`
- 前端：`cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check`
- 插件前端：`cd Plugins/SmartTerminal/web && pnpm run build`
- 全量后端测试：`cd ForgeSelf.Api.Tests && dotnet test`

## 6. 主要目录职责

| 目录 | 职责 |
|------|------|
| `ForgeSelf.Core/` | 跨宿主/插件共享的核心算法库；进程输出解码器（PC-011 类设施）唯一真源 |
| `ForgeSelf.Api.Tests/Architecture/` | 架构守卫测试（如 `JsonNonAsciiEscapingGuardTests`，PC-027 类守卫） |
| `ForgeSelf.Api.Tests/Plugins/<X>/` | 插件测试**真源**目录，宿主测试工程与插件单测工程双链接编译 |
| `Plugins/SmartTerminal/web/src/styles/themes/` | 插件主题 token 文件（暗色主题 `smart-terminal-dark.css`） |
| `scripts/` | 仓库级门禁脚本（棘轮基线、问题类扫描、工件链 pre-commit 校验） |
| `gate-logs/` | 本批全部门禁实测日志（只读证据） |
| `docs/11-troubleshooting/problem-classes/` | 问题类台账（PC-011/020/027 台账与守卫注记；本批只引用） |

## 7. 代码组织方式

- Controllers → Services →（XCode）Entities 三层；异步统一 async/await。
- 守卫三形态：**架构测试守卫**（`ForgeSelf.Api.Tests/Architecture/`，随全量跑）、**脚本守卫**（`scripts/check-*.ps1|mjs`，快档必跑）、**pre-commit 工件守卫**（`scripts/verify-pilot-artifacts.ps1`）。
- 测试源码真源模式：`ForgeSelf.Api.Tests/Plugins/<X>/**` 为真源，插件 `.Tests.csproj` 用 `<Compile Include … Link>` 链接编译（见 §3）。
- 问题类闭环（problem-class-closure）：单点修复必须升级为「同类扫查 + 守卫 + PC 归档」；PC-011（进程输出编码）、PC-020（插件测试 csproj 打包）、PC-027（JSON 非 ASCII 转义）是本批命中的三个类。

## 8. 现有工程规范

- `docs/04-standards/ai-native-engineering-workflow.md` v1.2.0：九阶段闭环 + 闸门 1/2/3；§5.6 分档——本批触发「中」档（碰 `ForgeSelf.Api/**` 宿主源码与共享测试基建，须全量 `dotnet test`）。
- DESIGN.md：token 单源、棘轮基线（新增裸色值/token 族违规即红）、**主题类改动亮/暗两态都必须显式验证（`system`≠暗色）**。
- 问题类闭环门禁：07 必含「问题类归档 / 重构提案 / 悬空教训清单」三节；`pwsh scripts/check-problem-classes.ps1` 绿。
- 日志判定（AGENTS.md §5.0）：日志正文即证据；exit code 不作为证据；gate-logs 为 append（`*>>`）模式，只读尾部即可对齐当次。
- 证据分级：Verified / Inferred / Unknown，禁止虚构，禁语「应该可以」「理论上通过」。

## 9. 适用问题类（Stage 0 · 缺陷任务强制节）

本批为门禁违例修复（缺陷类），按 `problem-classes/README.md` 标签命中三个问题类：

| 问题类 | 命中情况 | 复发 | 逃逸 | 本批处置 |
|--------|----------|------|------|----------|
| PC-011 子进程输出编码 | pc.log 首跑 S1/S2 签名违例 ×3（`TerminalSessionService.cs`、`SessionShell.cs`）：事件流默认解码 + 显式 UTF-8 Encoding 属性，GBK 等非 UTF-8 输出乱码 | 是（PILOT-073 已归档同类，宿主侧修过，插件侧残留） | PILOT-073 同类扫查未纳入插件侧站点（SmartTerminal 两文件当时仍用事件流默认解码），守卫落地后本批首跑即抓到残留（非守卫豁免；pc-check2 修复后即清零） | 按 PILOT-073 FR-1/FR-3 用 ForgeSelf.Core 既有解码器接线两处站点；pc-check2 已 `status=guarded 完成` 无违例（Verified） |
| PC-020 插件测试工程打包 | be-test 首跑 `PluginLoadDependencyGuardTests` 红：`SmartTerminal.Tests.csproj` 缺 `CopyLocalLockFileAssemblies`（2bde724 只补了 DbAdminTests 侧） | 是（DbAdminTests 先例同类） | 同类扫查覆盖不全：只扫了带 SQLite 依赖的插件测试工程 | 按 DbAdminTests 先例（该 csproj L11-12）补齐同属性；孤立复跑 6/6 绿（be-test-timeoutkill-plguard.log，Verified） |
| PC-027 JSON 写出非 ASCII 默认转义 | `JsonNonAsciiEscapingGuardTests` 扫出 4 处：存量 2 处（AuditLogStore.cs:20、PermissionClassifier.cs:213，2bde724 已用 `UnsafeRelaxedJsonEscaping` 修复入库）+ 在途 2 处（SmartTerminalToolFunctions.cs:11、TerminalInstanceForwarder.cs:23，归属并行批） | 是（PC-027 守卫落地当天即抓到） | 守卫是本批新增能力，首跑即抓存量+在途，属守卫生效而非逃逸 | 存量已修复（Verified）；在途 2 处**不改**，归因记录（在途文件清单见 03 Forbidden） |

PC-033（S3 未知 kind 'source'）在 pc-check2 中仍 FAIL，属并行批（toolchoice-without-tools）范围，本批不修——见 04 Forbidden。

## 10. 候选低风险任务

门禁首跑暴露的全部违例（handoff §2）中，本批承接的低风险项：

1. **A 插件测试工程编译/打包修复**（2 个 csproj 各 1-3 行，无业务逻辑）；
2. **B 插件前端 token 化**（新增暗色主题文件 + styles.css 去裸色值，视觉等价，构建门禁可验）；
3. **C 插件侧进程输出编码接线**（复用已入库 PILOT-073 解码器，纯接线，守卫可验）；
4. **D 存量 JSON 转义修复**（2 处 Encoder 注入，守卫可验）；
5. **E PC-027 守卫扫描精度修正**（赋值式才算写出站点，比较式为入站——仅改测试扫描逻辑）；
6. **F McpStdioWindowsShimTests 环境红**——只记录不修（handoff §3F 拍板）。

**选择原因**：①全部命中既有问题类，同类修复有先例与守卫背书；②单点改动极小、可逐条用机器门禁闭环验证（构建/单测/棘轮/扫描日志），无需人为审美判断；③不触碰在途批文件（LLM 代理批、smart-terminal-open 批的在途文件一律只归因不修改），风险隔离清晰；④用户指令即「跑门禁、解决违反的地方」，本批是对该指令的最小忠实执行。
