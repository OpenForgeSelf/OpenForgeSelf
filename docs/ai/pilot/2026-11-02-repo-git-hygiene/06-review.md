# 06 Review — 仓库 git 卫生与待提交批次收口

> 八问 + Final Decision。审查对象：gitignore 修正、3 文件停止跟踪、备份归档、9 个 pilot 目录门禁预演与补件。

## 八问

1. **测试都过了吗？** 本任务零代码改动；机器门禁全绿：9 目录工件预演 7 补后 PASS / 2 按规留置；check-problem-classes 19/19 OK；gitignore 三态实测符合预期。
2. **有没有跳过的验证？** 后端测试未复跑——零代码改动不触发 §5.6 任何档位的代码门禁；各代码批次验证沿用其批次 05（已逐批登记，来源等级见各目录）。
3. **真机验证做了吗？** 不适用（无 UI/运行时行为变更）；git 层验证为命令实测。
4. **失败模式想清楚了吗？** gitignore 负向豁免若过宽会把本应忽略的目录暴露——已核对：仅 `!.agents/`、`!.github/` 两条，`.github/agents|prompts` 仍按后续行忽略，`.forgeself/` 仍忽略（实测 exit=0）。
5. **有没有夹带？** 无——补件仅工件文档与声明行；两份「闸门1 待批」计划件明确不提交。
6. **回滚方案？** `git revert` 本 chore 提交；TODO.md.bak 在 `.trash/2026-11-02-git-hygiene/` 可还原。
7. **文档同步了吗？** TODO/日记/本 pilot 目录齐；.gitignore 规则变更带注释说明动机与日期。
8. **下一个最该做什么？** 用户验收提交序列；两份待批计划件（responses-incomplete-status、aiagent-ui-design-brief）走各自闸门1。

## Final Decision

**APPROVED** — 仓库元数据变更有实测依据、可回滚、未触碰业务行为；工件补件全部为已发生证据的登记与交叉引用，零虚构。
