# 05 Evidence — 门禁违例修复批（PILOT-080）

> 阶段：Stage 7-8 · Evidence｜分档：**中**（§5.6）｜证据分级：✅ Verified / ⚠️ Inferred / ❌ Unknown
> 判据来源：`gate-logs/` 日志正文（exit code 不作证据）；代码落点为 git status / git diff / 文件读取实测。

## 1. 机器判据总览（对应 01 §3 逐项）

| # | 判据 | 结果 | 等级 | 证据位 |
|---|------|------|------|--------|
| 1 | be-build 0 错误 | ✅ 0 错误 / 501 警告（既有） | Verified | gate-logs/be-build.log |
| 2 | st-tests 36/36 | ✅ 36/36 | Verified | gate-logs/st-tests.log |
| 3 | be-test-filtered2 仅剩 1 红 | ✅ 失败1/通过8/总计9；唯一红 = JsonNonAsciiEscapingGuardTests.cs:46 点名在途批文件（归因，非本批修复面） | Verified | gate-logs/be-test-filtered2.log |
| 4 | be-test-full 28 红 = 24 基线 + 4 非基线，零新增 | ✅ 失败28/通过3746/总计3774（12m33s）；对表见 §3 | Verified | gate-logs/be-test-full.log |
| 5 | fe-check 0 error | ✅ 0 error / 165 警告（既有）+ component-conventions PASS | Verified | gate-logs/fe-check2.log |
| 6 | fe-test 1209/1209 | ✅ 116 文件 / 1209 用例 / 19.58s 全绿 | Verified | gate-logs/fe-test2.log |
| 7 | fe-design2 棘轮 | ✅ style-tokens 1761 vs 基线 2501（−740）PASS；compliance a=209 b=0 c=0 d=0 PASS | Verified | gate-logs/fe-design2.log |
| 8 | pc-check2/pc-check3 除 PC-033 全绿 | ✅ PC-011/020/027 status=guarded 完成（pc-check2 清零；pc-check3 33/33 完成，PC-011 二次复发归档闭环机器校验通过）；仅 PC-033 FAIL（并行批归属，不属本批，两轮同判） | Verified | gate-logs/pc-check2.log / pc-check3.log |
| 9 | st-web-build 绿 | ✅ vite 6.4.5 / 11 modules / style.css 16.78 kB / index.js 81.54 kB / 474ms | Verified | gate-logs/st-web-build.log |
| 10 | C：GBK 不乱码 + XTrace duty | ✅ 代码落点实测（§5）+ 运行态实锤：task-3 走查 GBK 中文经 ProcessOutputText.Decode + ProcessOutputLineBuffer 流式解码渲染完整无乱码（对比修复前固定码页乱码）；长行折行 / ANSI 控制序列消费 / 会话切换历史保持全正常 | Verified（代码态 + 运行态） | §4；gate-logs/st-walkthrough.log + ForgeSelf.Web/screenshots/dev-stack-smart-terminal/ |
| 11 | B：亮/暗两态显式验证 | ✅ 亮/暗显式切 html.dark 双态走查绿（system 未作为暗色依据）；对比度抽测：标签 7.2:1、次要文字 5.1:1、.view-title 12.4:1（≥4.5:1 达标） | Verified | §4；gate-logs/st-walkthrough.log + ForgeSelf.Web/screenshots/dev-stack-smart-terminal/ |

## 2. 首跑基线对照（修复前 → 修复后）

| 门禁 | 修复前 | 修复后 |
|------|--------|--------|
| fe-check | ✅ 0 error（基线即绿） | ✅ 0 error |
| fe-test | ✅ 1209/1209（基线即绿） | ✅ 1209/1209 |
| be-build | ❌ 6 错误 | ✅ 0 错误 |
| be-test | ❌ 级联失败 | ✅ 28 红 = 24 基线 + 4 非基线 |
| design:check | ❌ 0→35 违例 + &&-短路 | ✅ 1761 棘轮 PASS（含 B 批 −740） |
| pc-check | ❌ PC-011×3（S1×1 + S2×2） | ✅ 清零（guarded） |
| 继承基线 | ~1516 tests / 13 红（PC-014 ForgeSetting×1、PluginFrontendFileMiddleware×2、ImGateway no-such、McpStdioWindowsShimTests env） | 当日全量对表见 §3.3 |

## 3. be-test-full 28 红对表

> **lead 已确认（2026-11-13）**：24 基线 + 4 非基线成立，零新增；本节对表定稿。

### 3.1 24 项基线红（非本批引入）
| 测试 | 数 | 归属 |
|------|----|------|
| ProjectWorkspaceDefaultRootTests | 15 | TODO L522 待移除区 |
| WorkflowPlanningIntegrationTests | 6 | 404 路由既有 |
| ScriptRunnerDiIntegrationTests.GetRuntimes | 1 | 既有 |
| ScriptRunnerTests.CancelAsync | 1 | 既有 |
| MafApprovalLoopTests.ConfiguredRule | 1 | 既有 |

