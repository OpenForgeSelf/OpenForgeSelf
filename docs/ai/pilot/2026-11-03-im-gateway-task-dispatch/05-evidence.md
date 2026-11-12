# 05 · Evidence — PILOT-079 T3（task-3：e2e 冒烟主链路 + 集成实测 + 走查取证）

- 任务：task-3（qa 负责）；日期：2026-11-04；作者：qa（ForgeSelf 团队成员）
- 证据分级：**Verified** = 亲自跑过/亲读日志拿到真实输出；**Inferred** = 凭代码与日志推断；**Unknown** = 未验证/本环境不可达。
- **宿主 build 状态标注（lead 2026-11-04 要求）**：缺陷 #1 = Watcher 不回填 RunId（ImTaskWatcher.cs:117-120 只读，QA 发现，lead 判缺陷单，be-dev 修复）；缺陷 #2 = IAgentRegistryService AddScoped 俘虏依赖（AIAgentPlugin.cs:44，QA 发现 → 缺陷单 #2，be-dev 改单例 + AgentRegistryCaptiveTests 守卫）。三轮 e2e 对应 build 见 §2。

## 1. 结论速览
- e2e 冒烟主链路（⓪-⑦ 全链）：**三轮全绿**（5 passed / 1 skipped fixme / exit 0，1.3m）。
- 既有 4 用例回归（AC6 默认关闭态）：三轮全绿。
- dev-stack AC4 双查实测：待走查（§5 占位）。

## 2. 红绿轨迹（三轮 e2e，同 spec 逐步逼近实码契约）
| 轮次 | build 状态 | 结果 | 红因/备注 |
|---|---|---|---|
| 一轮 | 含缺陷 #1 修复；**DI 修复前** | 4 passed / **1 failed**（1.2m） | 快速失败断言生效：「任务派发失败：未找到 Agent e2e-imtask-agent」。根因=缺陷 #2（派发链持有的注册表为启动期冻结实例，运行期 REST 新建 Agent 不可见）。种子本身成功：backend.log:479 新建 Agent: e2e-imtask-agent；:483 为 e2e afterAll 清理（lead 指示：此取证写入备注——**REST 写路径本身是通的，缺的只是派发侧可见性**）。处置：改走「config 绑定既有 Agent」路径（lead 批准为永久路径；AC5 原文即「REST 种子**或** config 绑定既有 Agent」二选一） |
| 二轮 | 同上（DI 修复前） | 4 passed / **1 failed**（2.1m） | ③ ack 断言首次通过（内置 agent.generalist 生效）。红因=环境能力缺口：隔离宿主模型目录为空 → RunOrchestrator「无可用对话模型，拒绝创建执行」（backend.log 00:44:33.878）→ AgentRun 实体未创建 → tasks-overview 无从出现。PC-013 明确失败语义正确。处置：config 绑定哨兵模型 e2e-unreachable-model（ChatModelResolver.cs:38-43 对调用方指定模型免目录校验；链路来源 weCom.BoundChatModelId → Router:67 → Dispatcher:110），Run 得以真实创建 |
| 三轮 | **含缺陷 #1 + #2 两修复** | **5 passed / 1 skipped / exit 0**（1.3m） | 全链绿（§3 证据值） |
| **最终轮** | **含缺陷 #1 + #2 + #3 三修复**（#3=pluginProvider 共享+5 契约实例注册，lead 三重亲核） | **5 passed / 1 skipped / exit 0**（1.4m） | 全链复绿；新会话 imtask:ab9ee257… Run 1 哨兵模型 → failed 终态链路照旧；既有 4 用例 AC6 回归绿 |

