# Intent

> 阶段：Stage 1｜Task ID：2026-10-17-problem-class-closure｜日期：2026-10-17
> 来源：用户输入（晚·2026-10-17）「按以下方案去做，增加相关技能、文档」——方案全文即闸门1 材料，已获批准。

## Problem

项目的经验沉淀机制「不缺记录，缺把教训变成约束的那一步」：
1. 教训写在散文里（07 报告 §10/§11、AGENTS.md 括号战争故事），没有 TODO id、没有守卫、没有任何环节检查它是否被执行——如 2026-10-10「跨插件契约只进 IServiceCollection」的固化建议至今仍是 TODO.md 里一条 P3；
2. 教训分散在至少六处（AGENTS.md、agent-workflow Part B、技能 troubleshooting、pilot 07 报告、11-troubleshooting/index、TODO），按问题类没有索引，下次设计新功能的人没有「先查这一处」的入口；
3. 现行规则与目标直接冲突：AGENTS.md §0「发现的其他问题记入 TODO 不顺手解决」+ 规范 §1「禁止无关重构」按字面执行，Agent 发现同类问题也只能记账且完全合规；
4. 大技能（plugin-development 74KB 等）膨胀，继续塞括号式战争故事只会加速失效。

## Why

同类问题二次复发（用户明确反馈 AgentHub 踩的坑在 todo 又发生）证明：靠「提醒自己注意」的散文式沉淀不构成防线。仓库已有两个做对的范例——`RepositoryScriptTests`（仓库级守卫封堵 PowerShell 编码问题）与 `dev-stack.ps1`（脚本取代手敲命令的静默陷阱）——共同点是**用机器执行的约束解决**，新机制应复制这一模式。

## Expected Outcome

四层知识模型落地：Case（事件档案）→ Problem Class（问题类=核心资产）→ Guard（可执行守卫）→ Lens（设计前置检查）。
一次缺陷修复的交付物 = 修好的代码 + 红灯复现 + 同类扫查结果 + 一个会自己报警的守卫 + 一份可被下次设计查到的记录。二次复发按「逃逸」处理并强制做逃逸分析。

## Constraints

- 同一事实只放一处：Case 讲经过，Class 讲规则和扫描方法，Guard 台账讲谁在守，AGENTS.md 与技能只放指针（§2.5 唯一真源原则的延伸）。
- 不新开顶层文档编号目录（docs/README.md §六「默认不新增目录」），升级 11-troubleshooting 承载。
- `scripts/verify-pilot-artifacts.ps1` 属 L4 宪法层不得由 Agent 自行修改——门禁扩展走独立脚本。
- 触发面限定：缺陷、回归、返工、「又出现」、基建任务；不对每个轻量任务全面触发（防机制被绕过/形式化）。
- 不重构大技能本体（plugin-development 瘦身属方案阶段四，本批不碰）；不动业务代码。
- AGENTS.md 本身按方案约束「只加三行指针」，战争故事不再往正文塞。

## Success Criteria

1. `pwsh scripts/check-problem-classes.ps1` 全绿退出 0，且能对各 PC 的 signature 实际执行扫描（人为植入一个违规样例时能红——阳性对照）。
2. `dotnet test --filter ProblemClass` 新增守卫测试全绿；其中类级枚举测试（契约共享表 / PluginDbs / 裸 fetch）具备「阳性对照」断言（扫描集合非空，防空转）。
3. `ProblemClassRegistryTests` 通过：每个 PC ≥1 个守卫（路径真实存在）或 no-guard-yet + TODO id + 截止日；台账与文件双向一致；每个 case 引用的 PC 存在。
4. 种子问题类 ≥3 个完整 PC 档案 + 对应 case 档案 + 两处索引（problem-classes/README、cases/README）登记。
5. AGENTS.md §0/§2.4、规范 v1.2.0、07 模板、pre-commit 五处接入点全部落笔（diff 可指认），且规范版本号升 v1.2.0。
6. 本 pilot 工件链 00-07 齐全，过 pre-commit 门禁试跑。
