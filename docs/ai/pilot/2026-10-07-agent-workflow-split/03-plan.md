# Plan

> 决策 D1–D4 先列，执行步骤 E1–E7 后列。每步改完即可独立验证。

## 决策

- **D1 按"Part 内主题簇"切 5 刀，不逐节成页**
  背景：22 个小节若各自成页 = 22 次指路改写 + 目录噪声。
  理由：读入成本与"同一次任务要用的规则是否在一起"相关——A1–A5 是"干活主线"，A6–A10 是"文档与汇报"，B1–B6 是"日常工程规则与坑"，B7–B12 是"体系备忘"。
  准则：单文件 ≤ ~310 行；§编号不变。
- **D2 旧路径保留为薄指路文件（不删、不做 redirect hack）**
  背景：AGENTS.md 23 处 + 历史 pilot 60+ 处 + 一处代码注释引用它。
  理由：删了就是断链；逐个改历史工件等于篡改记录（违 §7.5.3 ⑤）。
  代价：多一个 5 行的"跳板文件"，可接受。
- **D3 拆分动作交给脚本按行区间机械切割 + 哈希对账，不用 Write 手抄**
  背景：136KB 中文内容若逐段重写，编码/漏行风险远高于收益；本仓有过 BOM/乱码烤进交付物的实案（§B6）。
  判据：`拼接(parts) == 原文件` 逐字节一致（去掉头部 1–13 与 BOM 后）。
  一次性操作脚本放 `.temp/`（gitignore 内），**用完即删、不入库**；结论沉淀为 05-evidence 的可复现判据命令。
- **D4 AGENTS.md 的 13 处指针改到新文件（不留在指路层）**
  理由：AGENTS.md 每回合必读，指向应直达；且 AGENTS.md 有并行会话未提交的 §5.0 hunk ⇒ 改完用 hunk 级暂存只提交我的部分（本会话已两次成功执行）。

## 执行步骤

| # | 动作 | 目标文件 | 立即验证 |
| --- | --- | --- | --- |
| E1 | 生成脚本：读原文件 → 按行区间切 6 份（5 内容 + README 由我写） | `.temp/tmp/split-agent-workflow.ps1` | 每份行数 == 计划（203/238/309/142/20） |
| E2 | 拼接对账：把 5 份按原顺序拼回，与 `git show HEAD:docs/04-standards/agent-workflow.md`（去 BOM、去 1–13 行）逐字节比较 | 同上 | 差异为空 / SHA256 相等 |
| E3 | 标题清单对账：原文件全部 `^# `/`^## ` 行在新 5 份中恰好各出现一次 | `docs/04-standards/agent-workflow/**` | 排序后 `diff` 为空（仅允许新增的每份 H1） |
| E4 | 写 `agent-workflow/README.md`（§编号地址表 + 维护/落位规则 + 历史行号声明）与旧路径薄指路 | `README.md` + `agent-workflow.md` | grep 旧文件不再含任何规则正文行 |
| E5 | 改指路：`AGENTS.md`(13) / `docs/README.md`(3+速查表) / `packaging-upgrade-backup.md`(3) / `ai-native-engineering-workflow.md`(2) | 上述文件 | 全仓 grep：带 §A*/§B* 的旧路径写法只剩"历史工件 + 薄指路 + README 说明"三类允许项 |
| E6 | 相对链接可解析抽查（同目录裸名、跨目录 `../../`） | 新目录 | 逐个链接 Test-Path 为真 |
| E7 | 工件门禁 + hunk 级暂存提交 | `docs/ai/pilot/2026-10-07-agent-workflow-split/` | `verify-pilot-artifacts.ps1 -TaskId …` PASS；pre-commit 实跑通过 |

## 回滚点
E2/E3 任一不过 ⇒ `git checkout -- docs/04-standards/agent-workflow.md` + 删除 `docs/04-standards/agent-workflow/`（本次会话自建目录，可清）；
E5 之后失败 ⇒ 同样整体回退（指针与文件一起退，不留半截）。提交前不 push。

## 影响面
- 文件：新增 6、覆写 1（旧路径变薄）、改指针 4 个既有文档（AGENTS.md 有并行会话未提交 hunk ⇒ 需 hunk 级暂存）。
- 接口/数据库/构建：无。
- 不受影响：历史 pilot 工件、`.agents/skills/**`（技能里引的是 AGENTS.md §编号与新目录名？→ E5 附带检查技能内是否直呼 agent-workflow.md 路径，若有同批改指）。
