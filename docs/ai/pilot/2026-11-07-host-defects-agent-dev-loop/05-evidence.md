# Evidence — PILOT-079（host-defects-agent-dev-loop）

> 状态：**B5 已转绿可入库（B1-B4 已入库 / B6-B8 未做）**。2026-11-12 补记：到期查询对同一 DateTime 字段写两次比较时 XCode 复用一个参数，结果恒为空。

## 验收标准覆盖

- **AC1（每项缺陷红灯→绿灯证据）**：部分达成。B1-B4 已随 `49ec93e` 入库。**B5（P2③）2026-11-12 转绿**：`FullyQualifiedName~Scheduler` 32/32（`.temp/tmp/scheduler-b5-r7.log`，1s）。根因是 `NextRunTime > MinValue` 与 `NextRunTime <= now` 绑在同一参数上，查询恒空；改为 SQL 只比上界，MinValue 在内存过滤。夹具绝对日期 `2026-11-08` 已过期，改为相对未来时间；拒绝用例按任务名计数，不再用全表 `FindCount()`。B6-B8 未开工。
- **AC2（build 0 错误 / 全量新增红=0 / PC 扫描绿）**：build 0 错误 Verified（全量 3773 用例编译运行，2026-11-11）；全量对基线——32 红中 6 红即 B5 自身守卫测试红 ⇒ AC2 未达（责任=B5 批）；`check-problem-classes.ps1` 全量绿 Verified（38.6s，27 PC）。
- **AC3（Agent 经 run_terminal_command 跑通 >30s 构建与重定向落盘）**：代码已入库（B2 随 49ec93e）；**集成验证未执行 = Unknown**（需 dev-stack 实例 + Agent 自跑构建）。
- **AC4（Agent 自主开发新插件全流程闭环）**：**未执行**。ClockBadge 插件源码已存在于工作区（演示产物），但「Agent 自主闭环」的集成验证未跑。
- **AC5（PC-005/PC-013 归档更新含逃逸分析；新类定级）**：PC-005/PC-013 归档文件已入库（PC-013 归档随 49ec93e 更新）；**本批（P1② 第三路径）逃逸分析是否完整落在归档内未复核 = Unknown**；P2③/P3① 新类定级未做（B5/B6-B8 未收口）。

## 证据清单（实际产生）

| 项 | 等级 | 证据 |
|----|------|------|
| B1-B4 修复+测试入库 | Verified | 提交 `49ec93e`（含 SmartTerminal/McpCenter/AIAgent/测试文件）；过滤集 425/425（`.temp/tmp/filtered-tests-20261111-r3.log`） |
| B5 曾红 | Verified | 隔离复跑 6 红/32（`.temp/tmp/scheduler-filtered-r1.log`）。诊断：`byStatus=1 byNext=1` 合取后 `due=0`（`.temp/tmp/scheduler-b5-r5.log`） |
| B5 转绿 | Verified | `FullyQualifiedName~Scheduler` 32/32（`.temp/tmp/scheduler-b5-r7.log`，2026-11-12） |
| B6-B8 未做 | Verified | 工作区无 AIAgent/McpCenter 对应改动（git status，2026-11-12） |
| AC3/AC4 集成验证 | Unknown | 未执行 |

## 来源等级说明

本批验证口径：快档（过滤集）+ 中档（全量 3773/3741/32）；e2e/走查/真机均未跑（Unknown）。
