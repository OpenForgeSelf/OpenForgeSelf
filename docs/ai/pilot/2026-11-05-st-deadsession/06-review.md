# 06-review：SmartTerminal 死会话 404（task-18）

## 八问
1. **需求是否成立**：成立。51888 升级后实测复现（宿主重启后 S-R 恢复的陈旧会话 id 持续轮询 404 + 反复 toast）。
2. **根因是否找对**：是。三层：pollOutput 条件吞错（仅 reset 提示）+ loadCommands 无 catch（裸抛经 onMounted 反复 toast）+ S-R 恢复把不存在 id 留在活跃轮询面。修复后 e2e 红→绿闭环证实因果。
3. **修复是否最小**：是。单文件 11 处编辑 + 版本 bump + 1 e2e 用例；无后端改动、无新依赖、无色值新增（design 门禁零新增佐证）。
4. **影响面是否扫过**：是。同类扫查覆盖全部轮询/加载点（output/commands/getSession 404→登记终止；approvals 无 404 路径补防御 catch）；closeTab/runCommand/输入/重跑均按死态联动。
5. **测试是否充分**：快档全绿（build/check/design/e2e 2/2/vitest 1151 绿 3 红既有/后端过滤集 25/25）；e2e 含零 mock 终态断言（3s 零新增 404）+ 截图读图；红灯复现已做（G2 双验证）。
6. **有无回归风险**：低。死登记仅影响已 404 的会话；demo/fixture 路径不受影响；关闭标签即清登记；e2e 主链用例同批绿。
7. **是否引入新债**：无。winds up 更少错误请求（404 停打）；PC-024 台账已立供后续轮询点自查。
8. **e2e 基建发现是否外溢**：是——fresh worktree 首启 AIAgent 初始化挂起 → init-token 空 → 全部 e2e 假红于「ForgeSetting.config 中未找到 ApiToken」，重跑即恢复；已报 qa（task-13/14 加固域）并记 TODO。

## Final Decision
**APPROVED**（lead 自审；证据齐、红绿闭环、设计门禁零新增、无新增债）。
