# 08 铸己匣种子问题类（含证据出处）

> 来源 = 「问题类闭环」设计稿（2026-10-17 用户粘贴）§四，逐条取证后定档。**归档状态以 `docs/11-troubleshooting/problem-classes/README.md` 台账为准**，本表只记"候选 → 定档"的过程与出处，供追溯。

## 定档结果（2026-10-17 建档批）

| 候选 | 出处（设计稿引用） | 取证核实 | 定档 |
|---|---|---|---|
| 跨插件契约未进共享表 | `2026-10-10-aiagent-tools-features-fix/07` §10；architecture-design 铁律 4 | IContext.cs:7-25 语义 + WorkflowEnginePluginRegistrationTests 实存 | **PC-001** guarded（G1+G2 active，G4 提案） |
| 后端加鉴权前端裸 fetch | plugin-publish-verify 坑 8 | 全仓预扫 6 处 fetch 命中均有文件级 token 证据 | **PC-002** guarded（G3 测试+脚本） |
| 插件库未登记 PluginDbs | `ForgeSelf.Api/Plugins/README.md` §三实证段 | 存量 12 连接名 = PluginDbs 12 key（无新增红） | **PC-003** guarded（G2 枚举） |
| Agent 成功回报 ≠ 进程退出 | `2026-10-08-todo-chain-close/07` Problems Found | 修复散在 todo-ux-state 批次，通用性未读码确认 | **PC-004** open（**候选不定论**，none+TODO+expires 2026-12-31） |
| 单例 Initialize / provider 解析错 | troubleshooting 顶部缺陷记录 + 10-17 用户报错 | `UniversalGatewayListTodosScopeReproTests` 红灯锚定 | **PC-005** guarded-partial（G1 active，G2 planned；本案=10-08 同族逃逸） |
| PowerShell 5.1 编码/BOM | AGENTS §2.3 三实证代价 | `RepositoryScriptTests` 实存且在役 | **PC-006** 范例 guarded（G2） |
| 手敲命令静默陷阱 | AGENTS §2.3 + plugin-development 铁律 20 | `dev-stack.ps1` + probe-esbuild-write 实存 | **PC-007** 范例 structural（G4） |
| e2e 基建删除重试/killTree | `2026-10-08-todo-chain-close/07` | 已修且属一次性基建回归，不变量面窄 | **未建档**——设计稿即标"补 PC 与守卫"待评估；复发再建 |
| 文档残留弃用流程诱导跳过 pilot | AGENTS §0 第 3 条实证缺口 | 2026-09-30 已人工封堵（AGENTS 重写），specs/speckit 指引已清 | **未建档**——观察项，再现即建 G3 弃用词扫描 |

## 「AgentHub 踩坑在 todo 复发」的归属（用户未确认项）

设计稿末尾待确认问题保留原状：候选 = PC-001（契约没进共享表）/ PC-004（回报≠终态）/ PC-005（eager 解析）。**按方案要求两条候选均不定论**：PC-001 §5 与 PC-004 头部均记录此疑云；用户确认后再把对应 case 补档并升格。

## 新缺陷进入种子表的规则

- 同类扫查发现**跨插件/跨层通用**的不变量违反 → 建 PC（P2 归类）；
- 单点一次性环境杂音 → 只进 `index.md` 散例行，不建 PC（防台账注水）；
- 从散文教训（AGENTS 括号、坑表、07 报告"值得固化"句）迁移建档时：case §11 出处保留原散文位置，PC 正文替换散文成为新真源，原散文处改为一行指针（或下次触碰该文件时清理）。
