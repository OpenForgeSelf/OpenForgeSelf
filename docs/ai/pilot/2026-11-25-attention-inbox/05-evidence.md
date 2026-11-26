# Evidence

只记录这次实际跑过的命令和看过的截图。截图与造出方式的正文在 [`docs/02-features/102-human-attention.md`](../../../02-features/102-human-attention.md)。

## 验收对照

| 编号 | 等级 | 证据 |
| --- | --- | --- |
| AC1 | Verified | `evidence/01-loading-reading.png` |
| AC2 | Verified | `evidence/02-empty.png` |
| AC3 | Verified | `evidence/24-once-approved.png` 页头由 4 变为 3；`evidence/27-rescan-detected-process.png` 在跑多条、页头为 1 |
| AC4 | Verified | `evidence/03-light-attention-and-running.png`、`evidence/04-dark-attention-and-running.png` |
| AC5 | Verified | `evidence/13-destructive-confirm.png`、`evidence/14-cancel-still-pending.png` |
| AC6 | Verified | `evidence/15-approved-inplace.png` |
| AC7 | Verified | `evidence/16-rejected-inplace.png` |
| AC8 | Verified | `evidence/11-light-sources.png` 的按钮，`evidence/24-once-approved.png` 的结果句 |
| AC9 | Verified | `evidence/03-light-attention-and-running.png` 的终端卡片 |
| AC10 | Verified | `evidence/11-light-sources.png`、`evidence/27-rescan-detected-process.png` |
| AC11 | Verified | `evidence/60-agent-resume.png`。总览 status 为 stuck，卡片只有「继续」，原因是未调用 complete_step |
| AC12 | Verified | `evidence/58-agent-planning.png`（规划中）、`evidence/59-agent-running.png`（执行中）。总览对应 planning 与 running |
| AC13 | Verified | 本次新建任务停在 Running：`evidence/62-hub-running.png`、`evidence/63-open-hub.png` |
| AC14 | Verified | 本轮 `evidence/43-open-terminal-approval.png`、`evidence/56-terminal-running.png`（`tail -f /dev/null`，文案「命令还在跑」） |
| AC15 | Verified | 本轮 `evidence/54-launched-command.png`（PID 35163）、`evidence/57-sems-pid.png`（`/sems?id=3&pid=35163`，运行页标出该 pid） |
| AC16 | Verified | 本轮新建执行 id 7：`evidence/35-script-row.png`、`evidence/40-open-script.png`（`/script-runner?id=7`，状态运行中，开始时间与本次执行一致） |
| AC17 | Verified | 本轮新建执行 id 7：`evidence/36-workflow-row.png`、`evidence/41-open-workflow.png`（`/workflows?id=7`，运行中 1 分 12 秒） |
| AC18 | Verified | 本轮 `evidence/37-scheduler-running-row.png`（「这次触发还在执行」）、`evidence/48-run-now-triggered.png`（「已触发」） |
| AC19 | Verified | `evidence/64-scan-running.png` 写「扫描中」；`evidence/65-open-scan.png` 来源页仍是「扫描中…」且状态 Running |
| AC20 | Verified | `evidence/link-browser-use-.png`。来自第一次验证栈 |
| AC21 | Verified | `evidence/27-rescan-detected-process.png`、`evidence/28-open-detected-process.png` |
| AC22 | Verified | `evidence/34-unmatched-group.png` 未归属下的 sleep；`evidence/39-monitor-highlight.png` 高亮 pid 33819，地址 `id=33819` |
| AC23 | Verified | `evidence/26-unauthorized.png`。定时任务、脚本、工作流以及智能体待批列表在本宿主上不要求密钥 |
| AC24 | Verified | `evidence/29-plugin-disabled.png`。截完已重新启用浏览器插件 |
| AC25 | Verified | `evidence/03-light-attention-and-running.png`、`evidence/04-dark-attention-and-running.png`、`evidence/11-light-sources.png`、`evidence/12-dark-sources.png` |
| AC26 | Verified | 计划 Run：`evidence/61-open-plan.png`（`planId=1`，右侧是这一次卡住的 Run）。文件扫描：`evidence/65-open-scan.png`。不足 2 分钟的金色同意：`evidence/66-soon-gold.png` |

`evidence/20-light-terminal-and-scan.png` 当时页面上没有高风险终端行，也没有扫描行，不作为这两项的证据。`evidence/link-ai-agent-_session=evidence-session.png` 是修复前的页面，打开的是新会话；修复后的证据是 `evidence/23-open-agent-approval.png`。

## 命令

验证栈用 `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1` 拉起。页面走该栈的前端地址，请求带该栈的访问密钥。密钥和本机绝对地址不写入本文。

智能体任务：第一次 `POST /api/ai-agent/runs` 返回错误事件，内容为未找到可用对话模型。后来启用本机 mock 与慢响应模型后，总览出现 planning、running 和 stuck，AC11、AC12 的截图见上表。

