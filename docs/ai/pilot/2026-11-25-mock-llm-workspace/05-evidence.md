# 05 Evidence

## Verified

- MockLlmTests「Sample_规划提示返回真实计划」通过：submit_plan 的两步名称是「列出项目根目录」「根据列表写总结」；上一步已有工具结果时，新的用户消息仍能发出 complete_step。
- 同批 MockProvider 管道测试 3 通过。AgentFacade 的计划完成、执行记录、工作区关键字测试 3 通过。
- 浏览器：计划面板目标为「先列出项目根目录，再根据文件列表写总结」，没有「规划失败」。确认后会话已完成。展开 list_files 能看到 success、root 和 files 里的 .agents、.git、.forgeself。
- 记忆搜索返回 source=agent-workspace 的条目。设计系统项目列表里有草稿「助手确认」。

## 未通过后已修

规划器没收到 submit_plan 时回退成「准备 / 执行」。第二步把上一步的工具结果当成「本步已调用过工具」，吃掉了 complete_step。工具完成帧没有返回正文。这三处修复后复验通过。
