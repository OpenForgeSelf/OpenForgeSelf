---
feature_key: F102
feature_no: 102
status: implemented
last_updated: 2026-11-26
aliases: ["102-human-attention"]
---

# 102 · 运行实况（人的注意力）

宿主页可见名是「运行实况」，路由仍是 `/task-center`。它把要你决定的审批、要续跑的回合，以及这个工具里正在跑的任务和命令收在一页。决定仍走各来源自己的接口，不另建审批库，不新建插件。

IM 进展推送保持原样。本页不发系统通知。IM 任务网关若已开启，阻塞审批仍走它现有的审批卡。

## 页上有什么

打开后每 15 秒拉取。标签和页头上的数字只计「要你处理」（紧急审批 + 其余待批），不计在跑和终态。

| 层 | 放什么 | 你能做什么 |
| --- | --- | --- |
| 要你处理 | 智能体、数据库、终端、外部 Agent 的待批。破坏性或剩余不足 2 分钟进紧急。同一会话的多条收在一个标题下，仍逐条同意或拒绝 | 就地同意或拒绝。会破坏数据的批准前再确认，取消则留在待批 |
| 要续跑 | 智能体回合卡住，且该会话没有待批 | 只有「继续」 |
| 在跑 | 下面「正在跑的来源」。按项目名分组，对不上登记项目的放在「未归属」。这一段自己滚动，不截断 | 点一行到来源页，并带上查询参数 |
| 知道即可 | 已结束的智能体回合和外部任务，最多 20 条，失败在前 | 点一行去看来源 |
| 待触发 | 已启用、这次还没在跑的定时任务 | 点进只读页，或「立即运行」 |

同意或拒绝后，卡片先留一句结果，下一次刷新再离开。不弹成功提示。外部 Agent 的同意文案是「只放行这一次」。终端待批注明「本次宿主运行期间有效」：宿主退出后等待中的命令已经结束，不把无法恢复的待批再造出来。

「重新扫描」只查找已登记项目目录下的进程，不启动、不停止。其余来源靠 15 秒拉取。读取中只显示「正在读取」。某一路 401 只标这一路「需要访问密钥」，404 标「未启用」，其余照常。

整页只有第一张紧急审批的「同意」用金色填充。拒绝是朱红文字。破坏性确认的批准按钮用朱红实心。亮暗色由宿主管，这一页不放切换。

## 正在跑的来源

同一条执行只出现一次。待办委派出去的执行体就是外部 Agent 那条任务；能用 `agentTaskKey` 对上时，行上加「来自待办」，主点击仍是执行体。定时任务拉起的智能体回合若总览里已有 `scheduler:{taskId}`，只留智能体那一行。终端会话若已有待批，不再另开一条在跑。

| 来源 | 列表 | 点击 |
| --- | --- | --- |
| 智能体未结束回合和计划 Run | `GET /api/ai-agent/tasks-overview` | `/ai-agent?session=` 或 `/ai-agent?runId=` |
| 外部 Agent 未结束任务 | `GET /api/agent-hub/tasks` | `/agent-hub?taskId=` |
| 外部 Agent 待权限 | 上表中状态为 `AwaitingPermission` 的任务，再读 `GET /api/agent-hub/tasks/{id}/permissions` | 同一条任务链接 |
| 智能终端里还在跑的命令 | `GET /api/smart-terminal/sessions`，块状态 `running` | `/smart-terminal?sessionId=` |
| 工程台拉起且未结束的命令 | `GET /api/runs` | `/sems?projectId=&runId=`，`runId` 用 pid |
| 脚本执行中 | `GET /api/script-runner/executions?status=Running` | `/script-runner?executionId=` |
| 工作流正在执行 | `GET /api/workflows/executions?status=Running` | `/workflows?executionId=` |
| 定时任务这次触发还在跑 | `GET /api/scheduler/running` | `/scheduler?taskId=`，只读 |
| 待触发的定时任务 | `GET /api/scheduler/tasks` | 同一只读页 |
| 文件工具目录扫描 | `GET /api/filetools/folders/scans` | `/file-tools?scanId=` |
| 浏览器单实例已打开 | `GET /api/browser-use/session` | `/browser-use`，只读 |
| 重新扫描发现、且上面还没有的进程 | `POST /api/runs/check`，只在按下按钮时 | 对上项目走工程台；只剩 pid 走 `/system-monitor?pid=` |

要你处理的点击同样带参：智能体 `/ai-agent?session=`，数据库 `/db-admin?approvalId=`，终端 `/smart-terminal?sessionId=&approvalId=`，外部 Agent `/agent-hub?taskId=`。来自待办的次链接是 `/todo?todoId=`。

这些不收进在跑，因为它们不是一次正在执行的任务或命令：快捷链接、开发者工具、时钟、设计系统、首页、费用、记忆、设置、插件库、档案、提示词、技能、代码片段、聊天记录、MCP 服务器的启用状态、IM 通道的连接状态。IM 派出去的回合已经在智能体那一行。数据库没有「查询执行中」列表，只有待批。系统监控的全机进程表不搬进这一页。

## 决定仍回原处

| 来源 | 决定 |
| --- | --- |
| 智能体 | `POST /api/ai-agent/approvals/{id}/decide`，body `{ approved }`。跨会话列表是 `GET /api/ai-agent/approvals/pending` |
| 数据库 | `POST /api/db-admin/approvals/{id}/resolve`，body `{ decision, confirmDestructive }` |
| 终端 | `POST /api/smart-terminal/approvals/{id}/resolve`，body `{ decision }` |
| 外部 Agent | `POST /api/agent-hub/tasks/{id}/permissions/{requestId}/resolve`，body `{ allowed, note }` |

分类在 `ForgeSelf.Web/src/services/runtimeBoard.ts`。取数在 `ForgeSelf.Web/src/services/runtimeBoardApi.ts`。页面是 `ForgeSelf.Web/src/views/TaskCenterView.vue`。

文件工具的 HTTP 前缀是 `api/filetools`，不是 `api/file-tools`。
