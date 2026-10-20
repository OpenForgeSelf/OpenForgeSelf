# 07 最终报告（Final Report）

> 任务：2026-10-16-settings-proxy-mode-bypass｜完成日：2026-10-18｜闸门1 批准语：「开工」（三项拍板均按默认推荐）

## 1. Repository Understanding

我确认了：网络设置首版（pilot `2026-10-16-settings-network-proxy`，未提交未发布）为二值 `ProxyEnabled` 契约；代理决策集中在 `Services/Net/ModelProxy.cs`（IWebProxy 实时读 `ForgeSetting.Current`），出站管道经 `AppBuilder.cs` 的 IHttpClientFactory 默认 handler 覆盖全部 HTTP；设置契约在 `SettingsController.cs`；前端唯一消费面 `NetworkPanel.vue` + `settingsApi.ts` + `types/settings.ts`；测试面为 ModelProxyTests / SettingsControllerNetworkTests / HttpClientProxyWiringTests / NetworkPanel.test.ts / e2e settings-network.spec.ts。全部改动范围与本批 Scope 一致，无未知架构接缝（系统代理委托点 `_systemProxyProvider` 为既有注入位）。

## 2. Selected Task

把代理配置升级为行业通用形态：三模式（`system`/`manual`/`direct`）+ 旁路条目列表（五形态匹配、≤50 条、非法 400 带行号）+ manual 模式「测试」按钮（`POST /api/settings/test-proxy`，未保存表单值即测、零配置副作用、任何 HTTP 响应=代理可用）；保存即生效语义与密码三语义不变。

## 3. Changed Files

**后端**：`Models/ForgeSetting.cs`（ProxyEnabled→ProxyMode+ProxyBypass）、`Services/Net/ModelProxy.cs`（BR-1 四分支决策 + BypassMatcher + TryBuildProxyUri 兜底 + 系统委托）、`Controllers/SettingsController.cs`（契约改造 + test-proxy 端点）。
**后端测试**：`ModelProxyTests`（38 例重写）、`SettingsControllerNetworkTests`（20 例改造 + ForgeSnapshot 重试窗口 8 次递增）、`SettingsControllerTestProxyTests`（10 例新增）、`HttpClientProxyWiringTests`（6 例字段跟随）。
**前端**：`types/settings.ts`、`services/settingsApi.ts`、`components/settings/NetworkPanel.vue`（三模式 radio + 测试状态机 + 旁路行列表；走查后修复：5 控件内联定宽 240px、旁路输入 360px、错误左对齐、placeholder 缩短）、`__tests__/NetworkPanel.test.ts`（21 例）。
**e2e**：`e2e/settings-network.spec.ts`（4 用例新契约 + getByPlaceholder 同步）。
**文档**：`docs/02-features/042-network-proxy-settings.md`（整体重写为唯一真源）、`docs/04-standards/agent-workflow/b-engineering-daily.md` §B3（EP 定宽规则）、`TODO.md`、日记、本 pilot 00-07。

## 4. Validation

