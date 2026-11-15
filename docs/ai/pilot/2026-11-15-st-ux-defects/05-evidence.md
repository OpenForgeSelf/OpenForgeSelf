# 05 Evidence — 智能终端三缺陷实测修复（2026-11-15）

> 只记实际发生的事，Verified / Inferred / Unknown 分级。探针证据已按探索期规则即用即弃，验证结论全部沉淀为可重复测试。

## 1 复现红灯（TDD Red）

| 缺陷 | 红灯测试 | 结果 |
|------|----------|------|
| B（PC-011 逃逸） | `ForgeSelf.Core.Tests` `GbkChineseBytes_CoupledAsValidUtf8_PreferAnsiWhenAnsiYieldsCjk` | ✅ Verified 红：GBK「目录」字节被严格 UTF-8 误读（utf8 采信 → 期望 ansi-936 失败）；对照护栏 `Utf8ChineseWithLatinAccent_KeepsUtf8Verdict` 同轮即绿（真中文+重音不翻案） |
| A（PC-035） | `ForgeSelf.Api.Tests` `StStartDirTests` 5 例（以当时行为落地 StStartDir 后跑） | ✅ Verified 红 3 例：Tilde/TildeWithSubpath/NonRootedLeak；RootedMissing/NullOrEmpty 既有行为已正确故绿 |
| C（PC-036） | `SessionShellCmdCwdTests.Run_AfterCd_ReportsSwitchedCwd`（真实 cmd 进程） | ✅ Verified 红：cd 后返回 Cwd 仍是旧目录 |

红灯日志：`.temp/red-run2.log`（插件 4 失败/2 通过）、`.temp/red-core.log`（1 失败/1 通过）。

## 2 探针事实（实现前定案）

- cmd 交互式展开时机实测：`%CD%`/`%ERRORLEVEL%` 整行解析期展开（命令执行前）；`call echo` + `%%` 转义输出残迹 `%0%`，均不可用 → **两行采集协议**为唯一可靠形态（Inferred→已由集成测试转 Verified）。
- 测试基建逃逸成因（PC-011）：`ToAnsiBytesOrThrow` 主动拒收「对 UTF-8 也合法」的撞车形态（注释"理论上不发生"）→ 新增 `ToAnsiBytesAllowingUtf8Collision` 留档。

## 3 修复（Green）

| 文件 | 动作 |
|------|------|
| `ForgeSelf.Core/ProcessOutputText.cs` | 裁决器 2.5 步怀疑门（可疑拉丁扩展 + 无 CJK + ANSI 往返含 CJK 才翻案）+ `LooksLikeAnsiMisreadAsUtf8`/`ContainsCjk` |
| `Plugins/SmartTerminal/Services/StStartDir.cs` | 新建单一真源：`~`/`~/x` 展开、环境变量展开、脱敏报错（相对路径只回显原文） |
| `Plugins/SmartTerminal/Services/TerminalSessionService.V2.cs` / `TerminalSessionService.cs` | ResolveLocalCwd / V1 创建路径改走 StStartDir；SubmitCommand + ResolveApprovalV2 响应带 `Cwd` |
| `Plugins/SmartTerminal/Services/SessionShell.cs` | cmd 两行采集协议（phase2 `echo {done2}::%CD%` + `ApplyCmdCwd` LastIndexOf）；RefreshCwd 探针 cmd 用 `cd` |
| `Plugins/SmartTerminal/Models/SmartTerminalV2Models.cs` | `SubmitCommandResponse.Cwd` |
| `Plugins/SmartTerminal/web/src/types.ts` / `SmartTerminalView.vue` | `CommandResult.cwd`；run 与批准两条路径回写 `s.cwd` |
| `Plugins/SmartTerminal/plugin.json` | 2.0.1 → 2.0.2 |

## 4 验证结果（快档 + 问题类机器校验）

