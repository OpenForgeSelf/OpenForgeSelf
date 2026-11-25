# 07 Final Report — NETSDK1151 修复与本地 zip 产出

## 1 Repository Understanding
见 00：发布链三阶段，涉事 UpdateAgent（FDD 设计）与 Api（FDD publish），SDK 11.0.100。

## 2 Selected Task
修复 host publish 的 NETSDK1151（方案 A，用户 "a" 授权）：UpdateAgent 的 SelfContained 去掉 Condition 无条件 false。

## 3 Changed Files
| 文件 | 变更 |
|------|------|
| `ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj` | SelfContained 去 Condition → 无条件 false + 中文根因注释 |
| `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs` | 新增 G2 守卫 `Pc049_…` |
| `docs/11-troubleshooting/problem-classes/PC-049-…md`（新） | 问题类真源 |
| `docs/11-troubleshooting/cases/2026-11-24-release-netsdk1151-….md`（新） | 事件档案 |
| 台账 `README.md`×2 + `index.md` | 三处索引 |
| `docs/ai/pilot/2026-11-24-release-netsdk1151-selfcontained/`（新） | 00–07 八件工件 |

未碰：发布脚本、SDK、shim、并行会话在途文件（AGENTS.md / 工程流程文档 / hooks / build-frontend.ps1）。

## 4 Validation
- 最小 publish：EXIT=0，产物 FDD 单文件 17 MB（详见 05）。
- 完整 zip：`OpenForgeSelf-2.4.22.2611241442-win-x64.zip` 143 MB，SHA256 OK，关键文件齐备，ALL DONE in 85s。
- 门禁：check-problem-classes 39/39；Registry+PC-049 6/6；pilot 工件门禁（全开关）见下。

## 5 Evidence
见 05-evidence（AC1–AC4 逐条 Verified + 出处）。

## 6 Review
见 06-review：修复最小且针对根因，未绕过校验，台账自洽。

## 7 Risk
L1 已消解：FDD 形态经完整链 publish-updateagent 体积断言 + zip 产物双重确认。

## 8 Problems Found
1. **NETSDK1151**（本任务修复）：条件式 SelfContained 在 RID 赋值前求值失效 → PC-049。
2. **MSB4166**（环境）：本沙箱并行 MSBuild 节点崩溃，`-m:1` 规避；非仓库 bug，未归档为 PC。
3. **safe-delete 配额**（环境）：完整链不跳前端会卡批量删除守卫，属 PC-047 §遗留；用 -SkipFrontend 分阶段出包，未改守卫配置。

## 9 Process Evaluation
9.1 有效：build/publish 对照实验快速缩小范围到 publish-only 推断；读 SDK targets 原文 + 官方 task 源码取得确凿依据（未依赖猜测、未反编译）。
9.2 教训：MSBuild 属性求值顺序陷阱无编译期提示，注释声明的意图必须由守卫校验，否则注释与行为会悄悄分叉。

### 9.5 本批自复盘
- **做对了**：上一轮 PC-047 已沉淀"发布链多根因分层"方法，本轮直接定位第三层；不擅自改 shim/阈值，用脚本既有 -SkipFrontend 分阶段出包。
- **可改进**：首次最小 publish 未加 -m:1 遇到 MSB4166 多花一轮；后续在本沙箱跑 dotnet 构建默认带 -m:1 可省时。

## 10 问题类归档
PC-049（guarded，G2 active + G3 签名）+ case 2026-11-24-release-netsdk1151-selfcontained-inference + 三处索引。check-problem-classes 39/39、Registry 6/6。

## 11 重构提案
R0 不重构：单点属性声明修复 + 守卫已足够，无架构变更必要。

## 12 悬空教训清单
（空）
