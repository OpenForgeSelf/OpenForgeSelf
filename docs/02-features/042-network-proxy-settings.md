---
feature_key: F042
feature_no: 042
status: implemented
last_updated: 2026-10-18
aliases: ["042-network-proxy-settings", "网络设置", "模型请求代理", "不走代理的条目", "代理测试按钮"]
---

# 042 网络设置（模型请求代理）— 功能需求与设计

> 功能编号：042
> 状态：已实现（首版 2026-10-16 pilot `2026-10-16-settings-network-proxy`；升级为三模式+旁路+测试按钮 2026-10-18 pilot `2026-10-16-settings-proxy-mode-bypass`）
> 关联：003 API 服务器设置；100 密钥加密（代理密码复用 ISecretEncryptionService）
> 最后更新：2026-10-18

## 1. 功能需求

### 1.1 背景
后端向模型服务发出的请求（对话/流式/模型列表/密钥校验）需要按用户网络环境精确控制出网方式。首版只有「开/关」二值（开=手动代理，关=显式直连屏蔽系统代理），实测不满足两类场景：① 依赖系统代理但不想手填地址 ② 走代理但内网域必须豁免（代理规则命中不了内网时返回 502）。故升级为**行业通用的三模式 + 旁路条目 + 手动模式测试按钮**。

### 1.2 目标与语义（闸门1 批准案）
1. **三模式**（单选，默认 `system`）：
   - `system` 跟随系统：按 Windows 系统代理设置出网（等效 .NET 原生行为，含系统 ProxyOverride 豁免）；
   - `manual` 手动配置：走表单填写的 HTTP/HTTPS 代理地址+端口（可选认证）；
   - `direct` 直连·不走代理：**忽略系统代理**，全部请求直连（注意影响面见 §4）。
2. **不走代理的条目（旁路）**：一行一条、可加可删（上限 50 条），命中条目的目标地址**任何模式下都直连**；`manual`/`system` 显示该区块，`direct` 隐藏（全直连时旁路无意义）。
3. **测试按钮**（仅 `manual`）：按**当前表单值**（未保存也照测）经该代理发一次探测，判据=**收到任何 HTTP 响应（含 4xx/5xx）即代理可用**；探测目标可填，留空默认 `https://www.baidu.com`。
4. **保存即生效**：ModelProxy 每次回调实时读 `ForgeSetting.Current`，无需重启（现场已验证：加/撤旁路后 provider test 在 502↔404 间即时翻转）。
5. 本机回环（127.0.0.1/localhost）**永不走代理**，无需写条目。
6. 密码三语义：留空=不修改；输入=覆盖；点「清除密码」后保存=显式清空；DTO 只回显 `proxyPasswordSet`，密码本体永不下发。
7. **仅支持 HTTP/HTTPS**（BCL IWebProxy 限制）；SOCKS5 记 TODO。

## 2. 设计

### 2.1 配置字段（`Models/ForgeSetting.cs`，XML 落盘）
| 字段 | 含义 |
|------|------|
| `ProxyMode` | `system`（默认）/ `manual` / `direct`；非法值运行期按 `system` 处理并 Warn |
| `ProxyBypass` | 不走代理的条目，`\n` 分隔存储（API 侧为字符串数组）；解析时 trim、忽略空行、转小写去重 |
| `ProxyHost` | 代理服务器地址（仅域名/IPv4，**不含 scheme/端口**）；仅 `manual` 参与决策 |
| `ProxyPort` | 端口 1-65535；仅 `manual` 参与决策 |
| `ProxyUsername` / `ProxyPasswordCipher` | 可选认证（密文存储，见 100）；仅 `manual` 参与 |

> 旧字段 `ProxyEnabled` 已移除（首版未发布未提交，无存量配置；旧 XML 残留字段解析时被忽略、下次保存不再写出，无迁移逻辑）。

### 2.2 旁路条目形态（`BypassMatcher`，参考系统 ProxyOverride / NO_PROXY 惯例）
| 写法 | 匹配 | 例 |
|------|------|-----|
| `*.dom` | 后缀（含裸域自身） | `*.internal.dev` 命中 `a.internal.dev` 与 `internal.dev` |
| 裸域名 | 后缀（NO_PROXY 惯例，等同 `*.dom`） | `example.com` 命中 `sub.example.com` |
| IPv4 精确 | 主机相等 | `192.168.1.50` |
| `10.*` 段前缀 | 点分段前缀 | `10.*` 命中 `10.188.253.75`，不命中 `100.1.1.1` |
| 其它 | 非法拒绝（字符集 `A-Za-z0-9.*_:-`，单条 ≤128 字符，≤50 条） | — |

