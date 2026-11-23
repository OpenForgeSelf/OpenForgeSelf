# 05 Evidence（只记实际发生 · Verified/Inferred/Unknown 分级）

> 任务：PC-040 组合根省参修复批 ｜ 日期：2026-11-21（§8=2026-11-22） ｜ 环境：Windows / pwsh 7.6.6
> ⚠️ **灾后重建件（2026-11-23）**：§1-7 按会话记录重建（原稿随未推送提交丢失；原始日志随 .temp 清空，实测记述以灾前会话记录为准）；**§8 为灾前原文字节保留**。

## 1. 验证账（AC1-AC3 · Verified，灾前实测记述）
- AC1 组合根守卫测试：新增 AIAgentPluginCompositionTests（real-Apply 装配图断言 + 类级显式 arity 枚举）——修复前红（装配图缺 planGenerator 被断言捕获）、修复后绿（dotnet test 过滤集）。
- AC2 组合根修复：AIAgentPlugin eagerScope 解析 IPlanGeneratorService，new AgentFacadeRunExecutor(..., registry: null, planGenerator: facadePlanner) 全显式命名实参；插件后端 dotnet build 绿 + 插件前端 pnpm build 绿。
- AC3 同类扫查：DbAdminPlugin 组合根 new DbConnectionService(...) 显式传 systemConnNames: DbConnectionService.DefaultSystemConnNames（行为不变）+ 3 行注释记录同形态；构建绿。
- 守卫现状（2026-11-23 灾后核，Verified）：**守卫测试文件随代码批丢失，PC-040 守卫失守（status: open）**——重建绑代码级还原批（TODO 输入13 A 方案）。

## 2. 根因与复现（Verified，灾前记述）
组合根 new AgentFacadeRunExecutor(agentFacade, ctx, facadeAgent, facadeOrch, facadeModels) 漏传后续可选参；C# 可选参省略零警告；executor 内 planGenerator=null 时规划静默走回退且当时无日志 → 用户只见「规划失败，已用回退计划」无解释。复现=守卫测试红（AC1）。

## 3. 同类扫查（Verified，灾前记述）
全仓组合根 new *Service(... 可选参) 形态扫查：命中 DbAdmin 同形态（已显式化，AC3）；其余组合根调用点全显式。

## 4. 归档（Verified，灾前记述）
not-taken 067（四项不做）+ PC-040 立档（problem-classes/cases/index 三处）+ plugin.json v1.10.2 Description 修复记述；PC-039（v1.10.1 批同链归档）灾后同批重建立档。

## 5. 门禁（Verified，灾前记述）
pre-commit 四门（hygiene / pilot 工件链 / feature 回写 / problem-classes）全绿；commit-msg Pilot trailer 通过。

## 6. 发版（Verified，灾前记述 + 灾后实例核验）
package-plugin 打包 智能体-1.10.2.2611211050.zip（2026-11-21 10:50）并经本地目录更新源发布；运行实例 2026-11-22 14:51 应用成功（current 指向核验，见 §8 表行1）。

## 7. 提交与复验关系（Verified）
灾前闸门3 提交 5593abc（本批，未推送）；2026-11-22 复验证据以 426bab0 追加（§8 + AC4 勾选，未推送）。两笔均于 2026-11-23 灾难中丢失，本件为按会话记录的重建。

## 8 运行实例复验（六步⑤ · 2026-11-22 20:45-21:2x）〔灾前原文字节保留〕

> 用户装 1.10.2 后实测；运行实例只读（未启停宿主、未调改状态端点）。证据源=运行实例安装根（盘符按 §B13 略）版本目录 `plugins/AIAgent` 及其当日 XTrace 日志。

| # | 时点 | 动作 | 结果 | 判级 |
|---|------|------|------|------|
| 1 | 14:51 | 插件更新应用核验 | AIAgent current=1.10.2.2611211050（宿主 2.4.22.2611221338 于 14:44 启动） | Verified |
| 2 | 20:45:23-38 | 用户 planned 模式实测（会话模型 gpustack:qwen3_8_vision） | 规划回合真实运行（MafTaskLoopAgent 收据：工具 3/3，均为 list_files 探索）→ 预算耗尽未提交 submit_plan → `[PlanGenerator] 规划回合未收到 submit_plan，走回退` → 回退草稿计划 | Verified |
| 3 | 同窗 | PC-040 修复核验 | 实例日志零条 1.10.2 专属 Warn（组合根未装配 planGenerator）——装配正常，PC-040 未复发 | Verified |
| 4 | 20:46:41 | 回退计划被确认 | 已确认计划直接执行 Run 16（agent.programmer）——回退计划与正常计划在确认流程无视觉区分 | Verified |
| 5 | 21:0x-21:2x | 判责 | ①更新已生效、原病根已除；②现失败=「回合内未提交」设计内回退（日志有因），非 PC-040 复发；③UX 缺陷候选：note 仅计划面板弱化小字（AgentWorkspace.vue:198），对话流零提示；④弱模型嫌疑（8B，单样本） | Verified/Inferred 分列 |

- AC4 判定：满足「回退有 Warn 日志」支（复验通过）；「真规划 ≥1s」支未取样（Unknown）。
- note 到达客户端=Inferred（代码链：PC-039 封套 + session.logic 契约 + AgentWorkspace 模板；实例数据根未定位，未做落库实证）。
- 遗留：对话面回退显著性（UX）+ 弱模型规划健壮性 → TODO 待拍板（2026-11-22 21:09 输入）。