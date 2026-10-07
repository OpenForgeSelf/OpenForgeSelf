# Repository Understanding

> 阶段：Stage 0｜规范：`docs/04-standards/ai-native-engineering-workflow.md` §2
> 原则：所有条目来自真实仓库内容（命令输出/文件读取），禁止常识推测。

## 任务对象

`docs/04-standards/agent-workflow.md` —— AGENTS.md 的"详细版规则库"，被 `AGENTS.md §0/§2.3/§2.4/§2.5/§4/§5/§6/§7.5/§8/§10` 与三份技能反复指路。

## 体量（实测 2026-10-07）

| 指标 | 读数 | 依据 |
| --- | --- | --- |
| 行数 | **925** | `wc -l` |
| 字节 | **136,339（≈136KB）** | `wc -c` |
| 对比：全仓第二大 | `packaging-upgrade-backup.md` 262 行 / 63,982 B | 同上 |
| 对比：AGENTS.md 本体 | 412 行 / 48,732 B | 同上 |

## 内部结构（`grep -n '^# \|^## '` 实测行号）

- 头部说明 1–13（含"维护：新增规则→更新本文对应小节"）
- **Part A** 14–454：A1(16) A2(44) A3(71) A4(97) A5(176) **A6(217，136 行)** A7(353) A8(361，已弃用) A9(378) A10(444)
- **Part B** 455–905：B1(459) B2(496) B3(551) B4(625) B5(680) B6(743) B7(764) B8(774) B9(789) B10(838) B11(879) B12(891)
- **Part C** 906–925（变更记录）

## 入站引用面（Grep 全仓，排除自身）

| 来源 | 处数 | 形态 |
| --- | --- | --- |
| `AGENTS.md` | 23（其中 13 处带 §A*/§B*/Part 指向） | 「细则 → `agent-workflow.md` §A6」这类路由句 |
| `docs/README.md` | 7 | 目录说明 + 速查表 |
| `docs/04-standards/packaging-upgrade-backup.md` | 3（:6/:16/:236） | 「B4/B5/B10 同目录互引」 |
| `docs/04-standards/ai-native-engineering-workflow.md` | 2（:8/:156） | 分工声明 |
| `docs/ai/pilot/**`（历史工件） | 60+ | **含行号引用**：`agent-workflow.md:551`、`:607-610`、`L611/729/736` |
| `ForgeSelf.Web/e2e/global-setup.ts:141` | 1 | 代码注释指路 |

## 现有约束（影响方案选择）

1. **§A*/§B* 编号是地址**：AGENTS.md 与技能用它指路，历史工件也用它记账 ⇒ 拆分不得改编号。
2. **历史工件里有行号引用** ⇒ 任何重排都会让那些行号失效；历史是记录，不回改（AGENTS.md §7.5.3 ⑤ 逐回合留档原则）。
3. **共享文件写冲突**：本次会话已三次与并行会话在同一文件上叠 hunk（`AGENTS.md` §5.0 移位仍未提交、`ToolBridgeView.vue` 混改动）。单文件 136KB 的规则库 = 冲突面最大的一块。
4. **pre-commit 工件门禁**（`scripts/verify-pilot-artifacts.ps1`）要求被触碰的 pilot 目录 00–07 八件齐 ⇒ 本目录补齐后才可提交。
5. `scripts/hooks/pre-commit:24` 仍用 `powershell.exe`（已登记 TODO，非本批）。

## 测试方式（本任务可用的正规判据）

- 内容零丢失：按行区间切 → 重新拼接 → 与原文件 `git diff --no-index` 比对（应为空）
- 标题清单：原文件所有 `^# `/`^## ` 行在新文件中恰好各出现一次
- 指针可达：全仓 grep 不再存在指向已迁走内容的旧路径写法；旧路径仍有薄指路文件
- 工件门禁定向：`pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-07-agent-workflow-split`
