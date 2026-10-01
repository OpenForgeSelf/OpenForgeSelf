# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。

> **状态：NOT_STARTED（交接骨架）**——实现尚未开始。除「基线（规划会话）」一节是规划会话的真实实测外，其余各节**全部待实现方填写**；
> 空栏 = 未做，不代表通过。填写规则见 04-task.md「交接说明」；规划方事后按 06-review.md 独立复验，不采信本文自述。

## Task

PILOT-ds-m3-style-guideline（设计插件 v3.1.0：风格轴 + 预设库 + UX 规范）

## 基线（规划会话 2026-10-01 实测，Verified）

| 项                      | 命令 / 条件                                                                                                | 结果                                                                       | 来源等级 |
| ----------------------- | ---------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | -------- |
| 发现用例数（M1 开工前） | `dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem" --list-tests`       | 208（M1/M2 合入后会变大，开工时重取）                                      | Verified |
| 重定向 Temp 后          | 先 `$env:TMP=$env:TEMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'` 再跑同过滤集          | `测试总数: 208 通过数: 208 总时间: 4.6885 分钟`                            | Verified |
| 版本现状（M1 开工前）   | `DesignSystemConstants` 三常量 / `plugin.json`                                                             | 均为 `2.7.1`（git HEAD `61b327d`）；M1 → 2.8.0、M2 → 3.0.0、本任务 → 3.1.0 | Verified |
| `xcode` 工具            | `xcode` CLI（xcodetool 11.25.2026.901）已安装；既有实体含 `X.cs` + `X.Biz.cs`，`DesignSystem.htm` 为生成物 | 可用                                                                       | Verified |
| 新增实体类型的升级路径  | `DesignSystemTables.EnsureCreated()` → `EntityFactory.InitConnection("DesignSystem")`                      | 读码：对已有库自动补建新表（**升级测试 AC12 须实测，此处仅为读码**）       | Inferred |

## 开工复核（实现方填，对应 00「开工前复核清单」6 项）

| #   | 复核项                                                                                                                   | 命令 / 动作                   | 结果 | 来源等级 |
| --- | ------------------------------------------------------------------------------------------------------------------------ | ----------------------------- | ---- | -------- |
| 1   | M1、M2 均已合入且各自 06 Final Decision = APPROVED                                                                       | 读两个 pilot 的 06；`git log` |      |          |
| 2   | 基线重取：过滤集总数 / 通过 / 失败；总数 == 发现数；web `check/test/build`；既有 `e2e/plugins/design-system`；存量红对表 |                               |      |          |
| 3   | **黄金基线已录制**（先于任何生成器改动）：录制日期 / git HEAD / 键数                                                     |                               |      |          |
| 4   | `xcode` 对**未改动** `Model.xml` 跑一次：生成物与库内逐字节一致                                                          |                               |      |          |
| 5   | `font.display` 可行性探针：`ExportService` / `SeedBrandCatalog` 是否假设只有 sans/mono                                   |                               |      |          |
| 6   | `git status` 清点并行会话改动，与 `Plugins/DesignSystem/**` 无重叠                                                       |                               |      |          |

## 黄金基线记录（AC1/AC2；**必须在改任何生成器代码之前填写**）

| 项                          | 内容                                                                                                   |
| --------------------------- | ------------------------------------------------------------------------------------------------------ |
| 录制日期 / git HEAD         |                                                                                                        |
| 基线集合                    | 原 8 个预设 × (`shared` + 各主题层) + 3 个非预设请求（`后台管理系统` / `#7c3aed` / `hue=200,compact`） |
| 键数（`Dictionary` 条目数） |                                                                                                        |
| 录制方式                    | `StyleAxisGoldenTests` 内默认跳过的录制器，输出 `.temp/ds-m3/golden.txt`，人工贴入                     |
| 每个轴落地后重跑结果        | 阴影：<br>描边：<br>中性色：<br>字体：<br>圆角：<br>强调色：                                           |

## Changed Files

<!-- 实现方列出全部改动/新增文件（以 git status 为准）；与 04-task Allowed / 03 Files To Change 逐项对账，多出的要解释 -->

-

