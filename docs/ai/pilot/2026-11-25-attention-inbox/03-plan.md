# Plan

## 后端只读列表

- `IApprovalService.ListPendingAsync`，以及 Noop、持久化实现和测试替身。
- `GET /api/ai-agent/approvals/pending`。
- 工作流执行列表增加可选 `status`。
- `GET /api/scheduler/running`。
- `GET /api/filetools/folders/scans`。
- `GET /api/browser-use/session`。浏览器插件始终登记会话，工具仍只在有数据目录时注册。

## 前端分类与页面

- `runtimeBoard.ts` 纯函数分类，`runtimeBoardApi.ts` 分路取数。
- `TaskCenterView.vue` 改为五层。`tabs.ts` 与 `features.ts` 可见名改为运行实况，标签可挂阻塞数字。
- 顶栏启动 15 秒拉取，使数字不依赖当前是否停在这一页。

## 深链

- 宿主：脚本库、工作流库、系统监控、定时任务只读页、浏览器只读页。
- 插件：AgentHub、Sems、数据库、文件工具、待办、智能终端。智能体页已有 `session` / `runId`，本批只复核。

## 不改

IM 进展、各插件内部审批页的原有布局、系统通知、第二套审批库。