### 3.2 4 项非基线红（处置）
| 测试 | 处置 | 等级 |
|------|------|------|
| PluginLoadDependencyGuardTests | A1（CopyLocalLockFileAssemblies）+ A1b（Compile Remove）已修复；隔离复跑 6/6 通过（gate-logs/be-test-timeoutkill-plguard.log）+ st-tests 36/36 与 filtered 通过 8 佐证 | Verified |
| RunTerminalCommandTimeoutKillTests ×2 | 负载抖动（超时击杀时序），非缺陷，不修（07 §8 不做决策 #3） | Inferred |
| JsonNonAsciiEscapingGuardTests | 守卫点名在途批文件（WireStreamTranslator / TerminalInstanceForwarder 等）→ 归因记录，Forbidden 清单内 | Verified |

### 3.3 当日缺席观察
| 测试 | 之前状态 | 当日全量 | 处置 |
|------|----------|----------|------|
| McpStdioWindowsShimTests | 继承基线环境红（handoff §3F 定性） | 未点名（当日绿） | F 记录，不作修复项（07 §8 #4） |
| DesignAgentToolContractTests | 11-11 基线红 ×1 | 未复现（当日绿） | 对表记录，不计入 |

## 4. 插件六步闭环证据（插件任务硬性门禁）

| 步骤 | 证据 | 状态 |
|------|------|------|
| ① 门禁 | be-build / st-tests / st-web-build / pc-check2（§1） | ✅ Verified |
| ② 冒烟 e2e（task-1） | **run5 绿（task-1 完成，rev 3）**：日志正文 `[1/1] [chromium] › e2e/plugins/smart-terminal/smart-terminal.spec.ts:21:3 › smart-terminal smoke › route loads plugin view with title` → `1 passed (43.6s)`，0 失败；globalSetup republish 成功（publish → Authenticode 签名 → 宿主 spawn 健康）。截图 ForgeSelf.Web/screenshots/e2e/smart-terminal/smoke-view.png（1280×720）读图：图标/间距/颜色/遮挡/溢出 ✅、暗色一致、空态正常；Info 不阻断：右上角宿主壳图标暗底对比度偏低（非本插件改动面，移交观察）。过程 3 次环境类迭代（run3 进程树外部收割、run4 NuGetScratch lock 瞬时竞争自解）均非 SmartTerminal 功能缺陷 | ✅ Verified（gate-logs/st-e2e.log 日志正文自查 + lead 转发 task-1 rev 3） |
| ③ 起源码走查（task-3） | **双态全绿（2026-11-13 18:29-18:31，lead 19:21 转发 task-3 结论）**。环境（日志正文）：dev-stack -Start -SkipBuild 成功——后端 :7301 PID 70896 (200)、前端 :7399 PID 61980 (200)（端口占用自动顺延，非默认 7102/7002）、令牌已取、状态 .temp/dev-stack.json；全程未碰 :51888/.env/在途文件；结束 -Stop 释放构建锁。亮色：插件页加载正常，.view-title「智能终端」+ 版本徽标正常；会话列表/新建/输出区中英混排无乱码无截断。暗色（显式 html.dark）：token 一致（s-terminal-dark.css），无白底白字无裸色残留；对比度抽测 标签 7.2:1 / 次要文字 5.1:1 / .view-title 12.4:1（≥4.5:1）。五态：加载骨架 / 空态 / 出错横幅+重试+自愈 / 无权限 401 / 正常，全 ✅；重复点击去重、旧响应不覆盖新选择。输出管线（fix C 验收核心）：GBK 中文流式解码渲染完整无乱码。**无 Finding**（样式清单面：双态/五态/对比度/GBK；lead 补录的走查遗留缺陷 BUG#1-5 + DESIGN.md 合规缺口均在本批 A-F 范围外，分流见 06 §3 / 07 §7）。截图 3 张落盘齐（glob 亲验文件存在：light-01-empty-state.png / light-02-session-active.png / dark-01-session-active.png，其中 light-01 读图自查 ✅：.view-title v2.0.0 + EmptyState + 无乱码；lead 2026-11-13 20:12 复核实测 154930B 19:01 / 230787B 19:41 / 205695B 19:41，缺口已消——原「转发 6 张/磁盘 1 张」差异以磁盘 3 张实测为准；light-02/dark-01 未逐张读图，如实记录） | ✅ Verified（gate-logs/st-walkthrough.log 正文 + lead 转发 + 截图读图） |
| ④ 发布 | lead 域完成：release-local -UpdateDir -Sign **ALL DONE in 125s**（rerun3 19:57 起，lead 20:12 转发）；产物 OpenForgeSelf-2.4.22.2611131957-win-x64.zip (183MB) + RELEASE-NOTES-2.4.22.2611131957.md + SHA256SUMS.txt 落 D:\src\my-proj\OpenForgeSelf\updates；日志正文：Authenticode signing ok (12.2s，新增 2/跳过 3/失败 0)、UpdateDir SHA256 verification PASS (1 file(s) rechecked) | ✅ Verified（gate-logs/release-local4.log L1921-1948 + 尾部 ALL DONE/产物清单，pilot-writer 亲验） |
| ⑤ 运行实例只读复验 | 触发条件未达成（用户未在运行实例启用/更新本批改动） | — |
| ⑥ 插件文档更新 | 随 07 §5 回写建议（lead 域） | — |

