# Repository Understanding

任务：运行实况。可见名改为「运行实况」，路由保持 `/task-center`。

## 已确认的仓库事实

- 宿主页原来叫任务中心，只聚合开放任务、计划任务，以及前端再映射的定时任务。文件：`ForgeSelf.Web/src/views/TaskCenterView.vue`、`ForgeSelf.Web/src/services/taskCenterApi.ts`。
- 智能体审批列表原先必须带 `sessionId`。跨会话列表是本批加在既有 `IApprovalService` 上的 `ListPendingAsync`，端点 `GET /api/ai-agent/approvals/pending`。
- 数据库、终端、外部 Agent 已有各自的待批和决定接口。本批不改这些决定的存储。
- `GET /api/runs` 是工程台拉起且仍存活的命令，外加上次检测缓存。`POST /api/runs/check` 才扫描已登记项目目录下的进程。
- 工作流服务里已有运行中执行，HTTP 列表原先不能按状态过滤。定时任务日志有 Running，但没有「当前正在跑」列表，也没有页面。文件扫描在内存里有进行中的任务，HTTP 前缀是 `api/filetools`。浏览器插件原先没有状态接口，也没有页面。
- 智能体页已经解析 `session` 和 `runId`。其余来源页原先不解析本批要用的查询参数。
- IM 进展由 `ProgressPushEnabled` 控制，默认开启。本批不改这条推送。

## 不在本页的东西

快捷链接、开发者工具、时钟、设计系统、首页、费用、记忆、设置、插件库、档案、提示词、技能、代码片段、聊天记录、MCP 启用状态、IM 通道连接状态。它们不是一次正在执行的任务或命令。
