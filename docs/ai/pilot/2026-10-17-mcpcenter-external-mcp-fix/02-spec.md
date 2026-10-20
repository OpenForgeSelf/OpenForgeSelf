# 02-spec：外部 MCP 三缺陷修复批（2026-10-17）

## S1 initialize 必带 clientInfo（修 ①②根因，M1）
- `McpClientProtocol.BuildInitializeParams(protocolVersion)` 单点构造 `{protocolVersion, capabilities:{}, clientInfo:{name:"OpenForgeSelf-McpCenter", version:<插件程序集版本>}}`。
- 三传输 `InitializeAsync` 一律调用它，禁止再手写匿名对象（源码守卫钉住）。
- Acceptance：红灯测试绿；守卫测试绿。

## S2 握手后必发 notifications/initialized（M3-a）
- 三传输在收到 initialize result 后发送 JSON-RPC 通知（无 id），**尽力而为**：失败记 Warn 日志、不阻断连接（兼容宽松服务器；严格服务器下后续调用自会暴露）。
- 通知体 `{"jsonrpc":"2.0","method":"notifications/initialized"}`（params 省略）。
- Acceptance：Streamable 测试捕获到第二个 POST 即通知；stdio/SSE 由集成测试回归不破 + 源码守卫（含 "notifications/initialized"）。

## S3 Streamable HTTP 会话复用（M3-b）
- 从 initialize 响应头捕获 `Mcp-Session-Id`（若服务器返回），后续 POST 与 GET 流一律回传；同时携带协商后的 `Mcp-Protocol-Version` 头。
- 无会话头的服务器（如 DeepWiki 无状态）行为不变。
- 类注释「每次请求独立会话」的缓做声明作废。
- Acceptance：新单测——fake handler 返回 Mcp-Session-Id 头 → 断言后续请求携带该头。

## S4 编辑全回显 + 掩码不往返（修 ③，M2）
- `types/external.ts` StateDto 补 `url?/command?/args?`（后端 :131-135 本来就在发明文）。
- `openEditExternal` 回填真值；记录「掩码基线文本」（headersText/envText 打开时形态）。
- `saveExternal` 编辑态：掩码文本与基线**逐字相同** ⇒ 该字段提交 `undefined`（后端 `dto.Headers ?? base` 保留原值）；改动过 ⇒ 提交解析结果。
- 后端 `ValidateAndNormalize` 拒绝掩码形态值（`****` 开头且长度 ≤8 ⇒ 400，文案告知「脱敏展示非真实密钥」）。
- UI：编辑态请求头/环境变量输入框下加一行说明（掩码为脱敏展示，不改动则保留原值）。
- Acceptance：dev-stack 走查——编辑 deepwiki 见 url；带密钥条目编辑保存后 external-servers.json 密钥原文未变。

## S5 交互设计（ui-ux-design 走查口径）
- 编辑对话框打开即所见 = 已存配置真值（密钥除外，其以掩码呈现并有文字说明其语义）；点什么出现什么：保存→toast→列表刷新；无新增不可逆操作。
- 说明文案与既有表单提示同层级同字号，不引入新色值（Element Plus `--el-*` 变量）。

## 兼容性 / 回归面
- node mock 三传输集成测试（通知会被 mock 忽略或返回无主响应——各传输 Dispatch 均按 id 匹配，无 id 消息被丢弃，已核）。
- `McpClientSessionTests` 用 Moq 假传输，不经真实 InitializeAsync 载荷，不受影响。
- 接口无签名变化 → 无跨插件影响（McpCenter 内部类型）。
