# Task

> 阶段：Stage 4

## Objective

让展厅预览"看得全"：默认等比适应不被裁，1:1 / 最大化 / 拖拽改尺寸三条通道并存，画布外任何像素都可达，档位刷新不丢——全部由机器判据钉住。

## Scope Allowed

- `Plugins/DesignSystem/web/src/showroom/fit.ts`（新增）
- `Plugins/DesignSystem/web/src/showroom/Stage.vue`
- `Plugins/DesignSystem/web/src/showroom/Showroom.vue`
- `Plugins/DesignSystem/web/src/showroom/fit.test.ts`（新增）
- `ForgeSelf.Web/e2e/plugins/design-system/design-system-showroom.spec.ts`（只**新增** E 片，不改既有 A/B/C/D 片判据）
- `Plugins/DesignSystem/README.md`（展厅一节 + 已知缺口表）
- `docs/04-standards/agent-workflow.md`（§B 一条 UI 判据踩坑）
- `docs/ai/pilot/2026-10-04-showroom-preview-fit/**`（本工件链）
- `.forgeself/memory/2026-10-04.md`（输入22 执行与读数）

## Scope Forbidden

- 后端 `Plugins/DesignSystem/**/*.cs`、导出/投影文本、`DesignSystemTables`、数据库与迁移
- 宿主源码 `ForgeSelf.Api/**`、宿主前端 `ForgeSelf.Web/src/**`、e2e 基建（`global-setup.ts` / `playwright.config.ts` / fixtures）
- 既有 A/B/C/D 片用例的判据（**不得为了变绿去收窄它们**）
- 新增依赖、改构建配置、打 tag / push / commit（未获用户授权）
- 停/启/杀任何运行中的宿主进程（`:51888` 只做只读走查）

## Acceptance Criteria

- [x] AC1 适应档为默认，1372x768 下横向不被裁：读数 < 100% 且 `scrollWidth-clientWidth ≤ 2`，框右缘 ≤ 容器内容右缘，框渲染宽 ≥ 可用宽 90%（e2e E1）
- [x] AC2 1:1 档溢出可滚且**末端可达**：滚到最右后框右缘进入可见区；纵横末端都可达（e2e E2）
- [x] AC3 最大化档让位：网格塌单列（计算值一条轨道）且画布可用宽 ≥ 三列时 1.5×（e2e E3）
- [x] AC4 自由调尺寸：**真鼠标拖右下角**生效，且缩放比随之重算（e2e E4）
- [x] AC5 档位记忆：切档刷新保持；存储脏值回落「适应」不崩（e2e E5）
- [x] AC6 `fitScale` 纯函数单测覆盖上限 1、异常输入回落 1、极窄为正（`fit.test.ts`）
- [x] AC7 反向探针两级都实红后还原复绿：`fitScale` 写死 1 ⇒ 单测红 2 条、e2e 红 2 条（E1/E4）
- [x] AC8 插件前端门禁：`pnpm run check` 无错 + `pnpm run test` 262/262 + `pnpm run build` 成功
- [x] AC9 插件层 e2e 整目录 `--workers=1` 复跑无**新增**红（A/B/C/D 片不回归）
- [x] AC10 两档窗口（1372x768 / 1920x1080）截图读图，按 Level 3 清单核对间距/遮挡/溢出
- [x] AC11 文档同步：README 展厅一节 + 已知缺口；agent-workflow §B 踩坑；05/06/07 工件补齐
- [x] AC12 插件五步门禁：① 门禁（check/test/build）② 插件层 e2e（整目录 39/39）③ 本地插件包（`design-system-3.1.0.forgeself-plugin`，按包内容验真；**打 tag 发布需授权，未做**）④ 隔离实例走查 + 读图（e2e 即跑在隔离实例上，7 张图逐张读）⑤ **运行实例只读复验未做**（等用户升级后）

## Verification Commands

```bash
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build
cd ForgeSelf.Web && node node_modules/@playwright/test/cli.js test \
  --config=playwright.config.ts e2e/plugins/design-system --grep "预览视图档" --workers=1 --output=../.pw-out-ds-view
cd ForgeSelf.Web && node node_modules/@playwright/test/cli.js test \
  --config=playwright.config.ts e2e/plugins/design-system --workers=1 --output=../.pw-out-ds-view
```

判定看日志正文（`N passed` / `失败`），不看 exit code。
