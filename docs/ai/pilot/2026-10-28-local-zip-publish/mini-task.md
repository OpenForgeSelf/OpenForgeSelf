---
task_id: 2026-10-28-local-zip-publish
feature: 999-local-zip-publish          # 本地目录更新源发布（主路径）
risk: { level: L1, triggers: ["scripts/release/patch-esbuild.mjs", "docs/04-standards/packaging-upgrade-backup.md"], raised_by_agent: true }
gate1: { mode: user, at: 2026-10-28, ref: "用户输入28「帮我按照技能流程发布本地zip」" }
gate2: { mode: user, at: 2026-10-28, decision: "PENDING" }
gate3: { mode: agent, at: 2026-10-28 }
expected_files:
  - scripts/release/patch-esbuild.mjs
  - docs/ai/pilot/2026-10-28-local-zip-publish/
rollback: "git checkout -- scripts/release/patch-esbuild.mjs（构建链补丁可独立回退，不影响已出包）"
writeback: { feature_doc: none, reason: "本任务为发布动作 + 构建链环境修复，事实回写 packaging-upgrade-backup.md 与 agent-workflow/README.md §B10" }
---

# Mini Task — 本地 zip 发布（2.4.16）+ 构建链 esbuild 版本错配修复

> 级别：**轻量**（合并 Intent/Spec/Plan/Task，Evidence/Review 仍单独产出）。
> 流程依据：`docs/04-standards/ai-native-engineering-workflow.md` v1.2.0；发布动作依据 `.agents/skills/plugin-publish-verify/SKILL.md`（主路径 = 本地目录更新源）。

## 1. Intent（为什么做 / 做什么 / 做到什么程度）

- **Problem**：用户要求「按技能流程发布本地 zip」。执行门禁时实测到两个阻塞：① 宿主后端一度编译不过（并行会话正在改 provider 出站层，工作树处于半成品态）；② 12 个插件前端**全部**构建失败（esbuild 版本错配）。任一阻塞不解决，打出来的包必坏。
- **Why**：本地更新源是「给人装」的交付物（`-Sign` 必带），包内含宿主二进制与全部插件前端 dist；插件前端构建失败 ⇒ 包内 dist 缺失/陈旧 ⇒ 升级后插件白屏。
- **Expected Outcome**：本地更新源目录产出一个 **2.4.16.<yyMMddHHmm>** 的签名 zip，宿主与全部插件前端均可构建，包内插件版本与工作树一致。
- **Constraints**：不打 tag、不推 GitHub；**不停/启/杀用户运行中的宿主**（`:51888`）；`pwsh` 执行所有脚本（禁 `powershell` 5.1）；打包是**工作树快照**，必然带上他批未验证改动 ⇒ 必须向用户交代。
- **Success Criteria**：① 门禁绿（编译 / 单测 / 前端 check+test / 插件 build / 1 条冒烟 e2e）；② zip 产出且 `SHA256SUMS.txt` 一致；③ 签名 `Status=Valid` 且带时间戳；④ 布局不变量 L1/L2/L3 成立；⑤ 包内目标插件 dll 的 `FileVersion` 与本批改动一致。

## 2. Spec（Functional Requirements）

