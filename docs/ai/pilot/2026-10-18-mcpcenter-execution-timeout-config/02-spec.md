# 02 Spec — 外部 MCP 执行超时可配置

## Functional Requirements
- **FR-1** `McpGatewayConfig` 新增 `ExecutionTimeoutMs`（int，毫秒），内置默认 `300000`（5 分钟）。加载优先序与既有字段一致：环境变量 `FORGESELF_MCP_GATEWAY_TIMEOUT_MS` → `config.json` → 默认。
- **FR-2** 该值贯穿到三个外部传输的**执行请求超时**：`StdioMcpTransport` / `StreamableHttpMcpTransport` / `LegacySseMcpTransport` 各以构造入参接收 `requestTimeoutMs`，用于 `linked.CancelAfter(...)` 与（HTTP 传输）`_http.Timeout`。原 `const RequestTimeoutMs` 降级为 `DefaultRequestTimeoutMs`（仅作无参构造兜底），不再作为唯一真源。
- **FR-3** `McpClientManager` 构造注入 `McpGatewayConfig`，`CreateTransport(cfg)` 连接时读 `config.ExecutionTimeoutMs` 注入传输（静态方法改为实例方法或传参）。
- **FR-4** 配置对外可见可改：`McpCenterConfigDto.ExecutionTimeoutMs`（GET）、`McpCenterConfigUpdateDto.ExecutionTimeoutMs?`（PUT，不传=不变）；`McpCenterRuntime.GetInfo/UpdateAsync/PersistAsync` 对应读写；非法值拒绝并回落。
- **FR-5** 插件前端「网关配置」tab 新增「外部 MCP 执行超时」输入（单位**秒**，与后端毫秒互转），载入回填、保存组装，改后给明确生效提示（见交互设计）。
- **FR-6** 附带修：HTTP 传输 `_http.Timeout = TimeSpan.FromMilliseconds(requestTimeoutMs)`（原 `FromSeconds(ms)` 单位错）。

## Input
- `config.json` 新键 `executionTimeoutMs`；env `FORGESELF_MCP_GATEWAY_TIMEOUT_MS`（整型字符串）；PUT body `executionTimeoutMs`（秒→毫秒后的值，或按 UI 约定，见下）。

## Output
- GET 视图回 `executionTimeoutMs`（毫秒）；UI 展示为秒。
- 传输层慢响应在配置预算内不再被 30s 掐断；超时报错文案带实际生效秒数（现「外部 MCP 请求超时（{RequestTimeoutMs/1000}s）」→ 用实例字段值）。

## Business Rules
- **BR-1 范围**：仅"外部 MCP 执行请求超时"受控；`FramingProbeTimeoutMs(5s)`、`EndpointDiscoverTimeoutMs(15s)` 保持常量不变（连接建立阶段固定预算，PC-010 lens）。
- **BR-2 默认**：无任何配置 → 300000ms。
- **BR-3 合法域**：`1000ms ≤ ExecutionTimeoutMs ≤ 3600000ms`（1 秒 ~ 1 小时）。越界/≤0/非数字 → PUT 拒绝（明确中文错误），config.json/env 读到越界 → 回落默认并 XTrace 记警。上限防"永不超时"挂死线程。
- **BR-4 生效时机**：新值对**保存之后新建/重连的连接**生效；已挂起请求不被追溯打断（如实说明，见交互）。
- **BR-5 单位单一真源**：存储与传输一律**毫秒**；秒仅存在于 UI 展示/输入层（换算集中在前端，后端不混用）。

## Boundary Conditions
- env 与 config.json 同时存在 → env 胜（与既有三级序一致）。
- config.json 缺 `executionTimeoutMs` 键（老配置升级）→ 回落默认 300000，不报错。
- 传 0 / 负 / 巨大（>3600000）→ 拒绝。
- PUT 不携带该字段 → 保持原值不变（可空语义，同 Stateless）。
- 保存后未点重连的既有连接 → 仍按旧超时（界面须提示）。

## Error Handling
- PUT 越界：`ApiResponse` success=false + code=400 + 中文「执行超时须在 1 秒~3600 秒之间」。
- env/config 读到非法：不抛，回落默认 + 记日志（与既有 Load 容错风格一致）。
- 运行期真超时：`McpClientException("外部 MCP 请求超时（{n}s）：{method}")`，n 取实例生效值。

## Compatibility
- 向后兼容：老 `config.json` 无此键 → 默认 5 分钟；老 env 未设 → 默认。对外协议面不变（仍 1 universal_tool）。
- 默认从 30s→300s 是**行为变更**：更宽容（慢工具能过），不会让原本能在 30s 内完成的调用失败；无回归风险方向。

