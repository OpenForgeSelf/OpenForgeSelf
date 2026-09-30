# Intent — M13 设计令牌导入 / 回流（design-system 插件）

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：PILOT-design-system-import ｜ 日期：2026-09-30
> **状态：待闸门 1 确认。本文件只立项，未动任何实现代码。**
> 仓库理解（00）沿用 `docs/ai/pilot/design-system-v2/00-repository-understanding.md`，不另写一份；
> 行业依据沿用同目录 `research.md`（事实 1 DTCG 2025.10 稳定版、风险表「DTCG 树为唯一中间表示」）。

## Problem

设计系统插件现在有 **13 种导出格式、0 种导入**。
库能被投影成 DTCG / CSS / Tailwind v4 / SCSS / Less / TS / Tokens Studio / DESIGN.md / element-plus / registry / Stardust JSON / Stardust SQL / bundle，
但**没有任何入口把外部令牌文件读回库里**。后果不抽象：

- Figma / Tokens Studio 侧改完色阶，只能人工在界面上逐条搬（= 缺口 G3 的根，见 `docs/ai/pilot/design-system-v2/design.md`）；
- 一份外部现成设计系统的 CSS 变量 / DTCG 文件，进不来 → 只能从零 generate 再手改，"底座"退化成"一次性生成器"；
- 我们自己的导出物**也不能回流**：换机器、误删项目、想把某版快照里的值捡回来，全都只能重算。

## Why

- 用户的长期判定标准是「拥有完整设计功能的插件，而不是玩具」，并明确这是**长期迭代**的底座。单向数据流正好是"玩具"的形状：
  能产出漂亮文件，但接不住真实设计工作流里"另一头也在改"这件事。
- 行业口径已经收敛：DTCG 2025.10 是该组首个稳定版，Style Dictionary v4 起一等支持，Tokens Studio / Figma 都以它为交换面。
  我们既然已经把 DTCG 树定为**唯一中间表示**，那么"读这棵树"就是同一枚硬币的另一面，不需要新发明格式。
- 本仓既有纪律在这里正好复用：写入口只有一个（`TokenRepository.UpsertBatch`，带全图校验 + 手改保护）。
  导入只是把"外部文件 → 一批 TokenPatch"，**不新增第二条写库路径**——这也是它能安全落地的理由。

## Expected Outcome

用户在任何界面里拿到一份令牌文件，能在插件里选「导入」→ 看到**将要写入什么、和库里现状差在哪、哪些会被拒**→
确认后落库 → 审计立刻重跑 → 再导出能对上。范围边界见下面「Open Questions」，需用户拍板后才进 Spec。

## Constraints（硬性，逐条对照规范 §1）

1. **不新增 NuGet / npm 依赖**：JSON 解析用现有 `System.Text.Json`；CSS 变量若要做，只用现有解析路径（否则该子范围需单独点头）。
2. **不改表结构**：现有 `tier / type / path / aliasPath / value / themeId / lifecycle / generator` 足够承载导入结果；要加列即视为高风险，先升级给人。
3. **只走既有写入口**：导入必须经 `UpsertBatch` 的全图校验（成环 / 悬空 / 逆向引用整批拒写），**禁止**为导入开第二条落库通道。
4. **手改保护不得被绕过**：`Generator=manual` 的行默认不被导入覆盖，冲突必须回显（沿用生成的 `skippedProtected/conflicts` 语义）。
5. **来源可追溯**：导入行必须带可区分来源标记（如 `Generator=import:<format>`），否则"导入 200 条"和"生成 200 条"在库里无法分辨。
6. 发布口径不变：**全程不打 tag、只出本地 zip**；**禁止 agent 停/启/杀任何用户宿主进程**。
7. 验证口径不变：后端 `dotnet test` + 插件 `check/test/build` + 插件层 e2e（真实宿主、零 mock）+ 本地 zip 走查；不得用一次性脚本充当证据。

## Success Criteria（全部可机器判定，缺一即未完成）

1. **落库事实**：导入一份真 DTCG 样本后，库里出现的行数 / tier / 别名指向与样本一致（用例断言计数与若干具体路径，不断言"看起来成功"的提示文案）。
2. **round-trip 稳定**：`export?format=dtcg` → `import` → 再 `export` → 两次导出**逐字一致**；若做不到逐字一致，必须把差异列成可解释清单并让用例断言"差异集合 = 已知且为空或已解释"。
3. **反例必拒**：导入含别名环 / 悬空引用的样本 → **整批不写**，逐条返回诊断（路径 + 原因）；不允许出现"写了一半"。
4. **手改保护仍生效**：对 `Generator=manual` 的行做导入 → 该行不变，冲突项回显条数 > 0。
5. **门禁立刻重算**：导入后跑审计，新令牌的对比度 / 分层 / 命名结论必须出现在审计里（证明导入走的是同一套判定，不是旁路）。
6. **界面与真实路径**：插件层 e2e 在真实宿主里走完「选文件 → 预览差异 → 确认导入 → 库里读得到 → 再导出对得上」，并留证据行。
7. 四层门禁全绿 + 本地 zip 重打包 + 包内探针命中 + 文档回写（README / ROADMAP / `036` / 技能）。

## Open Questions（闸门 1 需用户拍板）

| # | 问题 | 我的推荐 | 取舍 |
|---|---|---|---|
| Q1 | 首期格式范围 | **只做 DTCG**（我们已把 DTCG 定为唯一中间表示，round-trip 判据天然闭合） | Tokens Studio / Figma Variables 原生 JSON 是第三方形状，字段语义要猜；CSS 变量捕获无 tier 信息，导入只能落 primitive，容易做出"名实不符"的导入 |
| Q2 | 导入目标 | **只允许导入到已有项目**（新项目必须先建，避免"导入即造项目"绕过项目/主题治理） | 允许"导入即建新项目"更省事，但会把项目命名与主题轴的决定权交给文件名 |
| Q3 | 冲突默认值 | 默认 `overwrite=false`（手改优先），界面上显式给"覆盖"开关 | 反过来会让一次误导入冲掉用户手工调色，代价不可逆 |
| Q4 | 是否要"导入预览"（不落库先看差异） | 要，且与 `generate/preview` 同形（纯计算不落库） | 不做预览则唯一的确认方式是真的导入再回滚，而库没有回滚 |
| Q5 | 是否覆盖 Tokens Studio 的 `$themes`（多主题一次进） | 留到 Q1 之后单独立项 | 主题轴（color/density/brand）是我们的一等概念，Tokens Studio 的 set 语义不是一一对应，硬映射会造出假对应 |
