# Specification

> 阶段：Stage 2 ｜ Task ID：2026-10-16-settings-network-proxy
> 依据：00-repository-understanding（出口 M1~M7 实测盘点）+ 01-intent。交互设计节按 ui-ux-design 技能模板编写并已 CRAP 自查。

## Functional Requirements

- FR-1 配置存储：`ForgeSetting` 新增「网络」类目 5 字段——`ProxyEnabled`(bool, 默认 false)、`ProxyHost`(string)、`ProxyPort`(int, 0=未填)、`ProxyUsername`(string)、`ProxyPasswordCipher`(string, AES 密文)。持久化走既有 `ForgeSetting.config` XML（不落库、不改表结构）。
- FR-2 代理决策组件：新增 `ModelProxy : IWebProxy`（单点，`ForgeSelf.Api/Services/Net/`），**每次请求实时读 `ForgeSetting.Current`**：
  - 未开启 ⇒ `GetUri` 返回 null（直连，且显式覆盖掉"跟随系统代理"的默认行为）；
  - 目标为回环/本机 ⇒ 返回 null（永不走代理）；
  - 开启且地址合法 ⇒ 返回 `http://{ProxyHost}:{ProxyPort}`；配置了用户名 ⇒ 附带 `NetworkCredential`（密码解密取明文，仅内存态）。
- FR-3 模型请求出口全覆盖：
  - M1~M5（全部经 `IHttpClientFactory`）：`AppBuilder` 用 `ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(...))` 给**所有 factory 生成的 client** 装上 `SocketsHttpHandler { Proxy = ModelProxy, BypassProxyOnLocal = true }`；
  - M6（`AgentChatController` 的 `OpenAIClient`）：构造 `OpenAIClientOptions` 时注入 `Transport = new HttpClientPipelineTransport(factory.CreateClient())`（复用已过代理配置的 factory client）；
  - M7（AIAgent 插件自建 HttpClient）：实测无请求调用（休眠字段），不改插件。
- FR-4 设置 API：`/api/settings` GET 增返回 `proxyEnabled/proxyHost/proxyPort/proxyUsername/proxyPasswordSet(bool)`（**永不回传密码**）；POST 接受同名可选字段并校验后落盘。
- FR-5 设置页 UI：新增分类 `network`（导航项「网络设置」，置于「API 服务器」之后；图标 `Rank`——已核实 `@element-plus/icons-vue` 存在该导出），新组件 `NetworkPanel.vue`：代理开关（el-switch）、服务器地址、端口、用户名、密码 + 保存按钮；形态对齐 GeneralPanel/ApiServerPanel（左标题右控件行式 + el-card）。

## Input

- 用户在「网络设置」面板填写：开关、host（域名/IPv4）、端口（1-65535）、可选用户名/密码。
- `POST /api/settings` JSON：`{ proxyEnabled?, proxyHost?, proxyPort?, proxyUsername?, proxyPassword? }`（缺省字段=不修改；`proxyPassword:""`=清空）。

## Output

- `GET /api/settings` → `data` 增加上述网络字段（密码以 `proxyPasswordSet` 布尔回显）。
- 后端模型请求实际经由的 handler：`Proxy` 解析结果随配置即时变化；`ForgeSetting.config` 中密码字段为密文。

## Business Rules

- BR-1 默认值=关闭=直连：不设置就不走代理（用户原话）；关闭态**显式禁用系统代理**（与现状"隐式跟随系统代理"是行为变更，属本需求本意，Spec 显式声明）。
- BR-2 回环永不走代理（本机 e2e/dev 回调、自访问不被代理劫持）。
- BR-3 生效时机：保存即生效，无需重启（`ModelProxy.GetUri` 每请求实时读取；IHttpClientFactory handler 缓存不影响实时性）。
- BR-4 密码安全：仅 `ISecretEncryptionService.Encrypt` 后落盘；GET/日志/前端均不回传明文；改密码只有"覆盖"或"清空"两种动作。
- BR-5 开启校验：`proxyEnabled=true` 时 host 非空且 port ∈ [1,65535]，否则 400 拒绝落盘（保证运行期不存在"开启但无效"状态；运行期若 host 仍解析失败则回退直连并 `Warn` 日志，请求不静默失败）。

