# 智能终端 v2 · 竞品调研与 MVP 建议

> 面向 ForgeSelf / OpenForgeSelf **宿主插件**的重新设计确认稿，不是独立桌面终端，也不是旧版智能终端的交互复刻。  
> 调研日期：2026-11-11（Asia/Shanghai）。功能与交互均依据公开产品页 / 文档，不凭空编造。

## 已确认范围（原型 v0.2，2026-11-12）

首版定位：宿主插件里的「可审批、可审计的命令会话」，不是独立终端，不以完整交互式 PTY / vim / htop 为验收标准。只做标签、不做左右分屏；SFTP 首版连入口都不露。权限闸保留 Auto / AskWrite / AskHighRisk，默认 AskWrite。SSH 配置与密钥进宿主凭据库，插件不存明文密码。

相对 v0.1 原型的两处交互调整（已按此改原型，详见 `CHANGELOG-v0.2.md`）：

1. 命令输入做成像真正的终端：提示符就在输出表面当前行，输入后回车即执行，不再使用下方独立输入框和「运行」按钮。写操作 / 高风险命令回车后仍可弹出确认。
2. 右侧 AI 做成真正的助手对话（消息流 + 底部提示词输入 + 发送），复用宿主已有 AI 网关 / 模型，默认不自动执行命令。

下文调研仍可对照。第五节里「简单左右分屏」和「AI 建议卡片」的待确认项已由本轮拍板取代；第四节信息架构中的「下方命令框」改为终端内提示符。

## 1. 背景与范围

旧版插件的**领域意图**（只作上下文，不沿用其 UX）是：在宿主内提供本机 / SSH **命令会话**，带基础多标签、权限分级、审计脱敏、通用 URL 启动，以及刷新后恢复标签与近期输出（不持久化密码）。当时明确不做完整交互式 PTY、分屏、SFTP。用户判定旧实现「极其糟糕、功能都用不了」，本轮推倒重来，范围需重新确认。

竞品仍覆盖领先的独立终端与编辑器内置终端，因为交互范式（标签、分屏、配置、块、搜索、AI）可以直接对照；落地时必须裁成「宿主内插件」：共用宿主外观与 AI 网关，不做第二个 Electron 壳，也不承担 RDP / VNC / X11 / 串口这类全能客户端职责。

## 2. 竞品矩阵

