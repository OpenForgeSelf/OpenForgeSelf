---
task_id: 2026-10-16-settings-proxy-mode-bypass
feature: 042-network-proxy-settings
risk: { level: L2, triggers: ["出站全链路代理决策", "设置契约字段更名 ProxyEnabled→ProxyMode"], raised_by_agent: false }
gate1: { mode: user, at: 2026-10-18, ref: "用户回复「开工」（三项拍板均按默认推荐）" }
gate2: { mode: user, at: 2026-10-20, decision: "分批提交批准；发布暂缓" }
gate3: { mode: reviewer, at: 2026-10-18, ref: "06-review APPROVED" }
expected_files:
  - ForgeSelf.Api/Models/ForgeSetting.cs
  - ForgeSelf.Api/Services/Net/ModelProxy.cs
  - ForgeSelf.Api/Controllers/SettingsController.cs
  - ForgeSelf.Api.Tests/Unit/ModelProxyTests.cs
  - ForgeSelf.Api.Tests/Unit/SettingsControllerNetworkTests.cs
  - ForgeSelf.Api.Tests/Unit/SettingsControllerTestProxyTests.cs
  - ForgeSelf.Api.Tests/Unit/HttpClientProxyWiringTests.cs
  - ForgeSelf.Web/src/types/settings.ts
  - ForgeSelf.Web/src/services/settingsApi.ts
  - ForgeSelf.Web/src/components/settings/NetworkPanel.vue
  - ForgeSelf.Web/src/components/settings/__tests__/NetworkPanel.test.ts
  - ForgeSelf.Web/e2e/settings-network.spec.ts
  - docs/02-features/042-network-proxy-settings.md
rollback: "git revert 本批 docs/feat 提交链（未发布，工作树无迁移负担）"
writeback: { feature_doc: done, reason: "042 整体重写为唯一真源；EP 定宽规则入 agent-workflow §B3" }
---

# Task（Agent 工作单元）

> 任务：2026-10-16-settings-proxy-mode-bypass

## Objective

把网络设置的代理配置升级为行业通用形态：三模式（跟随系统/手动/直连）+ 旁路条目列表（可增删、`\n` 落盘）+ 手动模式「测试」按钮（test-proxy 端点实时反馈），保存即生效语义不变。

## Scope

**Allowed**
- `ForgeSelf.Api/Models/ForgeSetting.cs`（ProxyEnabled→ProxyMode + ProxyBypass）
- `ForgeSelf.Api/Services/Net/ModelProxy.cs`（三模式决策 + BypassMatcher + 系统代理委托）
- `ForgeSelf.Api/Controllers/SettingsController.cs`（契约改造 + test-proxy 端点）
- `ForgeSelf.Api.Tests/Unit/`：ModelProxyTests / SettingsControllerNetworkTests / HttpClientProxyWiringTests（字段引用改造）/ 新增 SettingsControllerTestProxyTests
- `ForgeSelf.Web/src/`：types/settings.ts、services/settingsApi.ts、components/settings/NetworkPanel.vue、__tests__/NetworkPanel.test.ts、SettingsView.test.ts（如受 nav 影响）
- `ForgeSelf.Web/e2e/settings-network.spec.ts`
- 文档：042 档案、TODO、日记、本 pilot 05-07

**Forbidden**
- 非模型出口接代理（衍生项③）；SOCKS5（①）；其他 HTTP 出口接线改动（AppBuilder/AgentChatController 逻辑不动，字段更名波及除外）
- 无关面板/组件重构；新增第三方依赖；停/启/杀用户宿主；提交/发布（待闸门2/3）
- test-proxy 端点把明文密码写日志/落盘/缓存

## Acceptance Criteria

- [x] AC-1 manual 未命中旁路→代理地址；命中旁路/回环→null（矩阵单测 Verified）
- [x] AC-2 direct 恒 null；system 委托系统代理对象（委托+异常兜底 Warn 有单测；**Unknown① 已现场钉死：GetSystemWebProxy() 应用系统 ProxyOverride**，见 05-evidence）
- [x] AC-3 旁路五形态匹配单测全覆盖（`*.dom`/裸域名后缀/IPv4 精确/`10.*` 前缀/非法拒绝），大小写与子域用例
- [x] AC-4 GET/POST 契约含 proxyMode/proxyBypass、无 proxyEnabled；非法模式/旁路违规 400 带行号；密码三语义不回归
- [x] AC-5 test-proxy 单元层：参数校验、不可达代理 reachable=false+原文、零配置副作用；现场层：10808 真实反馈 Verified（`HTTP 200 · 1318ms` / `积极拒绝`）
- [x] AC-6 现场旁路闭环：manual+10808+旁路 gpustack 域 → provider test 不再 502（实测 D1 502 → D2 404 → D3 502 可逆翻转，不重启）
- [x] AC-7 后端 build 0 error + 本域测试全绿（74/74）；前端 check 0 error + test 全绿（995/995）；全量对表本批域零红（余 30 例为非本域基线，已逐条归因入 TODO）
- [x] AC-8 e2e 4 用例全绿（模式渲染/非法拦截/主链路+旁路持久化/测试反馈），修复后复跑仍 4/4
- [x] AC-9 交互规格逐条走查（ui-ux-design 清单 + D 系对比度 + h-4 占位回归）截图存证；走查发现的 3 项真缺陷（EP 控件宽度、错误对齐、placeholder 裁切）已修
- [x] AC-10 042 档案/TODO/日记回写完成，05/06/07 工件齐备

