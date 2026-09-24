# 调研：检测命令行 Agent 工具并委派任务的开源项目

> 调研日期：2026-09-24（输入 4）
> 目的：为 **AgentHub（032 外部 Agent 中枢 / 委派总线）** 插件的「CLI 探测 + 任务委派」设计提供业界参照。
> 范围：GitHub / npm / 官方文档中「能检测 opencode、claude、codex 等命令行 agent 工具，并能把任务委派给这些 CLI agent」的项目。
> 口径：以下事实均来自公开 README / 官方文档 / 市场页，标注了来源；未核实的项已注明，不臆造。

---

## 1. 结论速览

- 这类项目已形成完整生态，核心玩家分为三类：**CLI 编排层**（MCO/Hive、Vibe Kanban、Forkn、claude_codex_bridge 等）、**进程内托管 / SDK 桥**（Claude Agent SDK、OpenCode SDK、Rivet agentOS、OpenACP）、**协议层**（ACP——Agent Client Protocol，已成为「编辑器↔agent」「agent↔agent」互操作的事实标准）。
- 同时具备「**自动探测 + 委派 + 权限档位 + 统一事件流**」四件套的项目很少：**MCO**（命令行）、**OpenACP**（ACP 桥）、**Rivet agentOS**（进程内 VM）是三个最接近 AgentHub 目标形态的参照。
- 业界已收敛的委派执行通道：**无头 one-shot**（`opencode run` / `claude -p` / `codex exec`）→ **SDK 进程内控制** → **ACP 会话协议**（JSON-RPC over stdio，typed events）。AgentHub 的 P0 走无头 one-shot、P2 接 ACP 的方向与业界一致。

---

## 2. 候选项目清单（按类别）

### A 类：CLI 编排层（检测 + 并行委派多个 CLI agent）— 与 AgentHub 最同构

| 项目 | 仓库 / 分发 | 许可 | 一句话定位 | 关键机制（已核验） |
|---|---|---|---|---|
| **MCO** | github.com/mco-org/mco（npm `@tt-a1i/mco`） | MIT | CLI-first 的 AI coding agent 编排层，一次把任务并行发给多个 agent 并对比原始回答 | `mco doctor --json` 列出本机可用 agent；内建 provider 表（claude/codex/gemini/opencode/qwen/copilot/hermes/pi/grok/cursor）；适配器契约 = **detect / run / poll / cancel / transport decode**；`read_only / write / yolo` 三档执行模式映射到各家原生 flag；`--stream jsonl` 机器可读事件；明确「不根据检测到的二进制自动推断队伍，须显式选择并先确认」 |
| **Hive** | npm `@tt-a1i/hive`（hivehq.dev，MCO 后继） | — | 浏览器工作台：Orchestrator 规划委派，workers（Claude/Codex/Gemini/OpenCode/Qwen/Pi）以真实 PTY 进程并行实现/审查/测试 | 持久 agent 身份 + 共享任务图 + 一键重启队伍 |
| **Vibe Kanban** | github.com/BloopAI/vibe-kanban（npm `vibe-kanban`，~9.4k stars） | — | 看板规划 + workspace 执行：每个 agent 一个 branch / terminal / dev server | 10+ agent **抽象为子进程命令**（Claude Code/Codex/Gemini CLI/Copilot/Amp/Cursor/OpenCode/Droid/CCR/Qwen）；**git worktree 每任务隔离**；内联 diff 审查反馈。⚠️ 官方 2026 年已宣布 sunsetting，代码库保持开源、社区 fork 延续 |
| **Forkn** | VS Code 扩展 `forkn.forkn` | MIT | 一个侧栏同时跑 Claude Code / Codex CLI / Antigravity CLI / OpenCode | **Auto detection：自动检测已安装的 AI CLI 并免配置启用**；任务队列并行（`forkn.maxParallelTasks`，默认 3）；每 provider 二进制路径可配（claude/codex/agy/opencode）；实时流式输出；不托管任何 API key（直接用已登录 CLI） |
| **100doo** | VS Code 扩展 `experlab.100doo` | — | 在隔离 git worktree 上并行运行多个 AI coding agent | 主 agent 委派子任务并行；支持 Claude/Codex/DeepSeek/GLM/Grok/opencode 及自定义 Anthropic 兼容厂商（LM Studio/Ollama/OpenRouter） |
| **claude_codex_bridge** | github.com/seemseam/claude_codex_bridge | — | 可见的多 agent CLI 团队（Claude/Codex/Gemini/OpenCode/Droid）+ 项目记忆 + **tmux 监督** | 进程级团队编排与监督（详情未逐行核验，见 awesome 清单条目） |
| **Gate4Agent** | github.com/ZENG3LD（Rust 库） | MIT | 面向 CLI agent 的统一传输层 | Pipe/NDJSON、**PTY**、**ACP（JSON-RPC 2.0）** 三种模式 + tokio broadcast 事件扇出 |
| **AI Agent 协同调度助手** | VS Code 扩展 `LongBanner.ai-agent-collab-scheduler` | — | 一个面板协调 Codex + Claude Code 分工协作 | 工作区轻量协议 `.agents/`，不替换 agent 本体 |
| **Gum Agent Mesh** | VS Code 扩展 `preechagum.gum-agent-mesh` | — | 角色化委派：Claude=分析+实现、Codex=审查+验证、DeepSeek=总结 | 每角色绑定一个 CLI；角色可配（BYO agent） |
| **ClaudeQueue** | VS Code 扩展 `danielrafaelramos.claudequeue` | — | 并行会话 + 提示队列 | `/goal` `/agents` 子代理委派；每 token 实时流式 |
| **VS Code 原生多代理**（闭源，模式参考） | VS Code 1.109+ | 闭源 | Agent Sessions view 统一管理 Claude/Codex/Copilot | 本地 agent 即时交互 / 委派云端 agent 异步长任务；会话状态同一面板 |
| **GitHub Copilot CLI `/fleet`**（闭源，模式参考） | GitHub 官方 | 闭源 | 编排器把目标拆成独立工作项，并行派发多个子代理 | 计划 → 拆分 → 并行执行（多 agent 并行范例） |