Build: `dotnet build ForgeSelf.Api -p:OutputPath=bin\BuildCheck\` → **0 error**；前端 `pnpm run check` → **0 error**（149 warning 与基线一致）。

Unit Test: 本域过滤集 **74/74 绿**（38+20+10+6）；前端 `pnpm run test` **995/995 绿**（85 文件，含 NetworkPanel 21 例）；全量 `dotnet test` 失败 31/通过 2948——本域 1 例已修后复绿，余 30 例逐条归因均为**非本域基线红**（已登记 TODO「全量 dotnet test 基线红 30 例」）。

E2E: `pnpm exec playwright test e2e/settings-network.spec.ts` → **4 passed (1.3m)**，UI 修复后复跑仍 **4/4**（真实前后端零 mock，隔离数据根）。

## 5. Evidence

见 `05-evidence.md`（全节 Verified）。关键结论：
- AC-6 旁路闭环铁证（现场 :7301，不重启）：D1 manual+127.0.0.1:10808+旁路空 → provider test **HTTP 502**；D2 加旁路 gpustack 域 → **HTTP 404**；D3 撤旁路 → **502**（可逆翻转）。
- Unknown① 钉死：`WebRequest.GetSystemWebProxy()` **确实应用注册表 ProxyOverride**——system 模式空旁路下该域呈直连特征（404 而非 502），我方旁路为叠加语义（已回写 042 §4）。
- test-proxy 真实反馈：10808 → `{reachable:true, statusCode:200, latencyMs:1318}`；port 1 → `{reachable:false, message:"由于目标计算机积极拒绝，无法连接。"}`。
- UI 走查（ui-ux-design）修复 3 项真缺陷后几何实测：五框等宽 240、右缘统一 1215、placeholder 完整、错误左对齐、1440 下 FAB 净空 161px。证据形式降级如实交代：修复后宽视口 Browser 截图未产出（NATIVE_BROWSER_VIEWPORT_UNAVAILABLE），以 DOM 几何实测 + e2e 截图（settings-network-manual.png）替代。
- 数据清理完成：`*.s7-demo.internal`/`*.align-check.internal` 已删并保存，终态 `proxyMode=system, proxyBypass=[]`；dev-stack 已 `-Stop`；全程未触用户 :51888 实例。

## 6. Review

见 `06-review.md`。Final Decision：**APPROVED**（Risk L2；Critical/Major 无；Minor 4 项均已处置或登记：① EP 定宽规则已沉淀 §B3 ② ForgeSnapshot 只缓解未根治→并入既有 flaky 待办 ③ 宽视口截图缺位已如实标注 ④ direct 影响面已在 042 §4 交代）。

## 7. Risk

**L2**——契约字段更名（ProxyEnabled 移除）无存量迁移负担（首版未发布）；代理决策为出站全链路（direct 影响更新下载/插件出站的语义已在 042 §4 向用户明示）；测试改动均在本域过滤集内复绿。

## 8. Problems Found

1. EP 组件 Tailwind 宽度 class 无效（同特异性覆盖）——已修 + 规则沉淀 §B3 + 守卫候选入 TODO（PC-009 候选类）。
2. 旁路错误文案右对齐归属弱、placeholder 超宽裁切——已修（含 e2e 选择器同步）。
3. `ForgeSnapshot.Restore()` 全量并发撞 config 句柄（File.Replace IOException）——重试窗口扩至 ~6s 缓解；根治方向并入「进程级全局状态测试 flaky」待办。
4. 全量 dotnet test 基线红 30 例（非本域）——逐条归族登记 TODO，本批不修。
5. 窄视口 FAB 遮挡 / 模式切换高度跳变 / toast 遮导航——判定非本面板缺陷，各立 TODO 条目。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

流程缺口披露：并行会话分工（后端 S1/S2 由另一会话推进）下，本会话以「残留引用扫描 + dotnet build」做接续核对，避免了重复改造；e2e 首轮后台启动失败（终端复用干扰）改前台重跑——后台启动长任务需先确认日志文件产出。

## 10. 问题类归档

本批修复的缺陷（EP 宽度覆盖、错误对齐、placeholder 裁切、config 句柄 flaky）在既有 PC-001~008 中**无对应类**，属首见个案：
- **候选新类 PC-009「第三方组件库默认样式同特异性静默覆盖工具类」**：本轮首见、全仓受害面未扫查，按 problem-class-closure 触发条件（二次复发或专项）暂不建类，已落成 TODO 逐字条目「EP 组件尺寸被默认样式覆盖——守卫未自动化（P3，候选问题类 PC-009）」，规则先行沉淀 agent-workflow §B3。
- config 句柄 flaky 归入既有「config/*.tmp 句柄争抢」家族（代码注释 + 05-evidence 已记），落成 TODO 条目「进程级全局状态测试 flaky 彻底修复（同族新增 2026-10-18）」。

## 11. 重构提案

**R1（局部，本文件披露，不另开批次）**：`SettingsControllerNetworkTests` 对全局 `ForgeSetting.config` 的直接读写是 flaky 根因，真修＝测试类改注入/隔离存储——已并入既有 P3 待办，触发条件（阻塞 CI）到达时执行。
**R0（其余）**：ModelProxy/BR-1 决策链职责单一、BypassMatcher 内聚合理，本批不重构；跨模块的「设置页错误占位防跳动存量推广」已在 TODO（P3）挂账，属独立批次不混入本批。无架构级（R2/R3）诉求。

## 12. 悬空教训清单（必须为空）

（空）——本次全部发现/遗留均已落成逐字可检索条目：
- 「全量 dotnet test 基线红 30 例（非网络域）」→ TODO.md
- 「进程级全局状态测试 flaky 彻底修复」（ForgeSnapshot 同族）→ TODO.md
- 「EP 组件尺寸被默认样式覆盖——守卫未自动化（候选 PC-009）」→ TODO.md
- 「设置页窄视口下宿主 FAB 与内容列重叠」「设置·网络面板模式切换高度跳变」「保存成功 toast 遮盖左侧导航且仅 ≈3s」→ TODO.md
- 「NetworkPanel 密码 placeholder 尾部截断」「网络设置衍生项」（SOCKS5/非模型出口/匿名鉴权等）→ TODO.md（既有）
- 规则沉淀：EP 定宽 → agent-workflow §B3；旁路/系统代理事实 → 042 §4（唯一真源）
