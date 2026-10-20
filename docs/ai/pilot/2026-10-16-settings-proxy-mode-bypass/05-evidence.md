# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不根据代码推测测试结果。
> 来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。
> 执行会话：2026-10-18（闸门1 已批，用户指令「开工」）；S1/S2 主体由 2026-10-16~18 跨会话接力完成，本件记录**最终现场复跑**的结果。

## Task

PILOT `2026-10-16-settings-proxy-mode-bypass`（设置·网络：代理三模式 + 不走代理的条目 + 手动模式测试按钮），S1→S8。

## Changed Files

后端（S1/S2）：

- `ForgeSelf.Api/Models/ForgeSetting.cs` — `ProxyEnabled` → `ProxyMode`（默认 `system`）+ 新增 `ProxyBypass`（`\n` 分隔）
- `ForgeSelf.Api/Services/Net/ModelProxy.cs` — BR-1 决策顺序（回环/旁路 → direct → manual → system）+ `BypassMatcher`（五形态）+ `TryBuildProxyUri` 兜底 + system 分支委托注入的 `_systemProxyProvider`
- `ForgeSelf.Api/Controllers/SettingsController.cs` — GET/POST 契约换 `proxyMode`/`proxyBypass`（移除 `proxyEnabled`）、旁路逐条校验带行号、新增 `POST /api/settings/test-proxy`
- `ForgeSelf.Api.Tests/Unit/ModelProxyTests.cs` — 三模式矩阵 + 旁路五形态 + 真实系统代理 provider 探针
- `ForgeSelf.Api.Tests/Unit/SettingsControllerNetworkTests.cs` — 新契约读写/400 行号/密码三语义
- `ForgeSelf.Api.Tests/Unit/SettingsControllerTestProxyTests.cs` — 新建：test-proxy 校验、不可达透出原文、零配置副作用、响应不含密码

前端（S3/S4）：

- `ForgeSelf.Web/src/types/settings.ts` — `ProxyMode` / `proxyBypass` / `TestProxyRequest` / `TestProxyResult`
- `ForgeSelf.Web/src/services/settingsApi.ts` — normalize（模式白名单 + 旁路数组）+ `testProxy()`
- `ForgeSelf.Web/src/components/settings/NetworkPanel.vue` — 三模式 radio / 手动表单 + 测试按钮状态机 / 旁路行列表（增删 + 行内校验）
- `ForgeSelf.Web/src/components/settings/__tests__/NetworkPanel.test.ts` — 21 例
- `ForgeSelf.Web/e2e/settings-network.spec.ts` — 4 例（真实前后端零 mock）

## Build

Command:

```bash
dotnet build ForgeSelf.Api -p:OutputPath=bin\BuildCheck\
```

Result: **PASS**（来源等级：Verified）— 0 error（仅既有 CS8618/CS8614 等 nullable 警告）。

## Unit Test

### 后端·本域（网络代理域全量）

