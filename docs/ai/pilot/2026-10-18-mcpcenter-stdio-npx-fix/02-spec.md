# 02-spec（stdio 外部 MCP npx 三层修复批，2026-10-18）

## S1 Windows 批处理 shim 启动（缺陷①，M1——用户报错本体）

- `ResolveLaunch(command, args, pathDirs, isWindows, fileExists?)`：非 Windows 原样透传（execvp 自解析 shebang）；Windows 带路径分隔符直接判形态，否则 PATH 目录逐个探测 **exe > cmd > bat**（PATHEXT 优先级）；
- 命中 .cmd/.bat → `(ComSpec, "/d /s /c \"<命令全路径[含空格加引号]> <args…[含空格 token 加引号]>\"")`；命中 exe / 非 Windows → 直启（ArgumentList 原行为）；
- 全不见 → `McpClientException` 可行动报错：指名命令名、PATH 未命中、可用 `env.PATH` 覆盖、「快捷方式启动的进程不继承终端 PATH」；
- `ResolvePathDirs` 支持 `config.Env["PATH"]` 前置覆盖（注入式单测跨平台确定性）。

## S2 stdio 双帧自适应 + auto 协商（缺陷②，M1——修复①后 tools/list 超时暴露）

- 读侧 `ReadMessageAsync`：行首 `{` → NDJSON 整行；否则 `Content-Length` 头 + 空行 + 定长 UTF-8 字节体（未知头行容错跳过）；首命中锁定 `_observedFraming`；流结束返回 null；
- 写帧 = `_configuredFraming ?? _observedFraming ?? Ndjson`；
- auto 且未识别时：NDJSON 首发（官方 TS/Python SDK 生态多数，零探测成本），`FramingProbeTimeoutMs=5000` 无响应 → catch TimeoutException → 换 LSP 帧**重发同 id 一次**（`_pending.TryRemove` 天然去重，`_probeResent` 防循环）；
- 配置字段 `framing`：`""`=auto / `ndjson` / `lsp`（大小写不敏感，`ParseFraming` 非法值回落 auto）；贯穿 config→UpsertDto→controller merge→store ValidateAndNormalize 透传。

## S3 null params 省略键（缺陷③，M1——修复②后 npx 集成仍超时，A/B 探针定位）

- `McpClientProtocol.SerializeRequest(id, method, prms)` canonical 单点：prms 为 null **省略 params 键**（规范可选；FastMCP 系收字面 null 完全沉默实测）；
- 三传输（Stdio/StreamableHttp/LegacySse）RequestAsync 一律引用，禁止回退手搓（G3 源码扫描 `@params\s*=\s*prms` no-match）；
- 同族封堵：`tools/call` 的 `arguments` 空给 `{}` 而非 null。

## S4 测试靶子去同构（掩盖源治理，M2）

- `mock-mcp-server.js` 增 `--mode stdio-ndjson`（NDJSON 帧 stdio 靶子）；既有 LSP mock 集成测显式 `Framing="lsp"` 锁快路径（不白吃 5s 探测）；
- 新 e2e 用例「stdio npx 真实世界」**零容忍**：必须 connected + /test data.success=true + UI 段（`.external-item` 已连接/工具数 + 点「测试」→ `.el-message--success` 含「握手成功」+ 两张截图，截图前 `scrollIntoViewIfNeeded`）；**禁止**降级通过分支（旧应用层 e2e 的「优雅降级」即本次掩盖源）。

## 交互设计（ui-ux-design）

本批**无用户可见 UI 改动**（framing 仅 API/config 字段，UI 入口留待需要时加）；走查按既有条目：外部 MCP 列表项状态徽标（已连接/绿 toast）与 v2.5.0 版式一致，无新增控件，不需设计稿变更。

## 边界 / 回归

- 非 Windows 行为零变化（ResolveLaunch 首行短路透传；帧协商读侧本就兼容 NDJSON 行，LSP 写帧策略仅影响 stdio）；
- 老配置无 `framing` 键 ⇒ 默认 ""=auto，向后兼容；
- 白名单校验（AllowedCommands）保持 v2.1.0 安全边界不动；
- 回归风险：auto 探测给 LSP-only 服务器加一次 5s 首帧等待——既有集成/e2e 用 `framing=lsp` 显式锁形规避，真实用户 LSP-only 场景仅慢一次握手。
