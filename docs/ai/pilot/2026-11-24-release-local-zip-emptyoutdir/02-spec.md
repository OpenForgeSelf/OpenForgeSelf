# 02 Spec — 插件 vite emptyOutDir 类级修复

## Functional Requirements
- FR1：所有 `Plugins/*/web/vite.config.ts` 的 build 配置不得设 `emptyOutDir: true`（与宿主 `ForgeSelf.Web/vite.config.ts:31` 的 `false` 对齐）。
- FR2：发布链 `release-local.ps1` 在沙箱环境（safe-delete shim 注入）下能跑完整链并产出 zip。
- FR3：新增 G2 类级枚举守卫，遍历插件 config 断言 FR1，带阳性对照。

## Input / Output
- Input：15 个插件 `vite.config.ts`（当前 `emptyOutDir: true`）。
- Output：15 个文件改为 `emptyOutDir: false`；新守卫测试；PC-047 + case；本工件链。

## Business Rules
- BR1：canonical fix 单行替换 `emptyOutDir: true` → `emptyOutDir: false`，不改其他构建项（outDir/lib/external 等保持原样）。
- BR2：`emptyOutDir: false` 下产物为固定文件名（index.js、style.css）覆盖写，旧文件不残留为可加载入口。

## Boundary Conditions
- 只触碰 `Plugins/*/web/vite.config.ts` 与守卫/归档文件；不改宿主 config、不改发布脚本、不改 shim。
- 仅同根因命中项纳入本任务；他人会话的在途改动不碰。

## Error Handling
- 若重跑发布链仍失败于其他段，按问题类闭环另记 TODO，不顺手修。

## Compatibility
- `emptyOutDir: false` 是 Vite 标准选项，CI 无 shim 环境同样有效（仅不留空 dist，产物覆盖写），不影响 CI 发布通道。

## Failure Modes（缺陷类必写，源于 P0 命中的 lens）
- PC-006 lens「发布链本机环境前置」：本次根因正是本机沙箱环境（safe-delete shim）而非仓库代码 bug；本任务从代码侧消除环境依赖（不再触发删除），满足该 lens。
- 新插件/新 config 再次写 `emptyOutDir: true`：由 FR3 G2 守卫拦截；PC-047 signature（grep `emptyOutDir:\s*true`）兜底。

## Acceptance Criteria
- AC1：`pwsh release-local.ps1` 实跑产出 zip（Verified）。
- AC2：15 文件全改，`grep -rn "emptyOutDir: true" Plugins/*/web/vite.config.ts` = 0 命中。
- AC3：G2 守卫 `dotnet test --filter ProblemClassEnumerationTests` 绿；守卫自身有阳性对照（枚举到 ≥13 个 config）。
- AC4：PC-047 signature `check-problem-classes.ps1` 绿。

## Interaction Design
无用户可见 UI 交互（纯构建配置修复），不适用。