Command:

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~SettingsController|FullyQualifiedName~ModelProxy|FullyQualifiedName~HttpClientProxyWiring"
```

Result: **PASS**（Verified）

```text
已通过! - 失败: 0，通过: 74，已跳过: 0，总计: 74 - ForgeSelf.Api.Tests.dll (net10.0)
```

### 后端·全量对表

Command: `dotnet test ForgeSelf.Api.Tests`

Result: **本域 PASS / 全量 FAIL（31 例，其中 30 例非本域，属基线）**（Verified）

```text
失败! - 失败: 31，通过: 2948，已跳过: 0，总计: 2979 - ForgeSelf.Api.Tests.dll (net10.0)
```

逐条归因（31 例）：

- **本域 1 例**：`SettingsControllerNetworkTests.POST_缺省字段不修改_null不等于清空` — `Dispose → ForgeSnapshot.Restore()` 抛 `IOException: 无法删除要被替换的文件`（并发集合争抢 `ForgeSetting.config` 句柄，原重试窗口仅 5×200ms）。**修法已落地**：退避重试 `i*300ms`（最长约 6s），复跑本域 74/74 绿（Verified）。
- **非本域 30 例**（与本批改动无关，已登记 `TODO.md`「全量 dotnet test 基线红 30 例」）：`AIModel` 表缺失家族 16 例（MultimodalProcessor 9 + UnifiedAIGateway 6 + AIModelFeature 1，`no such table: AIModel`，= 同日并行会话在处理的问题）；WorkflowPlanningIntegration 6 例（已登记存量）；TodoDispatchFlow 2 例（盘符大小写，已登记）；源码扫描断言 2 例（`ChatController.cs` 找不到 / `Assert.NotEmpty` 空集合）；`RepositoryScriptTests` 1 例（`backfill-feature-ids.ps1` 缺 UTF-8 BOM）；ScriptRunnerDi 404 1 例；DesignSystem `ExecuteAsync_空参数_不抛` 1 例。

### 前端

Command:

```bash
pnpm exec vitest run src/components/settings/__tests__/NetworkPanel.test.ts   # 21/21
pnpm run test                                                                  # 全量
```

Result: **PASS**（Verified）

```text
Test Files  1 passed (1)      Tests  21 passed (21)          # NetworkPanel（改 placeholder 前后各跑一次，均 21/21）
Test Files  85 passed (85)    Tests  995 passed (995)        # 前端全量
```

## Integration Test

Result: **N/A**（依据：本批不涉数据库迁移与插件契约变更；后端跨层验证由 `HttpClientProxyWiringTests`（接线）+ 下方 e2e 真实宿主覆盖）。

## E2E

Command:

```bash
pnpm exec playwright test e2e/settings-network.spec.ts
```

Result: **PASS 4/4，两次**（Verified；globalSetup 每次 `dotnet publish` 当前源码 + 隔离数据根 `wt-b26d4625/publish/data`，零 mock）

```text
4 passed (1.3m)    # 第一次（S5 收口）
4 passed (1.4m)    # 第二次（placeholder 缩短 + 控件定宽修复后复跑，确认无回归）
```

覆盖：① system 默认态渲染（无手动表单 + 旁路区可见）② manual 非法地址前端拦截不落盘（含后端 GET 复核）③ 主链路 manual 保存 + 旁路添加/删除 + 刷新持久化 ④ 测试按钮不可达代理 → 行内红色错误且配置零副作用。

## 现场验收（S7 · dev-stack 源码前后端 :7301/:7399）

### 测试按钮（FR-3/BR-4）— Verified

```text
POST /api/settings/test-proxy {proxyHost:127.0.0.1, proxyPort:10808}
  → {reachable:true,  statusCode:200, latencyMs:1318, message:"代理可用（本次返回 HTTP 200）"}
POST /api/settings/test-proxy {proxyHost:127.0.0.1, proxyPort:1}
  → {reachable:false, statusCode:null, latencyMs:2031, message:"由于目标计算机积极拒绝，无法连接。"}
