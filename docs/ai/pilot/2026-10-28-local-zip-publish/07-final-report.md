# 07 · Final Report — 本地 zip 发布（2.4.16.2610281727）

## 状态

✅ **COMPLETED**（发布动作完成并核对通过；**运行实例只读复验⑤待用户升级后补做**）

## 1. Repository Understanding

- 发布链真源：`docs/04-standards/packaging-upgrade-backup.md` §1.1/§4-R10；操作流程 `.agents/skills/plugin-publish-verify/SKILL.md`；流程规范 `docs/04-standards/ai-native-engineering-workflow.md` v1.2.0。
- 发行串规则：`<major>.<minor>.<patch>.<yyMMddHHmm>`，三段号 + 脚本自动补时间码；本次 **2.4.16.2610281727**（前缀取自 `TODO.md:37` 预定值 2.4.16，时间码 2026-10-28 17:27）。
- 更新源落点真源：`<repo>/../updates/`（= `D:\src\my-proj\OpenForgeSelf\updates`），与运行实例 `update-settings.json` 的 `LocalDir` 一致。
- 工作树状态：本次发布为**工作树快照**，含 6 个批次改动（今日 tool-namespace + 5 个他批）。

## 2. Selected Task

按 `plugin-publish-verify` 主路径（本地目录更新源 + 页面自动更新）发布 2.4.16；发布前必须先解决实测到的两个阻塞（宿主后端半成品态、esbuild 版本错配）。

## 3. Changed Files

| 文件 | 动作 | 性质 |
|------|------|------|
| `scripts/release/patch-esbuild.mjs` | 新增 workaround 2：esbuild 二进制解析**优先本地平台包**，屏蔽机器级 `ESBUILD_BINARY_PATH` 指向的错版本二进制；重生成 `bin/esbuild` shim；本地平台包缺失时保留原 env 覆盖 | **构建链源码修复**（本任务唯一源码改动） |
| `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.live.spec.ts` | 新增：起源码/运行实例只读走查 spec（版本徽标 + 工具表格 + 网关配置 + 工具计数 + 0 控制台错误），截图落盘 | 新增 e2e 用例 |
| `docs/ai/pilot/2026-10-28-local-zip-publish/{mini-task,05-evidence,06-review,07-final-report}.md` | 本任务工件链 | 流程工件 |
| `TODO.md` / `.forgeself/memory/2026-10-28.md` | 登记 / 归档 | 流程台账 |
| `.temp/esbuild/esbuild-{0.25.0,0.25.12}.exe` | 用户输入28 指示：二进制复制进项目内临时目录，`ESBUILD_BINARY_PATH` 指向项目内副本 | 环境产物（不入 git） |

**未改动**：任何宿主/插件业务代码；用户运行实例（`:51888`）；git 历史（无 commit / tag / push）。

## 4. Validation

| 项 | 结果 | 等级 |
|----|------|------|
| 后端编译（宿主 + 测试工程） | 0 个错误 | ✅ Verified |
| 后端过滤集（19 过滤器） | 通过 320 / 失败 **4**（4 例全属并行会话 Anthropic 出站面，非本批） | ⚠️ |
| 宿主 `pnpm run check` / `test` / `design:check` | 0 error / 89 test files passed / 0 退出 | ✅ Verified |
| 插件前端 build（AIAgent 275.26 kB、McpCenter 75.19 kB） | EXIT=0 | ✅ Verified |
| 本插件冒烟 e2e（mcp-center，零 mock，真实后端） | **8 passed** | ✅ Verified |
| 运行时工具命名空间实证（dev-stack 后端日志，GBK 解码） | 127 工具零重名；改名表 **81/81 新名落地**、**81 旧名零残留**；豁免 8/8 在 | ✅ Verified |
| 起源码走查（dev-stack :7399，live spec） | 1 passed，**0 控制台错误**，徽标 v2.7.0，131 工具/116 带前缀 | ✅ Verified |
| 发布链 | `ALL DONE in 146s`，签名新增 2/跳过 1/失败 0，校验通过 3/3 | ✅ Verified |
| SHA256 复核 | 实际 = 清单 ⇒ MATCH | ✅ Verified |
| 签名有效性 | 两个 exe 均 `Status=Valid` + Sectigo 时间戳 | ✅ Verified |
| 布局不变量 L1 / L2 / L3 | 20 条插件清单在版本目录内 / 0 嵌套 / 安装根 0 条 | ✅ Verified |
| 包内内容 = 本批改动 | web/dist 与工作树 SHA256 一致；dll 探针：AIAgent 已无 3 个迁出工具，McpCenter 含 8 个迁入工具 | ✅ Verified |
| 全量 `dotnet test` | 本批未重跑（存量 testhost 崩溃基线，见 `2026-10-28-tool-namespace-ownership` 05-evidence AC-8） | ⚠️ 未跑 |

## 5. Evidence

