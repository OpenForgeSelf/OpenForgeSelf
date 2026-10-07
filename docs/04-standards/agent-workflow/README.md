# Agent 工作流与工程规则规范（AGENTS.md 详细版 + 项目不变规则归档）

> 状态：已实施（2026-09-24 分层落地：AGENTS.md 瘦身为每次必守版，本规则库承接详细版；原 `.forgeself/memory/MEMORY.md` 规则归档于此）
> 结构变更：**2026-10-07 由单文件（925 行 / 136KB，全仓最大）按主题簇拆分为本目录 5 个分册**；
> §A1–§A10、§B1–§B12 的**编号与规则文字一字未改**（拆分件正文拼接与原文件逐字符一致，SHA256 `B6F363BB34F2E7B97F4A8A81AA03DF542F7CC08EE3284F0D33B06A036421737A`）。
> 旧路径 `docs/04-standards/agent-workflow.md` 保留为**薄指路文件**，不承载规则正文。
>
> **维护规则（本条唯一承载处）**：新增/修改项目规则 → 写进下表对应的**分册小节**；仅当规则升级为「每次必守」时同步回写 `AGENTS.md`。
> `MEMORY.md` 不再承载不变规则，只存会话级索引与未沉淀的临时规律。**同一事实只允许一处承载**，别处写指路。

## §编号 → 分册 地址表

| 编号 | 小节 | 分册 |
| --- | --- | --- |
| §A1 | 技能体系（AGENTS.md §2.4 详细：职责边界、新建插件硬顺序、登记规则） | [`a-workflow-core.md`](a-workflow-core.md) |
| §A2 | 规划（§3 详细） | [`a-workflow-core.md`](a-workflow-core.md) |
| §A3 | 编码规范（§4 详细） | [`a-workflow-core.md`](a-workflow-core.md) |
| §A4 | 验证：**需求 → 该跑什么的完整决策表**、正规工具入口 | [`a-workflow-core.md`](a-workflow-core.md) |
| §A5 | 迭代控制（回滚策略等） | [`a-workflow-core.md`](a-workflow-core.md) |
| §A6 | 文档工作流 + **功能确认问答的唯一真源优先流程（AGENTS.md §2.5 细则）** + 文档反向同步 | [`a-workflow-docs-report.md`](a-workflow-docs-report.md) |
| §A7 | 设计稿工作流（§8） | [`a-workflow-docs-report.md`](a-workflow-docs-report.md) |
| §A8 | speckit SDD（**已弃用**，由 AGENTS.md §11 替代） | [`a-workflow-docs-report.md`](a-workflow-docs-report.md) |
| §A9 | 完成判定与汇报（§10 详细：五步自检、汇报模板） | [`a-workflow-docs-report.md`](a-workflow-docs-report.md) |
| §A10 | 群协作 SOP 对齐（plugin-team-sop） | [`a-workflow-docs-report.md`](a-workflow-docs-report.md) |
| §B1 | 全局约定 | [`b-engineering-daily.md`](b-engineering-daily.md) |
| §B2 | 验证与测试铁律（e2e / Playwright / 工具 / 本机环境前置） | [`b-engineering-daily.md`](b-engineering-daily.md) |
| §B3 | 前端工程规则 | [`b-engineering-daily.md`](b-engineering-daily.md) |
| §B4 | 后端工程规则 | [`b-engineering-daily.md`](b-engineering-daily.md) |
| §B5 | 插件体系与发布 | [`b-engineering-daily.md`](b-engineering-daily.md) |
| §B6 | PowerShell 工程坑（本项目高频；pwsh 铁律、编码、签名、**Git-Bash `*>` 展开通配符**） | [`b-engineering-daily.md`](b-engineering-daily.md) |
| §B7 | 环境速查 | [`b-engineering-platform.md`](b-engineering-platform.md) |
| §B8 | 架构要点 | [`b-engineering-platform.md`](b-engineering-platform.md) |
| §B9 | 测试覆盖与功能规格 | [`b-engineering-platform.md`](b-engineering-platform.md) |
| §B10 | CI 自动发布（tag → GitHub Actions） | [`b-engineering-platform.md`](b-engineering-platform.md) |
| §B11 | Qoder CN Agent SDK 接入（spec 037） | [`b-engineering-platform.md`](b-engineering-platform.md) |
| §B12 | dsh 架构对齐（040–042） | [`b-engineering-platform.md`](b-engineering-platform.md) |
| Part C | 变更记录（规则库自身的演进台账） | [`c-changelog.md`](c-changelog.md) |

## 该读哪一份（按场景）

| 场景 | 读 |
| --- | --- |
| 干活主线：要不要跑 e2e、跑哪一档、编码规范、失败怎么回滚 | [`a-workflow-core.md`](a-workflow-core.md) |
| 文档与收尾：文档工作流、**回答功能确认问题的真源流程**、汇报格式 | [`a-workflow-docs-report.md`](a-workflow-docs-report.md) |
| 写代码踩坑：前端/后端/插件/PowerShell/测试环境 | [`b-engineering-daily.md`](b-engineering-daily.md) |
| 体系与外围：端口环境、架构、CI 发布、SDK、dsh | [`b-engineering-platform.md`](b-engineering-platform.md) |

## 历史引用说明

`docs/ai/pilot/**` 的历史工件里存在 `agent-workflow.md:551`、`:607-610`、`L611/729/736` 这类**行号**引用。
拆分后这些行号不再对应同一位置，**历史工件不回改**（逐回合留档原则，AGENTS.md §7.5.3 ⑤）：
按同句中的 §编号解读即可，编号→分册见上表。要复核拆分本身，原文件仍在 git 里（拆分前的版本可用 `git show` 取回后与本目录正文逐字符对账）。