重新扫描：在已登记项目目录启动独立的长时间睡眠进程，浏览器打开 `/task-center` 后只点「重新扫描」。页面出现来源为「进程」的多行。当前点击地址是工程台的 `id` 与 `pid`。修复前，`GET /api/runs` 只返回本面板拉起的进程，扫描结果在下一次列表里消失。

未启用：`POST /api/plugin/browser-use/disable` 返回成功，浏览器状态接口变为 404，页面出现「浏览器 未启用」。随后 `POST /api/plugin/browser-use/enable`，状态接口恢复 200。

进程列表回归：

```text
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter FullyQualifiedName~CheckAndMerge_DetectsExternalProcess_ByWorkingDirectory
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1
```

该测试断言扫描能按工作目录对上项目，并且随后的当前列表仍包含这条 Detected 进程。

前端门禁、设计门禁和问题类台账的正文在下方「门禁」节，结果以该节写下的命令输出为准。

## 门禁

快档。全量 `scripts/ci/run-gates.ps1` 没有跑：它会带上仓库级全量测试和 e2e，这一批的判定用下面这几条。

- 前端类型检查：`pnpm exec vue-tsc -b` 退出标记 TSC:0。`eslint` 0 errors、176 warnings（既有警告，没有新增 error）。组件约定检查 CONV:0。
- 前端测试：`pnpm exec vitest run`。`Test Files  130 passed (130)`，`Tests  1301 passed (1301)`。`pnpm run test` 在本环境会停在 vitest 监听模式，所以一次跑完用的是 `vitest run`。
- 设计门禁：`pnpm run design:check`。`[style-tokens] PASS`，`[design-compliance] PASS`。
- 进程列表：`dotnet test --filter FullyQualifiedName~CheckAndMerge_DetectsExternalProcess_ByWorkingDirectory`。`Passed 1`。
- 问题类脚本：`scripts/check-problem-classes.ps1`。复跑正文是 `check-problem-classes OK：43 个 PC 签名/allowlist/guards 全部通过`。第一次跑失败，是因为本地 TODO 被收成只剩本任务一行，PC-004、PC-009、PC-039 的 planned 守卫检索不到原文。原文已放回本地 TODO（该文件不入库）。并入 main 时，主题浮层已经占用 PC-044，运行实况这条改记为 PC-052。
- 本任务工件：`scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-25-attention-inbox -EnforceHeader -EnforceAcCoverage -EnforceInteraction`。正文 `PASS: 全部 PILOT 工件链齐全`。不带 `-TaskId` 的全库扫描会把历史目录算进失败数，那不是本任务的结论。
- `ProblemClassRegistryTests` 在本环境失败：测试用反斜杠拼 `docs\11-troubleshooting\problem-classes`，Linux 上这个路径不存在。与本批 PC 正文无关，没有改这个测试。

## 本轮再验证

深链主参数改为 `id` 之后，用同一套 dev-stack 重新造了当次数据。进程匹配与深链单测：

```text
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~ProcessMatchTests|FullyQualifiedName~RunnerServiceTests.CheckAndMerge"
Passed!  - Failed:     0, Passed:    21, Skipped:     0, Total:    21
```

系统监控映射改完后重跑：`pnpm run check` 退出码 0，eslint `0 errors, 176 warnings`，组件约定 PASS。`pnpm exec vitest run` 为 `Test Files 131 passed`、`Tests 1312 passed`。`pnpm run design:check` 两条 PASS。

同一会话两张待批、取消破坏性确认、同意后留下「已同意，原任务继续」、约 16 秒后该结果消失且新的 `poll.txt` 出现：`evidence/50-destructive-dialog.png`、`evidence/51-destructive-cancelled.png`、`evidence/52-approved-stays.png`、`evidence/53-after-poll.png`。

决定失败留下原因：`evidence/49-decide-failed.png`，文案「审批已处理（rejected）」。

不足两分钟算紧急、过期不出现、知道即可最多 20 条且失败在前、同一 pid 只一条、终端待批不再另开在跑、定时任务已被智能体代表则不重复：`runtimeBoard.test.ts` 已绿。这些没有单独的页面截图。

本轮补验证没有改分类函数，也没有改业务代码。启用本机 mock 提供方并拉取模型后，`GET /api/ai-agent/tasks-overview` 出现 stuck，续跑地址为 `/api/ai-agent/runs/1/resume`。另启用一个本机慢响应的 OpenAI 兼容模型后，同一接口先后出现 planning 与 running。外部任务新建后 `GET /api/agent-hub/tasks/12` 为 Running。目录扫描在页面打开时 `state` 仍是 Running。`write_file` 待批剩余约 1 分钟时，卡片上的同意按钮带 `rs-btn-gold`。

全量 `scripts/ci/run-gates.ps1` 本轮没有跑。本轮没有改业务代码，没有重跑 `pnpm run check` 与 `dotnet test`。
