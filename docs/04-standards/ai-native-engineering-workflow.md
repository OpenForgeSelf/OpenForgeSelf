# AI-Native Engineering 闭环开发流程规范

> 版本：v1.1.0（2026-09-27）｜状态：**强制**——本项目所有开发类任务必须遵守
> 来源：用户群内指令（conv_01m3gqx2bgeqbpwyqk9b3qj22z · seq10 制定方案；seq14 明确本规范**独立自洽，不与任何其他流程体系做映射**，开发任务完全按本规范执行）。
> 配套模板：`docs/18-templates/ai-pilot/`（00-repository-understanding ~ 06-review 七份）
> 产物目录约定：`docs/ai/pilot/YYYY-MM-DD-<task-id>/`（每任务一个目录，**目录名前端加日期前缀**，如 `2026-09-30-e2e-shared-infra-dynamic-port`；**从 2026-09-30 起执行，已有旧目录不回溯重命名**；首切实验可直接用 `docs/ai/pilot/` 平铺）
> 群 SOP 引用：SOP 技能 `ai-native-engineering-loop`（流程定义与本规范一致，闸门定义见 §1.1，不依赖任何其他 SOP）
> **规范优先级**：开发任务的流程以本规范为唯一依据；项目内任何其他文档（含 AGENTS.md §1~§10、docs/04-standards/agent-workflow.md、其他流程（含已弃用的 specs/speckit）、其他 SOP）与本规范在流程上冲突时，一律以本规范为准。代码级工程规范（技术栈约定、测试命令、格式规范等）不属流程冲突，继续遵循仓库既有文档。

---

## 1.1 三道闸门（本规范自含定义）

| 闸门 | 定义 | 通过条件 |
| --- | --- | --- |
| 闸门1（规格确认） | S1~S4 工件（Intent/Spec/Plan/Task）完成后，交用户确认 | 用户明确批准后方可进入 Implement；未批回炉修订 |
| 闸门2（成果验收） | S7 Evidence + S8 Review 齐备后，按 §5 格式向用户交付最终汇报 | 用户验收通过前不得提交代码 |
| 闸门3（提交归档） | 闸门2 通过后执行 git 提交 + 文档同步 + 经验回写 | 仅由获指定角色执行 |

任何授权表述（如「你自己决策」「不用上报」）只豁免中间过程汇报频率，**不豁免三道闸门**；仅当用户明文授权免闸门时方可裁剪，且裁剪记录写入任务工件。

---

## 0. 核心主张

本规范验证并固化的不是「能不能把代码写出来」，而是：

> **一个 Coding Agent 能不能围绕结构化工程工件（Engineering Artifacts）完成一个可验证、可审查的小型变更。**

每个开发任务都是一次闭环运转，任何阶段不得跳过、不得合并伪造：

```text
Repository Understanding
        ↓
Intent
        ↓
Spec
        ↓
Plan
        ↓
Task
        ↓
Implement
        ↓
Test
        ↓
Evidence
        ↓
Review
```

**理解事实优先**：一切结论必须基于真实仓库内容，不允许凭常识推测技术栈、接口、模块。

---

## 1. 硬性约束（每个任务适用，违反即流程违规）

1. 不修改生产环境。
2. 不修改数据库结构（涉及迁移即属高风险，须升级审批）。
3. 不修改鉴权、权限、支付、安全核心逻辑（除非任务本身经闸门1 明确授权）。
4. 不新增大规模依赖。
5. 不进行无关重构。
6. 不修改与本任务无关的文件。
7. 不为「展示 Agent 能力」扩大任务范围。
8. 最终必须能够运行实际测试或构建命令验证。
9. 所有结论必须基于真实仓库内容，不允许猜测。
10. 测试、构建或验证失败时，不允许伪造成功结果。

---

## 2. 九阶段定义

