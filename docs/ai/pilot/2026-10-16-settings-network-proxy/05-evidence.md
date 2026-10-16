# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：Verified（亲自跑过）/ Inferred（推断）/ Unknown（未验证）。

## Task

2026-10-16-settings-network-proxy（设置·网络设置：模型请求 HTTP/HTTPS 代理）

## AC 追源（02-spec 验收标准 AC-1~AC-6，本文标记 AC1…AC6）

| AC | 判据 | 证据 | 等级 |
|----|------|------|------|
| AC1 | dotnet build+test 绿（ModelProxyTests 开/关/回环/凭据/无效配置） | Unit Test 节：过滤集 26/26，本批域零红 | Verified |
| AC2 | 工厂默认 client 与 typed AIService 的 handler.Proxy 为 ModelProxy（DI 真链路） | HttpClientProxyWiringTests 5/5（.NET 10 反射链） | Verified |
| AC3 | 前端 check 0 error + test 绿（NetworkPanel 逻辑/校验/密码语义） | Static Analysis 节 + 985/985 | Verified |
| AC4 | e2e settings-network.spec.ts 真实前后端通过 + 截图 | E2E 节 3/3 + Screenshots 三图读图 | Verified |
| AC5 | 现场验证：dev-stack 起后保存即生效（无需重启）+ 真机代理转发 | 现场验证节 + 真机代理转发验证节（三组对照） | Verified |
| AC6 | 关闭态=显式直连不再跟随系统代理，文档/面板文案双处可见 | 042 档案 §1.2-2/§4 + NetworkPanel 文案 + 单测关闭态例 | Verified |

## Changed Files

后端（修改）：
- `ForgeSelf.Api/Models/ForgeSetting.cs` — 新增 ProxyEnabled/ProxyHost/ProxyPort/ProxyUsername/ProxyPasswordCipher 五字段（XML 落盘）
- `ForgeSelf.Api/Controllers/SettingsController.cs` — GET/POST /api/settings 增代理字段读写、BR-5 校验（域名/IPv4 正则、端口 1-65535）、密码三语义（留空=不改/「清除密码」=置空）、密码 DPAPI 加密存储、DTO 只回显 proxyPasswordSet
- `ForgeSelf.Api/Services/Net/ModelProxy.cs` — 新增：IWebProxy 实时决策器（保存即生效、回环永不走代理、无效配置兜底直连+Warn、运行期与 BR-5 同规则 Regex 校验）
- `ForgeSelf.Api/AppBuilder.cs` — IHttpClientFactory 默认 handler 挂 ModelProxy（覆盖 M1~M5 模型请求出口）+ ISecretEncryptionService 复用
- `ForgeSelf.Api/Controllers/UnifiedAI/AgentChatController.cs` — OpenAI SDK transport 接同一 factory client（M6）

出口复核（Verified，提交前 diff 甄别）：`AIProviderRegistry.cs`、`AIModelService.cs` 的 M2/M3 出口本就经 IHttpClientFactory，AppBuilder 默认 handler 挂 ModelProxy 即覆盖，**本批未改这两文件**（工作区现存 diff 属并行 AIModel 别名批，不随本批提交）；Changed Files 早期版本的「出口收敛到这两文件」描述已按实况修正。

后端（新增测试）：
- `ForgeSelf.Api.Tests/Unit/ModelProxyTests.cs`（12 例）
- `ForgeSelf.Api.Tests/Unit/SettingsControllerNetworkTests.cs`（9 例）
- `ForgeSelf.Api.Tests/Unit/HttpClientProxyWiringTests.cs`（5 例，含 .NET 10 HttpClient._handler 反射链）

前端（修改）：
- `ForgeSelf.Web/src/types/settings.ts`、`src/services/settingsApi.ts` — 代理字段类型与 API
- `ForgeSelf.Web/src/components/settings/NetworkPanel.vue` — 新增面板（开关/地址/端口/用户名/密码/保存；错误行常驻等高占位防布局跳动）
- `ForgeSelf.Web/src/views/SettingsView.vue` — 注册 network 分类（Rank 图标，API 服务器之后）
- `ForgeSelf.Web/src/__tests__/SettingsView.test.ts` — navItems 索引修正

