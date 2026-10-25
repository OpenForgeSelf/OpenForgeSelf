# AI-Native Pilot Result（最终汇报 · PILOT-073）

> 任务结束强制格式｜状态只报事实，禁止模糊表述。
> task_kind: defect —— 本文件含规范 v1.2.0 强制三节（§10 问题类归档 / §11 重构提案 / §12 悬空教训清单）。

## 1. Repository Understanding

我确认了：子进程输出解码链路共 8 个同根因站点（宿主 ServiceManager/RuntimeDetector；插件 AIAgent RunTerminalCommandTool、ToolBridge CommandExecutor、AgentHub ProcessRunner/CliTransport、Scheduler TaskExecutor、ImGateway WeComScanAuthService），全部固定 `Encoding.UTF8` 设置或经其读取 GBK 原生 CLI（sc.exe 等按系统 ANSI 936 写）输出 → U+FFFD；异根因两处（McpCenter StdioMcpTransport = MCP 规范强制 UTF-8 协议流，ScriptRunner ScriptExecutor = 事件流形态）单列裁决/挂账。详见 00-repository-understanding.md。

## 2. Selected Task

PILOT-073：消除 PC-011 问题类（子进程多码页字节流被固定码页解码）——canonical 助手 `ProcessOutputText`（四级裁决：BOM→严格 UTF-8→ANSI 往返校验→宽容兜底）+ `ProcessOutputLineBuffer`（流式逐行）+ 8 站点迁移 + G2 源码守卫 + 问题类归档。

## 3. Changed Files

git status 实测 28 项，全清单见 05-evidence.md「Changed Files」节（8 站点 M + 2 助手新文件 + 3 测试新文件 + 5 plugin.json bump + TODO/3 索引/021 文档 M + PC-011/case 归档 + 本 pilot 目录）。

## 4. Validation

Build: `dotnet build ForgeSelf.slnx` **0 error**（Verified，`.forgeself/tmp-build-pc011-sites8.log`）

Unit Test: 编码/守卫专项 **57/57**、五插件过滤集 **355/355**、Core.Tests **38/38** 全绿（Verified）；修复前 GBK 用例实测红（AC1 红灯证据，Verified）

E2E: 插件层 N/A（无 UI 改动，发布批随 AC7 走）；现场验收 = dev-stack 真实宿主 HTTP 调用 GBK/UTF-8 双中文完好 + FR-3 留痕日志 `stdout=ansi-936`（Verified，05 现场验收节原文）

## 5. Evidence

见 `05-evidence.md`：AC1-AC6、AC8 全部 Verified（含守卫 MUTATION 探针实证可红、check-problem-classes 11 PC 全绿、ProblemClassRegistry 5/5）；**AC7（发布 + :18890 运行实例复验）= Unknown，待用户拍板，agent 未自行发布**。

## 6. Review

见 `06-review.md`：Final Decision **APPROVED**（范围 = AC1-AC6 + AC8）；Findings 0 Critical / 0 Major / 4 Minor（allowlist 撤销偏差、MUTATION 落盘纪律、AC7 待闭环、工具坑沉淀）。

## 7. Risk

L1 —— 公共解码链路影响 8 站点但契约零变化，双层守卫 + 全量绿测收敛；最坏退化情形（极端混合码页流出替换符）有 EncodingUsed 日志可查，不劣于修复前。

## 8. Problems Found

1. 首跑红灯无效（WorkingDirectory 不存在）——纠正为真实缺陷红后才进入修复（05 Unit Test 节如实记录两次形态）。
2. flow-map 签名正则双反斜杠不被 check 脚本反转义 → S1-S5 改单反斜杠后绿（PC-011 front-matter 现为正确写法样板）。
3. 脚手架 `new-problem-case.ps1` 漏写 cases/README 索引行 → 手工补；脚手架缺陷不属本任务范围，已挂账 `TODO.md`「new-problem-case.ps1 脚手架漏写 cases/README 索引行」（P3）。
4. 中文「兜底」两次错字（兑底/兢底）→ U+515C 修正，涉及 03-plan 与 021 文档两处。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（三处实施偏差均回写 03-plan「实施偏差」节） |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 问题类归档

- **PC-011（新建，首见）**：`docs/11-troubleshooting/problem-classes/PC-011-process-output-fixed-codepage-decode.md`（status=guarded；signature S1-S5；allowlist 两条带理由与 expires：StdioMcpTransport=2027-12-31、ScriptExecutor=2026-12-31）
- case：`docs/11-troubleshooting/cases/2026-10-22-process-output-fixed-codepage-decode.md`（十一节全文）
- 索引三处已更新：`docs/11-troubleshooting/index.md` 速查表、`problem-classes/README.md`（tags 表 + 总表）、`cases/README.md`
- 机器校验：`check-problem-classes.ps1` 11 PC 全 OK（EXIT=0）；`ProblemClassRegistryTests` 5/5

## 11. 重构提案

- **R1（已做，本批披露）**：8 站点解码收敛到 Core 单点裁决器 + 流式行缓冲助手，删除各站点重复的固定码页设置；RunnerService 定性纠偏一并入 canonical 链。
- **R2（跨模块，已挂账）**：ScriptRunner `ScriptExecutor` 事件流站点迁移至 ProcessOutputText —— TODO.md 逐字「PC-011」条目（P2，expires 2026-12-31），届时需闸门1 六段方案批准。
- **R3**：无（不涉架构级变更；解码裁决器落位 Core 与既有分层一致）。

## 12. 悬空教训清单（必须为空）

（空）。

以下为落成核对（均已是可检索项，非悬空）：
- PC-011 类级不变量 → §10 归档 + G1/G2/G3 三层守卫（active）；
- ScriptExecutor 遗留迁移 → TODO.md「PC-011」逐字可检索（expires 2026-12-31）；
- StdioMcpTransport 异根因裁决 → PC-011 正文裁决表 + allowlist 理由；
- 021 功能文档 run_terminal_command 行 → 已补输出解码说明与真源指针；
- new-problem-case.ps1 脚手架丢索引缺陷 → TODO.md 逐字挂账（P3，2026-10-22）；
- AC7 发布悬空 → 本汇报「待拍板项」直接呈报用户，非教训悬空。