## 3. 冒烟主链路实测值（三轮，Verified）
证据文件：`ForgeSelf.Web/screenshots/e2e/im-gateway/im-gateway-imtask-smoke.log`（trace 全文）；backend.log 同步佐证。
- ⓪ 绑定既有内置 Agent：GET /api/agents 断言 agent.generalist 存在且启用 ✅
- ① config 开启 + 回读：POST /api/im-gateway/config 200；GET 回读 `taskGateway.enabled=true、defaultAgentId=agent.generalist、pollIntervalSeconds=3、boundChatModelId=e2e-unreachable-model` ✅（真落盘）
- ② POST /api/im-gateway/simulate-inbound（**实码路由**；FR-8 工单旧文本 /api/imgateway/ 与实码不符，lead 裁决以实码为准、02-spec 已修齐）：200 `{success:true, msgId:sim-…}` ✅
- ③ ack 落消息表：direction=out、status=ok、content=「已建任务（编号 3a31871a）已指派 agent.generalist（通用助手），完成后自动回复」；sessionId=a127bcded600433e93197e558a3ab486 ✅
- ④ tasks-overview（source=planned，桶=全状态投影 TasksOverviewController:110/129-152）：runId=1 ✅
- ④b AC4 双查：GET /api/ai-agent/runs?sessionId=imtask:a127bcde… 命中 id=1；TaskInput 含 ComposePrompt 审批策略尾缀（标题首行=任务原文）✅
- ⑤ 绑定落库（publish 实例 ImGateway.db）：首查 `{TaskKey:3a31871a…, RunId:0, Status:pending}` ✅
- ⑥ 终态回复：Watcher（轮询 3s）经 failed 终态发 FR-5 失败模板，out/ok 落行 ✅（backend.log：Run 1 新建 → MemorySystem 工具真实调用 → LLM 失败「localhost:1234 拒连」→ failed → Watcher 回复）
- ⑦ 绑定行终态复核：**`{RunId:1, Status:failed, RepliedAt:2026-11-04 00:50:46.9}`——RunId 0→1 回填 + RepliedAt 幂等标记实证（缺陷 #1 修复的 e2e 端到端验证）** ✅
- 收尾：证据落盘 + 配置还原（含 boundChatModelId 回 null）✅；既有 4 用例三轮全绿（AC6）✅

## 4. 契约事实读码（Inferred→经 e2e 实证转 Verified 的关键项）
- ack 文案、SessionId=imtask:{会话}、ComposePrompt 审批说明尾缀、DefaultAgentId 仅会话未绑定时生效（ImTaskDispatcher.cs:93-96）、AgentRef 解析链、未连接 SendAsync 仅告警仍落 out/ok（WeComChannel.cs:154-167）——均由三轮绿反证。
- overview planned 桶=ListRunsAsync(status:null) 全状态投影（TasksOverviewController:110/129-152）——Run 终态后仍在桶内，断言无时序脆弱性。

## 5. dev-stack AC4 双查实测 + 走查截图（origin 源码本地前后端 :7301/:7399，lead 主导）
- **build 状态：含缺陷 #1 + #2 两修复；缺陷 #3 修复前**（缺陷 #3 = 插件 4 个 eager 块各自 BuildServiceProvider + 宿主主 provider 共 5 个并存注册表实例，裁决修复中；lead 2026-11-04 裁示：该分裂不影响本批展示数据，截图证据有效）。
- lead AC4 源码级 REST 实测（Verified，证据 `.temp/tmp/p079-ac4-*.json/log` 九件：0-config / 1-post-body / 1-post-resp / 2-simulate / 3+3b-overview / 4+4b-runs-sid / 5b-messages）：
  - config 开 TaskGateway（defaultAgentId=agent.generalist + 哨兵 boundChatModelId）→ simulate-inbound（channelKey=wecom:ac4-conv-1:lead-ac4）→ **tasks-overview 出现 planned:9（failed）** → runs?sessionId=imtask:fe2acdfacf1a4cb7929074e48c3116de 命中 Run 9（AC4 双查 ✅）→ 终态后消息表 #22 入站 / #23 ack out/ok / #24 ❌ 终态回复 out/ok（✅）。