| 门禁 | 结果 | 等级 |
|------|------|------|
| `dotnet test`（ForgeSelf.Core.Tests 全量） | ✅ 40/40 | Verified |
| `dotnet test --filter SmartTerminalTests` | ✅ 52/52（含 A 5 例 + C cmd 集成） | Verified |
| `dotnet build ForgeSelf.slnx` | ✅ 0 错误 | Verified |
| 插件前端 `pnpm run build` | ✅ dist/index.js 81.70 kB | Verified |
| 宿主前端 `pnpm run check` | ✅ 0 errors（165 warnings 基线） | Verified |
| 宿主前端 `pnpm run test` | ✅ 116 files / 1209 tests | Verified |
| `pnpm run design:check` | ✅ style-tokens PASS + design-compliance PASS | Verified |
| 插件冒烟 e2e `e2e/plugins/smart-terminal` | ✅ 1 passed (1.2m) | Verified |
| `check-problem-classes.ps1` | ✅ OK 36 PC | Verified |
| `ProblemClassRegistryTests` | ✅ 5/5 | Verified |
| 运行实例只读复验（:51888 sidecar 2.0.2 后） | 见 §6 | Verified（走查后补记） |

本批改动面 = 插件源码 + 插件前端 + 插件测试，未触宿主源码/仓库级脚本/共享测试基建/Playwright 配置 → 按 §5.6 跑**快档**，未跑中/深档（基线对账不适用，过滤集全绿）。

## 5 问题类闭环交代

- 复现红灯 → 同类扫查（V1+V2 同类同修；B 的站点面由 PC-011 既有 G2 枚举守卫覆盖）→ 守卫落地（G1×3）→ case/PC 归档：
  - PC-011 逃逸记录（第 3 案，首次真逃逸入运行时）+ 怀疑门修复 + 撞车形态测试留档；
  - 新建 PC-035（起始目录占位符 + 泄露面，延期债到期收口）；
  - 新建 PC-036（cmd cwd 采集三层缺失，含已知边界：cmd exit 恒 0 另行挂账不顺手修）。
- 机器校验：check-problem-classes OK（36 PC）+ Registry 5/5。

## 6 运行实例 live 复验（:51888 sidecar 2.0.2，2026-11-15 实测完成）

- token 鉴权探活：✅ 200（`/api/plugin/detail/smart-terminal`）。
- 版本徽标：✅ `.h1 .ver` = v2.0.2（插件卡片幂等激活分支跳过，2.0.2 已激活）。
- **live spec**：`ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal-ux-defects.live.spec.ts`（playwright.live.config.ts，testMatch `*.live.spec.ts`），终态 **1 passed (14.1s)**，日志 `.temp/live-202-r7.log`。
- **⚠️ 结构性发现（改判 B 的交付边界）**：live 首轮实测撞 B 仍乱码（`Ŀ¼:C:\Users\<用户>` 在页面上）——B 的裁决器在宿主侧 `ForgeSelf.Core`，sidecar 只换插件 DLL 送不到；且不能私有打包 Core（IContext 共享契约劈裂）。裁决与证据 → case §交付形态实测裁决 + packaging 真源 §1.8。live spec B 段按宿主版本门控（≥2.4.23 后自动断言），本轮记录 `[live] 宿主未携带 PC-011 怀疑门修复`。
- **C 第四层补修（live 实测抓到的漏网层）**：前端 blocks 轮询不消费 `pack.prompt` → ②③修复后提示符仍不上屏；补 `s.prompt = pack.prompt` + 重建 dist 同版本热更 → 实测通过。已并入 PC-036 §1/§3（第四层）。
- A（PC-035）✅：默认 `~` 直接创建成功、对话框关闭无报错横幅（截图 live-st-202-dialog-tilde / live-st-202-ps-created，读图核对）。
- C（PC-036）✅：PowerShell 7+ 与 cmd 两会话 `cd \` 后提示符均更新为 `<盘>:\ $`（截图 live-st-202-ps-cd / live-st-202-cmd-cd，读图核对；cmd 块内可见两行协议回显）。
- B（PC-011）：⏸️ live 断言门控待宿主更新；修复本体 Verified（Core 单测红→绿 + 插件集成测试）。
- 探针会话清理：✅ 会话 API close ×5（含前几轮失败运行泄漏会话），remaining open: 0。
- 零 pageerror（favicon 除外）。