前端（新增测试）：
- `ForgeSelf.Web/src/components/settings/__tests__/NetworkPanel.test.ts`（11 例）
- `ForgeSelf.Web/e2e/settings-network.spec.ts`（3 例，真实前后端零 mock + 备份恢复）

## Build

Command: `dotnet build ForgeSelf.Api -p:OutputPath=bin\BuildCheck\`（规避运行中宿主 DLL 锁）

Result: **PASS**（来源等级：Verified）— 0 errors。

## Unit Test

Command: `dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~ModelProxy|SettingsControllerNetwork|HttpClientProxyWiring`

Result: **PASS 26/26**（Verified）

全量（§5.6 中档，碰宿主源码必跑）：`dotnet test` 第二轮 **23 failed / 2956 passed / 2979**。归属对表：
- WorkflowPlanning×6 + ScriptRunnerDi×1 = 存量基线红（与本批无关，先于本批存在）；
- MultimodalProcessor×8 / AIServiceTests×3 / AIModelFeature / ChatControllerInvariant = 并行会话未提交改动残留 + 并发 flaky——**隔离复跑 UnifiedAIGateway 10/10、AIServiceTests+MultimodalProcessorCacheTests 21/21 全绿**（Verified）；
- RepositoryScriptTests BOM×2 = `scripts/backfill-feature-ids.ps1` 缺 UTF-8 BOM（30b076f 提交遗留，非本批文件，已记 TODO）；
- 本批域（ModelProxy/SettingsControllerNetwork/HttpClientProxyWiring/Settings/Proxy）**零红**。第一轮曾出现的 SettingsControllerNetworkTests config 句柄 IOException 红已用 Restore 重试修复（第二轮消失，Verified）。

前端 Command: `pnpm run test`

Result: **PASS 985/985**（Verified，含 NetworkPanel.test.ts 11 例）

## 提交前审计（Bugbot，2026-10-16 用户提醒补流程）

结论：**无 Critical/High**（注入/代理绕过/密码泄漏/XSS 关键面均有防护），2 条 CONSIDER：
1. `ModelProxy.GetProxy` 的 `new Uri` 构造在 Regex 放行的极端串下可能抛异常 → **已修**（try/catch 兜底直连+Warn），新增 Theory 4 例锁「永不抛异常」；复跑过滤集 **30/30 绿**（Verified）。
2. GET /api/settings 无 [Authorize]（既有设计），本批新增字段使匿名可读面扩大到 proxyHost/Port/Username → 改动鉴权语义超出本批边界，**记 TODO 待拍板**（含对前端/更新链匿名读取路径的影响评估）。

## Integration Test

N/A — 本任务无独立集成测试层；控制器行为由 SettingsControllerNetworkTests（真 ForgeSetting + 备份还原）与 e2e（真实落盘）覆盖。

## E2E

Command: `pnpm exec playwright test e2e/settings-network.spec.ts`（真实 publish 宿主 :7102 + vite dev 前端，零 mock）

Result: **PASS 3/3**（Verified）
1. 关闭态：显示直连说明、不渲染表单 ✓
2. 开启但非法：行内校验拦截、无 POST 发出、后端未落盘 ✓
3. 主链路：开启→保存→toast→GET 回显→刷新持久化 ✓（afterAll 恢复原配置）

调试期间定位并修复一个**真实 UI 缺陷**（Verified，探针证据）：行内错误 `<p v-if>` 的出现/消失改变布局——端口 el-input-number 的值在 blur 时才提交，点保存的 mousedown 抢焦点触发 blur→change→清错→**保存按钮在 mousedown/mouseup 之间上移 20px**→mouseup 落空→click 派发给行 DIV→handler 不触发（mousedown target=SPAN(save-btn)，mouseup target=行 DIV，MutationObserver 记录 port-error `<p>` 于 -48ms 被移除）。真人首次点击同样会被吞。修法：错误行常驻等高占位（h-4），并以单测锁住回归。

## Static Analysis

Command: `pnpm run check` — Result: **PASS**（0 errors / 148 warnings 存量，Verified）

## 现场验证（S7 / AC-5）

Command: `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1`（源码起前后端：backend :7301 / frontend :7399）

Result: **PASS**（Verified）
- `POST /api/settings {proxyEnabled:true,...}` → 同进程 `GET` 立即回显 enabled=True host='proxy.example.com'（**保存即生效、无重启**）；
- 前端 dev server :7399 返回 200；
- 测试数据已清理（proxyEnabled=false 归零），dev-stack 已 `-Stop`（只停本脚本自起的源码实例，未触碰用户运行中的宿主）。

## 真机代理转发验证（输入5 补验，2026-10-16）

Command: dev-stack 源码实例 :7301 + 本机 HTTP 代理 127.0.0.1:10808（Test-NetConnection 确认在监听）；驱动端点 `POST /api/ai-providers/1/test`（gpustack 提供方，OpenAI 兼容出口 M3）+ curl 外部对照。

Result: **PASS（Verified）**，三组对照：
| 场景 | 预期 | 实测 |
|------|------|------|
| 开+坏端口 127.0.0.1:1 | 失败且连接目标=代理地址 | 「无法连接…(127.0.0.1:1)」✓ 坐实流量过代理 |
| 开+10808 → api.openai.com | 经代理可达外网 | HTTP 401（真实上游响应）；同目标直连 HTTP 000 超时 ✓ 端到端转发有效 |
| 关 | 走直连 | test 返 404（直连通道，与 curl --noproxy 一致；404 属已知 test-connection 端点缺陷，与代理无关）✓ |

旁证：gpustack 内网域经代理 502/504、直连 401 正常——代理软件对该域转发规则不通（用户侧路由问题，非代码缺陷），并印证「代理不可达直接失败不回退直连」。

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/settings-network-off.png`（关闭态）
- `ForgeSelf.Web/screenshots/e2e/settings-network-invalid.png`（非法地址行内红字 + 输入保留）
- `ForgeSelf.Web/screenshots/e2e/settings-network-on.png`（开启态回显 proxy.example.com/7890/e2e-user）

