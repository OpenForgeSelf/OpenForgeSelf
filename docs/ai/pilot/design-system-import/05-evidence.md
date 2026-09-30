# 05 · Evidence — M13 DTCG 导入/回流（v2.7.0）

> 只记实际发生的事。分级：**Verified**（亲自跑过拿到真实输出）/ Inferred / Unknown。
> 日志（仓库外临时文件，已交代用完即弃）：`%TEMP%\m13-gate1-check.log` … `m13-gate7-release.log`。

## 门禁（四层，串行；2026-09-30 20:3x–20:5x）

| 层 | 命令 | 结果 | 级别 |
|---|---|---|---|
| 插件前端 check | `pnpm run check`（宿主 `vue-tsc -p ../Plugins/DesignSystem/web/tsconfig.check.json`） | 0 error | Verified |
| 插件前端 test | `pnpm run test`（宿主 vitest） | **76/76** 绿（6 文件；含 `classes.test.ts` 19 条的 `ds-mini` 守卫） | Verified |
| 插件前端 build | `pnpm run build` | `dist/index.js` **299.94 kB**（gzip 69.72 kB）、`style.css` 59.06 kB | Verified |
| 宿主 check | `pnpm run check`（ForgeSelf.Web） | **0 error** / 81 warning（历史遗留 vue/html-closing-bracket-newline 等，非本次引入） | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system/design-system.spec.ts` | **1 passed（2.6m）** | Verified |
| 全量后端 | `dotnet test`（ForgeSelf.Api.Tests） | **1695 项：通过 1686 / 失败 9 / 跳过 0**（11m28s）；**DesignSystem 0 红**（含 `ImportTests` 11 条全绿） | Verified |

## e2e 导入步骤（10b）的实测证据（真宿主、零 mock）

网络证据（日志 126/135/148/156 行）：`POST /import/preview?format=dtcg&theme=dark&overwrite=false` 200 →
`POST /import?format=dtcg&theme=dark&overwrite=false` 200 → `POST /import?theme=light&overwrite=true`（成环样本）→ 逐条断言通过。

- mark：`[import] 导入将写入的主题档（取自 select 的 value）=dark` —— **从页面自己的下拉 value 取**，
  不再读可见文本（`.ds-micro` 会把 `dark` uppercase 成 `DARK`，上一版靠 SQLite 大小写不敏感才碰巧相等）。
- mark：`导入回流：UI 写入 2 条（别名顺到 #123456）· round-trip 55517 字节逐字一致 · 成环拒写（诊断 2 条）`。
- 判据：UI 写入后 `GET /tokens/effective` 读回导入令牌的别名已顺到字面值 `#123456`；
  导出→导入→导出两次 DTCG **字节级一致**（55517 B）；成环样本整批不写且诊断可读。
- 截图：`ForgeSelf.Web/screenshots/e2e/design-system/13-import.png`（Level-3 读图：导入卡显示真实契约
  "格式 DTCG · 上限 5000 条 / 4096 KiB · 主题 DARK"、文件已选、预览/确认按钮分级、版本角标 2.7.0；无遮挡/溢出）。

## 先红后绿（判据均未削弱）

1. **真缺陷**：解析器按路径首段猜 tier，把库里语义层 `chart.series-1` 降级成 primitive →
   其别名"逆向指向上层"触发整批图校验拒绝（round-trip 与"不搬家"两条用例同时红）。
   修成 `ImportRowInfo`：库里已有路径层级/主题**以库为准**，只有新路径才推/落目标主题。
2. **守卫抓真问题**：`classes.test.ts` 发现 `ExportCenter.vue` 用了 `ds-mini` 却未在本文件定义 → 补 scoped 规则（否则按钮无样式）。
3. 测试自身两处错判（修正的是测试不是实现）：`#7c3aed` 归一断言（Value 列按原样存，归一在 ColorHex/oklch 侧）；
   "orphan 语义色进审计"假设（改成导入一对低对比 component 前景/背景，让对比度门禁自己抓——导入不豁免门禁）。
4. e2e `importTheme` 两连改：dark 档落库（页面 select 被全局主题同步）→ 读 innerText 拿到 uppercase `DARK`
   （靠 SQLite 大小写不敏感碰巧过）→ 改读 **select 的 value**。

## 全量后端 9 项红的归类（非 DesignSystem）

- 7 项 = 已登记的测试基建 staging 家族（`WorkflowPlanningIntegrationTests`×6 + `ScriptRunnerDiIntegrationTests`×1，staging 不拷 plugin.json → 404）。
- 1 项 = 已登记脆断言 `TerminalCommandGuardTests`（小写 `remove-item` vs 解码原文 `Remove-Item`，判定本身 IgnoreCase）。
- 1 项 = 新增观察 `ForgeConfigTests.ForgeSetting_配置文件归一到数据根Config目录`（期望 `Config\ForgeSetting.config`、实际未归一进 Config 子目录；
  工作区有另一会话对 `XCodeConfig.cs` 的在制改动）→ 已记 TODO，属宿主侧不动。
- 时序敏感两兄弟本轮未红：ScriptRunner cancel / Sems StopExternal（红过，仍留 TODO）。

## 包内探针（已回填 · 2026-09-30 21:0x，仓库外临时目录 `Temp\probe-270`，跑完已删）

- [x] zip `OpenForgeSelf-2.7.0-win-x64.zip`（76,742,590 B），实算 SHA256
      `45a4e5a95be063117277656e9899121a746d869dd9c943e63f1c3d07671b426c` == `SHA256SUMS.txt` 逐字一致
- [x] 包内 `Plugins/DesignSystem/plugin.json` → `"Version": "2.7.0"`
- [x] 包内 `DesignSystem.dll` → **FOUND `DtcgImporter`**（导入解析器在交付物里；`ImportLimits` 是 const 常量、编译期内联，不作探针判据）
- [x] 包内 `web/dist/index.js` → 导入入口文案全命中：`导入 / 回流`(1)、`重新预览`(1)、`确认写入`(2)、`选择 DTCG 文件`(2)
