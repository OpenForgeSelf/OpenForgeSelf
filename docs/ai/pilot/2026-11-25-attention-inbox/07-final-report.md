# Final Report

## 做了什么

运行实况按 102 的验收补了截图。修了三处会让页面和接口不一致的实现：脚本和工作流详情解开 `{data}` 信封并把数字状态译成页面用的词；已经写下结果的待批不再计入页头和标签；工程台当前列表保留最近一次扫描仍活着的进程。智能体当前页会按 `session` 打开该会话的待批。

## Validation

`vue-tsc -b` 通过。`vitest run` 130 个文件、1301 条通过。`design:check` 两条 PASS。进程列表回归 1 通过。没有全量 e2e，没有跑 `scripts/ci/run-gates.ps1`。细节在 05。

## Review

见 `06-review.md`。Final Decision：CHANGES_REQUIRED。缺截图的是要续跑、智能体未结束回合、文件扫描进行中、仅 pid 的系统监控。

## 问题类归档

[PC-052](../../../11-troubleshooting/problem-classes/PC-052-runtime-board-list-not-the-list-shown.md)。案例 [2026-11-26 运行实况列表与页面不一致](../../../11-troubleshooting/cases/2026-11-26-runtime-board-list-not-shown.md)。

## 重构提案

R0。信封解开、阻塞计数和扫描缓存都落在各自已经负责这件事的函数里。不把各来源再收成一个新的列表服务。

## 悬空教训

无。缺截图的四项写在 102 和 06，不另开没有归宿的建议。

## 风险

脚本、工作流和定时任务的「执行中」在宿主重启后仍可能留在列表里，进程已经不在。匿名打开时，智能体待批列表和定时任务、脚本、工作流仍会显示。文件扫描进行中、要续跑、智能体未结束回合、仅 pid 的系统监控没有截图。
