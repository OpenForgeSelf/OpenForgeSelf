# 03 Plan — 插件 vite emptyOutDir 类级修复

## Implementation Steps
1. `file: Plugins/{15个}/web/vite.config.ts` `reason:` 按 BR1 将 `emptyOutDir: true` 改为 `emptyOutDir: false`（Grep 定位的 15 处全部同构行）。
2. `file: ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs` `reason:` 新增 G2 枚举守卫 `PluginViteConfigs_MustNotSetEmptyOutDirTrue`：遍历 `Plugins/*/web/vite.config.ts`，断言无 `emptyOutDir: true`，并断言枚举到的 config 数 ≥13（阳性对照防空转）。
3. `file: docs/11-troubleshooting/problem-classes/PC-047-*.md` `reason:` 新建问题类（不变量/signature/guards/lens）。
4. `file: docs/11-troubleshooting/cases/2026-11-24-release-local-zip-emptyoutdir.md` `reason:` 归档本次 case。
5. `file: docs/11-troubleshooting/problem-classes/README.md` + `cases/README.md` + `index.md` `reason:` 更新三处索引。
6. `file: docs/ai/pilot/.../05-evidence.md`、`06-review.md`、`07-final-report.md` `reason:` Evidence/Review/汇报。

## Test Plan（守卫覆盖）
- 既有守卫复跑：`dotnet test --filter ProblemClassEnumerationTests`（全绿，防回归）。
- 新增守卫红灯→绿灯：
  - 红灯（修复前）：`pwsh release-local.ps1` = `pnpm build (plugin-web/AgentHub) failed` + `SAFE_DELETE_BULK_CONFIRM_REQUIRED`（已 Verified，`.temp/release-local-run2.log`）。
  - 绿灯（修复后）：`pwsh release-local.ps1` 全链通过产出 zip；守卫测试绿；`check-problem-classes.ps1` 绿。
- 类级枚举：15 个 config 全部纳入扫描，不逐例断言（BR4 能枚举就不 grep 到测试里）。

## Verification
- Build：`pwsh release-local.ps1`（发布链，含所有插件构建 + dotnet publish + 打包）。
- Unit：`dotnet test --filter ProblemClassEnumerationTests`（G2 守卫）。
- Other：`pwsh scripts/check-problem-classes.ps1`（PC 签名/守卫）；`grep -rn "emptyOutDir: true" Plugins/*/web/vite.config.ts`。

## 风险与缓解
- 风险：`emptyOutDir: false` 导致 dist 旧文件残留被打进包 → 缓解：产物固定文件名覆盖写，且发布链 StageAllPlugins 按配置收集；与宿主 13+ 插件既有实践一致。
- 风险：外部 `-Sign` 死锁进程（PID 67036）占用构建目录 → 缓解：本次重跑不传 `-Sign`，输出目录独立；死锁进程为他人会话资产，不杀。
- 风险：闸门2验收前不改版本/不提交 → 遵守。
