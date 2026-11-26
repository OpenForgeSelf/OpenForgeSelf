# Evidence

来源等级只记实际跑过的命令。

## AC1

Verified。`PersistentApprovalServiceTests` 含跨会话只返回未过期待批。过滤集 `dotnet test`：Passed 27，Failed 0。

## AC2

Verified。`runtimeBoard.test.ts`「把破坏性审批放进要你处理」与「同一会话已有待批时不再另开一条在跑」。

## AC3

Verified。同文件「待办委派出去的外部任务只留一行」「外部权限只放行这一次」「智能体已经代表的定时触发不再重复」。

## AC4

Verified。同文件「工程台按 pid 去重」「脚本、工作流、扫描、已打开的浏览器各有一行」「浏览器没打开时不占一行」。`FolderScanServiceTests` 在上述 27 通过之内。

## AC5

Verified。同文件「401 只标记这一路，404 标未启用，其余照常」。

## AC6

Verified。同文件「知道即可只留最近的失败优先，并截断到 20 条」。

## AC7

Verified。`runtimeDeepLink` 单测覆盖脚本执行、定时任务、进程 pid。插件单测：`approvalIdFromSearch`、`todoIdFromSearch`、`taskIdFromSearch`、`semsFocusFromSearch`、`scanIdFromSearch`、`terminalFocusFromSearch`。页面在挂载时调用这些函数并打开详情。智能体页原有 `applyDeepLinkQuery` 已解析 `session` 与 `runId`，本批未改其解析。

## AC8

Verified。`tabs.test.ts` 断言可见名为「运行实况」，阻塞数字 3 显示、0 不显示。

## 门禁

Verified。`pnpm run check` 退出码 0（0 errors，既有 warnings）。`pnpm run design:check` PASS。`pnpm exec vitest run --maxWorkers=2`：128 files，1291 tests，Failed 0。

## 未在本批验证

Unknown。没有跑全量 e2e，没有在运行中的宿主上点页面。深链的单测证明查询解析和分类结果，不代替浏览器里的点击。