```

UI 侧真实点击（1440×900 与 1280 e2e 视口）：按钮进入 disabled + 「测试中（最长 10s）…」→ 绿色 `代理可用 · HTTP 200 · 789ms`（复测 529ms）；改端口为 1 再测 → 红色 `由于目标计算机积极拒绝，无法连接。`

### AC-6 旁路闭环 — Verified（同 host、同代理地址，唯一变量=旁路条目，全程不重启进程）

> ⚠️ 事后更正（2026-10-18 桌面会话补充证据节）：D2/D3 中的「HTTP 404」并非直连本身可达成功的证明，
> 而是 `AIProviderService.TestConnectionAsync` 直接 POST 裸 Endpoint（`…/v1`）的 **URL 归一化缺陷**（已修，见下节）；
> 旁路→直连的因果判定仍然成立（502↔404 切换只由旁路变量驱动）。

```text
D1 manual + 127.0.0.1:10808 + 旁路[]            → provider test = 连接失败（HTTP 502）   # 走代理
D2 manual + 127.0.0.1:10808 + 旁路[gpustack…]   → provider test = 连接失败（HTTP 404）   # 命中旁路→直连
D3 撤旁路（回到 D1 条件）                        → provider test = 连接失败（HTTP 502）   # 因果可逆
```

判别器（证明 502 是"走代理"专属指纹、404/401 是"直连"特征）：

```text
经 10808 代理探 http://gpustack.luxvisions-inno.com/v1/models → HTTP 502
强制直连（-Proxy $null）探同 URL                              → HTTP 401
系统代理 shell 默认（命中系统 ProxyOverride）探同 URL          → HTTP 401
```

### Unknown① 钉死：`WebRequest.GetSystemWebProxy()` 是否应用系统 ProxyOverride — **答案：应用**（Verified）

- 现场事实：本机注册表 `ProxyEnable=1 / ProxyServer=127.0.0.1:10808 / ProxyOverride` 含 `*.luxvisions-inno.com`；
- system 模式 + 我方旁路为空时，provider test 得 **404（直连特征）** 而非 502（代理特征）→ `ModelProxy` system 分支的 `sp.IsBypassed(destination)` 对系统 Override 返回 true；
- 结论：system 模式天然尊重系统豁免，我方旁路条目在其上**叠加**（BR-1 ① 先于模式分支），故 system 模式下仍可为「不在系统豁免里」的地址加豁免——与 Spec FR-2 一致。

## Static Analysis

Command: `pnpm run check`（vue-tsc + eslint）

Result: **PASS**（Verified）— exit 0，`149 problems (0 errors, 149 warnings)`；warnings 数与改动前基线一致（既有 lint 警告，非本批引入）。

## UI 走查与修复（S7 · ui-ux-design 判据）

首轮走查（Browser 现场，视口被锁 607px）发现 8 项疑点，逐项复核后**本批修 2 项、其余按范围控制登记**：

| 观察 | 判定 | 处置 |
| --- | --- | --- |
| 手动模式 5 个输入框宽度失控（180 / 撑满 / 105 / 撑满 / 105px），密码 placeholder 被裁成「…认证时填」 | **真缺陷**。根因：Tailwind 宽度 class 被 Element Plus 组件自身 `width` 规则覆盖（同特异性、EP 后加载）；项目惯例本就是 EP 控件用内联 `style` 定宽（`UpdatePanel.vue:265`） | **已修**：5 处 `class="w-[…]"` → `style="width: 240px"`；复测五框等宽 240、右缘统一 1215（1440 视口实测） |
| 旁路行错误提示右对齐、远离归属行 | 真缺陷（归属弱） | **已修**：改左对齐，实测 `bypass-error` left=415 与输入框左缘一致 |
| placeholder 宽 228.5 / 243.9px > 文本区 218px，清空后尾部被裁 | 真缺陷 | **已修**：文案缩短（`如 proxy.example.com` / `可选：默认测 baidu`），e2e 选择器同步，复跑 4/4 |
| 「保存」按钮与测试反馈行被宿主「召唤助手」FAB 压住 | **窄视口（607px）特有**；1440 视口实测净空 161px、1280 e2e 截图无重叠 | 非本面板缺陷 → 记 TODO（宿主 FAB 与窄窗口内容列重叠属全局） |
| 编辑端口过程中旧的成功结果未立即消失（失焦/回车提交才清） | 非缺陷：`el-input-number` 失焦才写回 v-model，表单值未变则结果仍与表单一致 | 保留，在 02-spec 语义内 |
| 模式切换面板高度跳变（~600→~120px）、成功 toast 遮盖左侧导航、toast 仅 ~3s 不利断言 | 真实体验项，非本批契约 | 记 TODO，不在本批动 |

## Screenshots

- e2e（真实宿主+真实前端，1280 视口）：`ForgeSelf.Web/screenshots/e2e/settings-network-system.png`、`-invalid.png`、`-manual.png`、`-test-fail.png`
- 现场走查（源码 dev-stack :7399）：`ForgeSelf.Web/screenshots/dev/s7-01-system.png` ~ `s7-12-final-empty-system.png`（12 张，覆盖三模式/测试成功/结果重置/失败红字/旁路增删/非法行内错/刷新持久化/清理空态）
- 修复后复核：`s7fix-*` **未产出**（内嵌 Browser 视图 `NATIVE_BROWSER_VIEWPORT_UNAVAILABLE`，视口锁 607px 无法截图）；改以 ① 同源 iframe（真实 1440×900 视口）读 `getBoundingClientRect` 数值 + ② e2e 重跑产出的 `settings-network-manual.png` 读图作为视觉证据（来源等级：Verified）。

## 数据清理

- e2e：spec `beforeAll` 备份 / `afterAll` 恢复，且跑在隔离数据根 `wt-b26d4625/publish/data`，不触碰用户配置。
- dev-stack：走查产生的 `*.s7-demo.internal` / `*.align-check.internal` 已删除并保存，最终回读 `proxyMode:"system", proxyBypass:[]`；用户运行实例 `:51888` 全程未被读写、未被停启。

## Known Limitations

- 仅支持 HTTP/HTTPS 代理，SOCKS5 未支持（界面文案已说明）。
- `test-proxy` 只验「代理能否返回任何 HTTP 响应」，不代表上游模型可用（界面文案已说明判据）。
- 修复后宽视口截图受内嵌浏览器视图限制未产出，视觉证据以 e2e 截图 + 几何实测替代。

## Unresolved Issues

- 全量后端测试 30 例非本域基线红（清单已入 `TODO.md`，本批不顺手修）。
- 宿主 FAB 与窄窗口内容列重叠、模式切换高度跳变、toast 遮盖导航 3 项体验问题已记 `TODO.md` 待专项。
- 本批改动**尚未提交、未发布**（待闸门2 批准提交、闸门3 批准发布）。

## 补充证据（2026-10-18 桌面会话 · provider test URL 归一化修复 + 宿主真实成功）

> 触发：用户指令「让宿主能用，现在宿主 ai 提供者也测试失败，解决了验一下宿主真的可以测试成功」。
> S7 现场复测时发现：网络链路已通（test-proxy 10808 → HTTP 200），但 `POST /api/ai-providers/1/test` 仍 **HTTP 404**。

### 根因（Verified，非推断）

- `AIProviderService.TestConnectionAsync` 直接 `POST config.Endpoint`（用户配置为基地址 `http://gpustack.luxvisions-inno.com/v1`）→ 该路径本身 404；
- 真实聊天链路经 `OpenAICompatibleProvider.GetChatEndpointByProviderType` 归一化（补 `/chat/completions`，Anthropic 补 `/messages`），测试链路与聊天链路规则不一致 ⇒ 「聊天能用、测试必失败」。

