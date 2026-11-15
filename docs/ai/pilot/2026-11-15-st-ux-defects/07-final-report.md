# AI-Native Pilot Result（最终汇报）— 智能终端三缺陷实测修复（2026-11-15）

> 任务来源：用户直接指令（输入22，即闸门1 依据）；轻量裁剪 = mini-task.md + 05 + 06 + 本件。

## 1. Repository Understanding

我确认了：智能终端为 sidecar 插件（`Plugins/SmartTerminal`，后端 `SmartTerminalPlugin` + 前端 Vue 远程加载）；会话/命令执行在 `TerminalSessionService(.V2)` 与 `SessionShell`（每 shell kind 独立包裹/解析协议）；进程输出解码真源在**宿主侧** `ForgeSelf.Core/ProcessOutputText.cs`（PC-011 裁决器）；插件经 `PluginLoadContext` 加载，宿主侧库不可由 sidecar 携带（IContext 等共享契约，见 §10）。

## 2. Selected Task

用户 :51888 实例（v2.0.1）实测三缺陷：A) 新增会话默认 `~` 起始目录 400 报错且泄露宿主路径（点名补测试）；B) `ls` 输出乱码（GBK「目录」偶合合法 UTF-8 拉丁扩展，PC-011 逃逸#2）；C) `cd` 后页面当前目录不更新（四层缺失，第四层 live 实测抓到：前端轮询不消费 `pack.prompt`）。

## 3. Changed Files

23 个文件（771+/24-，已暂存）：`StStartDir.cs`（新）、`TerminalSessionService(.V2).cs`、`SessionShell.cs`、`SmartTerminalV2Models.cs`、`ProcessOutputText.cs`、`SmartTerminalView.vue`（cwd 回写 + 轮询 prompt 消费）、`types.ts`、`plugin.json` 2.0.2、测试 ×3（`StStartDirAndSessionShellTests.cs` 新 / `ProcessOutputTextTests.cs` 撞车形态对 / live spec 新）、归档文档（case 1 新共享 + PC-035/PC-036 新 + PC-011 更新 + 台账/索引）、packaging 真源 §1.8、插件文档 03、pilot 四件（mini-task/05/06/07）。

## 4. Validation

Build: ✅ `dotnet build` 0 err（宿主 + 插件 + Core）；插件 `Release` 发布构建 + `web dist` 构建 ✅（dist 已入 sidecar 2.0.2）。
Unit Test: ✅ Core ProcessOutputTextTests 8/8（夹具改盘符占位后复跑）；SmartTerminal 52/52；宿主 1209/1209。
E2E: ✅ 插件冒烟 `smart-terminal.spec.ts` 1 passed (48.0s，终版 dist)；✅ live spec（:51888 真实例）1 passed (11.6s，提交版文本复跑认证)；design:check ×2 ✅；hygiene -Staged PASS（23 文件 771 行 0 命中）；check-problem-classes OK（36 PC）+ Registry 5/5 ✅。**档位：快档全项 + 运行实例 live 复验**；宿主源码未触碰，未触发中档条件。

## 5. Evidence

05-evidence.md：红→绿日志、快档与机器校验读数、§6 live 复验（A ✅ 截图 dialog-tilde/ps-created；C ✅ 截图 ps-cd/cmd-cd 读图核对，提示符均 `<盘>:\ $`；B ⏸️ 宿主版本门控 ≥2.4.23，live 日志显式说明；探针会话 API 清零）。工件：`docs/ai/pilot/2026-11-15-st-ux-defects/`。

## 6. Review

06-review.md Final Decision = **APPROVED**（八问、问题类归档、重构提案 R1、悬空教训 5 条已全数落成 PC/TODO）。

## 7. Risk

L1：B 的 live 断言在宿主更新前处于门控跳过态（修复本体已绿，用户实例上仍会看到 `Ŀ¼` 直到宿主 ≥2.4.23）；L2：怀疑门原理性局限（纯拉丁重音 UTF-8 文本在 CJK 码页偏 ANSI，不可分，已留档）；L3：cmd 块退出码恒 0 为既有独立缺陷（本批不修，见 §10/TODO）。

## 8. Problems Found

① B 逃逸的结构性放大器：测试基建"不可构造"假设把撞车形态排除出测试宇宙（PC-011 §5 已归档 + 永久留档 helper）；② sidecar 不能携带宿主侧库修复——修复落位横跨程序集时"测试绿 ≠ 用户可见"（packaging 真源 §1.8 已回写）；③ C 第四层：快照字段优先 + 轮询不消费服务端刷新值 = 显示层永久陈旧（PC-036 §1/§3 已归档）；④ cmd `!ERRORLEVEL!` 恒 0（独立缺陷，TODO 已挂账）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS（mini-task 五要素，用户直接指令为闸门1） |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS（三缺陷各配红灯锚，红→绿有日志） |
| Test → Evidence | PASS（live 六轮迭代含两次改判均有原文留档） |
| Evidence → Review | PASS（05 §6 完成后 06 Final Decision = APPROVED） |

### 9.5 本批自复盘（必填）

- **经验提取（保留打法）**：① TDD 红灯锚 + 「反例变绿 + 正例不翻案」成对测试形态，对启发式裁决器防过宽翻案有效，复用；② live spec 用真实实例 + 幂等激活 + 自清理会话，调试失败轮的探针会话统一 API 清零，避免在用户实例留垃圾；③ 修复落位先问「改动在哪个程序集」，live 断言按交付边界做宿主版本门控而非硬断言/硬跳过。
- **待改进**：① live spec 断言连错三轮（正则形态、盘符、反斜杠串）才过——机制修正：界面文本断言先取一次实际值打印进日志再写正则（本轮 r4 已临时这么做，下批默认）；② 守卫（hygiene）应在写盘后立即跑，而不是收尾统一跑——本轮测试夹具盘符/反斜杠串返工即此损耗（§2.3 卫生规则早已立，执行滞后）；③ sidecar 交付边界应在动手前查 packaging 真源 §1.8（本次是 live 撞到才回查，真源查询已在 §2.3 强制，执行顺序滞后）。

## 10. 问题类归档

- PC-035（新建，首见）：`docs/11-troubleshooting/problem-classes/PC-035-tilde-startdir-unresolved-info-leak.md`；case = `docs/11-troubleshooting/cases/2026-11-15-st-ux-defects.md`。
- PC-011（既有类，第 3 案逃逸更新）：同 case；怀疑门修复 + 撞车形态测试对。
- PC-036（新建，首见）：`docs/11-troubleshooting/problem-classes/PC-036-cmd-session-cwd-capture-missing.md`（含第四层）；同 case。
- 机器校验：`check-problem-classes.ps1` OK（36 PC）+ Registry 5/5。

## 11. 重构提案

R1（局部，07 披露，不在本批做）：① `SessionShell.Parse` 若再加 shell kind，先抽「标记协议适配器」再扩 kind（06 §重构提案）；② 提示符格式唯一真源已在服务端 `BuildPrompt`，前端 ps1() 的本地拼接回退路径建议下批移除（须先确认无旧版服务端兼容需求）。

## 12. 悬空教训清单（必须为空）

- 无（cmd exit 恒 0 → TODO「cmd exit 码恒 0…」逐字可检索；B live 断言宿主更新后复验 → 同一条目附带说明；均非悬空）。