## Boundary Conditions

- BC-1 端口 0/负数/超 65535、host 含 scheme（`http://127.0.0.1` 这类粘贴）⇒ 后端 400（host 仅允许 域名/IP，校验正则 `^[A-Za-z0-9._-]+$` 或 IPv6 暂不支持，记 Unknown）；前端行内提示同规则。
- BC-2 密码解密失败（配置手改/密钥轮换）⇒ 视为无密码 + Warn 日志，请求仍可走代理（多数匿名代理可用），错误留痕。
- BC-3 `proxyEnabled=true` 但代理不可达 ⇒ 请求按 HttpClient 超时/连接失败原样抛错（不自动回退直连——防止"以为走了代理实际裸奔"的隐私事故；界面文案说明）。
- BC-4 长用户名/密码、host 前后空格 ⇒ 后端 `Trim` 后存储。
- BC-5 更新下载（UpdateChecker/UpdateService）、WorkflowEngine HTTP 节点、McpCenter、Scheduler webhook 不属于"模型请求"，本批**不接线**，维持各自现状（记 TODO）。

## Error Handling

- POST 校验失败 → 400 `{ success:false, message:"代理地址无效：…" }`；前端 `ElMessage.error` 显示原文并保留表单值。
- 运行期代理异常 → 沿用各调用点既有错误路径（AIService/Provider 已有 try-catch + 日志），错误信息透出不吞。

## Compatibility

- 旧 `ForgeSetting.config` 无新字段 ⇒ NewLife ForgeConfig 取属性默认值（false/""/0），行为=直连，无需迁移。
- API 既有消费方（GeneralPanel 等）不受 GET 增字段影响；POST 未提供网络字段时保持"不修改"语义。
- UI 无新设计稿：按既有设置面板视觉语言（CRAP-Repetition 同类控件统一），DoD 的"与设计稿对齐"以 GeneralPanel 行式布局为基准。

## Non-functional Requirements

- 不新增 NuGet/npm 依赖（BCL + 既有包能力内）。
- 每请求实时读内存单例 `ForgeSetting.Current`，无额外 IO。
- 面板加载 ≤1 次 GET，保存 ≤1 次 POST；无轮询。

## Interaction Design（交互设计 · 必写）

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 进入网络设置 | 左栏点「网络设置」 | 右卡片显示：开关（当前值）+ 表单区 | — | 表单区随开关显隐（见下） | 首次进入显示默认值（关/空） | PageHead eyebrow「Settings · 网络设置」与各面板一致 |
| 代理开关 | 切换 el-switch | 开=下方出现 地址/端口/用户名/密码 表单+保存按钮；关=表单区收起，出现说明「不发代理请求，全部直连（含忽略系统代理）」 | 开/关 | 切换本身不保存，随「保存」提交 | — | 关→开未填地址时保存被拦截（行内红字+边框变红，不只 toast） |
| 地址/端口输入 | 输入失焦/保存前 | 前端即时校验（host 非空、port 1-65535），不合法该行下方出 12px 错误文案 | 合法/非法 | 非法=行内错误 + el-input error 态 | placeholder：例 `127.0.0.1` / `7890`（placeholder 仅示意，不当数据） | 超长 host 省略显示 + title 全文 |
| 密码输入 | 已有密码时进入面板 | 输入框 placeholder「已保存（留空则不修改）」，`show-password` 可窥视**本次输入**，不回显旧密码 | 未设置/已设置/待修改 | — | `proxyPasswordSet=false` 时 placeholder「可选：代理需要认证时填写」 | 留空保存≠清空（前端不提交该字段）；清空需显式点「清除密码」链接（提交空串，防误触；待拍板#2） |
| 保存 | 点「保存」 | 按钮进 loading → POST 成功 `ElMessage.success('网络设置已保存，模型请求即刻生效')` → 表单值与回读一致；失败 `ElMessage.error(后端 message 原文)` 且不清表单 | saving/成功/失败 | toast + 按钮 loading（组件四态齐） | — | 双击防抖：saving 期间按钮 disabled |
| 测试连通 | 本批无此按钮 | —（记 TODO：后续加「检测代理连通」端点） | — | — | — | — |