### B 类：进程内托管 / SDK 桥（把 CLI agent 当库或子进程控制）

| 项目 | 仓库 / 分发 | 许可 | 关键机制（已核验） |
|---|---|---|---|
| **Claude Code Agent SDK** | 官方（TS / Python） | 闭源 SDK | 在**你自己的进程里运行 claude 二进制**，完整保留工具/权限/会话/hooks；`claude -p/--print` 无头模式；tool approval 回调 |
| **OpenCode SDK / Server** | npm `@opencode-ai/sdk` | 开源 | `createOpencode()` 一把拉起 server+client；`opencode run` 无头一次执行；`opencode serve` 常驻实例可被外部 attach；**opencode 本身可作 ACP server** 供任何 ACP 兼容编辑器/工具驱动 |
| **Rivet agentOS** | npm `@rivet-dev/agentos` / crates `agentos-runtime` | Apache-2.0 | 进程内轻量 VM；**内建 ACP agents（Pi / Claude Code / Codex / OpenCode）**，统一 prompt API；**统一 transcript 格式 + 自动持久化**；粒度权限（文件/网络/进程/env，出向网络默认拒绝）；agent-to-agent 委派 + 可恢复 workflows |
| **OpenACP** | openacp.ai（an1creator/OpenACP） | 开源 | 「agent↔消息平台」自托管桥；**Detect agents（扫描系统）** → 按需 spawn AgentInstance 子进程（ACP JSON-RPC over stdio）→ 管理生命周期与事件流；中间件链含 `permission:beforeRequest` 等 18 个钩子 |
| **opencode 的 Claude 委派插件** | npm `@khalilgharbaoui/opencode-claude-code-plugin`（unixfox 的维护 fork） | 开源 | opencode 插件**包装 claude CLI**：默认 spawn `claude --print` 无头（可切 PTY 交互）；**工具代理**——禁用 claude 内置 Bash/Edit/Write 等，改为走 opencode 自己的执行器与权限系统；MCP 配置桥；compaction 用独立短命 spawn |
| **OpenClaw acp-agents 工具** | OpenClaw（文档） | 开源 | 创建/恢复 ACP 运行时会话、登记元数据；父任务归属后台任务；**孤儿/终结 one-shot 会话自动关闭**（会话生命周期治理范例） |
| **codex-subagent skill** | github.com/davidondrej/skills（Tessl 注册表） | 开源 | 从另一个 agent 把自包含任务委派给 Codex CLI 子代理（ChatGPT 订阅认证，无需 API key） |

### C 类：协议 / 工作流层（不是委派实现，但定义互操作接口）

