# 06 Review — TB-UI-FIX-20261007

> 补写说明：本件与 07 由用户在 2026-10-08 明文要求补齐（原话「补 然后提交」），以便 pre-commit 工件门禁通过。
> 内容依据本目录 00~05 既有记录与仓库实读，不新增未发生的验证结论。
> 证据分级沿用 05：Verified / Inferred / Unknown，三档不混用。

## Requirement Check

| AC | 结论 | 依据 |
| --- | --- | --- |
| AC1 连点复制两次，序号 1→2，剪贴板读回一致 | **Unknown** | 05 §3 U2：全部交互行为未自动化验证，待手测 |
| AC2 按钮文案全程不含「已复制」 | **Unknown** | 同上（源码层面已改，行为未验证） |
| AC3 解析并执行后提示复位 | **Unknown** | 同上 |
| AC4 复制初始指令递增且不被清空 | **Unknown** | 同上 |
| AC5 失焦后点击粘贴框，选区 = 全文 | **Unknown** | 同上 |
| AC6 已有焦点再点击，选区折叠 | **Unknown** | 同上 |
| AC7 程序化 focus 不全选 | **Unknown** | 同上 |
| AC8 既有 6 条 e2e 仍绿；build 与 vue-tsc -b 通过 | **部分 Verified** | build 通过（05 V4/V20，exitCode=0）；**e2e 与 vue-tsc -b 未运行**（U4） |
| AC9 版本徽标 v1.0.3 | **Inferred** | plugin.json 已改 1.0.3（V7 无其他硬编码）；徽标实际显示未验证 |

## Scope Check

- **PASS**：改动限于 `Plugins/ToolBridge/web/src/ToolBridgeView.vue` 与 `plugin.json`（Version），符合 04 Allowed。
- **PASS**：未改命令守卫、CommandExecutor、后端代码；未停启宿主；未手工拷文件到发布整包。
- **PASS**：`clipboard.ts` 未改（原计划的可选项，实际未动）。
- **注意**：`Plugins/ToolBridge/` 在 git 中为 **untracked**（05 V9），本批改动没有版本控制备份。

## Test Check

- **FAIL**：e2e（既有 6 条）与 vitest 在本批改动后**未运行**（U4）；新增交互行为无自动化用例（D1 由用户明文裁剪 e2e）。
- **部分 PASS**：`pnpm build` 两次均 exitCode=0，新代码已进入 dist（V6）。
- **缺口**：该插件前端无 typescript / vue-tsc 依赖，构建不做类型检查（U3，既有缺口非本批引入）。

## Architecture Check

- **PASS**：沿用插件既有前端结构，未引入新模式或新依赖。
- **关注**：`dist/` 被插件 `.gitignore` 忽略（V8）；宿主经 `StageAllPlugins` 在构建后复制 dist（V15/V21），源码树 dist 变新后需宿主重建才可见（I5）。

## Risk

| 级别 | 项 |
| --- | --- |
| L2 | 交互行为零自动化验证 ⇒ 回归与用户手测之间无兜底 |
| L2 | 运行中的 51888 为**已发布整包**且 ToolBridge 为 1.0.2（V14/I8），1.0.3 需开发宿主重建或发布新版本才可见 |
| L3 | 右键/中键与缩放手柄判定为经验实现（I1/I2/I10/I11），未在界面验证 |
| L3 | 插件目录 untracked，改动无 VCS 备份 |

## Findings

- **Critical**：无。
- **Major**：AC1~AC7、AC9 全部 **Unknown**，验收证据不足以判定实现真正满足 Intent ⇒ 不具备闸门2 通过条件。
- **Major**：既有 6 条 e2e 未回归，存在改动破坏既有用例的可能未被排除。
- **Minor**：`write_file` 回执 `created` 字段对覆盖写恒为 true（I4），不可用于判断文件是否已存在。

## Final Decision

**CHANGES_REQUIRED** —— 实现与构建均已通过，但验收所需的行为验证缺失（AC1~AC7/AC9 = Unknown，e2e 未回归）。

解除条件（任一）：
1. 用户在 51888 或开发宿主手测 AC1~AC7、AC9 并把读数回填 05；
2. 补跑既有 6 条 e2e 与 vitest，确认无回归。

> 说明：05 §8 U9 记录「用户决定不经闸门2 直接构建发布测试」，该决定由用户明文作出；本件按规范给出独立审查结论，不以该决定覆盖审查判定。