| 产品 | 定位 | 关键能力 | 独特交互 | 对本插件：可借 / 应避 |
|------|------|----------|----------|------------------------|
| **Warp** | AI 原生的现代终端 / 开发工作台 | Blocks（命令与输出成组）、IDE 式输入、补全与纠错、Agent Mode、Command Palette、历史与 Warp Drive 工作流、密钥脱敏 | 以 Block 为导航 / 复制 / 过滤 / 作为 AI 上下文；自然语言可切到 Agent，执行前需批准 | **借**：输出按命令成块、把块交给 AI、命令面板、密钥脱敏、执行需确认。**避**：云端 Drive、会话分享、把插件做成完整 Agent 工作台 |
| **Wave Terminal** | 开源、可带图形能力的 AI 终端 | 可拖拽的终端 / 编辑器 / 预览 / AI 块；耐用 SSH（断线与重启后重连）；内联文件预览；`wsh` 命令；BYOK / 本地模型 | 左侧滑出 AI 面板（可带终端回滚上下文）；一键连远程 | **借**：SSH 断线重连、AI 只读上下文 + 写文件需批准、BYOK 走宿主已有模型。**避**：浏览器 / 预览 / 多 widget 工作区，插件里过重 |
| **Fig → Amazon Q Developer CLI → Kiro CLI** | 给已有终端做 IDE 式补全与命令行 Agent（不是终端仿真器本身） | 数百种 CLI 的声明式补全；基于上下文的行内补全；自然语言聊天；经许可的 agent 执行。AWS 文档已写明 Q CLI 升级为 **Kiro CLI**（2025-11-17 起） | 在用户当前终端上叠加补全面板；右箭头接受行内建议 | **借**：输入时给子命令 / 参数说明，而不是另做一套壳。**避**：依赖系统无障碍 API 去「贴」在别人的终端上——我们是自己的会话 UI，应内建补全，不必复刻 Fig 的挂钩方式 |
| **Ghostty** | 快、功能全、平台原生的终端仿真器 | 原生标签与分屏、GPU 渲染、主题、Kitty 图形协议；macOS Quick Terminal、安全键盘输入 | 强调「像系统原生应用」，而不是自绘控件 | **借**：分屏 / 标签要克制、本机化；密码输入期避免回显与误记录。**避**：GPU / 图形协议 / 下拉 Quake 窗口——这些是桌面仿真器职责，不是 Web 插件职责 |
| **iTerm2** | 功能极全的 macOS 终端替代 | 任意分屏、可标签与可搜索的 Profiles、全局搜索、Shell Integration（按提示符跳转上一条命令）、触发器、密码管理（钥匙串）、Instant Replay | Open Quickly（Cmd-Shift-O）按标题 / 主机 / 目录找会话；未激活分屏略微变暗 | **借**：可搜索的 SSH/本机配置库、按命令块跳转、未激活窗格降对比。**避**：触发器、Instant Replay、内置密码库做到桌面钥匙串深度——Web 插件应把凭据交给宿主，且默认不记住密码 |
| **Windows Terminal** | Windows 上多配置、多标签的现代终端宿主 | 标签（可撕出）、分屏、配置文件（cmd / PowerShell / WSL 等）、命令面板（Ctrl+Shift+P）、命令行参数启动布局 | 命令面板可进 `wt` 命令行模式；Alt 点击「新建」自动分屏 | **借**：标签 + 适度分屏、命令面板、用配置文件区分壳。这与旧需求「对标 WT 基础操作」同向，但分屏是否进 MVP 需拍板（旧版曾明确不做） |
| **Tabby**（原 Terminus） | 跨平台现代终端 + SSH / Telnet / 串口客户端 | 连接管理、嵌套分屏、记住标签、自定义配置与快捷键、SSH 密钥加密容器、Zmodem；另有 Web 版 | 配置即连接；布局可保存；插件扩展（含快捷命令） | **借**：左侧连接 / 配置列表 + 记住打开的标签。**避**：串口、做成可插件化的独立 IDE、再做一个 Web SSH 产品 |
| **Termius** | 面向个人与团队的现代 SSH 客户端 | 主机一键连接、工作区恢复、端到端加密 Vault 同步、片段库、SFTP、钥匙串 / 硬件密钥、实时多人会话、会话日志 | 片段可粘贴或运行，可对多主机执行，可作开机命令；自动补全可命中片段名 | **借**：主机树 + 片段（粘贴 / 运行分开）。**避**：云端 Vault、团队多人同屏、跨设备密文同步——超出本插件与「本地优先宿主」的边界 |
| **MobaXterm** | Windows 上「终端 + 远程工具箱」 | 多协议会话（SSH / RDP / VNC / X11 等）、侧栏会话管理、SSH 后自动出图形 SFTP、多机同执行、宏、主密码保管会话口令 | 连上 SSH 即出文件树，远程文件可双击编辑 | **借**：会话侧栏、可回放的命令宏 / 片段。**避**：X 服务、RDP/VNC、内嵌网络守护进程——不是本插件的产品形态 |
| **FinalShell** | 国内常用的 SSH / 服务器管理 / 远程桌面加速工具（官方定位） | 官方站表述为 SSH 工具、服务器管理、远程桌面加速，支持 Windows / macOS / Linux。公开首页未给出完整功能规格表 | （官方页信息不足，此处不臆造交互细节） | **借**：承认国内运维习惯是「主机列表 + 终端」。**避**：把未核验的第三方教程功能当作承诺；也不做远程桌面加速 |
| **Electerm** | 开源、跨平台的终端 / SSH / SFTP 等全能客户端（国内常见） | 多标签与分屏、书签、跳板与隧道、远程文件编辑、路径跟随的 SFTP、快捷命令、云同步书签、**内置 AI 助手**（生成 / 解释命令、Agent 模式）、`ssh://` 深链 | 终端与文件管理器同屏、路径同步；AI 走用户自配 LLM | **借**：书签式 SSH 配置、快捷命令、深链打开会话、AI 用宿主网关而非再做一套账号。**避**：RDP/VNC/串口/云同步书签；SFTP 是否做，单列开放问题 |
| **Xshell** | 企业市场常用的 Windows SSH 客户端 | 会话管理器（文件夹 / 树）、多标签与标签组、Compose 栏向多会话发送同一字符串、快速命令、正则搜索、隧道、日志、主密码 | 「写一次、发到多个已开会话」对批量运维很典型 | **借**：配置树、快速命令、（后续）向多个会话发送同一命令。**避**：与 Xftp / Xmanager 套件捆绑的文件与 X11 能力作为 MVP |
| **VS Code / Cursor 集成终端** | 编辑器内终端（与本插件同属「嵌在宿主里」） | 标签与分屏、配置下拉、查找、链接跳转、Shell Integration 装饰、刷新重连 / 重启后恢复 scrollback、多行粘贴警告 | VS Code：终端内联聊天，建议命令可「运行」或「插入」；可用上次命令 / 选区作上下文。Cursor：由 Run Mode 决定自动跑、询问还是进沙箱 | **借**：宿主内布局、会话恢复且不把密钥写入可同步存储、AI 建议默认「先插入、批准后再跑」。**避**：把终端默认交给 Agent 自动执行；VS Code 那套任务系统过重 |