`docs/ai/pilot/2026-10-28-local-zip-publish/05-evidence.md`（门禁 / 走查 / 发布 / 产物核对四段，含命令与真实输出）。
日志：`.temp/gate-backend2.log`、`.temp/gate-frontend.log`、`.temp/gate-plugin-web.log`、`.temp/gate-e2e-mcpcenter.log`、`.temp/release-2.4.16.log`。
截图：`ForgeSelf.Web/screenshots/live-dev-7399/{mcp-center-walkthrough,mcp-center-gateway}.png`（均已读图）。

## 6. Review

`06-review.md` — 九问全 PASS，Check 四项全 PASS，Risk **L1**，Findings：Critical 0 / Major 0 / Minor 3（均为核对脚本与断言层面，未进仓），**Final Decision = APPROVED**。

## 7. Risk

- **包内含 2 处非本批内容**（工作树快照固有）：5 个未验收批次的改动 + 并行会话 Anthropic 出站面 4 例红测。已向用户逐条交代并获放行。
- **运行实例升级由用户执行**：本地更新源不自动推；agent 不代点、不停/启/杀 `:51888`。

## 8. Problems Found

1. **构建链缺陷（新类）**：机器级 `ESBUILD_BINARY_PATH` 指向 0.25.12 全局二进制 + esbuild `install.js` 把它写死进 `lib/main.js` 与 `bin/esbuild` ⇒ 12 个插件前端（pin 0.25.0）构建**全部**失败，宿主（pin 0.25.12）侥幸不受影响。已修在 `scripts/release/patch-esbuild.mjs`。
2. **并行会话半成品态**：宿主后端在 14:38–14:52 编译不过（CS1503/CS0103），14:55 恢复。⇒ 发布必须等并行会话落定。
3. **核对脚本自身缺陷**：`-like` 误配正则转义导致 L1/L3 误报 0；live spec 连用 3 次错误断言（宿主类名 / 外部 MCP 工具无前缀）。均为一次性脚本/断言问题，未进仓。

## 9. Process Evaluation

- 有效做法：门禁先行 ⇒ 两处阻塞在**出包前**暴露，而不是打出坏包；`patch-esbuild.mjs` 作为既有环境补丁钩子，避免了临时脚本（AGENTS §5.0 红线）。
- 需改进：产物核对脚本应固化进 `scripts/`（本轮写在 `.temp/` 一次性执行）；`-like` 与正则混用需在守卫里禁。

## 10. 问题类归档

本任务**非缺陷类任务**（发布动作 + 构建链环境缺陷），未新增 `docs/11-troubleshooting` case/PC。
构建链缺陷的**台账登记**：`TODO.md`「🔄 进行中」条目已记录根因与修法；按 `problem-class-closure` 的四层模型，属**机制层未沉淀**（该类应进 `problem-classes/`），本轮未做 → 登记为 TODO（见下）。

## 11. 重构提案

1. 把本轮产物核对（SHA256 / 签名 / L1-L3 / 包内 dll 版本）固化为 `scripts/verify-package.ps1`，纳入发布链尾部自检（避免每次现写核对脚本）。
2. `problem-classes/` 增加「构建链环境错配」类目（esbuild 版本错配可作为首个 case）。

## 12. 悬空教训清单

| # | 教训 | 落点 |
|---|------|------|
| 1 | 打包是工作树快照 ⇒ 发布必须在并行会话落定后进行 | TODO.md 本条目 + 07 §8.2 |
| 2 | esbuild 版本错配会让 12 个插件前端构建全挂（宿主同版侥幸不受影响，易误判为"只有插件坏了"） | TODO.md 本条目 + `scripts/release/patch-esbuild.mjs` |
| 3 | 产物核对脚本 `-like` 误用正则转义会误报布局不变量 | TODO.md（固化为 `scripts/verify-package.ps1` 时一并加守卫） |
| 4 | 插件 dll 的 `FileVersion` 不是插件版本的可靠信号（真源是 plugin.json） | TODO.md 既有条目「包内插件版本徽标与实际内容可能不一致」 |
| 5 | 运行实例只读复验⑤须等用户升级后补做 | TODO.md 新条目（发布后复验） |

---

# 第二轮 Final Report — 2.4.17 发布（输入11 · 2026-10-29 00:3x）

## 状态

✅ **COMPLETED**（2.4.17.2610290009 签名包已落本地更新源并全判据核对通过；⑤运行实例只读复验待用户升级后补做，并合并 2.4.16/2.4.13/2.4.12 积压复验项）

## 执行摘要（第二轮）