- qa 走查截图四张（Verified，逐张读图核对 ui-ux-design 清单；`ForgeSelf.Web/screenshots/dev/`）：
  1. `p079-dev-01-task-center.png`——任务中心「计划任务」桶置顶 Run 9「写一份本周AI进展周报」（失败 pill + 重试 + 404 错误原文省略截断）；全状态桶（失败/卡住/已完成混排）、来源筛选态、共 21 · 异常 11 计数齐全；CRAP：列对齐一致、状态色语义清晰、长文本省略。
  2. `p079-dev-02-im-gateway-panel.png`——插件面板 v2.2.0 徽标；会话 lead-ac4 三条消息全镜：入站（右）/task → ack 出站（左）「已建任务（编号 ca024230）」→ ❌ 终态回复全文；「未连接」诚实状态；方向几何（D2：out 左 / in 右）可视。
  3. `p079-dev-03-task-gateway-card.png`——任务网关卡启用态全字段：开关 ON + 已启用徽标、命令前缀 tags（/task、任务：）、**默认 Agent=通用助手（=agent.generalist）**、**轮询 3 秒（=REST 配置）**、IM 审批 ON、同意/拒绝关键字 tags、默认值脚注。
  4. `p079-dev-04-terminal-reply-detail.png`——消息面板特写：❌ 任务 **#9** 失败 模板全文（**标签用 RunId 9 而非 ShortCode ca024230——dev-stack 侧 RunId 回填可视实证**，缺陷 #1 修复跨宿主一致）。
- 防误读注记（lead 实测发现）：① messages 端点必须带 channelKey 参数（缺省 400 验证），本会话 channelKey=wecom:ac4-conv-1:lead-ac4；② **Run 的 sessionId = imtask:{内部会话 GUID} 而非原始 conversationId**（与 e2e 三轮一致：imtask:fe2acdfa…/imtask:a127bcde…）。

- **缺陷 #3 修复后视觉复验（2026-11-04 01:1x，最终 build）**：qa 重开四视图判定——①任务中心计划桶 Run 10 置顶、②④会话三消息（#25-#27，❌ 标签=RunId 10）与 ③任务网关卡（还原后未启用态，禁用字段带可见原因）——**布局/样式/几何与 §5 基准逐项一致，无视觉变化，基准截图不重拍继续有效**（现态差异为配置还原与新数据，非样式回归）。dev-stack vite 于判定完成后被停用（lead 重建节奏），console 出现的 HMR/代理连接拒绝属环境事件，非应用错误。

## 6. Unknown / 风险（含 lead 2026-11-04 裁决归位）
- **completed 终态路径**：本环境无可用上游模型，e2e 仅证 failed 终态全链路；completed 与 failed 共享同一 Watcher 回复路径，单测背书（lead：终审门 2 显列）。
- **审批卡推送幂等 + 「同意 #N」关键字决策（FR-9/AC10）**：e2e 不可达（无真实 LLM 推不到工具审批 stuck；铁律禁 mock）→ spec 内 apiTest.fixme 维持不放开（lead 裁决）；归 task-2 单测替身 + dev-stack AC4 人工见证。
- **多任务同会话 inflight 双闸拒绝**：e2e 未断言（主链路单任务）；SubmitAsync open 态查询 + inflight TryAdd 双防线（AgentRunDispatchService DriveAsync 前）在码（Inferred），单测背书。
- **哨兵模型依赖**：e2e 依赖 config 绑定哨兵不可达模型使 Run 真实创建；若未来宿主改为「调用方指定模型也做目录校验」，④ 段将红——届时按新契约调整（风险注记）。
- **种子取证备注（lead 指示留档）**：backend.log:479 新建 Agent e2e-imtask-agent 成功 + :483 afterAll 清理——REST 写路径本身是通的，缺陷 #2 缺的只是派发侧可见性；该区分对终审有用。

