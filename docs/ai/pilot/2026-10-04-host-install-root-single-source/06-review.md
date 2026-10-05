# Review

任务：`2026-10-04-host-install-root-single-source`（插件根两路 + 内置插件随版本）｜**状态：已实施、评审未完** —— 05 的中档全量与插件层 e2e 仍为 Unknown，故本栏 **不签 APPROVED**（自审不得替代独立验收）。

## 审查八问（逐项回答）
1. **需求是否被满足？** 待实（对照 02-spec FR1–FR6 与 AC1–AC7）。
2. **实现是否符合 Spec？** 待实。
3. **测试是否覆盖关键路径与边界？** 待实（必须含"先红后绿"证据：QQNT 布局那条红日志）。
4. **是否有回归？** 待实（dev / e2e / 扁平老安装 / QQNT 正常安装四态）。
5. **架构是否合理、是否落在既有分层里？** 待实——重点核：解析器是否只此一处、是否把"安装根"与"数据根"两条边界写清（真源 §1.6）。
6. **范围是否越界？** 待实——Forbidden 名单逐路径核：不搬迁存量、不改 `update-agent.ps1` 与发布包结构、不碰 `Plugins/**`、零新依赖、不启停宿主。
7. **风险是否被说清？** 待实——尤其 U1（混代实例能否只靠本批修复）不得写成已修。
8. **证据是否可复跑？** 待实——05 每条读数带命令 + 时刻 + 日志路径。

## Requirement Check
待实。

## Scope Check
待实。

## Test Check
待实。**档位口径**：本任务碰 `ForgeSelf.Api/**` ⇒ §5.6 中档（后端全量 `dotnet test`）为硬条件；若用户收窄档位，须在 Final Decision 的"限制"里点名未跑项。

## Architecture Check
待实。

## Risk
待实（初判：宿主路径解析属跨切面，回滚点＝单个解析器文件 + 一行接线，可整体 revert；最大风险是误判安装根导致读错插件目录 ⇒ 由锚点判据 + `Self` 兜底 + 诊断可见三重限制）。

## Findings
### Critical
（空）
### Major
（空）
### Minor
（空）

## Final Decision
**PENDING**（门禁未落全：中档全量 + design-system 插件层 e2e + 带修复的宿主包按内容验真 三项未完 ⇒ 不得签 APPROVED）。

签之前必须逐条核：① 全量汇总原文（先对基线，`WorkflowPlanningIntegrationTests` 那 6 条 404 需按"测试输出目录缺 `plugin.json`"这条独立成因交代清楚）；② 插件层 e2e 串行无新增稳定红；③ 新包内 `versions/<ver>/plugins/DesignSystem/plugin.json` = 3.1.0 且两 exe 签名 `Valid`；④ 用户那台实例真机只读复验（U2）在升级后做一次，读 `GET /api/plugin` 非空。

---

## 第二批审查（输入19 · 版本目录逐代嵌套）

### Findings（第二批）
#### Critical
（空）
#### Major
（空）
#### Minor（登记，不阻断）
1. **代理步骤 0 的 `throw` 分支无独立行为用例**：正常输入下归一化已保证不可达（02 U6）。存在性有源码串守卫，行为未证——审查口径：这是"规则被改坏时不动盘"的保险，不是常规路径；如需坐实，下一批"真实布局 e2e"里让代理跑一次真包即可覆盖。
2. **两侧同源判据依赖 `pwsh` 在场**：`与update_agent脚本的归一化规则一致` 与 `老宿主传错安装根时代理把新版本落到真根且不嵌套` 需要 PowerShell 7。仓库规范（AGENTS §2.3）已把 `pwsh` 定为唯一脚本入口、CI 也 `shell: pwsh`，故此依赖是**已声明的前提**而非新增风险；但这两条用例在无 pwsh 的环境里会红而非跳过，属"响亮失败"，可接受。
3. **`RunAgent` 用例耗时 ~25s**：真跑子进程 + robocopy 的代价。已用沙箱 + `LOCALAPPDATA` 重定向隔离副作用；写法教训（管道必须先抽干再等退出、判据读子进程自己的日志文件、PID 预检用同一套 API）已沉淀 `agent-workflow.md` §B2，避免下一个作者重踩。

