# Agent Task

> 阶段：Stage 4｜Agent 可直接执行的工作单元，零自我决策空间。
> 前序工件：00–03 齐备。本任务书分三片（T-A / T-B / T-C），每片可独立交付、独立验收。

## 闸门状态（开工前置，必须全部 ✅ 才可 Implement）

| 闸门                                                           | 状态             | 依据                                                                                   |
| -------------------------------------------------------------- | ---------------- | -------------------------------------------------------------------------------------- |
| M1 闸门2（`2026-10-01-design-system-m1-agent-tools` 验收通过） | ✅ 通过（2026-10-01） | 06-review.md 的 Final Decision = APPROVED（第二轮复验）；M1 已合入 main `5fa914c`（2026-10-02） |
| M2 闸门1（用户批准本里程碑开工）                               | ✅ 批准（2026-10-02） | 用户原话：「批准，开工 M2 全量」（2026-10-02）；分工：本会话实现，按计划分片逐步推进、每完成一项汇报一次 |

## 交接说明（实现方必读）

> 分工同 M1：规划/验收方不写业务代码；实现方独立实现并自证；规划方事后独立复验并签 06/07。**不要重写 00–04**；偏差记入 03-plan「Plan 偏差记录」。

1. **读序**：`AGENTS.md`（§0/§5.6/§10/§11）→ AI-Native 规范 → 本目录 01 → 02 → **03（§U DOM 契约、§M 模特规格、§W 向导、§S 皮肤、§C 展厅数据层、§E e2e 改动）** → `.agents/skills/design-system-verify/SKILL.md`（自查表 #8 #12 #13 #17 #19 #24 #26–#31）→ `.agents/skills/e2e-testing/SKILL.md` → `.agents/skills/frontend-design/SKILL.md`（视觉质量；但**不得引入字面色值**，只读 `--ds-*`）→ M1 的 03-plan §D/§E/§H（本任务消费的端点契约）。
2. **环境约束**（同 M1，节选）：跑 `dotnet test` 前 `$env:TMP=$env:TEMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'`；终端 UTF-8；`--logger "console;verbosity=normal"`；报告总数 == 发现数；`--no-build` 期间不要重新编译；并行会话（plugin-dev-experience）的文件与共享文件（`AGENTS.md`/`TODO.md`/日记）只做局部精确替换；禁止停/启/杀宿主进程；未获授权不 `git commit/push/tag`。前端命令在 `Plugins/DesignSystem/web` 下跑（`check/test` 借宿主工具链，需 `ForgeSelf.Web/node_modules` 已安装）。
3. **开工复核**（00 末尾清单）先做并写入 05-evidence「开工复核」；`preview-css` 可行性探针**用完即删**，不得提交、不得作为验证结论。
4. **读图责任**：视觉 QA 要求逐张读截图（Level 3 清单）。若你无法读图，**不要假装读过**：在 05-evidence 标 `Unknown`，把截图路径列给规划/验收方读图；缺陷清单以读图结果为准。
5. **检查点**：CP-A（步骤 1–8）、CP-B（9–10）、CP-C（11–14）。每个检查点更新 05-evidence 对应节并在回复里给路径；**建议每片完成即交验收**（片级验收），全部通过后才做里程碑发布。
6. **回报格式**、**证据纪律**、**范围纪律**、**失败处理**：同 M1 04-task「交接说明」第 5–8 条。**06/07 的结论栏由规划方填写**。
7. **规划方如何验收**：见 06-review.md「验收清单（预注册）」。

## Task ID

PILOT-ds-m2-showroom

## Objective

设计插件 v3.0.0：四模式外壳（开始 / 展厅 / 工作台 / 交付与接入）+ 向导 + 展厅（衣柜 / 舞台 / 微调 + 五类场景模特 + 并排对比）+ 交付与接入页 + 术语词典 + 后端 `generate/preview-css`；DesignSystem 后端过滤集全绿且总数 == 发现数，插件 web `check/test/build` 全绿，新 e2e 在真实宿主通过，既有设计系统 e2e 无新增红。

## Scope

### Allowed

- 03-plan「Files To Change」列出的全部文件（新增/修改）。
- `Plugins/DesignSystem/web/src/**`（**除** `sections/*.vue` 14 个文件）、`web/package.json` 仅限 `version` 字段同步（若仓库惯例随版本更新；不增依赖）。
- `Plugins/DesignSystem/{Controllers/DesignSystemController.cs, Services/{ExportService,PreviewCssService,DesignSystemConstants}.cs, plugin.json, README.md, ROADMAP.md}`、`docs/02-features/036-design-system.md`。
- `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/{PreviewCssTests,MannequinVariableContractTests,DesignSystemAuthTests}.cs`；`ForgeSelf.Web/e2e/plugins/design-system/{design-system.spec.ts（仅 enterWorkbench）,design-system-showroom.spec.ts,design-system-helpers.ts}`。
- `.agents/skills/design-system-verify/SKILL.md`、`.agents/skills/design-system-consume/SKILL.md`；`.temp/ds-m2/**`；本目录工件、日记、`TODO.md`。