## AC → 证据矩阵（26 行必须全填；e2e / 读图不可行写 Unknown + 原因 + 替代证据）

| AC   | 判据（摘自 02-spec）                                                                                                                         | 验证命令 / 用例全名 | 结果 | 来源等级 | 输出摘要（贴关键原文，勿贴推断） |
| ---- | -------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- | ---- | -------- | -------------------------------- |
| AC1  | 黄金回归：原 8 预设 × 各层 SHA-256 与基线逐一相等；`GeneratorSeed` 逐字不变                                                                  |                     |      |          |                                  |
| AC2  | 缺省请求逐字节同基线（含 3 个非预设请求）                                                                                                    |                     |      |          |                                  |
| AC3  | 每轴每个非默认取值：白名单内 ≥1 条变化、白名单外逐条不变                                                                                     |                     |      |          |                                  |
| AC4  | 审计矩阵：7 轴 × 取值 × 3 基础请求 × 4 默认主题，0 critical                                                                                  |                     |      |          |                                  |
| AC5  | `meta.styleAxes` 与 `StyleAxes` 一致；非法取值 400；强度夹取+`Notes`；`accentStrategy` 优先                                                  |                     |      |          |                                  |
| AC6  | 复现：同请求两次逐字节相同；非默认 `Seed` 带后缀、默认无后缀                                                                                 |                     |      |          |                                  |
| AC7  | 预设恰 13 个；原 8 个 `request` 与基线逐字段相等；新 5 个与 §P 一致；每个"生成→审计"0 critical；覆盖矩阵；M1 回归全绿                        |                     |      |          |                                  |
| AC8  | 13 预设 × light/dark：`preview-css` == 落库后导出；M2 变量契约全过                                                                           |                     |      |          |                                  |
| AC9  | 工具/REST 增量：`design_create` 新字段落库后令牌差异成立；schema 键集更新并登记                                                              |                     |      |          |                                  |
| AC10 | 前端：「更多风格选项」全由 `meta.styleAxes` 渲染；`vocabulary` 守卫反向探针变红；`tune.ts` 映射                                              |                     |      |          |                                  |
| AC11 | e2e：editorial vs tech-crisp 标题 `font-family`/卡片 `box-shadow` 不同；向导创建后含 `font.display`                                          |                     |      |          |                                  |
| AC12 | 表与升级：`Model.xml` 仅新增一表；xcode 二次生成一致；`EntityTypes` 登记；旧库升级测试                                                       |                     |      |          |                                  |
| AC13 | `GuidelineGenerator`：确定性、14 个 codes、MUST≥1、引用存在、§G3 敏感性、数字守卫（+反向探针）                                               |                     |      |          |                                  |
| AC14 | 仓储/服务：只补空；manual 受保护；`overwrite` 覆盖；`generate` 响应含 `guidelines`；存量不回填                                               |                     |      |          |                                  |
| AC15 | REST：CRUD/过滤/`brokenRefs`/校验/鉴权/**无 DELETE**/409/软归档恢复                                                                          |                     |      |          |                                  |
| AC16 | 导出：brief 章/design-md 章/bundle 两文件/Manifest/agent-rules；空不开章；`contentHash` 随之变化；渲染值==`tokens/effective`；Stardust 仍 10 |                     |      |          |                                  |
| AC17 | 快照 schema 3：不凭空新增；v3↔v3 报变更；hash 含规范；无规范项目重发幂等；旧文件可读                                                         |                     |      |          |                                  |
| AC18 | 工具：`design_edit guideline` 写读回读+写开关；`design_lookup`/`design_context`/checklist；总数仍 8                                          |                     |      |          |                                  |
| AC19 | 界面：第 15 个 section、能力置灰；e2e 写一条规范 REST 回读一致；chip 值同源；原 14 入口无新增红                                              |                     |      |          |                                  |
| AC20 | 数据安全：无删除路径；归档软删；e2e 只软归档                                                                                                 |                     |      |          |                                  |
| AC21 | 视觉 QA 逐张读图；每个轴值肉眼可辨；缺陷修复或登记 TODO                                                                                      |                     |      |          |                                  |
| AC22 | README / ROADMAP / 036 / `design-system-verify` / `design-system-consume` 更新                                                               |                     |      |          |                                  |
| AC23 | 后端过滤集全绿且总数==发现数；web 三件；既有 e2e 全绿（14 个 nav、`entities===10`）                                                          |                     |      |          |                                  |
| AC24 | 范围：`Model.xml` 仅新增表；宿主/McpCenter 零 diff；无新依赖；组件蓝本仍 10；`AuditKinds.All` 不变                                           |                     |      |          |                                  |
| AC25 | 版本三常量与 `plugin.json` 均 `3.1.0`                                                                                                        |                     |      |          |                                  |
| AC26 | 规范全文清单（14 条 × 3 种 kind）已写入下节并交用户审阅                                                                                      |                     |      |          |                                  |