## Expected Files

见 Scope；新增文件仅 `SettingsControllerTestProxyTests.cs` + 本 pilot 目录工件。

## Verification Commands

```powershell
dotnet build ForgeSelf.Api -p:OutputPath=bin\BuildCheck\
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~ModelProxy|FullyQualifiedName~SettingsControllerNetwork|FullyQualifiedName~SettingsControllerTestProxy|FullyQualifiedName~HttpClientProxyWiring"
dotnet test ForgeSelf.Api.Tests   # 全量对表
cd ForgeSelf.Web; pnpm run check; pnpm run test
pnpm exec playwright test e2e/settings-network.spec.ts
# 现场：dev-stack 已在 :7301/:7399（必要时 pwsh scripts/dev-stack.ps1 重起，-Stop 收尾）
```

---

# 追加节 S9：测试连接「选定模型」弹窗（输入3，2026-10-18）

## Objective

点击「测试连接」时弹窗选择**已登记的模型**，确定后把模型传后端，用该模型发一条真实 chat 请求（用户原话指令内容「请回复且仅回复ok」），响应包含 ok（忽略大小写）即成功。

## 交互设计（点什么出现什么）

| 触发 | 结果 |
| --- | --- |
| 点「测试连接」（某 provider 已有登记模型） | 弹窗「测试连接 · 选择模型」：单选列表（该 provider 全部已登记模型，展示 alias ?? upstreamModelId，含启用/禁用状态），默认预选第一个启用模型；按钮 = 取消 / 确定 |
| 弹窗内点「取消」或关闭 | 不发任何请求，测试按钮/结果状态不变 |
| 弹窗内点「确定」 | 按钮进 disabled+加载中（最长 60s）→ 后端用选定模型发最小 chat（content=「请回复且仅回复ok」，max_tokens 放宽到 8）→ 绿色「连接成功 · 模型 {x} 回复 ok · {ms}」（或红色失败原文：HTTP 错误/鉴权失败/超时/回复不含 ok） |
| 该 provider 无登记模型 | 弹窗内空态提示「该提供方尚无已登记模型」，仅提供「不选模型，仅测连通」兜底按钮（= 现行为 POST 无 body）+ 取消 |

## Scope（Allowed）

- 后端：`IAIProviderService.TestConnectionAsync(long id)` → 加可选参数 `long? modelId = null`（接口+实现；现有无参调用点零改动兼容）；指定 modelId 时：查 AIModel 登记（XCode `AIModel.Find(id)`），用 `UpstreamModelId` 发真实 chat 探测，成功判据 = HTTP 200 **且响应体 content 包含 "ok"（OrdinalIgnoreCase）**；模型不属于该 provider → 失败结果（不 500）。
- 后端：`AIProviderController.TestConnection` 接受可选 body `{ modelId }`（[FromBody] nullable，无 body 时行为与现在完全一致）。
- 前端：`aiProvidersApi.test(id, modelId?)` 传 body；`AiProvidersPanel.vue` 测试按钮改弹 `ElDialog`（模型列表复用 `aiModelsApi.list({providerId})`，已登记含禁用）。
- 测试：后端守卫单测（ok 判据大小写不敏感/不含 ok 败/非本 provider 拒/无 body 回退旧行为）；前端 `AiProvidersPanel.test.ts` 新增弹窗用例（取消不发请求/选定传 id/无模型兜底）；e2e：扩展现有 AI 提供方相关 spec（若无则新增一条主链路用例）。
- Forbidden：不改聊天链路（OpenAICompatibleProvider 聊天逻辑）；不改 fetch-models；不动 :51888 实例。

## Acceptance Criteria

- [ ] AC-11 后端：带 modelId 的 test 对真实 gpustack 模型返回 success 且 message 含「ok」证据；回复不含 ok 时 success=false（单测覆盖大小写："OK"/"ok"/"Ok…"）
- [ ] AC-12 前端：弹窗选定→确定→传模型 id；取消=零请求；无模型时展示兜底按钮
- [ ] AC-13 门禁：本域过滤集绿 + check 0 err + vitest 全绿 + e2e 相关 spec 绿；dev-stack 现场弹窗走查截图读图 + 真实成功响应存证
- [ ] AC-14 文档：001 真源回写「选定模型测试」语义；05 补记；TODO/日记勾账
- [ ] AC-15 发布：release-local 本地更新源（用户明令「做完发布本地包」）+ 签名

## Verification Commands

```powershell
dotnet build ForgeSelf.Api -p:OutputPath=bin\BuildCheck\
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AIProvider"
cd ForgeSelf.Web; pnpm run check; pnpm run test
# 现场：pwsh scripts/dev-stack.ps1；provider test 带 modelId 真实成功；完事 -Stop
```