| 项目 | 说明 |
|---|---|
| **ACP（Agent Client Protocol）** | agentclientprotocol.com；JSON-RPC over stdio，typed events（流式、工具调用、权限请求）；opencode / Claude Code / Codex / Gemini CLI 均已支持；JetBrains、Coder（acp-go-sdk）跟进。作用类似「LSP，但面向 coding agent」 |
| **OpenSpec（Fission-AI）** | 轻量 spec 驱动框架，桥接 21+ AI 编码助手（Claude Code/Cursor/Windsurf/Copilot…）；解决「任务怎么描述、怎么对齐」，非委派执行 |
| **OACP（oacp.dev）** | 文件式跨 agent 协调协议（跨 agent 通信 + 持久共享内存，无守护进程）；Apache-2.0 |

---

## 3. 可借鉴的核心模式

### 模式 1：探测（Detect）——三件套：PATH 定位 + 版本/能力探针 + 登录态检查

- **Forkn**：自动检测已安装 CLI 并免配置启用；同时提供每 provider 二进制路径覆盖（默认 `claude`/`codex`/`agy`/`opencode`）。
- **MCO**：`mco doctor --json` 列出可用 agent；`--skill-health` 检查技能装载。
- **OpenACP**：Detect agents 扫描系统后按需 spawn。
- 要点：探测结果 ≠ 直接可用，还需**登录态探测**（各 CLI 认证差异巨大：订阅登录 / API key / Bedrock / Vertex）；MCO 明确要求「探测到 ≠ 自动选队，先给用户确认」。

### 模式 2：委派执行（Run）——无头优先，SDK/ACP 进阶

- **无头 one-shot**（P0 首选）：`opencode run "任务"`、`claude -p`、`codex exec`、`gemini -p`——管道化、可脚本、可超时、可杀进程。
- **SDK 进程内控制**：Claude Agent SDK 在自己的进程里跑 claude；OpenCode SDK 拉起 server+client。
- **ACP 会话**（P2 首选）：JSON-RPC over stdio，typed events 覆盖流式输出/工具调用/权限请求；opencode 可作 ACP server、Claude Code 可作 ACP client——**一个协议接入多家 agent**。
- **PTY 交互**：claude_codex_bridge 用 tmux 监督交互式会话；Hive 的 workers 是真实 PTY 进程；opencode 插件可切 PTY 交互模式（订阅计费）。

### 模式 3：适配器契约 + 统一事件流（MCO 最强）

- 每个 provider 一个适配器，暴露 **detect / run / poll / cancel / transport decode** 五元组——与 AgentHub design 的「输出映射 / Provider 扩展路径」同构。
- 事件流统一为 jsonl/SSE（MCO `--stream jsonl`）；**保留各次调用的原始输出与状态，不做自动解读/自动共识**（MCO 明确「keeps answer text opaque」）。
- agentOS：跨 agent **统一 transcript 格式 + 自动持久化**，可回放审计。

### 模式 4：权限与安全（对齐 AgentHub 已拍板的 A+C 模型）

- **MCO 档位映射**：一个执行档位（read_only / write / yolo）翻译成各家原生 flag；yolo 仅显式 opt-in。
- **工具代理**（opencode 委派插件）：委派给 CLI agent 时**禁用其内置危险工具（Bash/Edit/Write…），改为通过宿主执行器 + 宿主权限系统**——比直接 yolo 放行安全得多。
- **agentOS**：粒度权限（文件/网络/进程/env），出向网络默认拒绝，VM 级隔离。
- **Claude Agent SDK**：tool approval 回调，人在回路可编程化。

### 模式 5：任务编排与隔离

- **队列 + 并行 + 重跑**：Forkn（maxParallelTasks）、ClaudeQueue、VS Code Agent Sessions。
- **git worktree 每任务隔离**（Vibe Kanban / 100doo / aq.dev 指南）：每个任务独立分支/目录，杜绝多 agent 写同一工作区互相覆盖——直接解决 AgentHub 的「cwd 级互斥」缺口。
- **会话生命周期治理**：OpenClaw 关闭无主/孤儿的 one-shot ACP 会话；Hive 任务图 + 一键重启队伍。

---

## 4. 对 AgentHub（032）的落地建议