已读图核对（ui-ux-design 走查清单）：nav 高亮与图标、卡片标题不重复、行式布局与 GeneralPanel 一致（CRAP-Repetition）、label 左对齐、错误文案右对齐贴近控件、开关/保存按钮主色统一。

## Known Limitations

- ~~真实代理转发行为 Unknown~~ → **Verified（2026-10-16 输入5 真机补验）**：dev-stack :7301 + 本机 127.0.0.1:10808 代理，三组对照均符预期：① 代理开+坏端口 127.0.0.1:1 → test-connection 失败原文「无法连接…(127.0.0.1:1)」，连接目标=代理地址而非提供方域名，坐实流量过代理；② 代理开+10808 → api.openai.com 经代理 HTTP 401（真实上游响应），同一目标直连超时 HTTP 000——端到端转发有效；③ 代理关 → 走直连通道（与 curl --noproxy 一致）。开关确实改变出口路径，无静默回退。
- gpustack 内网域经代理 502/504、直连 401 正常：代理软件对该域名的转发规则不通，属用户侧路由问题（需在代理加内网 DIRECT 规则），非代码缺陷；同时印证「代理不可达直接失败、不回退直连」语义。
- 全量 dotnet test 存在并发 flaky（本机有长期运行宿主争抢 config/DB），以隔离复跑绿为准。
- 密码输入框 placeholder 在 240px 宽度下尾部截断（「…需要认证时填」），已记 TODO 不阻塞。

## Unresolved Issues

无（本批范围内全部通过；衍生项见 TODO.md：SOCKS5、连通性检测端点、非模型出口是否纳入代理、AIAgentService 休眠 HttpClient、backfill-feature-ids.ps1 缺 BOM、UnifiedAIGateway 并发 flaky 观察）。