### 2.3 决策顺序（`ModelProxy.GetProxy`，BR-1）
```
① 回环 或 命中旁路条目            → 直连（null）
② direct                          → 直连（null）
③ manual                          → 校验 Regex+端口；无效则 Warn 兜底直连；再 TryBuildProxyUri（构造失败也兜底直连）
④ system                          → 委托系统代理对象（IsBypassed 命中或无配置→直连；读取异常 Warn 兜底直连）
```

### 2.4 实现位置
| 文件 | 职责 |
|------|------|
| `Services/Net/ModelProxy.cs` | `IWebProxy` 实时决策器（BR-1 顺序）+ `BypassMatcher`（五形态）+ `TryBuildProxyUri` 兜底 + `DynamicProxyCredentials` 407 凭据实时读取；系统代理经可注入的 `_systemProxyProvider`（默认 `WebRequest.GetSystemWebProxy()`） |
| `AppBuilder.cs` | IHttpClientFactory 默认 handler 挂 ModelProxy（覆盖全部出站 HTTP，含更新器与插件） |
| `Controllers/UnifiedAI/AgentChatController.cs` | OpenAI SDK transport 接同一 factory client |
| `Controllers/SettingsController.cs` | GET/POST `/api/settings`（`proxyMode`/`proxyBypass` 数组，非法模式与旁路违规 400 **带行号**）+ `POST /api/settings/test-proxy`（收未保存表单值，临时 handler，10s 超时，**零配置副作用**，密码只存内存） |
| `ForgeSelf.Web/src/components/settings/NetworkPanel.vue` | 三模式 radio / 手动表单 + 测试按钮状态机（idle→testing→success/fail，改输入或切模式即重置）/ 旁路行列表（增删、行内校验、50 条上限禁用+原因） |
| `ForgeSelf.Web/src/services/settingsApi.ts` | 字段兜底归一（模式白名单、旁路数组）+ `testProxy()` |
| `ForgeSelf.Web/src/views/SettingsView.vue` | 分类注册 `network`（Rank 图标，位于「API 服务器」后） |

### 2.5 交互规格（点什么出现什么）
- `system`：只显示「按 Windows 系统代理设置…」说明 + 旁路区块，无地址表单；
- `direct`：显示「当前不发任何代理…」说明，**旁路区块整体隐藏**；
- `manual`：按 服务器地址（同行右侧「测试」按钮）→ 探测地址 → 端口 → 用户名 → 密码 顺序出表单，五个控件等宽 240px 且整列右缘对齐；
- 点「测试」：按钮 loading/禁用 → 反馈行显示 `代理可用 · HTTP {code} · {ms}ms`（绿）或错误原文（红）；地址/端口未填则行内提示「请先填写地址和端口」**不发请求**；
- 保存被前端校验拦截：行内红字（地址/端口下方、旁路区下方左对齐），**不发请求**；错误行常驻等高占位（见 §4）；
- 保存成功：toast「网络设置已保存，模型请求即刻生效」+ 服务端回显刷新表单；后端 400 原文透出、用户输入保留。

## 3. 使用指南
设置 → 网络设置 → 选模式：
- 用系统代理：保持「跟随系统」，如需为某些内网地址豁免，在「不走代理的条目」里添加（如 `gpustack.luxvisions-inno.com` 或 `10.*`）；
- 用固定代理：选「手动配置」→ 填地址（如 `127.0.0.1`，Clash 类本地代理填其 HTTP 端口如 `7890`）→ 可先点「测试」确认代理可用 → 保存即生效；内网域同样用旁路条目豁免；
- 完全不出代理：选「直连·不走代理」（会连系统代理一起忽略，见 §4）。

代理不可达时模型请求**直接失败，不会绕开代理直连**（命中旁路条目的除外）。

