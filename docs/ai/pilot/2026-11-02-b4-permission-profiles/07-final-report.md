# AI-Native Pilot Result（最终汇报）— B4 权限可配置化

> 任务：`2026-11-02-b4-permission-profiles`｜战役：2026-11-02-maf-runtime-hardening §B4｜执行：verify-dev｜2026-11-02
> 状态：✅ COMPLETED_WITH_RISK（风险均为已记录 V1 边界，无阻塞；闸门2 交 lead）

## 1. Repository Understanding

我确认了：ToolRegistry 六闸门管线 B8/042 已内建 `tools/pre-execute` Serial 三态（null=Allow）；生产此前无 pre-execute 监听器，本批为首个；HighRiskToolPolicy 硬名单的审批点在 MAF 包装（MafTaskLoopAgent 按 definition 包装，无参数上下文）；BrowserUse 插件域名白名单在插件本地 BrowserGuard（deny 码 domain_not_allowed）；XCode 宿主实体为手写模式（ToolApproval 范例：`this[String]` 索引器 + Biz EnsureCreated）；审批泵 fail-closed 已有（PersistentApprovalService）。详见 00-repository-understanding.md。

## 2. Selected Task

B4 权限可配置化：①PermissionProfile 实体（Agent/全局 profile + allow/ask/deny + 路径/域名/命令白名单，零配置红线=现状语义不变）②pre-execute 闸门消费策略 ③BrowserUse 域名白名单上收（纵深防御保留）④审计查询端点。契约先行知会 taskcenter-dev（已发全量快照）。

## 3. Changed Files

新增 9：`ForgeSelf.Abstractions/PermissionProfiles.cs`、`ForgeSelf.Api/Entities/PermissionProfile.cs`(+Biz)、`PermissionDecisionLog.cs`(+Biz)、`ForgeSelf.Api/Services/PermissionProfileService.cs`、`ForgeSelf.Api/Controllers/PermissionsController.cs`、`ForgeSelf.Api.Tests/Unit/PermissionProfileServiceTests.cs`
修改 6：`ForgeSelf.Api/AppBuilder.cs`（DI + pre-execute 监听器）、`Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs`（AC8 让位条件）、`Plugins/AIAgent/Services/ReactLoopAgent.cs`（Runtime.PermissionProfiles 注入面）、`AIAgentService.cs`/`RunOrchestratorService.cs`（RememberSessionAgent 接线）、`Plugins/BrowserUse/BrowserUseConfig.cs`（注释声明语义上收，零逻辑）
测试扩展：`ForgeSelf.Api.Tests/Plugins/AIAgent/MafApprovalLoopTests.cs`（+2 AC8 用例）
ToolRegistry.cs / HighRiskToolPolicy.cs 零改动（闸门内建/零配置在服务层实现——见 04-task 偏差记录）。

## 4. Validation

Build: **0 错误**（宿主/Abstractions/插件/测试工程，dotnet test 构建段 Verified；首轮 4 处编译错误已修复）
Unit Test: **本批新用例 23/23 绿**（21 服务+控制器 + 2 MAF AC8）；**中档全量 3435 = 基线 3432 + 23**，通过 3348 / 失败 87，**对表 qa-baseline 零新增红**——87 = 43 基线同族稳定·环境红 + 44 OutDir 旁路平坦布局伪红（标准 bin `--no-build` 隔离复跑 81 条全绿实证，E7）
E2E: 未跑（未触发深档条件：未碰 e2e 基建；插件 BrowserUse 仅注释，无插件维护闭环六步触发——无 web/ 与逻辑改动）

## 5. Evidence

`docs/ai/pilot/2026-11-02-b4-permission-profiles/05-evidence.md`：AC1..AC9 逐字映射全 ✅；Verified 证据 7 项（E1 单测 21/21、E2 MAF 6/6、E3 编译 0 错、E4 全量 3435/3348/87、E5 契约知会留痕、E6 锁避让、E7 隔离复跑 81 绿定性）。

