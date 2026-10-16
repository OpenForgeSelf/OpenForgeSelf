---
feature_key: F042
feature_no: 042
status: implemented
last_updated: 2026-10-16
aliases: ["042-network-proxy-settings", "网络设置", "模型请求代理"]
---

# 042 网络设置（模型请求代理）— 功能需求与设计

> 功能编号：042
> 状态：已实现（2026-10-16，pilot `docs/ai/pilot/2026-10-16-settings-network-proxy/`）
> 关联：003 API 服务器设置；100 密钥加密（代理密码复用 ISecretEncryptionService）
> 最后更新：2026-10-16

## 1. 功能需求

### 1.1 背景
后端向模型服务发出的请求（对话/流式/模型列表/密钥校验）此前只能依赖系统代理，无法按用户网络环境精确控制。设置页新增「网络设置」，按行业通用形态提供 HTTP/HTTPS 代理配置。

### 1.2 目标与语义（闸门1 批准案）
1. **开启**：后端全部模型请求出口（M1~M6）经配置的 HTTP/HTTPS 代理转发；
2. **关闭 = 显式直连**：忽略系统代理（与旧「跟随系统代理」行为不同，面板文案已明示）；
3. **保存即生效**：ModelProxy 每次回调实时读 `ForgeSetting.Current`，无需重启；
4. 本机回环（127.0.0.1/localhost）目标**永不走代理**；
5. 密码三语义：留空=不修改；输入=覆盖；点「清除密码」后保存=显式清空；DTO 只回显 `proxyPasswordSet`，密码本体永不下发；
6. **仅支持 HTTP/HTTPS**（BCL IWebProxy 限制）；SOCKS5 记 TODO。

## 2. 设计

### 2.1 配置字段（`Models/ForgeSetting.cs`，XML 落盘）
| 字段 | 含义 |
|------|------|
| `ProxyEnabled` | 代理开关（false=显式直连） |
| `ProxyHost` | 代理服务器地址（仅域名/IPv4，**不含 scheme/端口**） |
| `ProxyPort` | 端口 1-65535 |
| `ProxyUsername` / `ProxyPasswordCipher` | 可选认证（密文存储，见 100） |

### 2.2 实现位置
| 文件 | 职责 |
|------|------|
| `Services/Net/ModelProxy.cs` | `IWebProxy` 实时决策器：GetProxy 判定 + 回环绕过 + 无效配置兜底直连（运行期 Regex 与 API 层 BR-5 同规则）+ `DynamicProxyCredentials` 407 凭据实时读取 |
| `AppBuilder.cs` | IHttpClientFactory 默认 handler 挂 ModelProxy（覆盖 M1~M5） |
| `Controllers/UnifiedAI/AgentChatController.cs` | OpenAI SDK transport 接同一 factory client（M6） |
| `Controllers/SettingsController.cs` | GET/POST `/api/settings` 代理字段 + BR-5 校验（「仅支持域名或 IPv4（不能带 http:// 前缀或端口）」「开启代理时端口必须为 1-65535」） |
| `ForgeSelf.Web/src/components/settings/NetworkPanel.vue` | 面板 UI（开关/地址/端口/用户名/密码/保存） |
| `ForgeSelf.Web/src/views/SettingsView.vue` | 分类注册 `network`（Rank 图标，位于「API 服务器」后） |

### 2.3 交互规格（点什么出现什么）
- 开关关闭：只显示「当前不走代理」说明，不渲染表单；
- 开关开启：按 地址→端口→用户名→密码 顺序出表单行，保存按钮在底部行右对齐；
- 保存被前端校验拦截：行内红字（地址下方/端口下方），**不发请求**；错误行常驻等高占位（见 §4）；
- 保存成功：toast「网络设置已保存，模型请求即刻生效」+ 服务端回显刷新表单；
- 后端 400：错误原文透出，用户输入保留。

## 3. 使用指南
设置 → 网络设置 → 打开代理开关 → 填地址（如 `127.0.0.1`，Clash 类本地代理填其 HTTP 端口如 `7890`）→ 保存即生效。代理不可达时模型请求**直接失败，不会绕开代理直连**。

## 4. 注意事项与踩坑
- **行内错误文案必须常驻等高占位（h-4），禁止 v-if 出现/消失**：e2e 实证——el-input-number 的值在 blur 才提交，点保存的 mousedown 抢焦点触发 blur→change→清错→按钮上移 20px→mouseup 落空→首次点击被吞（真人同样中招）。
- 带 scheme 的地址（`http://host:7890`）不会被 `new Uri("http://…")` 拒绝而是规整成非法地址——校验必须用 Regex（域名/IPv4 白名单），不可依赖 try/catch UriFormatException。
- 关闭代理 = 屏蔽系统代理（显式直连），依赖系统代理出网的场景须开启本代理。
- 密码解密失败（换机/密钥轮换）降级为无密码并 Warn 留痕，不抛异常阻断请求。
- **真机验证结论（2026-10-16，127.0.0.1:10808）**：开启后模型请求确实经代理转发（外网 api.openai.com 经代理 401 可达、直连超时；坏端口时失败原文指向代理地址而非目标域）。注意：**代理软件自身的域名规则会原样影响结果**——内网域（如 gpustack）被代理规则拦截时返回 502/504 且不会回退直连；需在代理侧为内网域配 DIRECT 规则。
- **排障判据：收到 HTTP 502/Bad Gateway = 请求必已达转发代理**（由代理应用层返回）；若绕过代理直连失败，表现是连接异常/超时而非 502 状态码。另：转发代理模式下即使规则命中 DIRECT，**DNS 解析也在代理进程内完成**（客户端不解析），代理的 nameserver/fake-ip 解不了内网域时 DIRECT 也会 502——需在代理侧为内网域配 fake-ip-filter / nameserver-policy。
- 命令行设了 http_proxy 后某域「能正常返回」不必然是走了代理：shell 的 NO_PROXY / 系统代理 ProxyOverride 旁路列表可能命中该域使其实际直连——对照验证须用显式 `-x` 不带 bypass。
- **旧行为（本功能上线前）**：代码未显式配 proxy（git grep 零命中），.NET HttpClientHandler 默认 `UseProxy=true` → 跟随 **WinINET 用户级系统代理（含 ProxyOverride 旁路列表）**；现「关闭代理」= 显式直连，**不再读系统代理也不适用其旁路**；本程序代理也无旁路列表概念（仅回环绕过），需旁路某些域只能在代理软件规则层实现。
- test-connection/fetch-models 端点直连时返 404/401 属已知端点缺陷（fallback 模型/路径拼接，见 TODO 模型网关代码层缺陷），与代理功能无关。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `ModelProxyTests`（12 例） | 开关/回环/无效配置兜底/凭据实时读取/解密失败降级 |
| `SettingsControllerNetworkTests`（9 例） | GET/POST 契约、BR-5 校验、密码三语义、缺省字段不修改 |
| `HttpClientProxyWiringTests`（5 例） | factory client 管道确实挂 ModelProxy（.NET 10 反射链） |
| `NetworkPanel.test.ts`（11 例） | 关闭态/顺序/三类拦截不发 POST/Trim/密码三语义/400 原文/占位防跳动 |
| `e2e/settings-network.spec.ts`（3 例） | 真实前后端：关闭态、非法拦截不落盘、主链路保存+刷新持久化 |
