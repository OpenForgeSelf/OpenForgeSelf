---
name: problem-class-closure
description: 铸己匣「问题类闭环」流程。任何缺陷修复、回归、返工、踩坑暴露、用户说"又出现了/之前类似的"、Review 发现 Major 以上时使用。把单点修复升级为：复现测试 → 同类扫查 → 类级守卫 → 小范围重构提案 → 问题类归档（docs/11-troubleshooting）→ 回流 Spec/模板/技能。二次复发按"逃逸"处理并强制逃逸分析。
---

# 问题类闭环（problem-class-closure）

## 一句话原则

修的是「类」，不是「例」。一次修复的交付物 = 修好的代码 + 红灯复现 + 同类扫查结果 + 一个会自己报警的守卫 + 一份可被下次设计查到的记录。

## 何时用 / 不用

**用**：缺陷修复、回归、e2e/发布链路返工、用户说"又/还是/上次类似"、Review 发现 Major 以上、任何 07 报告想写"同类隐患"、基建/流程缺陷暴露。
**不用**：纯问答（走 AGENTS §2.5）；但问答中发现真实缺陷 → 记 TODO 并转本技能。

## 铁律（10 条）

1. **先红后绿**。复现不了不修；红灯输出入 05-evidence（Verified）。
2. **修之前先问"同类在哪"**。同类扫查是本任务的一部分，不是顺手。
3. **同根因的命中项属于本任务范围**（不算"无关重构"、不算扩范围，AGENTS §0 第 2 条例外）；异根因才进 TODO。
4. **二次复发 = 流程缺陷，不是代码缺陷**。必须做逃逸分析，守卫 ≥G2。
5. **可执行守卫 > 文档**。文档只承载"为什么/怎么扫/怎么修"，规则正文单点真源不复述（本仓铁律）。
6. 每个问题类必须有：一句话不变量 + 特征签名 + ≥1 个守卫（或 `path: none` + todo 逐字可检索 + expires 截止日）。
7. **主动提出重构**。07 必含"重构提案"节，无也写"无+理由"，不等用户问。
8. **教训不得悬空**：07 中任何"下一步建议/遗留/Minor"必须落成 TODO id 或 PC id（悬空教训清单必须为空）。
9. 证据分级 Verified/Inferred/Unknown，不得混用（规范 §6.2）。
10. 本技能是 §11 AI-Native 工件链里**缺陷类任务的增强，不是旁路**——不跳 §0 预飞、不跳闸门1。

## 流程骨架（细则见 references/，按阶段取用，不要通读）

| 阶段 | 做什么 | 产出 | 细则 |
|---|---|---|---|
| P0 分诊 | 建 TODO/日志；按 tags 查 `docs/11-troubleshooting/problem-classes/README.md`；命中=复发，先做 P7 逃逸分析 | 命中/未命中结论 | 01 |
| P1 复现 | 最低成本层写红灯测试（dotnet test / vitest / e2e 分层） | 红灯日志 | 03 |
| P2 归类 | 陈述"违反了什么不变量"；5 Whys + 第 6 问（为什么流程没让我写对） | 不变量 + 特征签名 | 01 |
| P3 扫查 | 按签名扫全仓（其他插件两棵树/宿主/前端/脚本/文档/e2e 基建） | 命中表：同根因/异根因/无命中 | 02 |
| P4 类级修复 | canonical fix 应用于全部同根因命中；类级测试先红后绿；定守卫等级；重构提案 | 修复 + 守卫 + 提案 | 03/04 |
| P5 重跑 | 按 agent-workflow §A4 决策表重跑对应整档；P1 红灯转绿；插件任务走 plugin-development 六步 | 绿灯日志 | AGENTS §0 |
| P6 归档 | 写 case + 建/更新 PC（模板 `docs/18-templates/postmortem/`）；更新两处 README 索引 + index.md 速查行 | case + PC | 07 |
| P7 上浮 | 逃逸分析 → 基建动作（模板/Spec 节/hook/lint/技能指针） | 基建变更清单 | 05/06 |

## 决策逻辑

**守卫等级**（取满足条件的最高项）：
- **G1** 实例回归测试：首次发生、影响面单点
- **G2** 类级枚举测试（反射/源码遍历所有插件/端点/脚本断言不变量）：结构性问题（契约/注册/鉴权/路径/编码）首次即用；任何二次复发强制
- **G3** 提交期/CI 守卫（`scripts/check-problem-classes.ps1` 签名、analyzer、hook）：G2 覆盖不到（跨语言/文档/脚本）或代价高；存量入 allowlist（必带 expires，过期即红）
- **G4** 结构消除（重构使错误不可表达）：三次复发，或 G2/G3 维护成本 > 重构成本

**重构分档**：
- **R0** 不重构；**R1** 局部（≤3 文件、同模块、不动公共契约）任务内做、07 披露即可
- **R2** 跨模块或动公共契约：六段方案（≤5 条改动）在闸门1 批准
- **R3** 架构级：升级 `architecture-design` + ADR，拆独立 pilot，本任务只做 G3 止血

**扫查超限**：命中 >10 处，或触及硬约束（DB 结构/鉴权核心）→ 修当前实例 + 建 G3 守卫（baseline 模式）+ 余项进 TODO（带 PC id）。

## 出口清单（缺一不得回复"完成"）

- [ ] 红灯→绿灯日志（05-evidence，Verified）
- [ ] 同类扫查命中表（case §4）
- [ ] 守卫已落地，或 `path: none` + TODO 逐字可检索 + expires
- [ ] case + PC 已写，`problem-classes/README.md`、`cases/README.md`、`index.md` 三处索引已更新
- [ ] 07 含"问题类归档 / 重构提案 / 悬空教训（必须为空）"三节
- [ ] 逃逸分析已做（二次复发必做），基建动作已落地或入 TODO

## 真源与指针（只放指针，不复述）

- 台账与索引：`docs/11-troubleshooting/problem-classes/README.md`（tags 检索表）
- front-matter schema 单一来源：`docs/18-templates/postmortem/problem-class.tpl.md`；case 模板：`case.tpl.md`
- 机器校验：`ForgeSelf.Api.Tests/ProblemClassRegistryTests.cs`（守卫的守卫）+ `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（类级枚举）+ `scripts/check-problem-classes.ps1`（签名扫描，pre-commit 挂接）
- 脚手架：`pwsh -NoProfile -File .agents/skills/problem-class-closure/scripts/new-problem-case.ps1`（建 case+PC 骨架并更新索引）
- 种子问题类与出处：`references/08-project-seeds.md`

## references 索引

| 文件 | 何时读 |
|---|---|
| `references/01-triage-and-root-cause.md` | P0/P2：分诊、问题类陈述、5 Whys+第6问写法 |
| `references/02-sweep-playbook.md` | P3：签名写法、扫描范围矩阵、命中表格式 |
| `references/03-repro-first-testing.md` | P1/P4：复现分层、红绿重跑、枚举式测试、守卫的守卫 |
| `references/04-refactor-proposal.md` | P4：R0-R3 分档细则、六段方案、与"无关重构"的边界 |
| `references/05-escape-analysis.md` | P7：四种失败模式归因表 → 基建动作 |
| `references/06-infra-upgrade.md` | P7：何时升级模板/Spec 节/hook/lint/技能铁律 |
| `references/07-recording-spec.md` | P6：目录、命名、front-matter 填写、登记与分工 |
| `references/08-project-seeds.md` | P0 检索辅助：铸己匣种子问题类（含证据出处） |
