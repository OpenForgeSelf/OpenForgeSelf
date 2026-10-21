# AI-Native Pilot 结果汇报（闭环）

> 状态只裁事实，禁止模糊（AGENTS.md §10.4/§10.5）。本文件为闸门2 交付底稿，聊天正文只报路径+摘要+待拍板。

## 1. Repository Understanding

已确认：`list_tools` 是 McpCenter 经 ToolExtensions 注册进宿主 IToolRegistry 的**工具函数**，网关与内置 AIAgent 的 universal_tool 都经注册表分发到它（单点合并杠杆）；外部工具原设计不注册进注册表（决策 D10）；跨插件供给标准姿势 = Abstractions 契约 + eager ctx.Register + 消费侧运行期 Get（裁决 A/F，判例 IWorkflowAIAdvisor）；AIAgent 因 ALC 隔离无法引用 McpCenter 程序集。详见 00/01。

## 2. Selected Task

McpCenter v2.6.0 + AIAgent v1.9.0：外部 MCP 工具进 list_tools 枚举 + 内外均可直调（用户输入「万能工具要在 list_tools 把 mcp. 开头的也带上，还有所有…」= 推翻 D10 枚举侧；「开工」= 闸门1 批准）。

## 3. Changed Files

契约层 1（IMcpExternalGateway.cs 新建）+ McpCenter 6（Adapter 新建/Plugin 注册/ListTools 合并/Forwarder/Manager/Session）+ AIAgent 1（UniversalTool 路由）+ 测试 4 文件（契约/Adapter/ListTools 扩/AIAgent 路由）+ e2e 1（v2.6.0 用例 + 存量 strict 一行修）+ 版本 3（McpCenter plugin.json+csproj、AIAgent plugin.json）。全表见 05-evidence。

## 4. Validation

Build: `dotnet build ForgeSelf.Api` exit=0（0 error）。
Unit Test: `dotnet test ForgeSelf.Abstractions.Tests` **46/46 绿**；`dotnet test ForgeSelf.Api.Tests --filter "~McpCenter|~UniversalTool"` **200/200 绿**（CodeReview 修复后复跑仍全绿）。
E2E: `playwright test mcp-center.spec.ts -g "v2.6.0"` **1 passed (1.3m)**；UI 渲染用例复跑 **1 passed (1.3m)**。

## 5. Evidence

见 `05-evidence.md`：AC-1~AC-7 全 Verified；e2e 证据 `ForgeSelf.Web/screenshots/e2e/mcp-center/list-tools-external.log`（枚举→直调→断开归零）；dev-stack 真连 deepwiki 实测（list_tools 出 3 条 `mcp.deepwiki.*` 带 schema + ask_wiki_question 真实回答原文）；截图 `gateway-tab.png` 读图确认 v2.6.0 徽标。

## 6. Review

见 `06-review.md`：Final Decision = **APPROVED**（Critical 0；CodeReview 1 Major + 2 Minor 均当场修复并复验：零 IO 快照、卸载窗口消序、契约文档订正）。

## 7. Risk

L2 — 新增跨插件契约（纯增量）；回滚 = git 还原本批文件回 2.5.2/1.8.0（原子，不影响超时批代码）。

## 8. Problems Found

- 本批代码首版即被 CodeReview 抓出 Major（枚举热路径隐性读盘 + 脱敏浪费）——教训入 §12。
- 走查暴露存量 2 项（均登记既有 TODO 不顺手修）：①全量 dotnet test 并发互扰 flaky（同族追加登记 P3 台账）；②`backfill-feature-ids.ps1` 缺 BOM（P2 已有条目）。
- 走查暴露存量测试缺陷 1 项已修：e2e `.port-input` strict 双元素（v2.5.2 样式类复用引入，测试侧一行修）。
- 环境插曲：agent 自起 dev-stack 锁构建 DLL（-Stop 解决）；后台终端两次未真正拉起命令（改前台执行解决）——均非产品缺陷。

## 9. Process Evaluation

| 阶段 | 结论 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（4 处实施偏差已回写 03-plan「实施偏差记录」节，其中 2 处为架构必然） |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 问题类归档

本批为功能修订任务（D10 枚举侧推翻），**无新缺陷类**（problem-class-closure 不触发）。历史缺口「内置 AIAgent 无 mcp. 路由」（2026-10-19 问答回合实测发现，TODO P3）由本批闭环，勾账已完成（T009，2026-10-19：TODO 移除、裁决落 ADR-003）。

## 11. 重构提案

**R0 不重构**。理由：全名命名已收敛 `McpExternalToolNames` 单点真源（提供方拼名/两消费方拆名同源）；枚举合并收敛 ListToolsToolFunction 单点出口；适配层零逻辑薄壳。再抽象（如把合并策略做成可插拔 provider 列表）超出当前需求规模。悬空候选（登记不实施）：`GetStates()` 与 `ConnectedSessions()` 未来若第三处需要会话快照可合并为一个视图层。

## 12. 最终教训清单（不得为空）

1. **契约写了「零 IO」注释，实现却走读盘聚合**——注释即不变量，评审时须拿文档逐句对实现（CodeReview Major 实证：GetStates 每次 store.Load + 脱敏）；热路径优先选内存结构原生快照（`_sessions` 字典）。
2. **跨插件共享的命名/解析规则必须上移契约层**——「常量真源放提供方」在 ALC 隔离下会被第二个消费方击穿（本批 Spec 原定 Manager 持拆分、实施时被迫上移 Abstractions）；立项时先问「消费方能引用到这份实现吗」。
3. **ctx.Register 与停会话 Effect 的相对顺序决定卸载窗口语义**——Fiber 逆序回滚 ⇒ 想让能力先摘就让后注册；一行换位，写注释固化理由。
4. **e2e 样式类复用会埋 strict-mode 雷**——新元素复用既有类名（.port-input）时，同文件既有精确选择器断言即成定时炸弹；本文件级全量复跑是暴露手段（v2.5.2 批只跑了新用例所以未暴露）。
5. **测试断言期望值先手算再写**（keyword 过滤对宿主/外部两侧同时生效，想当然写 2 实际 1）——首跑红一次。
6. 悬空项（完成后勾）：~~门禁④发布~~ ✅ 随本地包 2.4.12/2.4.13 落更新源（并行会话 release-local -Sign，含 v2.5.2 超时批）、~~⑥034/D10 文档回写（T009）~~ ✅ 2026-10-19 完成（034 反转+F034-R04+验证记录 v2.6.0；ADR-003 落 `docs/07-decisions/` 并登记 README；TODO 勾 P3）、~~闸门2 批准 + git 提交~~ ✅ 2026-10-20 用户指令批准，AIAgent 侧 `91987f3` + McpCenter/契约/文档侧 `2eac444`（并行会话合批，链已核）。仅余：⑤运行实例只读复验（用户升级到 2.4.13+ 后验 v2.6.0 生效 + list_tools 实走一遍）——见 TODO。