### 修复

- `ForgeSelf.Api/Services/AI/Providers/OpenAICompatibleProvider.cs` — `GetChatEndpointByProviderType` 改 `internal static`（连同两个私有辅助静态化，无实例状态，安全）；
- `ForgeSelf.Api/Services/AIProviderService.cs` — `TestConnectionAsync` 改用 `testUrl = GetChatEndpointByProviderType(type, config.Endpoint) ?? config.Endpoint`，Anthropic/OpenAI 两分支均 POST `testUrl`；
- `ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionUrlTests.cs` — 新建守卫：5 组 Theory 钉死归一化规则（裸 `/v1`→补全；已含 chat 路径→原样；自定义路径→只加后缀；Anthropic→`/messages`）。

### 验证（全部 Verified）

```text
① 守卫 + 本域过滤集：dotnet test --filter AIProvider|SettingsController|ModelProxy|HttpClientProxyWiring → 79/79 绿
② dev-stack 源码宿主（:7301）真链路：POST /api/ai-providers/1/test → {success:true, statusCode:200, latencyMs:251}（存证 .forgeself/tmp-providertest-s7.json）
③ test-proxy 真代理 10808：{reachable:true, statusCode:200, latencyMs:529}
④ UI 走查（Browser 子代理，源码前端 :7399，5 步全过）：三模式渲染 / 手动测试绿色反馈 / 旁路添加保存 / AI 提供方「连接成功 · 179ms」+ 拉取 7 模型；截图 screenshots/walk-step2~5-*.png 已读图核对
```

前端配套修复（同会话）：`NetworkPanel.vue` 丢失的 `onMounted(load)` 已补回（两会话踩踏产物，缺陷导致面板不拉服务端配置）；`NetworkPanel.test.ts` 删除并行会话 TEMP 探针 + `flushPromises` 时序修复，21/21 绿。

## 补充证据 S9（2026-10-18 · 「选定模型真实对话探活」+ T020 缺陷闭环 PC-009）

