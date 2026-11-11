# Evidence — PILOT-079（host-defects-agent-dev-loop）

> 状态：**进行中收口记录（B1-B4 已入库 / B5 红态未入库 / B6-B8 未做）**。本文件为 2026-11-12 分批归档时按工作区实况补记。

## 验收标准覆盖

- **AC1（每项缺陷红灯→绿灯证据）**：部分达成。B1-B4（P1① SmartTerminal DI / P2① 终端重定向+超时 / P2② 审批唤醒 / P1② 空流 Failed）的修复与测试已随 `49ec93e` 入库，过滤集 425/425 绿（2026-11-11 r3 日志）；**B5（P2③ 调度器 Once 四缺陷）红态**——隔离复跑 6 红/26 过/32 总（16s，`.temp/tmp/scheduler-filtered-r1.log`，与全量红集一致）：SchedulerSkipConsumptionTests ×2 等待超时、SchedulerOnceValidationTests ×4；B6-B8（P3①/P3②）未开工。
- **AC2（build 0 错误 / 全量新增红=0 / PC 扫描绿）**：build 0 错误 Verified（全量 3773 用例编译运行，2026-11-11）；全量对基线——32 红中 6 红即 B5 自身守卫测试红 ⇒ AC2 未达（责任=B5 批）；`check-problem-classes.ps1` 全量绿 Verified（38.6s，27 PC）。
- **AC3（Agent 经 run_terminal_command 跑通 >30s 构建与重定向落盘）**：代码已入库（B2 随 49ec93e）；**集成验证未执行 = Unknown**（需 dev-stack 实例 + Agent 自跑构建）。
- **AC4（Agent 自主开发新插件全流程闭环）**：**未执行**。ClockBadge 插件源码已存在于工作区（演示产物），但「Agent 自主闭环」的集成验证未跑。
- **AC5（PC-005/PC-013 归档更新含逃逸分析；新类定级）**：PC-005/PC-013 归档文件已入库（PC-013 归档随 49ec93e 更新）；**本批（P1② 第三路径）逃逸分析是否完整落在归档内未复核 = Unknown**；P2③/P3① 新类定级未做（B5/B6-B8 未收口）。

## 证据清单（实际产生）

| 项 | 等级 | 证据 |
|----|------|------|
| B1-B4 修复+测试入库 | Verified | 提交 `49ec93e`（含 SmartTerminal/McpCenter/AIAgent/测试文件）；过滤集 425/425（`.temp/tmp/filtered-tests-20261111-r3.log`） |
| B5 红态 | Verified | 隔离复跑 6 红/32（`.temp/tmp/scheduler-filtered-r1.log`，2026-11-12）；全量同集红（`.temp/tmp/full-tests-20261111.log`） |
| B5 代码内容 | Verified | 工作区 diff：TaskExecutionOutcome 枚举 + AdvanceScheduleState 跳过不消费 + schedule_delete 新工具 + ForgeJson 扫尾（git diff Plugins/Scheduler/ = +174/−36） |
| B6-B8 未做 | Verified | 工作区无 AIAgent/McpCenter 对应改动（git status，2026-11-12） |
| AC3/AC4 集成验证 | Unknown | 未执行 |

## 来源等级说明

本批验证口径：快档（过滤集）+ 中档（全量 3773/3741/32）；e2e/走查/真机均未跑（Unknown）。