### 交互验收标准（并入 AC，逐条可测）

- AC-UX-1：开关关→开，表单四控件出现且顺序=地址、端口、用户名、密码（亲密性成组）。
- AC-UX-2：开启且地址为空点保存：不发请求、地址行出现可见错误文案（禁用/报错有可见原因）。
- AC-UX-3：保存成功 toast 文案含「即刻生效」；刷新页面重进面板，各值仍在（点即保存落盘）。
- AC-UX-4：已有密码时 placeholder 明示「留空则不修改」；GET 响应中不存在密码明文字段（Network 面板任何状态不显示旧密码）。
- AC-UX-5：saving 中按钮 disabled 且 loading 图标可见（四态 default/hover/active/disabled 视觉可区分）。

### 走查符合性（DoD 绑定）

- 实现后按 ui-ux-design「走查 UI 符合性清单」逐项核对（含 C 组设置页对齐：PageHead 与卡片左右边界对齐、行布局统一 label 左控件右；D-1 文字对比度实测；F 组表单控件均有 label/aria-label）。
- e2e 截图读图基准：导航项高亮、表单显隐两态各一张存 `ForgeSelf.Web/screenshots/`。

## Acceptance Criteria

- [ ] AC-1 `dotnet build` + `dotnet test` 绿；新增 `ModelProxyTests`（关→null；开+非回环→代理 uri；回环→null；用户名→Credential；解密失败→无密码仍走代理）与 `SettingsControllerNetworkTests`（读写/校验 400/密码不回传/密文落盘）全绿。
- [ ] AC-2 出口覆盖有测试证据：断言 factory 生成的默认 client 与 typed `AIService` 的 handler `Proxy` 为 `ModelProxy`（DI 组合根冒烟：`BuildServiceProvider` 解析后反射/行为检查均可）；`AgentChatController` 构造的 `OpenAIClientOptions` 带 Transport（代码审查 + 集成探针测试择一，见 03）。
- [ ] AC-3 前端 `pnpm run check` 0 error、`pnpm run test` 绿；`NetworkPanel` 有 vitest 覆盖（显隐逻辑 + 校验拦截 + api 载荷）。
- [ ] AC-4 e2e `settings-network.spec.ts`（真实前后端、token 注入、测后恢复原配置）通过并截图。
- [ ] AC-5 现场验证（dev-stack）：起本地 HTTP 探针 listener → 面板开启代理指向它 → 发一次模型类请求（如 GET /api/aimodel/refresh 或 /v1/models）→ 探针收到 CONNECT/请求行 ⇒ 走代理实证；关闭开关 → 探针不再收到且请求直连成功。
- [ ] AC-6 关闭态行为变更有记录：默认直连不再跟随系统代理（文档/面板文案双处可见）。

## Compatibility / Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| SOCKS5 代理 | BCL `WebProxy`/`SocketsHttpHandler` 原生只支持 HTTP(S) 代理；SOCKS 需第三方依赖 | 本批仅 HTTP/HTTPS 代理；面板文案如实写「支持 HTTP/HTTPS 代理」；SOCKS5 评估记 TODO |
| IPv6 字面量地址（含冒号） | host 解析/拼接 uri 需 `[...]` 包裹 | 本批 host 正则不收 IPv6，记 TODO；日常代理地址（域名/IPv4）不受影响 |
| 密码「清空」交互形态 | 误触清空 vs 显式清除 | 默认推荐：显式「清除」链接（AC-UX-4 按此）；待闸门1 用户拍板（待拍板#2） |