## 7. 证据清单（实际产生的文件/日志）
- `ForgeSelf.Web/screenshots/e2e/im-gateway/im-gateway-imtask-smoke.log`（三轮冒烟 trace，含全部断言值）
- `ForgeSelf.Web/screenshots/e2e/im-gateway/im-gateway.log` / `im-gateway-chat.log` / `im-gateway-geometry-lock.png` / `im-gateway-direction-lock.png` 等（既有 4 用例证据，三轮刷新）
- `.temp/e2e/wt-b26d4625/backend.log`（宿主链路：新建 Agent :479/:483、Run 新建/失败/回复、派发成功）
- Playwright 终端输出（三轮：1 failed → 1 failed → 5 passed exit 0）
- `ForgeSelf.Web/screenshots/dev/p079-dev-01-task-center.png` … `p079-dev-04-terminal-reply-detail.png`（dev-stack 走查四张，§5 逐张核对结论）
- `.temp/tmp/p079-ac4-*.json`（lead AC4 源码级实测九件）
## 8. 返工验证轮（2026-11-04 夜 · qa 执行 · lead 召集）
> 背景：用户 :51888 真实模型环境提出 8 项验收反馈（任务板 task-8~task-16，#7 拆 T14+T15，补充第 8 项=T16）。返工批 9/9 落地并经 lead 验收后，qa 按 `.temp/tmp/p079r2-reverify-draft.md` 清单逐项复验。环境：dev-stack 7301/7399（`pwsh scripts/dev-stack.ps1`，起源码）+ 真实模型（gpustack / qwen3_8_vision，本地首获可用上游）。

### 8.1 ✅ Verified 九项
1. **T8 任务中心倒序**：REST tasks-overview planned 桶 sourceRef 严格倒序 10→1（对照修复前 00:55 实测乱序）；UI 全程时间递减。证据 `.temp/tmp/p079r2-verify-1-t8-overview.json`、`ForgeSelf.Web/screenshots/dev/p079r2-t8-t15-task-center.png`。
2. **T9 任务卡跳转带 runId**：点 stuck 行 → `/ai-agent?runId=14` 直达该 Run 执行记录卡（卡住原因全文、规划步骤、继续/重开/跳过/补位/取消五入口）。证据 `ForgeSelf.Web/screenshots/dev/p079r2-t9-jump-run14.png`。
3. **T10 completed 终态推送（dev-stack 侧）**：真模型下 Run 13 完成 → `✅ 任务 #13 完成`+摘要推送（22:51:21.184），绑定行 RunId 0→13 回填（缺陷 #1 修复活体实证）、RepliedAt 置位；推送内容=receipt 忠实投影（Run 17：receipt 691 字符→推送 513，无丢失）。编号双制维持（ack=短码，推送=#RunId）。**手机送达=用户环境待复验**。证据 `.temp/tmp/p079r2-verify-3-t10-completed-push.json`。
4. **T11 会话视图滚动**：打开会话自动滚底（`.ig-thread__scroll` 755>578，atBottom=true）；上翻后新消息到达 scrollTop 守恒于 0（内容 755→937、消息 4→6 条）不打扰。浏览器 DOM 实测。
5. **T15 stuck 行 UI**：「卡住」橙 badge 区别「失败」红 badge；行内诊断摘要（stuckReason 截断展示）；继续/终止入口（失败行为重试/终止）；点击直达现场（同 T9）。
6. **T16-9a 进展推送**：`▶ 任务 #14 执行中`（22:55:05）等三条活体（Run 14/15/17），指纹变化即推、非每拍空报；子开关与首轮基线单测在码（ImTaskDispatchTests:756/785）。
7. **T16-9b 配置回环**：stalledMinutes POST→回读生效→还原（poll 60/stalled 5 默认）；无变化真转 stuck 的行为面待可控行程（本地模型输出短，构造不出 5 分钟无变化长任务）。
8. **T16-9c 终态即推**：❌ 终态端到端 +1.06s（入站 22:47:50.587→❌ 22:47:51.651，恰首个 3s 扫描节拍）；✅ 滞后=终局合成 LLM 时长（非节拍）。证据 `.temp/tmp/p079r2-verify-9c-terminal-push.json`。
9. **抽查 A3/B1/F4**：A3 禁用态=「未启用」badge+字段置灰+原因说明可见（`p079r2-a3-config-panel.png`，且面板实时回读窗口配置=面板↔API 一致性）；B1 ack 0.37-0.4s（多次活体）；F4 无待审批发「同意」零误伤走聊天链路（`.temp/tmp/p079r2-verify-f4-no-misdamage.json`）。

