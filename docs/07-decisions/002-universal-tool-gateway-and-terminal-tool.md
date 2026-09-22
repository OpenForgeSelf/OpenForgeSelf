# ADR-002：万能工具聚合走既有 ToolRegistry 分发核，命令执行独立工具并默认白名单拒绝

- 状态：Accepted（2026-09-15）
- 关联：docs/07-decisions/001-cordis-kernel-architecture.md · specs/031-universal-tool-gateway/
- 背景Link：AIAgentService 内注释（77 工具全量挂载爆 prompt 的教训）· 无 shell 工具导致的 Agent 能力缺口

## 决策

1. **万能工具（universal_tool）不是新的分发引擎**，而是 `ToolRegistry.ExecuteToolWithResultAsync` 之上的调用壳：解析 `{tool, parameters}` → 原样转发 → 结果原样返回。注册、事件链（tools/pre-execute 拒绝门 / execute / post-execute）、使用统计零新增。
2. **命令执行（run_terminal_command）独立成工具**，不并入万能工具：它是"发红星能力"，需要独立的安全门（可执行白名单、管道/链拒绝、CWD 越界拦截），独立注册/统计/观察。万能工具允许转发到它（等价直调），不引入双重语义。
3. **命令执行默认白名单拒绝模式**：仅放行 dotnet / pnpm / node / git / ssh / pwsh，写死在 TerminalCommandGuard（V1 + 预留配置覆盖）。模型侧无法通过万能工具绕过——shell 层无管道无链式，所有拒绝路径不启动任何子进程。
4. **模型侧挂载走白名单增量**：universal_tool / run_terminal_command 加入 ResolveOwnToolDefinitions 白名单，不绕过 enabledToolNames 机制（空=全挂兼容保留，零回归）。

## 依据

- 分发核已完整存在且与需求精确匹配（specs/031/research.md F1）——新造引擎违反 001 号架构决策的 Cordis 内核原则（能力收敛于宿主服务契约）。
- 命令合并进万能工具会让"每类危险能力总有独立安全面"的审计原则失效，也使 guard 无法被单测隔离。
- 本地小模型上下文预算稀缺：3 个通用定义 + 精简集，优于全量挂载；等 embedding 推荐等重型方案回到历史已经证明的坑（400 错误）。

## 后果

- 正面：Agent 获得全量工具可见性 + 真实终端能力；插件动态注册的工具即刻可被万能工具命中；事件流/统计/step 记录零新增成本。
- 负面：万能工具多一跳字符串序列化（可忽略）；命令工具白名单需要随使用呼吸性扩列（运维成本 V1 可接受）。
- 边界：工具发现/名空间治理（remoteTargets 类映射）不在本 ADR 范围，触发条件见 design.md §12 not-to-do 台账。
