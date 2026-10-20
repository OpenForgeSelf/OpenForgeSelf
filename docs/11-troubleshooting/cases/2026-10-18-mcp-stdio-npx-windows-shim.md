---
id: 2026-10-18-mcp-stdio-npx-windows-shim
title: 用户配 npx @playwright/mcp@latest 外部 MCP 报 400「系统找不到指定的文件」；修复启动层后先后暴露帧型沉默与 null params 沉默（三层）
pc: PC-010
severity: P1
evidence: Verified         # 本 case 主证据等级：Verified | Inferred | Unknown
---
# Case：2026-10-18 npx 型外部 MCP 三层全灭（PC-010 建类案）

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC，不复述。

## 1 现象

用户报告（:51888 实例，宿主 2.4.9.2610180248）：MCP 中心 · 网关配置 · 新增外部 MCP，command=`npx`、args=`@playwright/mcp@latest`，点「测试」报 `请求失败(400): 无法启动外部 MCP 进程 'npx': An error occurred trying to start process 'npx' with working directory 'D:\src\tools\OpenForgeSelf\versions\2.4.9.2610180248'. 系统找不到指定的文件。`

修复过程呈**三层剥洋葱**：① 启动层修好后集成测推进到 tools/list 30s 超时（帧型沉默）；② 帧协商修好后 npx 集成仍超时（null params 沉默，A/B 探针定位）。旧应用层 e2e 的「stdio npx 优雅降级」分支让三层全灭时套件照样绿。

## 2 复现红灯