| AgentHub 现状/决策 | 建议 | 参照 |
|---|---|---|
| CLI 出口为 P0 主交互面 | **Provider 适配器契约直接采用五元组** detect / run / poll / cancel / transport decode；每 provider 声明式 profile 增补：探测命令、登录态探测、无头命令模板、事件解析规则 | MCO |
| 探测「仅安装探测 ~0.2 人日」待拍板 | 探测三层：PATH 定位 → `--version`/能力探针 → 登录态检查（env key / auth 文件 / 各家 status 命令）；支持用户覆盖二进制路径 | Forkn、MCO doctor |
| 权限模型 A+C（严格人在回路 + 显式授信） | 权限档位映射：read_only / write / yolo 翻译成各家原生 flag；对支持工具白名单的 CLI（claude --tools / opencode 插件式），**代理工具走宿主审批**而非直接放行 | MCO、opencode 委派插件 |
| cwd 级互斥缺口（design §19 G 项） | 每任务默认 git worktree 独立分支（无头委派天然隔离）；任务结束清理或保留由人选择 | Vibe Kanban、100doo |
| 会话读取混合（CLI 出口优先 + 磁盘兜底） | 统一 transcript 思路：跨 agent 统一转录格式 + 自动持久化，供任务台/复盘复用；会话文件读取注意各家格式差异（claude `~/.claude/projects/<cwd>/<sid>.jsonl` 等） | agentOS、opencode 委派插件 |
| SSE `fromSeq` 续读（design T016） | 事件流统一 jsonl/SSE；保留原始输出 + 状态字段，不自动解读 | MCO `--stream jsonl` |
| ACP 作为后续交互面（design 已列） | P2 接入 ACP：opencode 可作 ACP server、Claude Code 支持 ACP；参考 OpenACP 的「探测→spawn→生命周期」与 OpenClaw 的孤儿会话回收；Gate4Agent 提供现成 Rust 传输层可借鉴 | OpenACP、OpenClaw、Gate4Agent |
| 委派端到端真实验证（TODO 阻塞项，codex 0.153.0 已装） | 可先用 MCO 的 provider 表与命令形态做对照冒烟：`codex exec` 无头一次执行与 `--json` 输出解析 | MCO |

## 5. 风险与注意

- **生态变动快**：Vibe Kanban 官方已 sunsetting（社区 fork 延续）；MCO 主开发已迁移到 Hive；各家 CLI 计费/认证策略在变（如 `claude --print` 2026-06 起走 Agent SDK 独立计费）。落地时以「适配器 + 声明式 profile」隔离各家差异，避免与单一 CLI 深度耦合。
- **探测 ≠ 可用**：检测到二进制不等于已登录、不等于模型可用；AgentHub 的 agent 登记应显式区分「已安装 / 已认证 / 可用」三态。
- **闭源参考**：VS Code / GitHub 的多代理编排为闭源产品，仅借鉴模式，不可复刻实现。

## 6. 主要来源

- MCO README：raw.githubusercontent.com/mco-org/mco/main/README.md；镜像 gitblind.noratr.app/mco-org/mco
- Hive：skillsllm.com/skill/tt-a1i-hive；MCO README「Next chapter: Hive」
- Vibe Kanban README：raw.githubusercontent.com/BloopAI/vibe-kanban/main/README.md；npmjs.com/package/vibe-kanban
- Forkn：marketplace.visualstudio.com/items?itemName=forkn.forkn
- 100doo：marketplace.visualstudio.com/items?itemName=experlab.100doo
- claude_codex_bridge：awesome.ecosyste.ms/projects/github.com/seemseam/claude_codex_bridge
- Gate4Agent：gitblind.noratr.app/aannoo/awesome-cli-coding-agents
- agentOS：lib.rs/crates/agentos-runtime
- OpenACP：openacp.ai；docs.openacp.ai/using-openacp/agents；raw.githubusercontent.com/an1creator/OpenACP/HEAD/CONTRIBUTING.md
- opencode 委派插件：npmjs.com/package/@khalilgharbaoui/opencode-claude-code-plugin
- Claude Code：code.claude.com/docs/en/headless；code.claude.com/docs/en/agent-sdk/overview
- OpenCode：opencode.ai/docs/cli；dev.opencode.ai/docs/acp/；opencode.ai/docs/sdk
- ACP：jetbrains.com/acp；raw.githubusercontent.com/coder/acp-go-sdk/main/README.md
- VS Code：code.visualstudio.com/blogs/2026/02/05/multi-agent-development
- GitHub Copilot CLI /fleet：github.blog/ai-and-ml/github-copilot/run-multiple-agents-at-once-with-fleet-in-copilot-cli/
- OpenClaw：documentation.openclaw.ai/es/tools/acp-agents
- worktree 隔离：aq.dev/guides/run-multiple-ai-coding-agents-in-parallel/
- OpenSpec：deepwiki.com/Fission-AI/OpenSpec