## Non-functional
- 不改数据库结构、不新增依赖、不碰鉴权核心。纯配置 + 传参贯穿。
- 传输构造签名变更需同步所有 `new XxxTransport(...)` 站点（含测试里直接构造处）。

## Interaction Design（交互设计 · UI 必写）
> 落点：McpCenterView「网关配置」tab，紧接现有「会话模式」开关行（`McpCenterView.vue:846-855`）之后，同款 `form-row` 结构。

| 触发 | 结果 | 状态模型 | 反馈 | 空态/边界 |
|---|---|---|---|---|
| 进入网关配置 tab | 「外部 MCP 执行超时」数字输入显示 `300`（秒，来自 GET 毫秒/1000） | `timeoutInputSec: number` ref，载入回填 | 无 | GET 失败沿用既有「加载网关配置失败」错误 |
| 改数值（ElInputNumber，min=1 max=3600 step=10）| 输入框本地更新 | `timeoutInputSec` | 下方 `form-hint`：「外部 MCP 工具调用的等待上限，默认 300 秒；过小会掐断慢工具（DeepWiki/Playwright）」 | 清空/非法由 ElInputNumber 夹到边界 |
| 点保存 | 仅当 `timeoutInputSec*1000 !== 现值` 才把 `executionTimeoutMs` 放进 PUT | 成功→重新 GET 回填 | 绿 toast「网关配置已保存，执行超时 {n} 秒（对新建/重连连接生效）」；失败→红 toast 带后端中文错误 | 无字段变化不发请求（沿用既有 `Object.keys(update).length===0` 短路）|
| 保存后既有连接未重连 | — | — | hint 追加一句「已连接的服务器需断开重连后采用新超时」 | 不误导为即时生效 |

**交互验收标准（并入 AC）**
- AC-UI-1 载入：GET 后输入框值 = 后端 `executionTimeoutMs/1000`。
- AC-UI-2 保存往返：改成 600 保存 → PUT 带 `executionTimeoutMs:600000` → 成功 toast → 再 GET 回显 600。
- AC-UI-3 边界：输入框 min/max 夹住（不能填 0 或 >3600）；后端同样二次校验（前端不是唯一防线）。
- AC-UI-4 生效提示文案含「新建/重连连接」限定，不谎称即时。
- 走查符合性：ui-ux-design CRAP（与相邻「会话模式」行同构对齐、标签/输入/hint 三段间距一致），走查按 UI 符合性清单核对，截图读图。

## Acceptance Criteria
- [ ] AC-1 默认：全新 config → `ExecutionTimeoutMs==300000`；UI 显示 300。
- [ ] AC-2 贯穿：改配置+重连后，一个 >30s 且 <新超时的外部工具调用能成功返回（不再 30s 掐断）。
- [ ] AC-3 env 覆盖：`FORGESELF_MCP_GATEWAY_TIMEOUT_MS=10000` 优先于 config.json。
- [ ] AC-4 校验：PUT 越界值被拒（400 中文）；config.json/env 非法回落默认。
- [ ] AC-5 单位 bug 修：HTTP 传输 `_http.Timeout` 用 `FromMilliseconds`。
- [ ] AC-6 子超时不变：帧协商 5s / endpoint 15s 常量与 PC-010 守卫全绿。
- [ ] AC-7 GET/PUT DTO + 前端类型/API/UI 全链一致（Stateless 同款）。
- [ ] AC-8 单测覆盖默认/env 覆盖/校验回落/贯穿注入；e2e 覆盖配置往返 + 一条慢工具（可用 mock 延时端点）在新超时下通过。

## Failure Modes（PC-010 lens 预填）
- PC-010 相邻：改动传输构造/超时常量 → 若误把 `FramingProbeTimeoutMs`/`EndpointDiscoverTimeoutMs` 一起参数化或改值，会破坏 stdio 帧协商与 SSE 发现（回归 PC-010 修复）。**满足方式**：这两个子超时**显式保持常量**，测试断言其值不变；单测 `McpStdioFramingTests`/`McpClientIntegrationTests` 复跑绿。
- 新字段透传教训（PC-010 §3：store/ValidateAndNormalize 吞新字段）：本配置走 `McpGatewayConfig`（非 `McpExternalServerConfig`），不经 `ValidateAndNormalize`；但 config.json 序列化/反序列化必须含新键（`PersistAsync` 的匿名对象要加 `executionTimeoutMs`），否则保存即丢——AC-2 覆盖。

## Non-Goals（明确不改）
- 不加"每服务器独立超时"（待拍板，若采纳另开子批）。
- 不给宿主 IToolRegistry 转发路径新增超时。
- 不改对外协议、不改鉴权。