- 红灯测试 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpStdioWindowsShimTests.cs`（command="npx" 真实集成，Windows-only）：修复前失败输出与**用户报错逐字同款**（`无法启动外部 MCP 进程 'npx': ... 系统找不到指定的文件`）——用户报障的机器可复现锚。
- 探针事实（一次性，用完即删，结论沉淀进测试）：① server-everything stdio 只答 NDJSON；② 对它发 Content-Length 帧 got-reply=False（完全沉默）；③ 同一 NDJSON 帧 `params:{}` 应答 / `params:null` 沉默（A/B 坐实）。

## 3 根因（5 Whys + 第 6 问）

1. 为什么 400？→ CreateProcess 找不到 `npx`——Windows 下它是 `npx.cmd` 批处理 shim，UseShellExecute=false 不按 PATHEXT 解析。
2. 为什么代码这么写？→ ProcessStartInfo 照"命令名直启"的跨平台直觉写，未在 Windows 目标机实测（缺陷是 Windows-only，dev 侧 Linux 语义自洽）。
3. 为什么修好启动层仍连不上？→ 客户端只会说 LSP 帧，官方 TS SDK 服务器只答 NDJSON 且对 LSP 请求沉默——规范 SHOULD 形态 ≠ 生态实况。
4. 为什么帧协商修好还有第三层？→ `RequestAsync(method, null)` 把 `params` 序列化成字面量 null，FastMCP 系收到即沉默；`arguments:null` 同族。
5. 为什么单测/e2e 从未暴露？→ 自家 mock 与客户端同构（LSP 帧、宽松 null），旧 e2e 还给 npx 失败写了降级分支——**宽松/同构靶子 + 宽松断言**双重掩盖（PC-008 教训第三次命中）。
- **第 6 问：为什么现有流程/守卫没拦住？** PC-008 的 lens 只覆盖"协议载荷必填项"，没有"进程启动 OS 语义"与"帧型生态实况"两问；`docs/02-features` 与技能无 stdio 传输实现指引；真实世界 e2e（npx 官方包）此前不存在——本批补为 G2 零容忍用例。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|-----------------|----------|-------------------------------|
| `Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs` 构造函数 ProcessStartInfo | 是（缺陷①） | 已修（ResolveLaunch PATH 探测 + ComSpec 包装）+ G1 |
| `.../StdioMcpTransport.cs` ReadLoop/WriteFrame（单帧型 LSP） | 是（缺陷②） | 已修（双帧读侧嗅探 + auto 协商 + Framing 配置）+ G2 |
| `.../StreamableHttpMcpTransport.cs` `@params = prms` | 是（缺陷③，跨传输） | 已修（SerializeRequest 单点）+ G2/G3 |
| `.../LegacySseMcpTransport.cs` `@params = prms` | 是（缺陷③，跨传输） | 已修 + G2/G3 |
| `Plugins/McpCenter/Services/ExternalServersStore.cs` ValidateAndNormalize | 衍生面（逐字段拷贝吞新字段 Framing） | 已修（透传）+ 单测锁 |
| `ForgeSelf.Api.Tests/.../Fixtures/mock-mcp-server.js`（只有 LSP stdio） | 是（靶子同构面） | 已补 `--mode stdio-ndjson`（双帧靶子） |
| 旧应用层 e2e「stdio npx 优雅降级」分支 | 是（掩盖面） | 已封堵：新用例零容忍 connected，降级分支删除 |

## 5 修复与被否方案

修法 = PC-010 §3 canonical fix 全量应用（ResolveLaunch / 双帧协商 + Framing 贯穿链 / SerializeRequest）。被否方案见 PC-010 §3。验证：定向 32/32 绿；插件 e2e 6/6 绿（npx server-everything toolCount=13、握手 3661→3689ms）；门禁③ dev-stack API 按**用户原文配置**（npx @playwright/mcp@latest，无 -y）connected=True toolCount=25、/test success 3851ms；UI 截图两张（列表已连接 + 「握手成功」绿 toast）。

## 6 重构提案与结论

R1（任务内已做）：请求序列化收敛 `McpClientProtocol.SerializeRequest` 单点（三传输去重，与 PC-008 的 BuildInitializeParams 同处收口）。R2 提案（未做，理由）：把三传输公共的 pending/超时/去重骨架抽 `JsonRpcChannelBase`——本批 Stdio 的 RequestAsync 已因帧协商特殊化，抽象收益待第三条新传输出现再兑现，避免为两例提前造基类；登记 TODO 不悬空（见 07）。

## 7 守卫落地

- G1 `McpStdioWindowsShimTests.cs`（红灯转绿 + ResolveLaunch 6 组注入式单测 + 白名单报错文案）
- G2 `McpStdioFramingTests.cs`（auto 命中/回退、explicit、ParseFraming 矩阵）、`McpClientProtocolGuardTests.cs` PC-010 段（null 省略键直测 + 三传输扫描）
- G2 e2e「stdio npx 真实世界」零容忍用例（含 UI 截图取证段）
- G3 `scripts/check-problem-classes.ps1` PC-010 S1 签名（pre-commit）：`@params\s*=\s*prms` 回退即拒

## 8 逃逸分析（二次复发必做，首见可写「N/A 首次」）

N/A 首次（本类建类案）。与 PC-008 的关系：同插件同"外部 MCP"域但**异根因层**（PC-008=协议合规载荷层；本类=进程启动/帧协商/序列化沉默层），编号独立、lens 互补不重叠。

## 9 基建改进

- mock-mcp-server.js 增 `--mode stdio-ndjson`（靶子不再与客户端同构，帧型矩阵可测）；
- dll 被并行 dev-stack 宿主锁定的绕行 `dotnet test -p:BaseOutputPath=.temp\altbuild\` 记入日记（不停他批进程铁律下的标准手法）；
- e2e 截图取证经验：Playwright 断言不滚动，证据图要 `scrollIntoViewIfNeeded` 后再截（本 case 读图发现首图无条目，补后复跑核图）。

## 10 复用赋能

后续任何「拉起用户配置的外部进程」（stdio 插件、update-agent 调外部命令、脚本工具页）必过 PC-010 lens 第 1 问（目标 OS 解析语义）；任何 stdio/裸 socket 协议客户端实现前必做**真实官方实现**的 A/B 探针（帧型/空字段两问），禁止只对自家 mock 下合规结论。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| 缺陷①复现=用户报错原文 | Verified | 红灯测试修复前失败输出（本批 05-evidence）；用户 :51888 报错截图文字 |
| 缺陷②帧型沉默 | Verified | 探针实测 got-reply=False（结论已锁进 McpStdioFramingTests，探针文件已删） |
| 缺陷③null params 沉默 | Verified | A/B 探针（{}应答/null沉默）；G2 直测 + G3 源码扫描守卫 |
| 三层修复后全链绿 | Verified | 定向 dotnet test 32/32；e2e 6/6（npx toolCount=13 握手 3689ms）；门禁③ API 用户原文配置 connected=True toolCount=25 |
| UI 层生效 | Verified | `ForgeSelf.Web/screenshots/e2e/mcp-center/npx-shim-list.png`（v2.5.1 徽标+已连接+13 工具）、`npx-shim-test-toast.png`（「握手成功」绿 toast） |
| 无残留测试数据 | Verified | e2e finally DELETE；dev-stack 侧 live-npx-playwright 已从 external-servers.json 清除 |