> 触发：用户输入3（弹窗选已登记模型 → 后端用 modelId 发真实 chat「请回复且仅回复ok」→ 响应含 ok 判成功，做完发布本地包）；拍板「没有模型禁止测试」。闸门1=该拍板（04-task 追加节 S9）。

### 改动与契约（Verified）

- 后端 `AIProviderService.TestConnectionAsync(id, modelId?)`：body 带 modelId → 用该模型发 `POST {chat/completions}`，`messages=[{user,请回复且仅回复ok}]`，**max_tokens=128**（思考型模型给 8 必被 finish_reason=length 截断，gpustack deepseek-v4-flash 实测）；判据 HTTP 200 且 ExtractContent 文本含 ok（OrdinalIgnoreCase）；省略 body=旧仅探连通（向后兼容）。模型不存在/不属该 provider 明确拒绝。
- `ExtractContent` 回退链：`message.content` → `reasoning_content`（qwen3）→ **`reasoning`（vLLM 0.31/gpustack deepseek 系，本会话两轮诊断发现的新知）** → choices[0].text（补全式）→ Anthropic；失败消息附原始响应前 500 字（诊断即产品，两轮定位全靠它）。
- 前端 `AiProvidersPanel.vue`：无已登记模型 → 测试按钮禁用 + tooltip（拍板语义）；点测试 → `provider-test-dialog` 弹窗选模型（默认预选第一个启用模型），取消=不发请求，确定=真实对话测试并展示「连接成功 · 模型回复「xx」+延迟」。

### 验证（全部 Verified）

```text
① 守卫单测 AIProviderTestConnectionOkGuardTests：35/35（含 reasoning_content×2、reasoning×3、优先级、数组 content 拼接、content:null 负例）
② AIProvider 域 dotnet test 57/57；前端门禁 pnpm run check 0 errors（152 warn 基线一致）+ vitest 1002/1002（含新增 request.test.ts 3 条）
③ 真实铁证：dev-stack 源码宿主 POST /api/ai-providers/{gpustack}/test body{modelId} → {success:true, latencyMs:2004, message:"连接成功 · 模型回复「ok」", statusCode:200}（存证 .forgeself/tmp-providertest-s9.json；Authorization Bearer 真实密钥）
④ e2e ai-providers.spec 全套 11/11 绿（--workers=1，tmp-e2e-ai-providers-s9-allgreen.log）：T018 禁用断言 / 弹窗全链路（mock 上游 18083 断言收到 请回复且仅回复ok/mock-model/max_tokens=128）/ 上游关停后真实失败提示；截图 screenshots/e2e/provider-test-{btn-enabled,dialog,ok}.png
⑤ T020 真因定案（非竞用）：干净端口窗口确定性红 → 宿主 request.ts 对 204 空体无条件 res.json() → SyntaxError 被 deleteProvider 空 catch 吞掉；修复+守卫后绿。问题类闭环：PC-009 + case 2026-10-18-host-request-204-empty-body 已建档，check-problem-classes/RegistryTests 随批跑
```

### 本批新增/改动文件（S9）

`ForgeSelf.Api/Services/AIProviderService.cs`、`ForgeSelf.Api/Controllers/AIProviderController.cs`（body 重载）、`ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionOkGuardTests.cs`、`ForgeSelf.Web/src/types/aiProvider.ts`、`ForgeSelf.Web/src/services/aiProvidersApi.ts`、`ForgeSelf.Web/src/services/request.ts`（PC-009 修复）、`ForgeSelf.Web/src/services/__tests__/request.test.ts`（新）、`ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue`、`ForgeSelf.Web/src/components/settings/__tests__/AiProvidersPanel.test.ts`、`ForgeSelf.Web/e2e/helpers/mock-upstream.ts`（chatContent 可选参）、`ForgeSelf.Web/e2e/ai-providers.spec.ts`、`docs/02-features/001-ai-provider-config.md`（真源回写）、PC-009/case/README 台账。

### 遗留（不阻断发布）

- `deleteProvider` 等确认框空 catch 语义拆分 → TODO P3；PC-009 G2 类级枚举守卫 → TODO P2（expires 2026-12-31）。
- 发布 2.4.10 本地包（输入3 明令）：见 07-final-report 与日记；运行实例只读复验待用户页面点自动更新（live 项）。