### Stage 0：Repository Understanding（理解仓库）
- **动手写代码之前必须完成**。检查：项目根目录、README、解决方案文件、项目文件、主要源码目录、测试项目、构建脚本、现有文档。
- 识别：①项目用途 ②主要技术栈 ③Backend/Frontend 结构 ④测试方式 ⑤构建方式 ⑥主要目录职责 ⑦代码组织方式 ⑧已有工程规范。
- ⛔ 禁止根据常识推测技术栈，必须从仓库实际内容确认。
- OpenForgeSelf 速查基线（仍需按当次仓库现状复核）：Vue 3.5 + Vite 前端（`ForgeSelf.Web/`，pnpm，`pnpm run check`/`pnpm run test`）；.NET 10 + SQLite + NewLife.XCode 后端（`ForgeSelf.Api/`，`dotnet build` + `dotnet test`）；插件架构（`Plugins/` + `plugin.json`）；测试体系见 AGENTS.md §5.3。
- **产物**：`00-repository-understanding.md`（含候选低风险任务及选择理由）。

### Stage 1：Intent（意图）
- **产物**：`01-intent.md`（模板 `docs/18-templates/ai-pilot/01-intent.tpl.md`）。
- 只回答「为什么做 / 做什么 / 做到什么程度」：Problem、Why、Expected Outcome、Constraints、Success Criteria。
- ⛔ Intent 中不得提前锁定具体代码实现。

### Stage 2：Spec（规格）
- **产物**：`02-spec.md`。
- 从真实 Repository 与 Intent 推导，至少含：Functional Requirements、Input、Output、Business Rules、Boundary Conditions、Error Handling、Compatibility、Non-functional Requirements、Acceptance Criteria。
- ⛔ 不得发明不存在的接口、类、模块；不确定点必须显式记录为 `Unknown`，不得自行假定。

### Stage 3：Plan（计划）
- **产物**：`03-plan.md`。
- 必须具体到真实文件（`file:` + `reason:`），含 Implementation Steps、Test Plan、Verification（Build/Unit/Integration/E2E/Other）。
- ⛔ 不允许只写「修改 Service、增加测试」这类模糊描述。

### Stage 4：Task（Agent 工作单元）
- **产物**：`04-task.md`。
- 把任务变成 Agent 可直接执行的工作单元：Task ID、Objective、Scope（Allowed/Forbidden）、Acceptance Criteria（checkbox 清单）、Expected Files、Verification Commands。

### Stage 5：Implement（实现）
- 走到此阶段才允许改代码。要求：
  1. 严格按 Plan 执行；2. 不扩大 Scope；3. 不主动重构无关代码；4. 不改无必要文件；5. 优先复用现有项目模式；6. 遵循项目已有代码规范（AGENTS.md §4.2 + docs/04-standards/）。
  7. **发现 Plan 与仓库实际不符 → 先记录偏差、修正 Plan，不得直接绕过。**
  8. 项目使用特定 ORM（NewLife.XCode）、请求封装、组件体系、测试体系时，必须用项目现有方式，禁止自行引入另一套实现。

### Stage 6：Test（真实验证）
- 至少执行 **Build + 相关 Unit Test**；本仓库当前可用的验证入口：前端 `pnpm run check` + `pnpm run test`（`ForgeSelf.Web/`）、后端 `dotnet build` + `dotnet test`（`ForgeSelf.Api/` + `ForgeSelf.Api.Tests/`）、插件前端 `cd Plugins/<X>/web && pnpm run build`、插件层 e2e（`e2e/plugins/<id>`，Playwright）。任务涉及哪层就跑哪层，全部实际存在者不得省略。
- 记录**真实结果**（命令 + PASS/FAIL + 计数）；失败时记录原因、已尝试步骤、最终状态 `BLOCKED`。
- ⛔ 禁止写「应该可以 / 理论上通过 / 看起来没问题」。

