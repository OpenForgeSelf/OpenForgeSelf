---
status: draft
version: 0.1
---
# 功能文档需求清单与稳定需求 ID（规范 + 阶段 2 迁移方案）

> 本文定义"功能文档里给每条需求一个稳定 ID"的规范，以及把现有 39 篇 `docs/02-features/` 迁到该规范的分步方案。
> 目的：让"需求 → 设计 → 测试 → 证据"能按 ID 串成一条线，消掉"AC 每个任务从 AC1 重新编号、无法追溯"的结构性缺陷。
> 本文属 `docs/04-standards/`（非宪法层，Agent 可改）；其中涉及的闸门脚本改动在 `scripts/`（L4）。

---

## 1 现状核实（2026-10-06 实测）

| 事实 | 数据 |
|---|---|
| `docs/02-features/` 文档数 | **39 篇** |
| 带 front-matter 的 | **0 篇**（都用 `>` 引言块写元信息） |
| 有「需求清单」章节的 | **0 篇** |
| 已使用 `F<NNN>-R<NN>` 稳定 ID 的 | **0 处**（功能文档与 pilot 工件都没有） |
| 编号重复 | **3 组**：`028-`（project-workspace / wxt-dual-mode）、`031-`（ai-agent-execution-records / im-gateway）、`036-`（design-system / filetools-folder-ranking） |

结论：这是**从零迁移**，且现有编号方案不满足"稳定唯一键"——`F<功能号>-R<序号>` 里的"功能号"目前**不唯一**，必须先解决冲突，否则 ID 会撞车。

---

## 2 需求 ID 规范

### 2.1 键（feature_key）

功能文档在 front-matter 用 `feature_key` 声明稳定键，需求 ID = `<feature_key>-R<NN>`。

- 编号唯一时：`feature_key: F034`，需求 ID `F034-R01`。
- 编号冲突时：**两个都加字母后缀**（不偏袒任一方），如 `F028a` / `F028b`。
- `feature_key` 一经登记**永不改变**；文件名可改，键不改。

### 2.2 编号冲突映射表（提案，需你确认）

| 文件 | feature_key |
|---|---|
| `028-project-workspace.md` | **F028a** |
| `028-wxt-dual-mode.md` | **F028b** |
| `031-ai-agent-execution-records.md` | **F031a** |
| `031-im-gateway.md` | **F031b** |
| `036-design-system.md` | **F036a** |
| `036-filetools-folder-ranking.md` | **F036b** |

（字母按文件名字典序分配；`aliases` 记录原文件名，旧引用仍可解析。若你更希望"主文档保留裸号、后来者加后缀"，改这一张表即可。）

### 2.3 需求 ID 规则

1. **只增，不改号**：已发布的 `R<NN>` 永不重编号。
2. **删除不回收**：需求废弃 → 状态标 `废弃`，ID 保留（否则历史引用断裂）。
3. 新需求取**下一个空闲号**；同一功能内 `R01..R99`。
4. 一个需求 ID 对应**一条可验证的需求**，不是"一个页面"或"一个文件"。
5. 需求 ID 必须出现在三处：功能文档需求清单、pilot 工件、**测试名或注释**。

---

## 3 功能文档格式

### 3.1 front-matter（新增）

```yaml
---
feature_key: F034              # 稳定键；冲突时加字母后缀
feature_no: 034                # 展示用编号（可为空）
status: implemented            # draft / implemented / deprecated
last_updated: 2026-10-06
aliases: ["034-mcp-center"]    # 兼容旧引用：文件名/旧编号
---
```

### 3.2 需求清单（新增章节，紧邻「功能需求」）

```markdown
## 需求清单（稳定 ID，只增不改号）

| ID | 需求 | 类型 | 验收要点 | 状态 | 覆盖测试 | 来源 |
|----|------|------|----------|------|----------|------|
| F034-R01 | 转发单个 MCP 工具调用 | FR | 调用返回工具结果，含错误码 | 已实现 | `ForgeSelf.Web/e2e/mcp-tools.spec.ts` | `docs/ai/pilot/2026-10-05-mcp-center-endpoint-url` |
| F034-R02 | 管理面鉴权 | BR | 无 token 返回 401 | 已实现 | `…/e2e/mcp-tools.spec.ts` | 同上 |
```