| # | 需求 | 判据 |
|---|------|------|
| FR-1 | 门禁：后端编译 0 错误 + 过滤集绿；宿主 `pnpm run check`/`test`/`design:check` 绿；全部插件 web 可构建；本插件 1 条冒烟 e2e 绿 | 命令真实输出（读日志正文，不拿 exit code 当证据） |
| FR-2 | 构建链修复：esbuild 二进制解析**优先本地平台包**，屏蔽机器级 `ESBUILD_BINARY_PATH` 指向的错版本二进制 | `scripts/release/patch-esbuild.mjs` 增加 workaround 2；host 与插件 web 各跑一次后构建绿 |
| FR-3 | 发布：主路径本地目录更新源，`-Sign`，`-UpdateDir` = 仓库上级 `updates/` | zip + SHA256SUMS.txt + RELEASE-NOTES 落 `..\updates\` |
| FR-4 | 产物核对：zip 布局 L1/L2/L3 + 包内插件 dll 版本 + 签名有效性 + SHA256 复核 | 机器判据（见 §5 AC） |
| FR-5 | 边界：不碰用户运行实例（`:51888`）、不 commit、不打 tag | 无 `Stop-Process`/无 git 写操作 |

**Failure Modes（问题类）**：`docs/11-troubleshooting/problem-classes/README.md` 未检索（本任务非缺陷类，是发布动作 + 构建链环境缺陷）。
**Interaction Design**：本任务无用户可见 UI 改动 ⇒ 不适用。

## 3. Plan（Implementation Steps）

| 步 | 文件 | 动作 | 验证 |
|----|------|------|------|
| S1 | `.forgeself/memory/2026-10-28.md`、`TODO.md` | 落日志 + 建待办 | 读回确认 |
| S2 | `scripts/release/patch-esbuild.mjs` | 增加 workaround 2：本地平台二进制优先 + 屏蔽机器级 override + 重生成 `bin/esbuild` shim | 对 host + 2 个插件 web 跑 patch 后 `vite build` 绿 |
| S3 | `.temp/esbuild/` | 复制 0.25.0 / 0.25.12 两份二进制到项目内并以 `ESBUILD_BINARY_PATH` 指向项目内副本（用户输入28 指示） | `--version` 实测 |
| S4 | — | 门禁：后端 build+test、宿主 check/test/design:check、插件 web build、mcp-center 冒烟 e2e | 日志正文 |
| S5 | — | 起源码 dev-stack 走查 + 截图读图（保持运行不撤） | 截图 + 读图 |
| S6 | — | **闸门2.5**：起源码地址 + 截图交用户，等明确放行 | 用户回复 |
| S7 | — | `release-local.ps1 -Version 2.4.16 -Sign -UpdateDir ..\updates` | ALL DONE + zip 产出 |
| S8 | — | 产物核对（L1/L2/L3 + 包内 dll 版本 + 签名 + SHA256） | 机器判据 |
| S9 | 工件 + TODO + 日记 | 回写 05/06/07、归档待办 | `verify-pilot-artifacts.ps1` PASS |

## 4. Task（工作单元）

**Scope Allowed**：`scripts/release/patch-esbuild.mjs`、`docs/ai/pilot/2026-10-28-local-zip-publish/`、`.forgeself/memory/2026-10-28.md`、`TODO.md`、`docs/04-standards/` 事实回写。
**Forbidden**：改业务代码（宿主/插件）；改用户运行实例；git commit / tag / push；删用户文件。

**Acceptance Criteria**

- [ ] AC-1 门禁全绿（编译 0 错误；过滤集本批域内绿；宿主 check+test+design:check 绿；插件 web build 绿；mcp-center 冒烟 e2e 绿）
- [ ] AC-2 esbuild workaround 2 落地并经真实构建验证（host + 插件）
- [ ] AC-3 起源码走查完成，截图读图，dev-stack 保持运行
- [ ] AC-4 闸门2.5 用户放行（记录放行原话）
- [ ] AC-5 zip 产出 + SHA256 一致 + 签名 Valid 带时间戳
- [ ] AC-6 L1/L2/L3 成立 + 包内目标插件 dll 版本与工作树一致
- [ ] AC-7 向用户交代「工作树快照必然带上他批未验证改动」
- [ ] AC-8 05/06/07 工件齐 + TODO 归档 + 日记回写

**Verification Commands**

```powershell
# 门禁
dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -v m
dotnet test  ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --no-build --filter "<14 组过滤>"
pnpm --dir ForgeSelf.Web run check ; pnpm --dir ForgeSelf.Web run test ; pnpm --dir ForgeSelf.Web run design:check
node scripts/release/patch-esbuild.mjs Plugins/AIAgent/web ; pnpm --dir Plugins/AIAgent/web run build
node ForgeSelf.Web/node_modules/@playwright/test/cli.js test --config=ForgeSelf.Web/playwright.config.ts e2e/plugins/mcp-center/mcp-center.spec.ts --output=<空目录>
# 发布
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/release-local.ps1 -Version 2.4.16 -Sign -UpdateDir ..\updates
```

---

# 第二轮追记 — 2.4.17 本地 zip 发布（输入11 · 2026-10-28 23:20）

> 闸门1（第二轮）：用户输入11 原文「按照技能流程发布本地zip」＝发布动作放行已给（§8.5.4）；版本号用户未指定 → 顺延 **2.4.17**（步2.5 一并向用户确认）。

## Intent（第二轮）

- **Problem**：2.4.16（17:27 发）之后工作树又落了三批未发包改动——① mcp-center 工具豁免（整插件裸名，mcp-center 2.8.0）；② 全类型出站统一 SDK + Anthropic 官方兼容层（宿主 AI 出站面重写）；③ 插件边界批（text-tools 并入 dev-tools 1.1.0、文件三件套迁 file-tools 1.2.0 保留名不变）。2.4.16 包内 DLL 命名已过期（TODO :28 注记）；边界批 07 风险 1（运行实例残留旧 TextTools.dll）正待本版收敛。
- **Expected Outcome**：`updates/` 产出 **2.4.17.<时间码>** 签名 zip；包内**无 TextTools 插件**；dev-tools 1.1.0 / file-tools 1.2.0 / mcp-center 2.8.0 / ai-agent 1.9.6 与工作树一致。
- **Constraints**：同第一轮（`-Sign` 必带、禁停/启/杀运行实例、pwsh、不 commit/tag）；打包=工作树快照（含他批改动）须向用户交代。

## 第二轮 Plan

| 步 | 动作 | 验证 |
|----|------|------|
| R1 | 门禁（中档，宿主源码被触）：build → **全量 dotnet test 独跑** → 前端 check/test/design:check | 日志正文，新增红=0 对表基线 |
| R2 | e2e：mcp-center + file-tools + ai-providers 一轮（`--output` 空目录） | 通过数 |
| R3 | 起源码走查：dev-stack → 新边界界面实证（插件市场无 text-tools、版本徽标、AIAgent composer 名册、mcp-center 裸名、provider 四类型）+ 截图读图，**保持运行** | 截图 + 读图 |
| R4 | **闸门2.5**：地址+截图交用户，确认版本 2.4.17 并放行 | 用户回复 |
| R5 | `release-local.ps1 -Version 2.4.17 -Sign -UpdateDir ..\updates` | ALL DONE |
| R6 | 产物核对：SHA256/签名/L1-L3/包内插件清单（TextTools 缺席 + 四插件版本）+ probe-dll-string（`dev_text_stats` 在 DevTools.dll、`list_files` 在 FileTools.dll） | 机器判据 |
| R7 | 05/06/07 追记 + TODO/日记收口 | verify-pilot PASS |

## 第二轮 AC

- [x] AC-R1 中档门禁绿（全量新增红=0：8 基线红对表 + 3 批次夹具红当场修后复绿 + ForgeConfigTests 1 例存量 flake 隔离复跑绿并记 TODO）
- [x] AC-R2 三 e2e（判定：单 worker 复跑 17/18——ai-providers 6 红全为资源饥饿假红、mcp-center 全绿；file-tools `:232` 1 真红归因**存量 P2 缺陷 TODO「folder_stats ACL」**（SLOW_SCAN_DIR=C:\Program Files 命中 ACL 拒绝目录→整体 Failed），FolderScanService 本批零触碰，闸门2.5 已向用户披露后放行）
- [x] AC-R3 走查截图读图齐（7 张 → `ForgeSelf.Web/screenshots/live-dev-7399/`，关键 3 张已读图：插件市场 19 卡无 text-tools / FileTools 扫描占比闭合 / provider 四类型下拉）
- [x] AC-R4 用户放行 2.4.17（原话：闸门选项「放行，发 2.4.17（Recommended）」，2026-10-28 23:5x）
- [x] AC-R5 zip + SHA256 一致 + 签名 Valid 带时间戳（2610290009 包：SHA256 `3ebb41ea…` 复核 MATCH；两枚 exe Get-AuthenticodeSignature Status=Valid + Sectigo RFC3161 时间戳，脚本内 signtool 校验 3/3）
- [x] AC-R6 L1=19 / L2 嵌套 0 / L3 根级 0 + 包内插件版本与工作树一致（dev-tools 1.1.0 / file-tools 1.2.0 / mcp-center 2.8.0 / ai-agent 1.9.6）+ 新版 TextTools 缺席（plugins/ 下无 TextTools 目录）；DLL 探针：`dev_text_stats`∈DevTools.dll、`list_files`/`read_file`/`file_dir_stats`∈FileTools.dll ✓。⚠️ 存量残留（2.4.16 同款、惰性）：versions/<ver>/publish/ 子树 27 条（旧式 .plugin 清单 ×13 含 texttools.plugin），宿主只扫 plugins/ 两路不受影响，已记 TODO 发布链治理
- [x] AC-R7 工件/TODO/日记收口（本轮完成）

## 第二轮执行记录（2026-10-28 23:20 → 10-29 00:2x）

- **发布执行**：`release-local.ps1 -Version 2.4.17 -Sign -UpdateDir ..\updates` 跑了 **3 轮**：
  - run1 `2610282355`：staged=20 —— 包内 `plugins/TextTools/`（完整 DLL+清单）复活。根因 1：`artifacts/publish/Plugins/TextTools/` 陈旧构建输出（solution 已移除 TextTools，dotnet publish 不清输出目录）→ 清 staging 残留 + 撤包。
  - run2 `2610282359`：staged 仍 20 —— 清了输出但 **ForgeSelf.Api/obj 的 MSBuild publish 增量缓存**按陈旧 ResolvedFileToPublish 清单把已删插件产物再复制回来（新生 TextTools 文件时间戳 = 旧值，实锤为缓存复制非重建）→ 清 Api obj/bin 重打 + 撤包。
  - run3 `2610290009`：staged=**19** ✓，全判据过 → **终版包 = `OpenForgeSelf-2.4.17.2610290009-win-x64.zip`（161.8 MB，ALL DONE 111s）**。缺陷包 2355/2359 已移 `.trash/2026-10-28-stale-texttools-artifacts/`（含 api-obj/api-bin 陈旧缓存与四处 TextTools 残留拷贝，可回溯）。
- **走查（R3）**：dev-stack :7301/:7399 只读走查全绿（token 验真 200 → 插件 API 19 个 text-tools 消失 → 插件市场 19 卡 → AIAgent v1.9.6 composer 名册 read_file/write_file→file-tools、calculate 裸名→mcp-center、dev_text_stats→dev-tools → FileTools v1.2.0 真实扫描 54.46%+45.54%=100% 闭合 → MCP 中心 v2.8.0 → provider 类型四选项），ForgeSelf 页面 0 控制台错误；走查后 `dev-stack.ps1 -Stop` 干净收尾（打包需 bin 锁）。
- **给用户的版本交代**：发行串 **2.4.17.2610290009**（两枚 exe FileVersion/ProductVersion 同串）；更新源只剩此一枚 2.4.17。
