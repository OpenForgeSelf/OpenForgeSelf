---
id: 2026-10-17-mcp-external-client-protocol
title: 外部 MCP 协议客户端侧合规缺失：initialize 缺必填 clientInfo、不发 initialized 通知、会话头不回传、/test 响应契约与前端脱节
pc: PC-008
severity: P1
evidence: Verified         # 本 case 主证据等级：Verified | Inferred | Unknown
---
# Case：2026-10-17 外部 MCP 新增连接/测试失败 + 编辑不回显（PC-008 建类案）

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC，不复述。

## 1 现象

用户报告（2026-10-17，:51888 运行实例）：MCP 中心 · 网关配置 · 外部 MCP ① 新增（填入 DeepWiki 预设）保存后「连接」失败；② 「测试」也失败；③ 点「编辑」不回显全部值。复现批实测：:51888 `/test` 端点回 400，message 含远端 `-32602 Invalid request parameters`；对 DeepWiki 直接 A/B 对照（补 clientInfo 即握手成功）坐实根因在客户端 initialize 载荷。修复批门禁② e2e 又暴露第 4 项：握手成功后「测试」仍误报「测试失败」（/test 响应 data 契约脱节）。

## 2 复现红灯

- 红灯测试 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalInitializeClientInfoTests.cs`（复现批，node mock 捕获 initialize 请求体断言含 clientInfo）：修复前失败输出 `initialize params 缺少规范必填的 clientInfo`（断言体见该文件；红灯日志存复现批 05-evidence）。
- 现场实测（Verified）：:51888 POST `/api/mcp-center/servers/{id}/test` → HTTP 400 `-32602`；DeepWiki A/B 对照见复现批 `docs/ai/pilot/2026-10-17-mcpcenter-external-mcp-repro/05-evidence.md`。
- 修复批门禁②：e2e「外部 MCP UI 主链路」用例（v2.5.0）步骤 3 红灯——点「测试」后 `.el-message--success` 30s 不出现（error-context 快照无 message 节点；实为 error toast 已 3s 自动消失）。

## 3 根因（5 Whys + 第 6 问）

1. 为什么真实服务器握手失败？→ initialize params 缺规范必填 `clientInfo{name,version}`，严格服务器按 -32602 拒绝。
2. 为什么缺？→ 传输层载荷照"能跑通的最小对象"手搓，未对照 MCP 规范 Required 字段。
3. 为什么测试全绿没发现？→ 项目自带 mock MCP 服务器宽松（不校验 clientInfo），单测/集成/e2e 全部只对宽松靶子。
4. 为什么编辑不回显还险些丢配置？→ `openEditExternal` 硬编码空值 + 前端类型丢 url/command/args；且脱敏串回填会把真实密钥覆盖成 `****`（衍生 Major）。
5. 为什么握手成功仍显示「测试失败」？→ `/test` 成功时 data 只有 `{id,ok}`，前端 `apiPost` 解包后读不到 `success/message`（它们在信封层）——契约两层脱节。
- **第 6 问：为什么现有流程/守卫没让我一开始就写对？** Spec 的 Failure Modes 没有"对外协议客户端必填字段核对"这一问（无 lens 覆盖）；mock 靶子的"宽松度"从未被审视（测试数据面盲区）；端点响应形状无任何机器核对（vue-tsc 只查声明、vite build 不查运行时）。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|-----------------|----------|-------------------------------|
| `Plugins/McpCenter/Services/McpClient/StreamableHttpMcpTransport.cs` InitializeAsync | 是（缺 clientInfo/通知/会话头） | 已修（canonical + NotifyAsync + 会话头回传） |
| `Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs` InitializeAsync | 是 | 已修 |
| `Plugins/McpCenter/Services/McpClient/LegacySseMcpTransport.cs` InitializeAsync | 是 | 已修 |
| `Plugins/McpCenter/web/src/McpCenterView.vue` openEditExternal + types/external.ts StateDto | 是（编辑回显链） | 已修（真值回显 + 掩码基线不提交） |
| `Plugins/McpCenter/Controllers/McpExternalController.cs` Test（data 形状） | 是（客户端-前端契约面） | 已修 + G2 契约测试锁形 |
| `Plugins/McpCenter/Services/ExternalServersStore.cs`（掩码值可落盘） | 衍生面 | 已修（RejectMaskedValue）+ G2 |
| `Plugins/McpCenter/web/src/types/dsh.ts` DshMcpConfigDto 缺 url/entryId | 异根因（dsh 重构残留） | 本批最小补字段解封 check 门禁；视图旧链路迁移仍为 TODO（2026-10-17 stateless 批已登记条目） |
| McpCenterView `v-loading` 未注册 / 窄视口卡片劣化 | 异根因 | TODO（McpCenter 插件前端视觉/指令缺陷条目 ③④） |

## 5 修复与被否方案

修法 = PC-008 §3 canonical fix 全量应用（三传输 clientInfo + initialized 通知 + Streamable 会话头回传；前端编辑全回显 + 掩码基线防护 + 后端掩码拒绝；/test data 补 success/message/durationMs）。被否方案见 PC-008 §3。验证：后端 121/121、插件前端 build/check/test 绿、mcp-center e2e 5/5（含新用例 7 证据）、dev-stack 走查 5 步全 PASS（截图 `screenshots/g3-devstack-20261017/`）。

## 6 重构提案与结论

R1（任务内已做）：initialize 载荷收敛为 `McpClientProtocol` 单点，三传输去重。R2+ 无提案——理由：客户端侧已统一 canonical，无跨模块重复；「改用官方 C# MCP SDK 替换自研传输层」属架构级 R3，需 `architecture-design` 单独立项评估（网关侧已用 SDK，客户端侧替换收益/成本未论证），本批不做、不留悬空。

## 7 守卫落地

- G1 `McpExternalInitializeClientInfoTests.cs`（红灯转绿，捕获全部请求体断言 clientInfo + 无 id 通知）
- G2 `McpClientProtocolGuardTests.cs`（形状直测 + 三传输源码扫描 + 掩码拒绝枚举）、`McpStreamableSessionHeaderTests.cs`（会话头两分支）、`McpClientIntegrationTests.cs` TestEndpoint 两测（/test data 形状锁）
- G3 `scripts/check-problem-classes.ps1` S1 签名（pre-commit）：`Services/McpClient/*.cs` 出现手写 `new { protocolVersion` 即拒

## 8 逃逸分析（二次复发必做，首见可写「N/A 首次」）

N/A 首次（本类建类案）。

## 9 基建改进

- 修复 `new-problem-case.ps1` 计数 bug（`Measure-Object -Maximum` 返回 Double 致 `{0:D3}` 格式崩，PC 创建段必现）——本 case 归档过程实证并修复；
- e2e 排障经验入库：ElMessage 反馈 3s 自动消失，error-context 快照"无 message"≠"未出现"（断言窗口与渲染生命周期）；中文 `--grep` 受控制台码页破坏，一律 ASCII；
- 走查发现 :51888 之外的既有缺陷复现确认（v-loading）并回写 TODO 条目。

## 10 复用赋能

后续任何**对外协议客户端**（新 MCP 传输、OAuth/OIDC、第三方 API 封装）设计与走查必过 PC-008 lens 四问；新增端点若前端类型声明消费 data 字段，必须配 G2 形状锁测试；mock 靶子被用于"合规性"结论前，先核其宽松度是否掩盖必填项。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| initialize 缺 clientInfo 致严格服务器 -32602 | Verified | 复现批 05-evidence（:51888 实测 + DeepWiki A/B）；红灯测试修复前失败输出 |
| 三传输修复后握手成功 | Verified | `dotnet test` 121/121（2026-10-17 修复批）；e2e 用例步骤 2「已连接 + 2 个工具」 |
| 掩码不往返（编辑原样保存不丢密钥） | Verified | e2e 步骤 4「编辑保存（PUT）成功且 url 未丢」；G2 RejectMaskedValue 测试 |
| /test 契约修复生效 | Verified | dev-stack API 实测 data 含 success:true/durationMs:4；G2 TestEndpoint 两测；e2e 步骤 3 握手成功 toast |
| UI 主链路（新增→连接→测试→编辑→删除） | Verified | mcp-center e2e 5/5（47.3s 新用例）+ 走查截图 `screenshots/g3-devstack-20261017/01-05` |
| 走查环境无残留测试数据 | Verified | Browser 走查步骤 6（删除后空态）+ finally DELETE 兜底 |
