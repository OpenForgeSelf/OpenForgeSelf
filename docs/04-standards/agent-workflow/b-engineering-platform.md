# 项目工程规则 B-2（B7-B12：环境速查 / 架构要点 / 测试覆盖 / CI 自动发布 / Agent SDK 接入 / dsh 架构对齐）

> 本文件是 `agent-workflow.md`（2026-10-07 起拆分）的**一个分册**。§编号（A1-A10 / B1-B12）与规则文字**未作任何改动**，
> 索引与「§编号 → 文件」地址表见同目录 [`README.md`](README.md)；旧路径 `../agent-workflow.md` 保留为薄指路文件。

<!-- ===== 以下为原文（自 docs/04-standards/agent-workflow.md 按行区间拆入，未作任何改写） ===== -->
## B7 环境速查

- 后端端口 `:7102`（`ForgeSetting.config` PortNumber 可改；当前 publish 实例 51888）；前端 dev `:7002`（vs 旧 Stardust 6680/6681，勿混淆）。`vite.config.ts` 的 dev 代理固定指 7102——与 publish 实例的 51888 不冲突（dev 与 publish 是两种运行形态，dev 数据根在程序目录 `Data/`）。
- 运行模式：托盘应用（`publish/OpenForgeSelf.exe --console`），前端由后端 wwwroot 托管。
- **前端 UI 库是 Element Plus 2.14.3**（Vue 3.5.13 / vue-router 4.5 / pinia 3.0 / vite 6.2.4）。`.specify/memory/constitution.md` 写的"Naive UI"已过时，以 `package.json` 为准。
- 包管理器 pnpm；Node >= 20。全屏背景图用固定定位 `<img>`（非 CSS `background-image` 外链，本环境外链不渲染）。
- 认证：API 密钥 AES-256-CBC 加密存 ForgeSetting.config ApiToken，`ApiKeyPolicy` Bearer 校验。数据库：SQLite + EF Core + NewLife.XCode 并存；插件有各自 DB 文件。
- 托盘：`TrayIconManager`（H.NotifyIcon），右键菜单含「铸己匣」标题、打开主界面、检查更新、服务管理、关于、退出；「退出」用 Task.Run 避免消息循环死锁。
- 前端构建：`pnpm run check`（vue-tsc + eslint）为提交门禁；CI workflow 已配置。

## B8 架构要点

