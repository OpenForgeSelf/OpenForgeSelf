# 05 Evidence — 发布本地 zip 排障与插件 emptyOutDir 类级修复

## Changed Files
- 15 × `Plugins/<X>/web/vite.config.ts`：`emptyOutDir: true` → `false`（各 1 行）。
- `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`：新增 `Pc047_PluginViteConfigs_MustNotSetEmptyOutDirTrue`（G2 枚举守卫 + 阳性对照）。
- `docs/11-troubleshooting/problem-classes/PC-047-release-local-zip-emptyoutdir.md`（新建）。
- `docs/11-troubleshooting/cases/2026-11-24-release-local-zip-emptyoutdir.md`（新建）。
- `docs/11-troubleshooting/problem-classes/README.md`、`cases/README.md`、`index.md`：三处索引 + 1 条存量缺索引（2026-11-23-hover-reveal-width-jump）。
- `TODO.md`：新增 2 条遗留（沙箱配额、PC-034 假红）。

## 验证结果（Verified = 亲自跑）

| 项 | 命令 | Result | 出处 |
|----|------|--------|------|
| 红灯复现 | `pwsh release-local.ps1`（足量超时） | FAIL：`pnpm build (plugin-web/AgentHub) failed` + `SAFE_DELETE_BULK_CONFIRM_REQUIRED`（vite emptyDir） | `.temp/release-local-run2.log:72-84` Verified |
| 配置残留 | `grep -rn "emptyOutDir: true" Plugins/*/web/vite.config.ts` | 0 命中 | Verified |
| diff 范围 | `git diff --stat -- Plugins/*/web/vite.config.ts` | 15 files, 15+/15- | Verified |
| G2 守卫 | `dotnet test --filter …Pc047…` | Passed（阳性对照 config≥13） | `.temp/pc047.trx` Verified |
| PC 签名 | `pwsh scripts/check-problem-classes.ps1` | OK：37 PC 全通过，EXIT=0 | Verified |
| 台账自洽 | `dotnet test --filter ProblemClassRegistryTests\|…Pc047…` | 6/6 Passed | `.temp/pc047c.trx` Verified |
| emptyOutDir 修复生效 | `pwsh release-local.ps1`（修复后） | AgentHub 构建通过；失败点前移到 AIAgent `pnpm install` | `.temp/release-local-fixed.log` Verified |

## Acceptance Criteria 逐条对应（AC1~AC4 → 证据）
- **AC1**（发布链实跑产出 zip）：**未达成** —— 非仓库 bug，沙箱单次 tool call 删除配额 49 导致 AIAgent `pnpm install` 临时文件清理再次触发 bulk guard（`.temp/release-local-fixed.log:78`，Verified）。emptyOutDir 类根因已消除（AgentHub 构建通过）。环境层解法待用户拍板，已记 TODO（见 07 §13）。
- **AC2**（15 文件全改、grep 0 命中）：**达成** —— `grep -rn "emptyOutDir: true" Plugins/*/web/vite.config.ts` = 0；`git diff --stat` = 15 files 15+/15-（Verified）。
- **AC3**（G2 守卫绿 + 阳性对照）：**达成** —— `Pc047_PluginViteConfigs_MustNotSetEmptyOutDirTrue` OUTCOME=Passed，阳性对照 config 数 ≥13（`.temp/pc047.trx`，Verified）。
- **AC4**（PC-047 签名 check-problem-classes 绿）：**达成** —— `check-problem-classes.ps1` OK 37/37、EXIT=0；Registry+守卫 6/6（`.temp/pc047c.trx`，Verified）。

## Known Limitations
- **完整发布链 zip 未产出**（非仓库 bug）：修复 emptyOutDir 后，发布链在 AIAgent `pnpm install` 清理 `_tmp_*` 时再次触发 `SAFE_DELETE_BULK_CONFIRM_REQUIRED`（`.temp/release-local-fixed.log:78`）。根因 = 沙箱单次 tool call 删除配额 49，15 插件链累计必超限。需环境层解法（拆分多次工具调用 / 临时调阈值 / 换无 shim 环境），已记 TODO，待用户拍板。
- 用户 11:29 外部 `-Sign` 进程（PID 67036）死锁 19 分钟（CPU 恒 0.55s），未触碰（他人会话资产）。
- 既有 `Pc034_DiConstructedPluginServices` 测试假红（PC-034 双文件 `.SingleOrDefault()` 抛错），与本任务无关，已记 TODO。

## Unresolved Issues
- 沙箱配额环境下完整 zip 产出路径（待拍板）。
- PC-034 编号冲突修复（另批）。