- **交付**：`OpenForgeSelf-2.4.17.2610290009-win-x64.zip`（161.8 MB，ALL DONE 111s，签名 3/3 校验通过 + Sectigo RFC3161 时间戳，SHA256 复核 MATCH）→ `D:\src\my-proj\OpenForgeSelf\updates\`（更新源仅此一枚 2.4.17）。
- **本批交付物在包内实证**：插件 API/市场 19 个、text-tools 消失；dev-tools 1.1.0（吸收 text-tools）、file-tools 1.2.0（文件三件套+保留名 list_files/read_file/write_file 探针命中）、mcp-center 2.8.0（calculate 裸名豁免，composer 名册实证）、ai-agent 1.9.6（出站 SDK 统一，provider 类型四选项含 OpenAI Responses）。
- **门禁**：中档全量 dotnet test 新增红=0（3 夹具红当场修 + 1 存量 flake 记 TODO）；前端三绿；e2e 17/18（唯一真红=存量 P2 缺陷 folder_stats ACL，用户知情放行）。
- **走查**：dev-stack :7399 七面全绿 + 7 图读图，0 控制台错误。

## 过程缺陷与修复（3 轮发布）

| 轮 | 现象 | 根因 | 处置 |
|----|------|------|------|
| run1 2610282355 | staged=20，包内 plugins/TextTools 复活 | `artifacts/publish/Plugins/TextTools/` 陈旧构建输出（solution 已删项目、publish 不清输出） | 清残留撤包 |
| run2 2610282359 | 仍 staged=20 | **ForgeSelf.Api/obj 的 MSBuild publish 增量缓存**按陈旧清单复制已删插件产物（新生文件时间戳=旧值实锤） | 清 Api obj/bin 撤包 |
| run3 2610290009 | staged=19 | — | **终版合格** |

缺陷包未进更新源、未通知用户（无错误交付）。全部残留与陈旧缓存移 `.trash/2026-10-28-stale-texttools-artifacts/`（可回滚）。

## Changed Files（第二轮）

| 文件 | 动作 | 性质 |
|------|------|------|
| `ForgeSelf.Api.Tests/XCodeConfigTests.cs` | 删 text-tools 陈旧 InlineData（插件已并入 dev-tools） | 夹具跟随（边界批行为变化） |
| `ForgeSelf.Api.Tests/Plugins/ExtensionPointManagerTests.cs` | 假工具名 `Test Tool 1` → 保留名 `list_files`（过新命名空间校验，Id 断言不变） | 夹具跟随 |
| `Plugins/FileTools/FolderStatsToolFunction.cs` | （前批 diff）`folder_stats`→`file_dir_stats` 改名，本批验证 | 验证对象 |
| pilot 工件 05/06/07 + mini-task 第二轮节 | 追记 | 流程工件 |
| `TODO.md` / 日记 | 收口 + 新登记发布链残留治理 | 流程台账 |

**未改动**：发布链脚本（scripts/release/**）、宿主/插件业务代码、用户运行实例、git 历史。

## Risk（第二轮）

1. **file-tools e2e `:232` 红**（存量 P2 缺陷「ACL 拒绝目录→整体 Failed」，2.4.16 同样带着）——已向用户披露并放行，缺陷修复另行立项（TODO 已挂关联）。
2. **包内 `versions/<ver>/publish/` 27 条存量残留**（2.4.16 同款；旧式 .plugin 清单 ×13 含 texttools.plugin；宿主不扫、惰性）——TODO 登记「发布链产物残留治理」，随下一版收敛。
3. 工作树快照含他批未验收改动（固有属性，闸门2.5 已交代）。

## 问题类归档（第二轮）

- 暴露的「**发布链产物残留**」（artifacts staging / Api obj 增量缓存 / 项目根 publish/ 三层不清扫 ⇒ 已删插件产物可复活进包）属**发布链基建缺陷**：本轮完成单例修复-验证闭环，**类级守卫未落地**（staging 后按 slnx 插件项目数校验包内清单数 + package-release sanitize 清 publish/ + 发布前清 Api obj）——已记 TODO P2，触发 scripts/release/** 改动须走中档门禁，专项批处理。
- `ForgeConfigTests` flake（ConfigUnifier 进程级全局态污染）已记 TODO P2（存量，与本批无关）。

## 悬空教训（第二轮新增）

| # | 教训 | 落点 |
|---|------|------|
| 6 | solution 删项目后 `dotnet publish` **不清输出目录**，陈旧插件产物会被打包带走 | TODO 发布链治理条 + 本 07 |
| 7 | MSBuild publish **obj 增量缓存**可让已删项目产物在清输出后**再次复活**（时间戳不变=复制非重建）——清输出不够，须清 obj | 同上 |
| 8 | probe-dll-string 子串语义判不出「改名后旧名 ABSENT」（旧名常成新名子串或类名残留），权威判据=运行名册 | 06-review Minor 5 + TODO 探针增强 |
| 9 | 发布后 `versions/<ver>/` 嵌套 `publish/` 子树（双层 PublishDir 倍增）随包 2 轮了仍未被发现——**包内清单数必须与 slnx 插件项目数对账**，这条对账就是本轮抓到 TextTools 复活的判据 | TODO 守卫提案 |
