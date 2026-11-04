# Final Report — F1 修复批（mini-task 裁剪）

> 任务：`2026-11-05-db-admin-f1-connloss`｜状态：**终稿**。

## 1 Repository Understanding / 2 Selected Task

见 mini-task.md（根因链与改动清单）。

## 3 Changed Files

`Plugins/DbAdmin/DbAdminPlugin.cs`（删 Apply 抢跑 + 根因注记）、`Plugins/DbAdmin/plugin.json`（0.1.1）、`ForgeSelf.Api.Tests/Plugins/DbAdminTests/DbAdminFallbackGuardTests.cs`（新增守卫 2 用例）、本工件目录 4 件。

## 4 Validation（分档交代）

**快档全绿**：dotnet build 0 error；DbAdmin* 过滤集 **168/168**（含 2 新守卫）；插件 web build 68.60kB + vitest **61/61**；db-admin e2e 冒烟 **1 passed (59.9s)**；dev-stack 重启链三断言（restore PASS / 无兜底日志 / CWD 无库文件）。宿主后端全量未跑（未触宿主源码，qa-ci 中档兜底）。

## 5 Evidence / 6 Review

05-evidence.md（E1-E8 全 Verified + 边界如实交代）/ 06-review.md（APPROVED）。

## 7 Risk

- qa 第二次保存（03:33:28）行去向未定（POST 响应待 qa 佐证），不阻塞主因。
- 数据抢救挂第四轮宿主上线后经 API 重建（05 §2）。
- 0300/0327 包基底记录与实际构建时点存在出入可能（单文件压缩致字符串探测不可靠），以复现实验为锚（05 §1）。

## 8 问题类归档 / 9 重构提案 / 10 悬空教训

- **问题类**：F1 属「插件 Apply 阶段依赖宿主未就绪状态」类——同类扫查仅 DbAdmin 命中；守卫测试 G2 固化 XCode 兜底机制；无既有 PC 复发、docs/11-troubleshooting/ 无新增（F1 归档随本工件目录承载）。
- **重构提案**：R4 宿主 InitializeXCodeDatabase 对「已存在但零表的登记路径库」可加告警日志（空壳库检测）；R5 XCodeTestFixture 模式迁移批（lead 已挂 TODO，晨报时段领）。
- **悬空教训**：① 插件 Apply（Build 阶段）严禁触发 DAL/实体初始化——登记时序在宿主侧，先例 AgentHubTables.cs:14 注释应作为插件开发技能条款沉淀（建议回写 plugin-development 技能）；② 单文件发布宿主 DLL 的字符串探测不可靠（压缩），判定以行为复现为准；③ dev-stack -Stop 只杀状态文件记录的 PID，前序异常退出留下的宿主进程成孤儿会锁 DLL——build 前 `Get-NetTCPConnection -LocalPort 7301` 自查。