## 3. 跨产品的稳定交互模式

1. **命令块（Command Blocks）**  
   Warp 把一次输入与输出收成可选择、可复制、可过滤的块，并可作为 AI 上下文；iTerm2 Shell Integration 与 VS Code 也按提示符 / 命令边界跳转。这比无结构的滚屏更适合「看结果、问原因、再跑一条」。

2. **配置 / 主机列表 + 标签**  
   Termius、Xshell、Tabby、MobaXterm、Electerm、iTerm2 都以「先选配置、再开会话」为主路径；标签（或工作区）承载已打开的会话。这比只有「+ 新建一个空白标签再手填」更符合 SSH 使用。

3. **命令面板 + 历史搜索**  
   Warp、Windows Terminal（Ctrl+Shift+P）、VS Code、iTerm Open Quickly 都把「找会话 / 找命令 / 找动作」收成一个搜索框。插件里可用同一入口覆盖：新建本机壳、打开某 SSH 配置、插入片段、跳到某标签。

4. **片段（Snippets / 快速命令）**  
   Termius 明确区分「粘贴」与「运行」，支持开机命令与多会话执行；Xshell / Electerm / MobaXterm 也有快速命令或宏。这是运维高频动作，实现成本低于 SFTP。

5. **AI 辅助，但默认不自动落命令**  
   Warp Agent、Wave AI、VS Code 终端内联聊天、Electerm AI、Cursor Run Mode 均已把「用自然语言要一条命令 / 解释报错」做成主流能力。共同底线是：**建议先展示，执行需用户确认**（Warp 文档甚至把 Agent 比成需要盯着的自动驾驶）。宿主已有 AI 网关，插件不宜再做独立账号体系。是否把 AI 放进首个可发布里程碑，仍需拍板，但调研上它已接近「现代终端的桌面预期」。

6. **分屏是桌面终端标配，插件里要克制**  
   WT、iTerm、Ghostty、VS Code、Tabby、Electerm 都支持分屏。旧版曾明确不做。作为宿主插件，**建议 MVP 只做「同一标签内左右两栏」**，不做任意嵌套分屏。是否纳入首版见开放问题。

7. **恢复会话、不恢复秘密**  
   VS Code 可在窗口重载时重连进程并恢复 scrollback；Tabby 记住标签；Wave 强调耐用 SSH 与自动重连；Termius 可恢复工作区。共同约束：口令与密钥不进可持久化、可分享的存储。旧需求里的刷新恢复与此一致，建议保留。

## 4. 推荐的信息架构（插件内）

