# 00 Campaign Workflow — MAF Runtime 硬化（B2–B5）

> Task ID：`2026-11-02-maf-runtime-hardening`｜日期：2026-11-02｜状态：**执行中（用户已令开工，闸门1 视为已批）**
> 目标链：`Agent → Run → Plan → Tool → Permission → Approval → Execute → Verify → Result`
> 真源：`docs/01-architecture/maf-centric-architecture.md`（四层 + 六闸门叙事）
> 前置：maf-first-class 批已入库（603a915）+ 走查批（07d314b）——链路前 6 段已通，本战役补完后 4 段。

## 1. 批次定义（scope / 验收 / 依赖）

### B2 结果硬化（Verify + Result）——最优先
- **问题**：AI 自述成功 ≠ 可验证结果；`filesChanged` 结构化字段不落库（TODO 230 实证：todo id=56 回读全空）；无结果报告。
- **范围**：① 修复 filesChanged 落库链（工具 args 解析或 TodoRecord 写入，先复现再修，TDD）② Run 级 Verify 闸门：Run 定义出口验收（exitCriteria），Execute 后进入 Verify 阶段（回读/断言/测试执行），证据（文件清单/命令输出/截图路径）挂 AgentRun/AgentStepRun ③ Run 结果报告：Run 结束生成结构化收据（改了什么/验了什么/证据在哪），RunRecordPanel 展示。
- **主要文件**：`Plugins/AIAgent/MafLoop/*`、`Services/AIAgentService.cs`、`ForgeSelf.Api/Entities/AgentRun*.cs`、`Abstractions/SessionEvent*`、`Plugins/AIAgent/web/src/components/RunRecordPanel.vue`。
- **验收**：filesChanged 复现测试红→绿；含 exitCriteria 的 Run 在记录里可见 Verify 阶段与证据；结果报告 UI 可读。**门禁：快档全绿 + 新增真库守卫。**

### B3 任务中心（Task/Run 驱动补全）
- **范围**：① 跨入口任务总览 API（汇聚开放任务会话 Run + 计划任务 AgentRun + Scheduler AgentRun）② 宿主「任务中心」视图（列表/状态/失败重试入口/续跑提示）③ 后台并行 Run 基础（Run 与聊天视图解耦，状态轮询）。
- **主要文件**：`Plugins/AIAgent/Controllers/`（新文件 TasksOverviewController）、`ForgeSelf.Web/src/views/`（新 TaskCenterView）、`features.ts`/tabs 注册。
- **验收**：三个入口的任务在同一列表可见、状态准确；失败任务可见重试入口。**门禁：快档 + 插件前端 build + 走查截图。**

### B4 权限可配置化（统一 Permission）
- **范围**：① 权限策略数据化（按 Agent/工具：allow/ask/deny + 路径/域名/命令白名单），pre-execute 闸门消费策略 ② BrowserUse 域名白名单上收 ToolRegistry（铁律 2）③ 审批审计视图 + IM 审批推送（钉钉/企微，可后置到 B4.2）。
- **主要文件**：`ForgeSelf.Api/Services/ToolRegistry.cs`、`Abstractions/HighRiskToolPolicy.cs`、新 PermissionProfile 实体、设置页新面板。
- **验收**：策略可配且生效（改配置→同工具行为变化实测）；审计可查。**门禁：中档全量 dotnet test（碰宿主源码）+ 快档。**

### B5 旁路收敛
- **范围**：删 `ReactLoopAgent` 与 `UseMafRuntime` 开关（默认常开）、AgentHub 启发式旁路接入 MAF 或删除（架构 §7 已授权方向）。
- **验收**：全量测试绿、无死代码引用、grep 零旁路调用。**门禁：中档 + 全量 e2e（删代码面大）。**

## 2. 执行顺序与并行

```
B2（verify-dev）──┐
                  ├─→ B4（等 B2 释放 ToolRegistry/AIAgent 写面）─→ B5
B3（taskcenter-dev）┘（与 B2 并行：新文件为主，无写面冲突）
qa-gate 全程：先建基线（当前树全量 dotnet test + pnpm check/test 基线红清单），每批收口前复核门禁
```

- 写面隔离：B2=Plugins/AIAgent 既有文件 + Entities；B3=新 Controller 文件 + ForgeSelf.Web/src + features.ts；冲突文件（features.ts）归 B3 独占。
- 每批完成 = 代码闭环 + 05/06/07 工件 + pre-commit 过钩子（commit message 带 `Pilot:` 尾注）。

## 3. 测试铁律（全员）

1. Bug 修复必须 TDD：先红后绿，禁止直接改实现（AGENTS §6）。
2. 门禁分档按 §5.6：改插件=快档（过滤集 + check/test + 插件 build）；碰宿主源码/脚本=中档全量；动 e2e 基建=深档。
3. 零 mock 端到端走 e2e 体系；禁止一次性 temp 脚本验证；截图必读图。
4. 缺陷类任务走 problem-class-closure（复现→同类扫查→守卫→case/PC 归档→07 三节）。
5. 环境前置：`$env:NO_PROXY='localhost,127.0.0.1,::1'` + `$env:TEMP=$env:TMP='<repo>\.temp\tmp'`；判据看日志正文不拿 exit code。
6. 数据根隔离：测试只碰 dev-stack（:7301/:7399）；**禁止停/启/杀 :51888 用户实例**；dev-stack 停起用 `scripts/dev-stack.ps1`。
7. 提交：message 末行 `Pilot: <目录名>`；发布动作须用户明示。

## 4. 完成定义（战役级）

- B2–B5 四批 05/06/07 齐、对应门禁绿、走查截图入库、TODO 相应条目移除。
- 用户验收（闸门2）后按闸门3 归档；发布另行请示。

---

## 收口记录（2026-11-03 01:4x · lead）

五批全部完成入库：B1 `603a915`（前置）→ B2 `cf01aa5` → B3 `2c6b30c` → B4 `3c4d7ce`+`943bb0c` → B5 `28a5d78`。
门禁总账：中档全量 3427/3339/88 零引入红（89 基线闭合 ✓）；深档 e2e B5 归属 0；台账 23 PC 绿；残差双向闭合。
工件：各批 pilot 目录（docs/ai/pilot/2026-11-02-*）+ qa 审计 b4/b5 报告（本目录）。
目标链九环全通：Agent → Run → Plan → Tool → Permission → Approval → Execute → Verify → Result（MAF 主干唯一路径）。
遗留：P2 e2e 陈旧断言清理批；P3 枚举序列化候选 / 死代码第二批 / cancel 专测；3 环境特例复发监视（见 TODO.md）。
