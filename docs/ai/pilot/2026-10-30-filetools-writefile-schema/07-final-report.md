# 07 Final Report：filetools-writefile-schema（2026-10-30）

## 验证结果（Validation）与审查（Review）

- **快档门禁**：过滤集 36/36（ToolSchemaJsonGuardTests 3 + FileToolsWritePersistTests 4 + FileTools 域回归 29）EXIT=0；插件冒烟 e2e 5/6（唯一红 = 取消流既有缺陷，对照实验定责非本批引入，已记 TODO P2）。
- **机器校验**：`check-problem-classes.ps1` 18 PC 全绿；`ProblemClassRegistryTests` 5/5。
- **发布核验**：2.4.20 / 2.4.21 两包 SHA256 与 SHA256SUMS.txt 逐字符一致、签名「失败 0」、包内 plugin.json 实测提取核验（1.2.1 / 1.2.2）。
- **升级核验**：:51888 实例 PID 与 `versions\2.4.21.2610301259` 路径 + 设置页徽标截图核验。
- **fs 端到端**：写入→回读逐字符一致 + pwsh 磁盘独立复核；区间替换回读；exec 正例/负例；必填校验。
- **未跑**：中档/深档门禁（未触宿主源码与 e2e 基建，§5.6 触发表不要求）；前端三件套（零前端变更面）。
- **Review**：八问 + Final Decision=APPROVED 见 [06-review.md](06-review.md)；闸门2 用户验收通过后本提交方为闸门3 产物。

## 交付状态

- **状态**：✅ COMPLETED（闸门2 待用户验收；验收通过前不提交 git）
- **目标达成**：用户 goal 六环全闭环——复现（红灯 3/3 + 现场三连挂）→ 测试用例（G2×2 共 7 用例，双红→绿）→ 修复（schema 重复键 + 落盘调用缺失）→ 本地 zip（2.4.20 + 2.4.21，-Sign，SHA256/包内版本三核）→ 浏览器代点升级（2.4.19→2.4.20→2.4.21，徽标核验 + 截图）→ fs 复测（读写回环逐字符一致 + 磁盘独立复核 + exec 正负例 + 必填校验 + search_tools）。
- **运行实例现状**：:51888 = v2.4.21.2610301259（PID 43580），file-tools 插件 1.2.2，write_file 端到端可用。

## 主要变更

| 文件 | 变更 |
|------|------|
| Plugins/FileTools/Services/FileToolFunctions.cs | 删 242-243 重复键行（PC-017）；成功分支补 `File.WriteAllText(absPath, final)`（PC-018） |
| Plugins/FileTools/plugin.json | 1.2.0 → 1.2.2 |
| ForgeSelf.Api.Tests/Architecture/ToolSchemaJsonGuardTests.cs | 新增 G2：全插件 verbatim schema 可解析 + file-tools 实例级 + write_file 五参锁 |
| ForgeSelf.Api.Tests/Unit/FileToolsWritePersistTests.cs | 新增 G2：write_file 落盘持久化四用例（磁盘回读为准） |
| docs/11-troubleshooting/ | PC-017 + PC-018 + case×2 + 三处索引 |
| updates/ | OpenForgeSelf-2.4.20.2610301232 / 2.4.21.2610301259（-Sign） |

## 问题类归档（技能 problem-class-closure）

- **PC-017**（新）：工具 ParametersJsonSchema 重复键少闭合括号 → 该工具经网关任何调用全挂。G2 `ToolSchemaJsonGuardTests` active。case：2026-10-30-tool-schema-json-duplicate-key。
- **PC-018**（新）：执行体报 success 但真实副作用未落定（write_file 零落盘；曾被 PC-017 遮蔽）。G2 `FileToolsWritePersistTests` active。case：2026-10-30-tool-success-without-side-effect。
- 机器校验：ProblemClassRegistryTests 5/5；check-problem-classes.ps1 18 PC 全绿。

## 重构提案

- **R0（已执行）**：两处均为单点语句级修复，不重构。
- **R1 记 TODO**（须另立任务）：①ToolRegistry 注册期 eager 解析全部工具 schema（坏 schema 启动即报错并点名工具，把失败从调用期提前到注册期）；②FileTools 其余副作用工具扩面持久化回读断言；③schema 守卫扩面实例级枚举（覆盖 DesignSystem 拼接型）。

## 悬空教训清单

**空**。四项后续工作（上述 R1×3 + file-tools 取消流 e2e 既有红）均已落 TODO.md ⬜ 待办（含来源与优先级），无未登记悬空项。

## 遗留与风险

- file-tools 取消流 e2e 既有红（取消后 Failed≠Cancelled）：与本批异根因（对照实验 Verified），TODO P2 已登记，修时须核查 10-29 坏合并是否伤及 FolderScanService 取消分支。
- 2.4.20 包在架上约 20 分钟（含零落盘缺陷），期间 write_file 假成功——本会话自用探针，无用户数据经此写入；2.4.21 已修复。
- dev-stack（:7301/:7399）本轮为构建/发布让路停机，任务收尾已恢复并在 TODO 协作区回帖。
