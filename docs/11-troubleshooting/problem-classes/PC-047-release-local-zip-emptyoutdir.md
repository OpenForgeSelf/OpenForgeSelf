---
id: PC-047
title: 插件 web 构建 emptyOutDir:true 触发沙箱 safe-delete 批量删除守卫，本地发布链首插件即中断
tags: [发布链, safe-delete, vite, 插件前端, 构建配置]
invariant: 插件 web 的 vite lib 构建不得设 emptyOutDir:true——vite 清空 dist 的批量删除会撞沙箱 safe-delete 守卫（单次 tool call 删除配额 49），发布链首插件即中断、zip 不产出；一律 emptyOutDir:false（产物固定文件名覆盖写）
status: guarded
first_seen: 2026-11-24
recurrences: []
cases: [2026-11-24-release-local-zip-emptyoutdir]
signature:
  - {id: S1, kind: grep, glob: "Plugins/*/web/vite.config.ts", regex: "emptyOutDir:\\s*true", expect: no-match}
  - {id: S2, kind: test, path: "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs", status: active}
lens:
  - 新增插件 web 的 vite.config.ts 时，build.emptyOutDir 必须与宿主 ForgeSelf.Web/vite.config.ts 一致设为 false，不得用 true 清空 dist。
  - 发布链在本机沙箱报 pnpm build 失败且日志含 SAFE_DELETE_BULK_CONFIRM_REQUIRED 时，先判是 vite 清空 dist（本类）还是 pnpm install 临时文件（另一环境约束，见 case §遗留），不要归为仓库代码 bug。
  - emptyOutDir:false 的前提是产物文件名固定（lib 模式 index.js/style.css）；若改成 hash 文件名须另行处理旧文件清理，不能盲目照搬 false。
---
# PC-047 插件 web emptyOutDir:true 触发沙箱 safe-delete 批量删除守卫

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

**不变量**：插件 web 的 `vite.config.ts` 必须 `build.emptyOutDir: false`。设为 `true` 时，vite 在构建前 `fs.rmSync` 清空 `dist/`；本机 WorkBuddy 沙箱注入的 `node-safe-delete-shim` 把每次删除计入「单次 tool call 配额 49」，超限即以 `SAFE_DELETE_BULK_CONFIRM_REQUIRED` 拒绝 → vite build exit 1 → 发布链 `release-local.ps1` 在首插件中断、zip 不产出。

**为什么会犯**：
- 宿主 `ForgeSelf.Web/vite.config.ts:31` 早已设 `false`（SOP `docs/04-standards/doc-reverse-sync-sop.md:49` 有记录），但 15 个插件从初始模板起就写 `true`，横切遗漏无人扫。
- Vite 默认行为与文档示例倾向 `emptyOutDir: true`，新插件复制配置时自然沿用。
- 本机 shim 是环境层约束，不会在 CI（无 shim）复现 → 靠 CI 守卫发现不了。

## 2 特征签名与扫描方法

- **S1**：grep `Plugins/*/web/vite.config.ts` 中 `emptyOutDir:\s*true` 即红（应为 0 命中）。
- **S2**：G2 枚举测试 `ProblemClassEnumerationTests.Pc047_PluginViteConfigs_MustNotSetEmptyOutDirTrue`，遍历所有 `Plugins/*/web/vite.config.ts` 断言无 `emptyOutDir: true`，带阳性对照（config 数 ≥13）。
- **盲区**：只扫 `vite.config.ts` 字面配置；若改为环境变量或动态注入 `emptyOutDir` 则扫不到，需人工复核。宿主 `ForgeSelf.Web` 由另一 SOP 覆盖，不在本类签名内（已设 false）。

## 3 Canonical fix

单行替换 `emptyOutDir: true,` → `emptyOutDir: false,`（15 个插件全部执行同一修法）。依据：产物为 lib 模式固定文件名（`entryFileNames: 'index.js'`、`assetFileNames: 'style[extname]'`），覆盖写不会产生多版本残留；与宿主 `ForgeSelf.Web/vite.config.ts:31` 一致。

**被否方案**：
- 全局关 `CODEBUDDY_SAFE_DELETE_ENABLED=0` / 调高 bulk 阈值 → 绕过安全守卫、仅本会话有效、CI 无此变量，非仓库侧根治。
- 改 shim 白名单 → shim 在宿主程序内（仓库外），不可改、不该改。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（Pc047…） | `dotnet test` | 动态注入 emptyOutDir 扫不到 |
| G3 | `scripts/check-problem-classes.ps1` S1 签名 | pre-commit / 每次归档校验 | 同上 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| [2026-11-24-release-local-zip-emptyoutdir](../cases/2026-11-24-release-local-zip-emptyoutdir.md) | 2026-11-24 | 发布链首插件 AgentHub 构建被 safe-delete 批量删除守卫拦截，zip 不产出 | 否（首次） |