### Forbidden

- 改数据库结构：`Plugins/DesignSystem/Data/Model.xml` 与 `Data/Entities/**`。
- 改 `web/src/sections/*.vue`（14 个专业 section 原样）；改 `plugin.json` 的 `frontend` 四项契约；删减/弱化既有 e2e 断言。
- 改 `ForgeSelf.Api/**`（宿主）、`Plugins/McpCenter/**`、`Plugins/AIAgent/**`、其他插件；改 M1 已交付的工具/服务行为（发现 M1 缺陷 → 记 TODO 并通知规划方，不顺手修）。
- 新增 npm / NuGet 依赖；使用 Element Plus 组件；引入图表/动画/UI 第三方库；`v-html` 渲染后端/用户文本；原生 `window.prompt/alert`；新增 `window.confirm`。
- 模特/展厅里出现字面色值、字体栈、阴影/圆角数值字面量（见 03 §M 写法约束）。
- 任何删除库文件/数据的能力；`git commit/push/tag`（未授权）；停/启/杀用户运行中的宿主进程；用一次性临时脚本作验证结论（AGENTS §0 红线）。

## Acceptance Criteria

- [ ] **T-A**：AC1–AC14（preview-css 同源与零写库、变量契约、外壳四模式、`window.prompt` 清零、向导状态机、术语词典、皮肤作用域、展厅数据层与竞态、模特零字面量、A 片 e2e）
- [ ] **T-B**：AC15–AC17（场景/设备框、并排对比、B 片模特守卫）
- [ ] **T-C**：AC18–AC24（交付页、复制回退、工具表与写开关、试审查、深链、可达性、视觉 QA）
- [ ] **全局**：AC25–AC28（web 三件 + 无新依赖、后端过滤集 + 既有 e2e 回归 + spec diff 审查、禁改文件零 diff、文档与技能）

## Expected Files

见 03-plan「Files To Change」。工件：`docs/ai/pilot/2026-10-01-design-system-m2-showroom-wizard/00–07`。

## Task Slices

| 片      | 步骤（03 Implementation Steps） | 完成定义（DoD）                                                       | 片级验证                                        |
| ------- | ------------------------------- | --------------------------------------------------------------------- | ----------------------------------------------- |
| **T-A** | 1–8                             | AC1–AC14 全 Verified；既有 e2e 回归与基线一致；CP-A 证据写入 05       | 后端过滤集 + web 三件 + 新 spec A 块 + 既有 e2e |
| **T-B** | 9–10                            | AC15–AC17 全 Verified；A 片全部用例仍绿                               | 同上 + B 块                                     |
| **T-C** | 11–14                           | AC18–AC28 全 Verified（e2e 不可行项 Unknown + 替代证据）；05 全部填完 | 全套 + 视觉 QA                                  |

## 技能增补建议（`design-system-verify` 自查表续号，写"历史坑"式一句话 + 判据；实现方可据实际踩坑增删）

1. **试穿（展厅）零写库且同源**：试穿前后 `DesignProject`/`DesignToken` 行数不变；`generate/preview-css` == 落库后导出（去注释规整空白）——预览和交付不同源就是假模特。
2. **模特变量契约与零字面量双守卫**：模特 DOM 里每个 `var(--ds-*)` 在后端导出里真存在（契约测试）；模特源码无字面色/字体栈/尺寸字面量（扫描守卫 + 反向探针）。
3. **皮肤作用域必须参数化**：同页并排两个不同系统，各自计算底色 == 各自后端 `semantic.surface-bg` 的 hex（逐位）；不是写死一个 `.ds-skin`。
4. **原生对话框清零**：`window.prompt/alert` 源码 0 命中、`window.confirm` 不再新增（扫描守卫 + 反向探针）；e2e 不再靠 `page.on('dialog')` 兼容。
5. **快速切换的竞态**（沿用 #19）：展厅连点衣服，只显示最后一件；序号守卫有用例。

## Verification Commands

```bash
# 环境
$env:TMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'; $env:TEMP=$env:TMP

# 后端
dotnet build Plugins/DesignSystem/DesignSystem.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem" --list-tests

# 插件前端
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build

# e2e（真实宿主）
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system/design-system-showroom.spec.ts
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system

# 工件与范围
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-01-design-system-m2-showroom-wizard
git diff --stat -- Plugins/DesignSystem/Data/Model.xml ForgeSelf.Api Plugins/McpCenter Plugins/DesignSystem/web/src/sections
```