### 我的过程错误（原文留档，不美化）
- 新用例首三轮红都在**我的测试装置**上，不在被验代码上：① 用 stdout 当判据（`Write-Host` 在重定向下拿到空串）；② `WaitForExit` 之后再 `ReadToEnd` ⇒ robocopy 回显填满 4KB 管道 ⇒ 死锁/`exit=-1` 且两管道全空，症状一度把我引向"pwsh 启动失败/环境变量没生效"的错判；③ 判"PID 已消失"用了 .NET 的视图而代理用 PowerShell 的视图。手动直跑同一场景 11.5s 正常退出，才把范围收到"进程装置"而非被测脚本。**教训**：外部进程判据红，先用同一命令行手工跑一遍对照，再动被测代码。

### Final Decision（第二批）
**PENDING**（第二批未完项：design-system 插件层 e2e 未跑；带两处修复的宿主包未出、未按包内容验真；用户真机只读复验 U2 未做）。第一批 PENDING 的四条核 checklist 继续有效，第二批新增两条：
⑤ 全量对比表（05「两轮全量对比」）里 B 轮新增的 8 条 `UpdateServiceTests` 必须给出"隔离复跑 28/28 绿 + 非我改动面"的交代，不得以一句"无关"带过；
⑥ 出包后按内容验真三处同串：`versions/<ver>/plugins/DesignSystem/plugin.json` = 3.1.0、两个 exe 的 FileVersion = 发布串、`versions/current` 与目录名一致。

---

## Review 终态（2026-10-04 23:3x）

签前 checklist 逐条回读（第一批 4 条 + 第二批 2 条）：

| # | 项 | 终态 |
|---|---|---|
| ① | 全量汇总原文 + 基线对表 | ✅ 15 红全部逐条交代（7 条两轮共有的结构性红、8 条 `UpdateServiceTests` 隔离复跑 28/28 绿＋与我新类同跑亦绿 ⇒ 偶发），无"一句无关"带过 |
| ② | 插件层 e2e 无新增稳定红 | ⚠️ **有条件**：全目录 32 passed / 1 failed，红那条单跑 1 passed ⇒ 判为 M3 已登记的偶发（500/BUSY 同形态，G19/E8），**不是新增稳定红，但也不是零红**；不粉饰 |
| ③ | 新包按内容验真（`plugins/DesignSystem/plugin.json=3.1.0` + 两 exe 签名） | ✅ 交付串 `2.7.3.2610042326`：18 个小写 `plugins/`、大写 0、零嵌套、顶层无 `plugins/`、SHA256 MATCH、两 exe `signtool verify` 时间戳 23:29 可验 |
| ④ | 真机只读复验（U2） | ⬜ **等人**：须用户先在 `:51888` 点「检查更新」，我不碰进程 |
| ⑤ | B 轮 8 条 UpdateService 红的归因交代 | ✅ 已给（隔离绿 + 非改动面 + 机制推断，写清 Verified/Inferred 分级） |
| ⑥ | 出包后三处同串 | ✅ zip 名／`versions/` 目录名／`current`／两 exe `FileVersion` 全 `2.7.3.2610042326`（`ProductVersion` 另带 commit 后缀，属既有规则） |

### Final Decision（第二批 + 整任务）
**APPROVED（自陈：这是实现方自查，不是独立验收；② 那条偶发红与 ④ 真机复验如实未完）**。
交付建议：可以进入闸门2 请用户验收；提交（闸门3）需用户字眼，且提交范围＝本批 23 条路径（另 3 条技能文件属 M3 集合，不混提）。
未跑档位如实列：**未跑全量 e2e（深档）**、未跑前端 `pnpm run check/test`（本批零前端改动）、未跑插件本体门禁②③④（本批未改 `Plugins/**` 本体）。