```
宿主顶栏 / 状态栏（非插件）
└ 智能终端
   ├ PageHead：眉题 + 标题 + 主操作「新建会话」
   ├ 左栏：配置与会话（本机壳 / SSH 配置，可搜索；标明已打开 / 已断开）
   ├ 中栏：会话标签
   │    ├ 输出区：按命令分块的终端表面（原型为模拟输出，非真实 PTY）
   │    └ 输入区：多行友好的命令框 + 历史 / 补全
   └ 右栏（可关）：AI 建议 | 片段 | 命令历史
浮层（互斥优先建议）：风险确认 → 交互式密码 → 写操作审批（若保留权限闸）→ 命令面板
```

主路径：从左栏打开配置或「新建会话」→（SSH 无密钥时先出密码框，确认后再建标签）→ 在块化输出里工作 → 用历史 / 片段 / AI 插入下一条 → 必要时批准后执行。

## 5. 建议的 MVP（v0.1 确认范围）

下列条目均能在竞品中找到依据，且适合宿主插件。标「待确认」的不视为已定范围。

**建议纳入**

- 本机壳会话（bash / zsh / sh；Windows 下 cmd / PowerShell / pwsh）与 SSH 会话
- 可搜索的配置列表（本机壳 + 已保存 SSH：主机、端口、用户、认证方式；**不保存明文密码**）
- 基础多标签：新建 / 切换 / 关闭 / 重命名；各会话输出与历史隔离
- 命令输入、按命令分块的输出、块内复制、输出 / 历史搜索
- 新建会话流程：本机直接开；SSH 默认密钥，否则交互式密码，取消则不建标签
- 片段：保存、插入或运行（运行若触及写操作，走已定的审批策略）
- 刷新 / 重进插件：尽量恢复标签、连接摘要、上限内输出；密码永不落盘
- 展示与审计中的密钥脱敏（对标 Warp Secret Redaction 与旧版审计底线）
- 连接断开态与一键重连（SSH 若无密钥，重连再次走密码框）
- 空态 / 加载 / 错误 / 未登录（401），与项目交互模式库一致

**建议纳入，但需你确认**

- **简单左右分屏**（同一标签两会话）。竞品几乎标配；旧版曾排除。原型里演示了两栏，便于看清密度。
- **AI 侧栏**：解释上一块输出、建议下一条命令，动作为「插入」或「批准后运行」。建议复用宿主模型配置，默认关闭自动执行。若首版要极小，可先留入口、后接模型。

**明确非目标（本轮不建议做）**

- 完整桌面仿真器：真实全屏 TUI 兼容作为验收标准、GPU、Kitty 图形、Quake 下拉窗
- RDP / VNC / X11 / 串口 / 内嵌 FTP 服务（MobaXterm / Electerm 全能项）
- 云端保险库、团队实时同屏、跨设备密文同步（Termius）
- 内置 SSH 密钥生成与托管 UI（与旧非目标一致；密钥应由宿主凭据体系处理）
- 密码进 URL、或任何「记住本会话密码」勾选
- Wave 式多 widget（浏览器、文件预览网格）作为默认工作区
- 完整 Windows Terminal 配置 JSON / 标签撕出到独立窗口

**刻意延后、不在 MVP 承诺里写死**

- SFTP / 远程文件树（Termius、MobaXterm、Electerm、FinalShell 生态里很常见，旧版也已延后）
- 向多个已打开标签同时发送同一命令（Xshell Compose / Termius 多主机片段）
- 跳板机、端口转发的完整 UI
- 官方单测级「完整交互式 PTY」——产品对外仍建议说「宿主内的命令会话 / 终端工作台」，PTY 深度是实现问题，需单独拍板

## 6. 开放问题（展示原型时请确认）

1. **产品定义**：还是「可审批、可审计的命令会话」，还是做成更接近完整交互式终端（用户可跑 vim / htop 一类 TUI）？这决定后端是「命令 + 流式输出」还是必须上 PTY。
2. **权限闸**：Auto / AskWrite / AskHighRisk 与审计抽屉是否仍是差异化能力？若保留，默认是否仍为 AskWrite？
3. **分屏**：首版是否包含「一标签两窗格」？还是只做标签？
4. **AI**：首版是否接上宿主模型？默认「仅建议」还是允许在确认后代为执行？
5. **SFTP**：是否明确放到下一里程碑，还是连入口都不露？
6. **自动化入口**：旧的 URL 深链与 `st_*` Agent 工具是否继续作为一等能力？
7. **凭据存哪**：SSH 配置与密钥只存在插件内，还是并入宿主凭据库？
8. **Windows 与 Linux 壳**：是否仍保持双边一等（对方平台壳列出但标记不可用）？