## 规范全文清单（AC26；交用户在闸门2 审阅措辞）

> 由实现方在步骤 7 之后导出：对 `console`、`marketing`、`product` 三种 kind（density 取 `default`、industry 取 `general`）各生成一份，逐条贴出 `code / 分类 / 标题 / 摘要 / 规则（级别+文本）/ 引用令牌`；再附 §G3 各 industry/density 的差异条目。**未经用户确认的措辞不得宣称"文案已通过"。**

| 项                        | 内容 |
| ------------------------- | ---- |
| 清单文件路径              |      |
| 交付用户的时间 / 回复位置 |      |
| 用户反馈与处理            |      |

## 公式调整记录（03 §A3「系数可微调」；无调整写「无」）

| 轴 / 风格 | 原系数 | 新系数 | 原因（审计 critical 摘要 / 读图结论） |
| --------- | ------ | ------ | ------------------------------------- |
|           |        |        |                                       |

## Build

Command:

```bash
```

Result: PASS / FAIL（来源等级：）

```text
```

## Unit Test

Command:

```bash
```

Result: PASS / FAIL（来源等级：）；总数 / 发现数 / 失败数：

```text
```

## Integration Test

<!-- 升级测试（AC12）、渲染同源、快照幂等等；写明用例全名与结果 -->

Result: PASS / FAIL / N/A（依据：）

## E2E

<!-- design-system-style.spec.ts（A 块 / B 块）+ 既有 e2e/plugins/design-system 回归 -->

Result: PASS / FAIL / N/A（依据：）

## Static Analysis

<!-- 插件 web：pnpm run check；后端：dotnet build 的 warning 数（新增代码须 0） -->

Result: PASS / FAIL / N/A

## Screenshots

<!-- 视觉 QA 矩阵：轴 × 取值 × 场景；路径 ForgeSelf.Web/screenshots/e2e/design-system/m3-*.png；逐张读图结论；无法读图写 Unknown 并列路径 -->

| 截图 | 对应轴 / 取值 | 读图结论 | 来源等级 |
| ---- | ------------- | -------- | -------- |
|      |               |          |          |

## 反向探针记录（自查表 #24：新判据必须造反例证明会响）

| 探针             | 操作                                                         | 预期                                        | 实际（贴原文） |
| ---------------- | ------------------------------------------------------------ | ------------------------------------------- | -------------- |
| 数字守卫必响     | 往规范模板塞一句含 `16px` 的规则                             | `GuidelineGeneratorTests` 数字守卫变红      |                |
| 预设覆盖矩阵必响 | 把某预设的某个轴改回默认值                                   | 覆盖矩阵用例变红                            |                |
| 引用存在必响     | 删除一个规范引用的令牌                                       | `brokenRefs` 含该路径且界面标"令牌已不存在" |                |
| 词表守卫必响     | 在 `vocabulary.test.ts` 的被扫源码里造一个轴取值三成员字面量 | 守卫变红                                    |                |
| 黄金回归必响     | 临时把 `soft` 阴影某系数改 0.1                               | 黄金回归变红（验证后还原并重跑全绿）        |                |

## Plan 偏差汇总

<!-- 条数 + 03-plan「Plan 偏差记录」位置；无写「无」 -->

## Known Limitations

## Unresolved Issues

<!-- 失败时如实记录：FAIL + 原因 + 已尝试 1./2./3. + 最终状态 BLOCKED -->

## 阻塞

<!-- 无写「无」 -->