- 「验收要点」必须是**可执行判定**（命令输出或断言），否则该行不合格。
- 「状态」∈ `待核对 / 已实现 / 废弃`。迁移期统一先填 `待核对`。

### 3.3 需求变更记录（新增章节，文档末尾）

| ID | 变更 | 日期 | 来源（pilot） |
|----|------|------|----------------|

---

## 4 追溯链（ID 怎么串起来）

```text
docs/02-features/<NNN>-*.md        ① 登记 F<key>-R<NN>（唯一事实源）
        ↑ 回写（闸门3，必须同一提交）
docs/ai/pilot/<task>/04-task.md    ② 头部 requirements: [F<key>-R01, …]
docs/ai/pilot/<task>/02-spec.md    ③ 每条 AC 标注它实现哪个 R
        ↓ 实现
测试名 / 注释                       ④ 带 F<key>-R<NN>
docs/ai/pilot/<task>/05-evidence.md ⑤ AC → 证据，逐条
```

闭环：R 是锚点，pilot 是增量，测试是证明。**R 只改功能文档，pilot 只引用不新造**。

---

## 5 工具与闸门

| 组件 | 状态 | 作用 |
|---|---|---|
| `docs/18-templates/feature.md` | ✅ 已加 front-matter + 需求清单 + 变更记录 | 新文档出生即合规 |
| `scripts/backfill-feature-ids.ps1` | ✅ 新增 | 半自动补 front-matter 与需求清单占位（只增不改、幂等、默认 dry-run） |
| `scripts/verify-pilot-gate.ps1` | ✅ 已加回写检查 | 04 头部 `feature` 指向的功能文档**必须**在同一 diff 内被修改；`requirements` 里的 ID 必须已在功能文档登记 |
| `scripts/verify-pilot-artifacts.ps1` | 已有 AC 覆盖检查 | 02 的 ACn 必须在 05 有对应证据 |
| `classify-risk.ps1` | ✅ 已修 | 补上**未跟踪新文件**不计入改动的缺口（否则新文件不判级） |

---

## 6 迁移步骤（阶段 2）

### 2a 规范落地（已完成于本轮）
模板 + backfill 工具 + 闸门检查 + 规范文档。

### 2b 回填 39 篇功能文档
1. `pwsh scripts/backfill-feature-ids.ps1`（dry-run，先看 feature_key 分配与冲突字母）。
2. 确认映射表后 `-Apply`：注入 front-matter + 需求清单占位。
3. **逐篇填真实需求**（不能留占位）：从现有「功能需求 / 目标 / 使用指南」提炼 R，每行给可执行验收要点。
   - 建议按**近期活跃**优先：034 / 036 / 038 / 031 / 032 / 028，其它按需。
   - 每篇填完把该篇 `status` 从 `draft` 改到位。
4. **回填完成判据（逐篇）**：需求清单无「待填」；每条 R 有可执行验收要点；至少一条 R 关联到现存测试文件或在「需求变更记录」注明"暂无测试"。

### 2c 接通闸门
5. 之后每个 pilot 的 04 头部写 `feature: <NNN 或 key>` 与 `requirements: [...]`。
6. 闸门在提交时机械校验：功能文档被同提交修改 + R 已登记。
7. **L1 抽检**：前 4 周全部抽检，之后 20%（risk-policy §5）。
8. **判级校准**：拿最近 5 个 pilot 回放 `classify-risk.ps1`，与你直觉对齐后再把 `risk-policy` 转 `approved`。

### 验收
- 39 篇中**已认领的**功能文档：需求清单无占位、ID 唯一、无撞号。
- 抽 3 篇：从 R ID 能一路查到测试与 05 证据。
- 一次故意"改了代码但没回写功能文档"的提交被闸门拒绝。

---

## 7 未决 / 需你拍板

1. **冲突映射表**（§2.2）是否认可"双方都加字母后缀"？
2. 回填**优先级**：是否按"近期活跃优先"逐篇推进，而非一次性 39 篇？
3. `status` 取值是否就用 `draft / implemented / deprecated` 三档？
4. 是否要求**每个 R 必须有测试**？我倾向"实现类 R 必须有；文档/文案类 R 可免"，但这会削弱可验证性——请你定。
5. 现存 39 篇里**已有 3 组撞号**，是否顺手把文件名也改成不撞号（会动 3 个文件名，影响历史引用）？我默认**只加 feature_key、不改文件名**。