- **架构设计/变更统一走 `architecture-design` 技能**（`.agents/skills/architecture-design/SKILL.md`）：先查依据（调研 `docs/06-research/001` → 既有架构文档 → ADR/台账），不足再调研并回写调研文档；禁止脱离依据自作设计。铁律：契约入 Abstractions；禁止共享文件/静态类当跨插件契约；插件服务取宿主契约一律 ctor 注入 `IContext` + `ctx.Get<T>()`（禁缓存实例）；高风险先升级。
- **宿主→插件能力供给三层模型**（设计：`docs/01-architecture/host-capability-seams.md`）：L1 服务契约（pull，默认主力：契约在 Abstractions + Provider eager `ctx.Register` + 消费 `ctx.Get` 软依赖 null 降级）；L2 事件通知（push 补充）；L3 waterfall/serial 拦截管道（特例，多方竞争改写才用）。选型决策树：能降级→L1 停；需实时→补 L2；需拦截改写→L3；缺了不该启动→硬依赖 Consumes（缓建）。
- **插件拆分模板**：参照 MemorySystem——csproj = EF Core 包（10.0.8）+ `NewLife.Core` + `NewLife.XCode` + FrameworkReference Microsoft.AspNetCore.App + 引用 Core/Abstractions；plugin.json 需 `EntryAssembly="X.dll"`；`Apply(IContext)` 内 `DAL.Create(ConnName)` 探活。System.Diagnostics.PerformanceCounter 仅 SystemMonitor 需要。
- **拆插件独立程序集后测试宿主必须同步插件 DLL**：Backend 的 `Stage*`（AfterTargets=Build）只复制插件 DLL 到 Backend 自身输出，**不流入 Tests 输出**。Backend.Tests.csproj 需 `StagePluginDllsToTestOutput` 目标（AfterTargets=Build，从 Backend bin 按 `%(RecursiveDir)` 同步到 `$(OutDir)Plugins\`），否则插件加载失败、其 DI 服务未注册 → 控制器 GetRequiredService 抛异常（WorkflowEngine 实测）。
- **宿主 `IAIService` 无工具调用能力**：要做「工具循环 + 按 chatModelId 路由 + 流式」只能复用宿主 `IAIProvider`/`AIProviderRegistry`（已下沉 Abstractions + `HostProvidedServiceContracts` seed），插件 `ctx.Get<IAIProviderRegistry>()` 解析；`chatModelId`（`provider:upstreamId`）剥 `:` 前缀取上游模型 id。
- **图片识别缓存**：统一网关 `/v1/chat/completions` 多模态图片识别 = 逐图识别+逐图缓存。`IImageRecognitionCache` → `LocalFileImageRecognitionCache` 存 `ContentRootPath/Data/ImageRecognitionCache/<会话键>/<sha256>.json`（临时文件+原子改名；会话键非法字符替换、截断 64、防路径穿越）。缓存键 = SHA256(视觉模型id|来源|detail或mediaType|url或base64)。**失败占位结果不写缓存**，下轮重试。当前无过期/清理策略。
- **聊天记录实时流式**：`ChatRecordStreamRecorder` 采用 tee 流——代理 LLM 流式响应时一边回给调用方，一边按「每满 1000 字符或 500ms」节流 `UpsertRecordAsync` 增量落库（同一 `RequestId` 首插后更新），并通过 `/ws` WebSocket 推送 `chat_record_chunk` / `chat_record_completed`。复用现有原生 WS 通道（`wsService`），不引入 SignalR。
- **流式 token 用量（usage）透传铁律**：三段任缺一段都丢——① provider `OpenAIStreamChunk` DTO 必须含 `usage`（空 `Choices` 哨兵分片不能 `continue` 丢弃）；② 网关 DTO 必须含 `usage` 且 `HandleStreamAsync` 透传到 SSE 分片（哨兵保持 `choices:[]`）；③ 上游仅在 `stream_options.include_usage=true` 时才回 usage——provider 对 `Stream=true` **强制 `include_usage=true`**。回归测试：`UnifiedAIGatewayIntegrationTests.ChatCompletions_Streaming_PropagatesUsageField`。
- **插件 SSE 事件序列化必须 camelCase**：控制器 `JsonSerializer.Serialize(payload)` 默认 PascalCase，嵌套对象（如 `UnifiedUsage.PromptTokens`）会输出 PascalCase，前端按 camelCase 解析恒为 0。**统一 `new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`**。
- **证书/加密数据加载失败 → 容错重建不整体禁用**：插件初始化路径上的持久化证书/加密数据加载失败 → **catch 后改名保留原件（`.bad-<时间戳>`，不删除）→ 重建 → 告警**，不得让单点失败禁用整个插件（`CryptographicException: 指定的网络密码不正确` = `X509Certificate2(pfx, password)` 密码不匹配/文件损坏的典型错误）。
- **`CertificateRequest.CreateSelfSigned` 返回的证书已含私钥**：再 `cert.CopyWithPrivateKey(rsa)` 抛「The certificate already has an associated private key」。直接 `cert.Export(X509ContentType.Pfx, pwd)` 即可。
- **WinDivert 部署结构（FlowForge）**：`WinDivert.dll` 必须与 `WinDivert64.sys` 同在 exe 根目录（csproj `<None Include>` 加 `<TargetPath>X</TargetPath>` 落到根目录）；驱动文件被内核锁定删除报访问被拒，`sc stop WinDivert` 需管理员；驱动文件不随业务代码变化时跳过 sys 覆盖即可。

## B9 测试覆盖与功能规格

### 真机走查（playwright.live.config.ts 打运行中实例）
- 🔴 **live 配置不走 globalSetup**：`helpers/real-auth.getRealApiKey()` 的取值优先级是 `E2E_API_TOKEN` > 最近一次 globalSetup 落盘的 `.temp/e2e/<ts>/state.json`。对 51888 这类常驻实例跑 live 用例**必须显式注入当前真实密钥**（`E2E_API_TOKEN=$(node scripts/get-forge-token.cjs …)`），否则读到过期 state token → 401 空响应 → 「Unexpected end of JSON input」假象（spec 036 首轮实测）。
- 破坏性 live 用例（会重启/换版实例）必须双显式门控（env 开关 + 显式 token），普通套件绝不命中；见 `e2e/update-live-apply.spec.ts` 的 `test.skip` 模式。

### 功能测试覆盖（e2e，2026-08-06 归档）
21 项功能全覆盖、36 用例全通过（对接真实后端 + 真实认证，绝不 mock）：

| # | 功能 | e2e 文件 |
|---|------|----------|
| 1 | 首页 | app.spec.ts |
| 2 | 聊天 | chat.spec.ts |
| 3 | 待办 CRUD | todo.spec.ts |
| 4 | AI 提供方 | ai-providers.spec.ts |
| 5 | AI 提供者全流程 | ai-provider-chat.spec.ts |
| 6 | API 服务器/端口/Token | port-config + token-*.spec.ts |
| 7 | 快捷链接 CRUD | quicklinks.spec.ts |
| 8 | 聊天记录 | chat-records.spec.ts |
| 9 | 插件商店 | plugin-store.spec.ts |
| 10 | API 网关 | api-gateway.spec.ts |
| 11 | SPA 回退 | spa-fallback.spec.ts |
| 12 | Prompt/技能 | prompts-skills.spec.ts |
| 13 | MCP 工具 | mcp-tools.spec.ts |
| 14 | 记忆系统 | memory.spec.ts |
| 15 | Agent 管理 | agents.spec.ts |
| 16 | 系统监控 | system-monitor.spec.ts |
| 17 | 代码片段 | code-snippets.spec.ts |
| 18 | 工作流 | workflows.spec.ts |
| 19 | 个人中心 | profile.spec.ts |
| 20 | AI Agent | ai-agent.spec.ts |
| 21 | 全部功能/插件子页 | all-features-plugins.spec.ts |

**测试驱动修复**：memoryApi POST、MemoryView storeToRefs、后端软删除过滤、systemMonitorApi 路径/解包/字段映射、AgentType/source 数字枚举、listSnippets/listWorkflows 解包、chatApi 路径与请求体契约。

### 已完成功能规格（specs/，tasks 全部 [X]）
| Spec | 功能 | 任务 |
|------|------|------|
| 001 | AI 提供方配置数据库化 | 25/25 |
| 002 | Provider 模型列表管理 | 35/35 |
| 003 | API 服务器设置 | 22/22 |
| 004 | 模型列表与网关集成 | 25/25 |
| 005 | 待办追踪插件 | 32/32 |
| 007 | 背景图可见度/透明度 | 33/33 |
| 008 | 托盘服务与自动更新 | 19/19 |
| 009 | Web 端口与 Token 安全 | 23/23 |

（006-set-background-image 仅有 spec.md，未生成 tasks）

## B10 CI 自动发布（tag → GitHub Actions，2026-09-26 落地）

**入口**：`git tag -a v<X.Y.Z> -m "发版说明" && git push github v<X.Y.Z>` → Actions 自动构建打包并创建 GitHub Release（**OpenForgeSelf/OpenForgeSelf**，2026-09-26 已转**公开**；由 xxred/OpenForgeSelf 迁入，旧仓仅作历史镜像，更新源 appsettings `Update:GitHubRepo` 与 remote `github` 均指新仓）。

**设计契约（用户拍板）**：workflow 只做「装工具链 + 调脚本」，全部发布动作封装在 `scripts/release/*.ps1`，本地与 CI 跑同一条命令——`pwsh scripts/release/release-local.ps1 -Version v0.1.0`。
**本地目录更新源（2026-09-27 新增）**：`release-local.ps1 -Version v<X.Y.Z> -UpdateDir <目录>` 打包后把 zip + `SHA256SUMS.txt` + 更新说明拷贝到指定目录；设置页「更新源 = 本地目录」填该目录，页面点「检查更新 → 下载 → 重启并更新」即走现有更新流程（本地 zip 直接由 update-agent 接管，无需 GitHub）。

| 脚本 | 职责 |
|------|------|
| `release-local.ps1` | 一键编排：前端→宿主 publish→打包→发版说明（= CI 唯一构建入口） |
| `build-frontend.ps1` | 宿主 web（→`ForgeSelf.Api/wwwroot`）+ 8 个插件 web（→`Plugins/<X>/web/dist`），逐包 `pnpm install --frozen-lockfile`；`-HostOnly/-PluginsOnly/-Plugin` 分步调试 |
| `publish-host.ps1` | `dotnet publish -c Release -r win-x64 --self-contained true -p:Version=<ver>`（下载即用 exe，无需装 .NET） |
| `package-release.ps1` | 清 Data/Log/Config/_backups/pdb → zip + SHA256SUMS；`-Sign` 可选接 sign-publish.ps1（输入38：SQLite 运行时构件注入块已删除——驱动为包依赖，随发布外置） |
| `make-release-notes.ps1` | tag 注解 + 上一 tag 以来 commit 生成 RELEASE-NOTES（需 fetch-depth 0） |
| `publish-release.ps1` | `gh release create`（tag/资产/notes）；本地凭 gh keyring，CI 凭 `secrets.GITHUB_TOKEN`；版本含 `-` 后缀自动 prerelease |

> 打包产物布局 / 升级备份 / 缓存生命周期规则以 `docs/04-standards/packaging-upgrade-backup.md` 为唯一真源；本节只记操作流程与踩坑。

**硬规则与坑（本轮实战）**：
- ✅ **System.Data.SQLite.dll 不再需要手工入库（输入38，2026-09-29）**：`XCode.SQLite` 包正式依赖 + 单文件剔除外置机制取代旧 `build/runtime/Plugins` 注入（仓库该目录已移 `.trash/`）；e_sqlite3.dll 随 NuGet RID 依赖落盘。
- 🔴 **`.github/` 曾被 .gitignore 屏蔽**（历史清理误伤），workflow 必须入库才生效——已在 .gitignore 解除并注释。
- 宿主 SPA 与插件 `web/dist` 全部是 gitignore 的生成物：CI 必须先跑 `build-frontend.ps1` 再 `dotnet publish`（`StageAllPlugins` 只拷已存在的 dist）。
- 单实例 Mutex 挡冒烟测试：本机已跑 publish 宿主时，起第二个实例用 `FORGESelf_INSTANCE_ID=smoke`（Program.cs:47）；exe 固定监听 7102，忽略 ASPNETCORE_URLS。
- PowerShell：`Invoke-ReleaseStep { … }` 的 scriptblock 内对脚本级变量赋值**不回传**（子作用域），路径等结果须在块外先算好；`gh run watch` 非交互必须显式传 run-id，否则立即退出且管道后 exit 0 假成功。
- 🔴 **CI Node 必须 ≥22**：DesignSystem 插件 `web/package.json` build 直跑 `node scripts/gen-tokens-css.ts`，依赖 Node 原生 type-stripping（Node 20 报 `ERR_UNKNOWN_FILE_EXTENSION`）。本地 Node 26 掩盖了此依赖；workflow `setup-node` 已钉 22。
- 网络受限环境取 CI 日志：`results-receiver.actions.githubusercontent.com` 可能不可达（`--log-failed` 失败），改用 `gh api repos/<o>/<r>/actions/runs/<id>/logs > ci.zip` 下载解压按步骤 txt 定位。
- 重打测试 tag：`git tag -d` + `gh api -X DELETE .../git/refs/tags/<tag>` + 重新 `git tag -a` 推送即可再触发（`gh run rerun` 会复用旧 tag commit 的 workflow，改了 workflow 时**不要用 rerun**）。
- 产物实测：self-contained zip ≈ 74MB / 562 文件；windows-latest 全流程 ≈ 6-10 分钟。
- 验收流：测试 tag（如 `v0.0.0-ci-test`，自动标 prerelease）先跑通 → 下载 Release 资产核对 SHA256 → 再打正式 tag。
- 🔴 **git push github 走系统代理**：外网通时 `git push github` 直连失败（Recv failure / 443 不通）而 `gh` 可用——gh 走 WinHTTP 代理、git 不走。绕行：`HTTP_PROXY=http://127.0.0.1:10808 HTTPS_PROXY=同值 git push github <ref>`（端口以 `netstat` 实测本机代理为准）。持久化可 `git config --global http.https://github.com.proxy http://127.0.0.1:10808`（用户侧决定，勿擅改全局）。
- 🔴 **GitHub Release 下载资产必须走资产 API 直链**（`api.github.com/repos/<o>/<r>/releases/assets/{id}` + `Accept: application/octet-stream`）；`browser_download_url` 只作人读展示。单测 mock fixture 必须同时含 `url` 与 `browser_download_url`（spec 036 教训：fixture 缺 `url` 字段导致 13 项单测全绿没拦住线上 404）。
- 🔴 **匿名访问私有 GitHub 仓库返回 404，不是 401/403**：诊断「检查更新 404」时先确认仓库可见性与进程是否拿到凭据，别当成 URL 拼错或限流。仓库已转公开（2026-09-26）后更新检查保持匿名，`FORGESELF_UPDATE_TOKEN` 相关逻辑已移除。
- 🔴 **「启动后台任务 + 前端轮询」协议必须在持锁临界区内预置首个进行中状态**：`StartDownload` 旧实现先 `Task.Run` 再由任务置 `downloading`，POST 响应/首轮轮询读到 `checked` → 前端把 checked 当终止态永久停轮询 → UI 等不到「重启并更新」（51888 实机两次复现，4ef4b5c 修 + 回归单测）。泛化：**状态机对外可见的状态序列不允许出现协议里的"终止态"夹在启动与进行中之间**。
- 🔴 **并发写路径下，状态机每一侧的写入（不只启动侧）都必须守卫「在途状态」**：CheckAsync 完成时无条件 `Set("checked"/"idle"/"failed")`，会覆盖在途的 downloading → 前端又停轮询（51888 第 6 轮插桩实锤：`/download→downloading` 后 2 秒 `/progress→checked`；6b09654 修：首写 checking 与终写均在 lock 内、当前状态 ∈ {downloading,verifying,extracting,ready,applying} 时跳过回写 + 红灯回归单测）。检查/下载两条并发路径写同一状态机时，**终止态回写必须条件化**，且单测必须构造真实并发时序（慢速 mock HTTP + 交错调用）而非顺序调用。
- **随包分发的 .ps1 由 powershell 5.1 拉起时同样受 BOM 铁律约束**（见 B6）：update-agent 无 BOM → 解析崩、日志写不出、宿主自停后无人重启，实例整段下线。
- `appsettings.json` 有明文 ApiKey 入历史：**仓库转 public 前必须先处置**（见 TODO 批次 D）。
- 🔴 **发布治理（seq37 指令）**：含已知功能缺陷的版本 **禁止推送 tag/发布 Release**（打 tag 即触发 CI 发布，等于把缺陷分发出去）；已推的缺陷版本经用户授权后撤销（gh release delete --yes + 双端删 tag + 本地删 tag，防 `--tags` 误复推）。2026-09-26 实例：v0.2.0–v0.2.3 已从 OpenForgeSelf/OpenForgeSelf 与 gitee 撤销，仅保留 v0.2.4 起干净版本；旧 xxred 镜像未动（用户未授权）。
- 🔴 **发布治理（2026-09-27 用户指令 seq17）——agent 禁止停/启/杀宿主**：宿主升级一律由 update-agent 自更新（用户/页面点「自动更新」）；agent 的发布职责止于「打 tag → CI 出 Release（或 `release-local.ps1 -UpdateDir` 出本地 zip）→ 验证产物」，**任何情况下不得 `Stop-Process` 用户运行中的 ForgeSelf**（含 `D:\src\tools\ForgeSelf`、`:51888` 实例）。旧 run-plugin-publish-verify.ps1 的「杀掉非 publish 实例重起」行为已废除；该脚本仅保留插件版本化侧载（宿主不重启）可选用途（须用户同意）。发布完成判据 = Release 资产可下载且 SHA256 核对一致（或本地目录 zip 可被页面检查出新版本），不是「宿主已重启」。

---

## B11 Qoder CN Agent SDK 接入（spec 037，2026-09-27 协议验证）

- **国内站 SDK = `@qodercn-ai/qodercn-agent-sdk` + `qoderclicn`**（非国际站 `@qoder-ai/qoder-agent-sdk`）；本机 npm 拦 install 脚本，装完必须手动补跑 `node node_modules/@qodercn-ai/qodercn-agent-sdk/scripts/postinstall.cjs`。
- 🔴 **受信目录内 `permissionMode:'default'` + `canUseTool` 不会触发审批**（CLI 直接自动放行，实测零回调；受信来源 `~/.qoder-cn/settings.json permissions.trustDirectories`）。宿主强控唯一链路：SDK `hooks.PreToolUse` 返回 `permissionDecision:'ask'` → `canUseTool` 必达 → 回传宿主审批。
- **协议非 LSP 非 ACP**：SDK↔CLI 为私有双向 JSONL over stdio；MCP 层才是 JSON-RPC 2.0。禁止 C# 复刻私有协议，用 Node 桥 + 自定版本化宿主协议隔离（契约见 `specs/037-qoder-agent-sdk-bridge/SPEC.md` §4）。
- `interrupt` 的正常终态是 `done subtype=error_during_execution`，不是崩溃；每轮恰好一个 `done`，收口只认 `done`。
- 认证失败退出码 41（`CLI_EXIT_CODE_AUTH_ERROR`）；PAT 不自动刷新，轮换后必须新会话。
- **ACP 整合路径（037 R2，实测）**：qoderclicn 无原生 ACP；官方 `@agentclientprotocol/sdk`（npm 1.5.0，`AgentSideConnection`/`ClientSideConnection`/`ndJsonStream`）可把 SDK 会话包装成标准 ACP agent（原型 `specs/037-.../bridge/acp-agent.mjs`，ACP 客户端 7/7 PASS）。schema v1 `session/new` **必填 `mcpServers`**（漏传报 Invalid params）。AgentHub 侧统一走 `IAgentTransport(Kind=Acp)`，权限=Acp `session/request_permission` ⇄ SDK `PreToolUse ask→canUseTool`。
- **通用 JS 运行时归宿主**（`IJsBridge`，Abstractions 层）：与 `IAIProviderRegistry` 同模式（宿主能力、多插件消费）；宿主只 spawn/收发 JSONL，协议属使用方；宿主不自动联网 npm install（本机 npm 拦 install-scripts）。

---

## B12 dsh 架构对齐（040–042，B1–B9 收官，2026-09-28 沉淀）

- 🔴 **XCode `WhereExpression` 不支持 `NotLike`、不支持对 `Like`/`StartsWith` 表达式取反**（`!Like()` 不存在，`!StartsWith` 产生无法编译的表达式树）。排除型过滤直接拼**原生 SQL 片段**：`where &= "SessionKey Not Like 'plan:%'"`（B9-3 三次编译失败换来的结论）。
- 🔴 **`Environment.GetFolderPath(SpecialFolder.UserProfile)` 读的是 Windows Known Folder（注册表），不读 `USERPROFILE` 环境变量**——测试沙箱重定向 `USERPROFILE` 对它无效。两条派生路径（Known Folder 分支 vs 环境变量分支）在 WebApplicationFactory（Testing 环境）下会分叉撞真实用户目录（B9-4 根因，比"TEMP 重定向不生效"深一层）。**根治（方案 A，已批准落地）：`FORGESELF_DATA_ROOT` 环境变量在 `DataLocationService` 双解析重载中最前置重定向**（静态版与实例版语义严格一致，空白视为未设置；先红后绿 ×2 实证）。
- 🔴 **`[..N]`/`Substring(0,N)` 前必须 `Math.Min(N, len)` clamp**：内容短于 N 直接 `ArgumentOutOfRangeException`（B9-6 spill 预览 1KiB 越界实测）。
- **插件前端 vitest 测试由宿主统一收集**：宿主 `vitest.config.ts` include 覆盖 `../Plugins/*/web/src/**/*.test.{ts,tsx}`；**禁止在插件目录内直接跑 vitest**（无自身测试配置，21 条收集 15 红是错误 harness 的噪音，非回归）。
- **多 call 帧序与前端配对契约**：B8 `ExecuteBatchAsync` 帧序 =「全部 `tool/call`+`ToolStarted` 先落 → 批执行 → 逐 result+`ToolCompleted`」；同名多次调用的结果事件必须按**最早 pending FIFO** 配对（`settleToolEventFifo`），LIFO 会互换结果。
- **删除旧 API 必配 grep 守门测试**（仿 `IAgentLoop_Removed`/`LegacyToolExecutionFace_Removed`）：grep 源码断言旧符号零出现，注释行豁免——防止退役 API 静默回潮。
- **前端全量 vitest 有并发资源竞争抖动**（同批失败数 1→6 漂移、单测 9s 超时）：失败先**单文件重跑复判定性**（单跑 18/18 绿 = 抖动），别急着当回归修。
- **.NET 测试环境绕法（固化，B9-4 后最小化；2026-10-02 输入4 起数据根自动隔离）**：跑前 `taskkill /F /IM testhost.exe`；命令行前缀赋值 `TEMP`/`TMP`（沙箱拒写系统 Temp，`XCodeTestFixture` 曾因此 597 连红）。**`FORGESELF_DATA_ROOT` 已无需手工设置**——`ForgeSelf.Api.Tests` 的 `TestDataRootIsolation`（`[ModuleInitializer]`）在测试程序集加载时自动兜底：未显式设置时把数据根指向仓库内 `.temp/dotnet-test/<时间戳>-<pid>`（`.temp/` 已 gitignore），并把 `XTrace.LogPath` / `Setting.LogPath` / 全部已知 `Config<T>` 的 `FileName` 一并归位到该隔离根，切断「读程序目录残留 Core.config（固化宿主 LogPath/连接串）→ 写宿主日志/库」链路。**手工前缀仅在需要指定位置/覆盖时使用**（显式设置时本机制短路，e2e/CI 链路不受影响）。dev 实例窗口先 `tasklist`/`Get-CimInstance` 确认（MSBuild nodemode worker 不算实例）。
  - 🔴 **隔离必须三件套齐（2026-10-02 输入4 实证）**：只设数据根不足以隔离——`NewLife.Setting`/`XCodeSetting` 的 `Config<T>.FileName` 默认相对程序目录（`Config\Core.config`），程序目录一旦残留固化了宿主 `LogPath` 的旧 Core.config，测试进程读到即绕过隔离。且**顺序敏感**：必须**先 `ConfigUnifier.UnifyAllConfigFiles` 重定向 FileName，再设 `XTrace.LogPath`**；反序会在 `XTrace` 首访时触发 `NewLife.Setting` 按默认相对路径自动创建 `bin/Config/Core.config` 脏文件。
  - ⚠️ **宿主侧关联缺陷（已知，未修）**：`Program.cs:38` 与 `AppBuilder.cs:89` 的 `Setting.Save()` 早于 `ConfigUnifier` 重定向 → 生产 publish 程序目录仍可能残留 Core.config；测试侧已由上述机制规避，宿主侧修复另立 TODO。

---