## 4. 注意事项与踩坑
- **`direct` 的影响面大于「模型请求」**：代理挂在 IHttpClientFactory 默认 handler 上，选直连后**更新下载、插件出站**同样直连。依赖系统代理出网的场景不要选「直连」。
- **行内错误文案必须常驻等高占位（h-4），禁止 v-if 出现/消失**：e2e 实证——el-input-number 的值在 blur 才提交，点保存的 mousedown 抢焦点触发 blur→change→清错→按钮上移 20px→mouseup 落空→首次点击被吞（真人同样中招）。
- **Element Plus 组件定宽必须用内联 `style="width: …"`，不能用 Tailwind `class="w-[240px]"`**：同特异性下 EP 自身 `width` 规则后加载会覆盖，实测导致同列五个输入框宽度失控（180/撑满/105px）且 placeholder 尾部被裁。项目惯例参照 `UpdatePanel.vue`。
- **带 scheme 的地址（`http://host:7890`）不会被 `new Uri("http://…")` 拒绝**而是规整成非法地址——校验必须用 Regex（域名/IPv4 白名单），不可依赖 try/catch UriFormatException；反之 Regex 放行的边界串（`.`、`..--..`）仍可能在 `new Uri` 抛，故 `TryBuildProxyUri` 一律 try/catch 兜底直连。
- **`GetSystemWebProxy()` 确实应用系统 ProxyOverride**（2026-10-18 现场钉死）：本机 `ProxyOverride` 含 `*.luxvisions-inno.com` 时，`system` 模式即使我方旁路为空，该域也呈直连特征（404/401 而非 502）。因此我方旁路条目是**叠加**在系统豁免之上（BR-1 ① 先于模式分支），用于豁免「不在系统豁免列表里」的地址。
- **排障判据：收到 HTTP 502/Bad Gateway = 请求必已达转发代理**（由代理应用层返回）；绕过代理直连失败的表现是连接异常/超时而非 502。故「加/撤旁路条目 → provider test 在 502↔404 翻转」可作为旁路生效的判别器（2026-10-18 实测 D1/D2/D3）。转发代理模式下即使规则命中 DIRECT，**DNS 解析也在代理进程内完成**（客户端不解析），代理的 nameserver/fake-ip 解不了内网域时 DIRECT 也会 502——需在代理侧配 fake-ip-filter / nameserver-policy。
- 命令行设了 http_proxy 后某域「能正常返回」不必然是走了代理：shell 的 NO_PROXY / 系统代理 ProxyOverride 旁路列表可能命中该域使其实际直连——对照验证须用显式 `-x` 不带 bypass。
- 密码解密失败（换机/密钥轮换）降级为无密码并 Warn 留痕，不抛异常阻断请求。
- test-connection/fetch-models 端点直连时返 404/401 属已知端点缺陷（fallback 模型/路径拼接，见 TODO 模型网关代码层缺陷），与代理功能无关。
- **旧行为（本功能上线前）**：代码未显式配 proxy，.NET HttpClientHandler 默认 `UseProxy=true` → 跟随 WinINET 用户级系统代理（含 ProxyOverride）；`ProxyMode` 默认 `system` 即恢复该行为，故升级后默认出网方式不变。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `ModelProxyTests`（38 例） | 三模式矩阵、回环、旁路五形态（含大小写/子域/段前缀）、非法配置兜底直连、凭据实时读取、解密失败降级、真实系统代理 provider 不抛异常 |
| `SettingsControllerNetworkTests`（20 例） | GET/POST 契约（含 `proxyEnabled` 已移除的负面断言）、模式三值校验、旁路 trim 落盘/清空/非法带行号/超 50 条/超长、密码三语义、缺省字段不修改 |
| `SettingsControllerTestProxyTests`（10 例） | 参数校验 400、不可达代理 reachable=false+原文、**零配置副作用**、响应不含密码 |
| `HttpClientProxyWiringTests`（6 例） | factory client 管道确实挂 ModelProxy（.NET 10 反射链） |
| `NetworkPanel.test.ts`（21 例） | 三模式渲染区、手动校验与 h-4 占位、测试状态机（拦截/loading/成败/重置）、旁路增删与校验、保存载荷与密码三语义、400 原文透出 |
| `e2e/settings-network.spec.ts`（4 例） | 真实前后端零 mock：system 渲染、manual 非法拦截不落盘、主链路保存+旁路持久化、测试按钮失败反馈且零副作用 |