## 7. 来源（本轮实际打开或核验的页面）

- Warp 功能总览与快速入门 / AI / Blocks：[https://www.warp.dev/all-features](https://www.warp.dev/all-features)、[https://docs.warp.dev/quickstart/](https://docs.warp.dev/quickstart/)、[https://www.warp.dev/ai](https://www.warp.dev/ai)、[https://docs.warp.dev/agents/local-agents/agent-context/blocks-as-context/](https://docs.warp.dev/agents/local-agents/agent-context/blocks-as-context/)
- Wave：[https://docs.waveterm.dev/](https://docs.waveterm.dev/)、[https://docs.waveterm.dev/waveai](https://docs.waveterm.dev/waveai)、[https://github.com/wavetermdev/waveterm](https://github.com/wavetermdev/waveterm)
- Fig / Amazon Q / Kiro：[https://github.com/aws/amazon-q-developer-cli-autocomplete](https://github.com/aws/amazon-q-developer-cli-autocomplete)、[https://aws.amazon.com/about-aws/whats-new/2024/06/amazon-q-inline-completions-command-line/](https://aws.amazon.com/about-aws/whats-new/2024/06/amazon-q-inline-completions-command-line/)、[https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/command-line.html](https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/command-line.html)、[https://kiro.dev/docs/upgrade-guides/migrating-from-q/](https://kiro.dev/docs/upgrade-guides/migrating-from-q/)
- Ghostty：[https://ghostty.org/docs/features](https://ghostty.org/docs/features)、[https://ghostty.org/docs/about](https://ghostty.org/docs/about)
- iTerm2：[https://iterm2.com/features.html](https://iterm2.com/features.html)、[https://iterm2.com/documentation-general-usage.html](https://iterm2.com/documentation-general-usage.html)
- Windows Terminal：[https://learn.microsoft.com/en-us/windows/terminal/](https://learn.microsoft.com/en-us/windows/terminal/)、[https://learn.microsoft.com/en-us/windows/terminal/panes](https://learn.microsoft.com/en-us/windows/terminal/panes)、[https://learn.microsoft.com/en-us/windows/terminal/command-palette](https://learn.microsoft.com/en-us/windows/terminal/command-palette)
- Tabby：[https://github.com/Eugeny/tabby](https://github.com/Eugeny/tabby)（README）
- Termius：[https://termius.com/index.html](https://termius.com/index.html)、[https://docs.termius.com/terminal/snippets](https://docs.termius.com/terminal/snippets)
- MobaXterm：[https://mobaxterm.mobatek.net/features.html](https://mobaxterm.mobatek.net/features.html)
- FinalShell 官方站（定位说明，功能矩阵不完整）：[https://www.hostbuf.com/](https://www.hostbuf.com/)
- Electerm：[https://electerm.org/](https://electerm.org/)
- Xshell：[https://www.xshell.com/en/xshell-all-features/](https://www.xshell.com/en/xshell-all-features/)
- VS Code 终端：[https://code.visualstudio.com/docs/terminal/basics](https://code.visualstudio.com/docs/terminal/basics)、[https://code.visualstudio.com/docs/terminal/advanced](https://code.visualstudio.com/docs/terminal/advanced)
- Cursor 终端工具：[https://cursor.com/docs/agent/tools/terminal](https://cursor.com/docs/agent/tools/terminal)

旧插件领域文档仅作范围对照（未复用其界面结构）：`/workspace/OpenForgeSelf/Plugins/SmartTerminal/docs/`。视觉 token 取自仓库 `DESIGN.md` 与成功先例 `/workspace/lab-prototype/lab-prototype.html`。
