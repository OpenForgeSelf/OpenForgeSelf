# Review

需求与截图在 [`docs/02-features/102-human-attention.md`](../../../02-features/102-human-attention.md)。本页不把缺截图的项写成通过。

## 八问

1. 需求是否被实现：AC22 已有未归属进程和系统监控高亮截图。AC16、AC17、AC18 换成了本轮新建且仍在跑的截图。AC11、AC12、AC19 仍没有对应行。本轮新建的外部任务没有停在执行中。计划 Run 没有可打开的记录。
2. 是否改了不该改的：IM 进展未改。没有新建插件。没有第二套审批库。页面没有亮暗色切换。
3. 同一执行是否会重复：分类单测覆盖待办、终端待批和已写下结果的待批不再计入数字。扫描到的进程与本面板拉起的同一 pid 只留一条。
4. 空数据风险：读取中不先画 0。空页有单独文案。扫描结果必须留在随后的列表里，有回归测试。
5. 失败是否拖垮整页：401 与 404 分路提示。智能体待批列表和定时任务、脚本、工作流在本宿主上不要求密钥，匿名页仍会显示这些路。这是实测行为。
6. 设计门禁：`design:check` 两条 PASS。核对表在下面。
7. 验证缺口：没有全量 e2e，也没有跑 `scripts/ci/run-gates.ps1`。文件扫描进行中、要续跑、智能体未结束回合、一份真实的计划 Run、本轮新建后仍在执行的外部任务，没有截图。24 万个文件的扫描在 281ms 内结束。
8. 回滚：还原本任务提交即可。

## 规范核对

| 条 | 等级 | 依据 |
| --- | --- | --- |
| 颜色用宿主 token，页面不新增裸色值 | Verified | `pnpm run design:check`：`[style-tokens] PASS`，`[design-compliance] PASS` |
| 不用胶囊按钮 | Verified | 同上，设计门禁没有新增违反 |
| 不用 emoji 当界面文案 | Verified | 截图里的状态是汉字：正在读取、已同意、已拒绝、未启用、需要访问密钥 |
| 整页只有第一张紧急同意是金色填充 | Verified | `evidence/03-light-attention-and-running.png` |
| 拒绝是朱红文字 | Verified | 同上 |
| 破坏性确认是朱红实心 | Verified | `evidence/13-destructive-confirm.png` |
| 空态、读取中、分路错误都有文案 | Verified | `evidence/02-empty.png`、`evidence/01-loading-reading.png`、`evidence/26-unauthorized.png`、`evidence/29-plugin-disabled.png` |
| 异步读取不先出空态或 0 条 | Verified | `evidence/01-loading-reading.png` |
| 深链打开对应详情 | Verified | 102 深链表。主参数是 `id`。仅 pid 已有系统监控截图。扫描与计划 Run 仍是 Unknown |
| 不改 IM 进展 | Verified | 本批 diff 不包含 IM 进展推送 |
| 不新建插件 | Verified | 页面仍是宿主 `/task-center` |
| 不把全机进程表搬进本页 | Verified | 只收录已登记项目目录下的进程。类型对不上的也留下，projectId 为 0 |
| 打开页面和定时器不扫进程 | Verified | 进程行是在点击「重新扫描」之后出现的 |
| 亮色和暗色都看过要你处理与在跑 | Verified | `evidence/03`、`evidence/04`、`evidence/11`、`evidence/12`。暗色是显式 `html.dark` |
| 决定后不弹成功提示，结果留在卡片上 | Verified | `evidence/15-approved-inplace.png`、`evidence/16-rejected-inplace.png`、`evidence/24-once-approved.png` |

## Final Decision

CHANGES_REQUIRED

AC11、AC12、AC19 没有截图。AC22 本轮已有截图。计划 Run 和本轮新建的外部任务执行中也没有截图。缺图的原因是：没有对话模型所以建不出 Run；目录扫描在 281ms 内结束；新建的外部任务 36ms 内失败。不把这些写成通过。
