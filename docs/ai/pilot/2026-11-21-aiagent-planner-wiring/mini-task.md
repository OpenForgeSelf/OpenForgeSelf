# PILOT-082 后批｜planned 模式规划静默回退（PC-040 组合根省参）——组合根修复 + 守卫（mini-task）

> 任务协调人：lead ｜ 裁剪裁定：**轻量**（≤3 代码文件：AIAgentPlugin.cs + AgentFacadeRunExecutor.cs + 组合根守卫测试；Intent/Spec/Plan/Task 合并本文件，Evidence/Review 单独产出 05/06/07）
> 状态：闸门1 已过（2026-11-21；缺陷类按 problem-class-closure 增强）
> ⚠️ **灾后重建件（2026-11-23）**：原稿随未推送提交丢失，本文按会话记录重建；实现代码批（PILOT-082 五缺陷 + PC-039 基座 + 本批守卫）尚待源码级还原（TODO 输入13 A 方案），还原前 PC-040 守卫失守（status: open）。

## 0. 对外方案（六段）

### 1) 目标
用户报：planned 模式恒「规划失败，已回退计划」且界面无解释（2026-11-21 会话实测复现）。
根因已证：组合根装配 AgentFacadeRunExecutor 漏传 planGenerator——新增可选参数后调用点滞后，C# 省参零警告，仅真实宿主装配路径暴露；规划器恒 null → 规划静默走回退且无日志。
成功判据：组合根守卫测试（real-Apply 装配图断言 + 显式 arity 枚举）红→绿；实例日志规划路径可见（专属 Warn 零命中）。

### 2) 改动（≤5 条）
| 文件:行 | 动作 |
|---|---|
| Plugins/AIAgent/AIAgentPlugin.cs（组合根 Apply） | eagerScope 解析 IPlanGeneratorService，以**全显式命名实参**传入 AgentFacadeRunExecutor（registry/planGenerator 不省参） |
| Plugins/AIAgent/Services/AgentFacadeRunExecutor.cs | 规划器不可用分支补 Warn 日志（静默降级禁令；前端提示文案与透传契约不变） |
| ForgeSelf.Api.Tests/Plugins/AIAgent/AIAgentPluginCompositionTests.cs（新增） | real-Apply 装配图断言 + 类级显式 arity 枚举守卫（违例即红） |
| Plugins/AIAgent/plugin.json | v1.10.2 + Description 追加修复记述 |

### 3) 不改
- 前端 note 文案与 PC-039 透传契约（session.logic 契约测试锁定，文案「规划失败，已用回退计划」保持）；
- DbAdmin 之外的组合根（同类扫查命中 DbAdmin 同形态，同批显式化：systemConnNames 显式传参，行为不变）；
- 规划预算/超时参数与回退显著性 UI（两者已记 TODO 待拍板，见 07）。

### 4) 验证
快档：dotnet build + dotnet test（组合根守卫过滤集）；Plugins/AIAgent/web pnpm build；守卫先红后绿；发布后运行实例只读复验（AC4，2026-11-22 六步⑤）。

### 5) 代价与风险
回滚点=单提交；风险：arity 守卫对后续新增可选参误报（守卫报告强制可见，白名单内收敛）。

### 6) 待拍板
无，默认按上述执行（缺陷修复批）。

## 1. 交互设计（无界面交互）
无界面交互：本批零 UI 改动。用户可见行为不变：计划卡 note 文案与 PC-039 封套透传契约由 session.logic 契约测试锁定；本批动作面为组合根装配与日志，无新增端点/入参/界面态。

## 2. 验收标准（AC）
- AC1 组合根守卫测试（real-Apply 装配图断言 + 显式 arity 枚举）先红后绿；
- AC2 组合根修复落地：GetService 解析 IPlanGeneratorService + 全显式命名实参（零省参），插件快档门禁绿；
- AC3 同类扫查 DbAdmin 组合根同形态显式化（行为不变，构建绿）；
- AC4 运行实例只读复验：回退有 Warn 日志支满足（2026-11-22 实测，见 05 §8）；真规划 ≥1s 支未取样（Unknown）。