### 8.2 ⚠️ 部分实证二项
- **T12 分段发送**：实现在码（ImTaskWatcher.cs:294-311 TrySendInSegmentsAsync：段尾标 (i/n)、顺序发送、失败即停不置 RepliedAt 下轮全量重发）+ 单测×4（ImTaskDispatchTests:853/871/885/905）。本地行为面未触发：两次构造（2500 字直出、12 遍重复）均被本地模型短输出规避（推送 513<阈值 2000）——**分段行为=用户环境待复验**。附带实证：推送链对 receipt 无截断。
- **T13 根类清扫**：守卫测试 PluginRawCreateScopeGuardTests×2（禁 ctx 裸 CreateScope + 已治理锚点防静默失效）+ case 归档 `docs/11-troubleshooting/cases/2026-11-04-raw-createscope-on-ctx-sweep.md` + PC-005 更新，静态核 ✅；**守卫实跑 2/2 绿**（lead/be-dev 执行，`.temp/tmp/p079r2-verify-t13guard2.log`）。

### 8.3 回归护栏
- **e2e（AC6+冒烟，构建=返工批全修复版）**：5 passed / 1 skipped（1.7m）/ EXIT 0。覆盖 D1/D2 几何语义锁、契约探针（withToken 200 / noToken 401 / isArray / camelCase）、冒烟主链路 simulate→/task→ack→双查→绑定→终态回复。日志 `.temp/tmp/p079r2-verify-e2e-run.log`。**返工批零回归。**
- **后端全量留底**：`.temp/tmp/p079r2-verify-fulltest.log`（3393 绿 / 125 红，与基线对表净真新增红=0）+ `.temp/tmp/p079r2-verify-newred-check.log`。

### 8.4 用户环境待复验三项（:51888）
1. T10 「✅ 任务 #N 完成」手机送达（本地只验推送链与模板正确性）。
2. T12 >2000 字分段多送行为（本地模型输出长度不可控）。
3. 9a 长任务极端值（多步长时间运行的进展推送节拍/频率上限）。

### 8.5 既有缺陷登记（非本批引入 · lead/be-dev 定性）
- **LLM 调用 400（agent.programmer）**：Run 15 失败根因=工具结果无上限×模型踩中大目录，属既有缺陷，TODO 已登记、不阻塞本批。qa 收窄数据：绕 IM 直打编排层（POST /api/ai-agent/runs，纯 taskInput）无 400（plan/step/content 全通），400 为 IM 派发请求形状（审批策略前言+工具+历史组合）命中；F1/F2 活体审批循环因此阻塞，按 FR-9 单测×5 在码背书 + 定性结论入包（审批卡幂等/同意决策/跨会话拒绝/零误伤/超时指引）。

### 8.6 过程留痕
- 复验清单（含逐项结论回填）：`.temp/tmp/p079r2-reverify-draft.md`
- JSON 证据八件：`.temp/tmp/p079r2-verify-{1-t8-overview,3-t10-completed-push,5-t12-segments,7-t14-run3-snapshot,7-t14-stuck-snapshot,7-t14-f1-snapshot,9c-terminal-push,f4-no-misdamage}.json`
- 截图五张：`ForgeSelf.Web/screenshots/dev/p079r2-{t8-t15-task-center,t9-jump-run14,a3-imgw-panel,a3-config-panel,t12-t11-thread}.png`
- dev-stack 生命周期日志：`.temp/tmp/p079r2-verify-devstack{,2,3}.log`
- 测试数据：Run 12-18 + 消息表暂留（闸门2 走查现场引用，用户验收后清）