## 6. Review

`06-review.md` 自审 **APPROVED**（八问全过；Minor×3：决策日志量级 / 审计合并查询上限 / HasRule 每回合 2 次缓存查询——均 V1 边界，悬置见 §7）。闸门2 交 lead 复核入库。

## 7. Risk

L2（宿主源码触碰，全量门禁 + 零配置红线测试兜住）。悬置项（TODO.md 无新增条目，随本工件承载）：审计保留策略（决策日志逐调用落行）、审计查询 SQL 下推、profile 内存缓存、ToolApproval Approver 列、IM 推送审批（B4.2）、glob V1=前缀语义。

## 8. Problems Found

1. OutDir 旁路全量的平坦布局会使 44 条依赖 Plugins 目录布局/相对路径的用例假红（Chat 26 + Provider/AI/Multimodal 14 + 源扫描 2 + DI 2）——**判责必须带隔离复跑**，否则会误伤 B2/B3 携带代码。已按 qa-gate 三判流程定性。
2. 写长 C# 文件时第 3 方工具（本会话写入链）存在偶发内容损伤（本批 2 处：文件名丢 .cs、5 处片段残缺），全量读回 + 小步修复兜住——沿用 B2 教训「compact 写 + 读回校验」。
3. job_kill 后 testhost 子进程可遗留锁 OutDir（本批 119960）——清理时先核对 pid 非用户进程。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（六闸门/包装点/插件口径先取证再动刀） |
| Intent → Spec | PASS（AC1..AC9 无连字符；契约先行 22:37 开工令当日冻结） |
| Spec → Plan | PASS（S1-S9 步进；ToolRegistry 零改动为合理收敛） |
| Plan → Code | PASS（偏差 5 条全部留痕 04-task） |
| Code → Test | PASS（23 新用例；真管线 + 真库；AC8 正反两面） |
| Test → Evidence | PASS（全量对表 + 隔离复跑归责，零新增红） |
| Evidence → Review | PASS（自审 APPROVED，闸门2 待 lead） |

### 9.5 本批自复盘（必填）

- **经验提取（保留打法）**：① OutDir 旁路（r2→r3）+ 孤儿 testhost 清理 = 与 dev-stack 并存的构建/测试动线，本批两次实证可用；② 全量红「三判归属」必须做隔离复跑（--no-build 标准 bin）才可判责——本批 44 条伪红避免了一次对 B2/B3 的误报；③ 契约先行 + send_message 快照让 UI 批零等待启动。
- **待改进（1 条）**：全量跑批与增量过滤集共用同一 OutDir 导致自我撞锁（MSB3027 r2）——机制修正：同批内每个跑批实例用独立 OutDir 编号（本批已改 r3），后续按 B2 规则「跑批前查 job_list + 换号」固化。

## 10. 问题类归档

无缺陷类闭环触发（本批为功能批，无缺陷修复/回归/返工）。执行期发现 3 项问题见 §8，其中第 2 项（长文件写损伤）属既有 PC（B2 实证的写入链损伤类）再次遭遇，未新增 PC——依据 problem-class-closure「同根因同类」原则，守卫（compact 写 + 全量读回）已沿用。第 1 项（OutDir 布局伪红）为测试基建现象，非产品缺陷，已沉淀为 9.5 待改进机制；第 3 项一次性进程清理，无类可归（偶发，理由：job_kill 语义固有）。

## 11. 重构提案

R0（不重构）——理由：本批按「唯一闸门消费」原则落位，无平行面残留；Minor×3 均为量级悬置项（V1 数据规模下成本可控），待真实用量触发再立项（届时属 R1 局部，07 披露即可）。

## 12. 悬空教训清单（必须为空）

（空——§8 三项均已落处：①9.5 待改进 ②既有 PC 守卫沿用 ③一次性偶发已说明理由；悬置项全部在 §7 与 06-review Findings 承载。）