> 铁律：③ 必做且双绿不能替代；② 禁止铺全流程 e2e（1 条主链路即可）。

## 5. 代码落地证据（实测，逐条对应 03 §2）

| 批 | 文件 / 行 | 验证方式 | 等级 |
|----|-----------|----------|------|
| A/A1b | SmartTerminal.Tests.csproj L8-10（PC-020 注释+开关）、L25（通配保留）、L26-29（A1b 注释+Remove） | git status M + 文件读取 | Verified |
| A2 | DbAdminTests.csproj L29-31（XCodeTestFixture 链接）；L12/L22 属 2bde724（只验证） | 文件读取 + git log | Verified |
| B | themes/smart-terminal-dark.css（新增 untracked）+ styles.css:1-2（@import+注释） | 文件读取 | Verified |
| C | SessionShell.cs L17-18 / 37 / 43 / 58；TerminalSessionService.cs L795-802 / 920-931（L927-931 = XTrace duty） | 文件读取 | Verified |
| C 禁触核验 | ForgeSelf.Core/ProcessOutputText.cs、ProcessOutputLineBuffer.cs | `git status --porcelain -- ForgeSelf.Core` 为空 | Verified |
| D | AuditLogStore.cs:20、PermissionClassifier.cs:213（2bde724 已入库）；在途 2 处归因不修 | git log + 文件读取 | Verified |
| E | OpenAIResponsesOutboundSchemaTests.cs:46-49（+5 行 Regex 排除比较式） | git diff 实测 | Verified |
| F | 无代码变更（记录项） | — | — |

## 6. Unknown 汇总（不留含糊）

1. ~~task-1 e2e 最终结论~~ → **已解除（2026-11-13 18:04）**：run5 绿 `1 passed (43.6s)`（§4 ②），AC-8 前半闭环。
2. ~~task-3 走查结论（亮/暗、五态、对比度、GBK 运行时输出）~~ → **已解除（2026-11-13 18:29-18:31 走查，lead 19:21 转发）**：亮/暗显式双绿、五态齐、对比度 7.2:1/5.1:1/12.4:1 ≥4.5:1、GBK 运行态无乱码（§4 ③）→ AC-8 全闭环，判据 #10 运行态 / #11 解除。**05 §6 Unknown 清零。**

## 7. AC 覆盖映射（02 §7 验收标准 → 本文件证据节；守卫机械校验 AC1~AC9）

| AC | 判据（02 §7） | 证据落点（本文件） | 等级 |
|----|---------------|--------------------|------|
| AC1 | pc-check2 除 PC-033 全绿（status=guarded 完成） | §1 #8（pc-check2 清零 + pc-check3 33/33） | Verified |
| AC2 | be-build 0 错误（501 警告既有） | §1 #1（0 错误 / 501 警告） | Verified |
| AC3 | st-tests 36/36；be-test-filtered2 9 中 8 过、唯一红=守卫点名在途 2 文件 | §1 #2、§1 #3（唯一红 JsonNonAsciiEscapingGuardTests 归因） | Verified |
| AC4 | be-test-full 28 红 = 24 基线 + 4 非基线，零新增 | §1 #4 + §3（3.1 基线 24 / 3.2 非基线处置 4 / 3.3 当日缺席） | Verified |
| AC5 | fe-check 0 error + component-conventions PASS；fe-test 116 文件/1209 用例全绿 | §1 #5、§1 #6 | Verified |
| AC6 | fe-design2 棘轮 1761（基线 2501）无新增；compliance a=209 b=0 c=0 d=0 | §1 #7 | Verified |
| AC7 | st-web-build 绿（vite 6.4.5，11 modules，dist 产出） | §1 #9 | Verified |
| AC8 | 插件冒烟 e2e 1 条通过 + 亮/暗两态走查证据 | §4 ②（run5 1 passed 43.6s）、§4 ③（双态五态对比度 GBK 全绿）、§1 #10/#11 | Verified |
| AC9 | 05/06/07 齐备；07 含问题类归档/重构提案/悬空教训三节且悬空教训清单为空 | 本文件齐备 + 06-review.md + 07-final-report.md（三节齐） | Verified |
