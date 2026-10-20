# Plan

> 任务：2026-10-16-settings-proxy-mode-bypass ｜ 全部文件为真实存在路径

## Implementation Steps

### S1 后端 · 配置模型 + 决策器

- file: `ForgeSelf.Api/Models/ForgeSetting.cs` — reason: `ProxyEnabled` 替换为 `ProxyMode`（String，默认 `"system"`）；新增 `ProxyBypass`（String，`\n` 分隔，默认 `""`）；注释与 Description 同步三模式语义。
- file: `ForgeSelf.Api/Services/Net/ModelProxy.cs` — reason: `GetProxy/IsBypassed` 按 BR-1 决策顺序改造；新增旁路条目解析/匹配（`BypassMatcher`：`*.dom`/裸域名后缀、IPv4 精确、`N.*` 段前缀；大小写不敏感）；`system` 模式委托 `WebRequest.GetSystemWebProxy()`（懒取、异常兜底直连 + Warn）；`DynamicProxyCredentials` 在 `manual` 模式外不参与（系统模式凭据交系统对象）。实测钉死 Unknown①（GetSystemWebProxy 是否带 ProxyOverride）并把结论写进 05-evidence。
- file: `ForgeSelf.Api.Tests/Unit/ModelProxyTests.cs` — reason: 旧 `ProxyEnabled` 断言改造为 `ProxyMode`；新增三模式矩阵 + 旁路五形态匹配用例（AC-1/2/3）。

### S2 后端 · 设置 API + 测试端点

- file: `ForgeSelf.Api/Controllers/SettingsController.cs` — reason: GET/POST 契约换 `proxyMode`/`proxyBypass`（DTO string[] ↔ 落盘 `\n` join），BR-5 扩展校验（模式枚举、旁路字符集/长度/条数、违规带行号）；新增 `[HttpPost("test-proxy")]`：临时 `HttpClient(new HttpClientHandler{Proxy=new WebProxy(host:port), Credentials=…})` GET 探测 URL（默认 `https://www.baidu.com`），返回 `{reachable, statusCode?, latencyMs, message}`；超时 10s；密码仅内存使用，XTrace 不得输出入参密码。
- file: `ForgeSelf.Api.Tests/Unit/SettingsControllerNetworkTests.cs` — reason: 契约改造 + 旁路违规/行号/上限用例（AC-4）。
- file: `ForgeSelf.Api.Tests/Unit/SettingsControllerTestProxyTests.cs`（新增） — reason: test-proxy 参数校验（非法 URL/缺端口 400）、不可达代理（127.0.0.1:1）reachable=false、无配置副作用断言（AC-5 单元层面；真实可达留给 S7 现场）。

### S3 前端 · 类型与 API

- file: `ForgeSelf.Web/src/types/settings.ts` — reason: `proxyEnabled` → `proxyMode: 'system'|'manual'|'direct'`、`proxyBypass: string[]`；测试请求/结果类型。
- file: `ForgeSelf.Web/src/services/settingsApi.ts` — reason: 字段同步 + `testProxy(payload)` 调用封装。

### S4 前端 · NetworkPanel 改造

- file: `ForgeSelf.Web/src/components/settings/NetworkPanel.vue` — reason: 按 02-spec 交互规格：ElRadioGroup 三模式；manual 出表单（地址/端口/用户名/密码 + 同行「测试目标(可选)」小输入 + 「测试」按钮三态）；旁路条目行列表（添加/删除/空态文案/≥50 禁用）；错误行沿用 h-4 常驻占位；结果态在输入变更/模式切换时重置。
- file: `ForgeSelf.Web/src/components/settings/__tests__/NetworkPanel.test.ts` — reason: 旧 11 例改造（开关→单选组）+ 新增：模式渲染、测试拦截/成败文案、结果重置、条目增删与 payload、400 行号回显（AC-3/交互 AC）。

### S5 e2e 改造

- file: `ForgeSelf.Web/e2e/settings-network.spec.ts` — reason: 三用例改造为：① `system` 模式说明渲染；② `manual`+非法地址 → 行内拦截不发 POST（保留防吞点击回归注释）；③ 主链路：`manual` 保存 + 旁路添加/删除 + 刷新持久化；④ 测试按钮：填不可达代理（127.0.0.1:1）点测试 → 行内错误文本出现。beforeAll/afterAll 备份恢复扩展新字段。

### S6 门禁

- 后端：`dotnet build ForgeSelf.Api -p:OutputPath=bin\BuildCheck\` + `dotnet test --filter ModelProxy|SettingsControllerNetwork|SettingsControllerTestProxy|HttpClientProxyWiring`；全量 `dotnet test` 对表（本批域零红）。
- 前端：`pnpm run check` + `pnpm run test`；改动涉及 SettingsView 注册则同跑 SettingsView.test.ts。

### S7 现场验证（AC-5/AC-6）

- dev-stack（:7301 在跑）→ 浏览器 :7399：手动+10808 点测试 → 成功反馈截图；旁路加 `gpustack.luxvisions-inno.com` 保存 → provider test 走直连（不再 502，上游 401/正常）；测试按钮对 127.0.0.1:1 出红字。读图走查 UI 符合性清单（含 D-1/D-2 文字对比）。验证后按用户偏好留配置（默认恢复 system 或保持用户选择——现场询问无需，保留可复现态并告知）。

### S8 文档回写

- file: `docs/02-features/042-network-proxy-settings.md` — reason: 语义章改三模式 + 旁路 + 测试；§4 补 Unknown① 实测结论。
- file: `TODO.md` — reason: 衍生项②（测试连接）⑥（bypass）勾销/更新；本待办移除。
- 工作日记勾账；05/06/07 工件补齐。

## Test Plan

| 层 | 内容 | 判定 |
| --- | --- | --- |
| 单测后端 | 三模式×旁路矩阵、契约校验、test-proxy 参数与不可达 | 全绿，旧断言无假绿 |
| 单测前端 | 面板交互状态机（模式/测试三态/条目增删/重置） | vitest 全绿 |
| e2e | 4 用例真实前后端 | 全绿 |
| 现场 | AC-5 真实 10808 测试反馈 + AC-6 旁路闭环 | 截图 + API 输出 Verified |

## Verification（映射）

Build→S6 后端 build；Unit→S6 两侧过滤集；Integration→N/A（同上一批口径）；E2E→S5；Other→S7 现场 + 截图读图（ui-ux-design 清单）。

## 回滚点

改动前工作树（上一批成果未提交但已过全链验收，git diff 可回溯）；数据库/文件无迁移；ForgeSetting.config 字段替换为容忍解析，回滚=还原代码即可。