### Stage 7：Evidence（证据）
- **产物**：`05-evidence.md`。
- 只记录**实际发生的事情**：Changed Files、各验证项的 Command + Result、截图路径（涉 UI 时）、Known Limitations、Unresolved Issues。
- ⛔ 不得根据代码推测测试结果。验证结果须标来源等级：**Verified**（亲自跑过，拿到真实输出）/ **Inferred**（凭代码推断）/ **Unknown**（未验证）——三者禁止混用。

### Stage 8：Review（审查）
- **产物**：`06-review.md`。Reviewer 视角（可由测试审查岗或另一 Agent 承担）重查八问：
  1. 实现是否真正满足 Intent？2. 是否符合 Spec？3. 是否超出 Scope？4. 是否改了不该改的文件？5. 测试是否覆盖 Acceptance Criteria？6. 是否存在明显回归风险？7. 是否存在架构不一致？8. Evidence 是否足以证明任务完成？
- 输出：Requirement/Scope/Test/Architecture Check（PASS/FAIL）、Risk（L0~L4）、Findings（Critical/Major/Minor）、Final Decision（APPROVED / CHANGES_REQUIRED / BLOCKED）。
- CHANGES_REQUIRED → 回到对应阶段重跑闭环，不得带病交付。

---

## 3. 独立性与优先级（用户指令 seq14：不与既有体系映射）

1. 本规范是开发任务**唯一的流程依据**，九阶段 + 三道闸门（§1.1）自含闭环，不引用、不映射、不复用项目内任何其他流程体系的阶段定义或闸门语义。
2. 与其他文档的关系只有两类：
   - **流程冲突**（谁说了算）：一律以本规范为准（见头部「规范优先级」）。
   - **非流程事实**（技术栈、目录、命令、格式规范等工程事实）：以仓库真实内容与其对应工程文档为准——这是 Stage 0「基于真实仓库」的要求，不构成流程映射。
3. 群 SOP `ai-native-engineering-loop` 是本规范在群协作中的**镜像**（内容一致性由本规范 §7 持续改进条款保证），流程裁定权仍在本规范。

## 4. 任务级别裁剪（由任务协调人裁定并记录）

| 级别 | 适用 | 工件要求 |
| --- | --- | --- |
| 全量 | 新功能、插件任务、公共接口/契约变更 | 00~06 七件齐备（`docs/ai/pilot/<task-id>/`） |
| 轻量 | ≤3 文件的缺陷修复、边界测试补充 | Intent/Spec/Plan/Task 可合并为单文件 `mini-task.md`（含五要素），Evidence 与 Review 仍**必须单独产出** |
| 纯问答/查状态 | 不占流程 | 无工件 |

裁剪不等于跳过：任何级别都必须有 Test→Evidence→Review 的真实验证与审查记录。

## 5. 汇报格式（任务结束强制）

按模板 `docs/18-templates/ai-pilot/07-final-report.tpl.md` 出报告（含 Repository Understanding、Selected Task、Changed Files、Validation、Evidence、Review、Risk、Problems Found、Process Evaluation、最重要的问题、下一步建议 十一节）。载体：**markdown 正文直接发在群消息里**；仅在用户明确要求或内容超单消息上限时才用附件，且附件外必须仍有正文摘要。

## 6. 禁止事项汇总

1. ⛔ 跳过中间过程直接写代码。
2. ⛔ 发明不存在的接口/类/模块；把猜测当事实写入工件。
3. ⛔ 伪造、美化、省略验证结果；用「应该可以」类模糊表述收尾。
4. ⛔ 借实验/任务之名扩大 Scope、做无关重构、引入大依赖。
5. ⛔ 未经闸门2/3 授权执行 Merge / Deploy / 修改生产 / 自动关闭 PR。
6. ⛔ Evidence 与 Review 缺件交付（即使轻量任务也不豁免）。

## 7. 持续改进

每次闭环结束，Process Evaluation（模板 §9）指出的「卡点/信息丢失/Agent 猜测」若有规律性 → 沉淀到本规范或 agent-workflow.md 对应小节；流程变更须升格本规范版本号并同步群 SOP。
