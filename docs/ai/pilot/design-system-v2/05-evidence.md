# Evidence — design-system v2.0.0

> 阶段：Stage 7｜只记录**实际发生过**的验证，逐条标来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。
> 记录时间：2026-09-29（M1–M5 收口轮）

## Task

`PILOT-ds-v2`（`01-intent.md` / `02-spec.md` AC1–AC25 / `03-plan.md` M1–M5 / `04-task.md` 任务索引）

## Changed Files（本轮累计，按层）

**后端（`Plugins/DesignSystem/`）**
- `Data/Model.xml`（新增，12 表）+ `Data/Entities/*`（xcode 生成 24 文件）+ `Data/DesignSystemTables.cs`
- `Services/`：`Oklch.cs`、`ContrastMath.cs`、`TokenGraph.cs`、`TokenRepository.cs`、`DesignProjectService.cs`、`CatalogRepository.cs`、`AuditRepository.cs`、`AuditEngine.cs`、`ColorRampGenerator.cs`、`SemanticResolver.cs`、`ScaleGenerators.cs`、`TypographyGenerator.cs`、`DesignGenerator.cs`、`ExportService.cs`、`ReleaseService.cs`、`BuiltinIcons.cs`、`DesignMapper.cs`、`DesignSystemConstants.cs`
- `Controllers/DesignSystemController.cs`、`DesignSystemPlugin.cs`、`DesignSystem.csproj`、`plugin.json`（2.0.0）
- 宿主：`ForgeSelf.Api/Data/XCodeConfig.cs`（`PluginDbs` 增 `DesignSystem → design-system`）
- 测试：`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/`（9 个文件）+ `ForgeSelf.Api.Tests.csproj` 引用插件

**前端（`Plugins/DesignSystem/web/`）**
- 新增：`src/http.ts`、`src/api.ts`、`src/state.ts`、`src/design/derive.ts`、`src/design/skin.ts`、两份 `*.test.ts`、`src/components/PanelState.vue`、13 个 `src/sections/*.vue`、`tsconfig.check.json`、`package.json`（补 check/test 脚本）
- 重写：`src/DesignSystemView.vue`、`src/sections/{ComponentGallery,TokenShowcase}.vue`
- 下线（移入 `.trash/designsystem-v1/`）：`design/{generate,exporters,presets,schema,storage,colorScale,tokensToCss}.ts`、`scripts/gen-tokens-css.ts`、`sections/DesignStudio.vue`、`sections/{console,marketing}/*`、未使用组件件 7 个

**文档/规范**
- `docs/ai/pilot/design-system-v2/`（00/01/design/02/03/04 + research + 本文件）
- `docs/02-features/036-design-system.md`（新建）、`docs/07-decisions/not-taken-decisions.md`（009 APCA、010 前端第二套实现）、`Plugins/DesignSystem/{README,ROADMAP}.md`（按 v2 事实重写）、`AGENTS.md §2.4`（登记 `design-system-verify`）、`docs/04-standards/agent-workflow.md`（B3/B4 新规律）、`.agents/skills/design-system-verify/SKILL.md`

## Build

Command:

```bash
dotnet build Plugins/DesignSystem/DesignSystem.csproj
dotnet build ForgeSelf.Api
dotnet build ForgeSelf.Api.Tests
```

Result：**PASS**（Verified）

```text
插件：335 warning 全在 Data/Entities 生成物；手写代码 warning = 0（grep 计数验证）
宿主：0 error / 4 warning（宿主既有）
```

## Unit Test（后端）

Command:

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~Plugins.DesignSystemTests"
```

Result：**PASS 163/163**（Verified，2026-09-29 09:12，2m46s，跳过 0）

```text
已通过! - 失败: 0，通过: 163，已跳过: 0，总计: 163
覆盖：Store/Graph/Oklch/Contrast/ColorGeneration/GenerationAudit/ExportProjection/ReleaseSnapshot/BuiltinIcon/Auth
```

## Unit Test（插件前端，跑在宿主 vitest 入口）

Command: `cd Plugins/DesignSystem/web && pnpm run test`

Result：**PASS 20/20**（Verified）

```text
Test Files 2 passed (2)｜Tests 20 passed (20)（derive.test.ts + skin.test.ts）
```

## Static Analysis

Command: `pnpm run check`（宿主 vue-tsc + `tsconfig.check.json`）

Result：**PASS**（Verified，`error TS` 计数 0）；`pnpm run build` → `dist/index.js 248KB / dist/style.css 52KB`（Verified）

已知限制：eslint 仍覆盖不到 `Plugins/*/web/src`（宿主 flat config 的 `files` 不能越出宿主 `src/**`）→ 记 TODO，不宣称已达成。

## Integration / E2E

Command: `pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system`（globalSetup 自动 publish 宿主 + 起前端 + 注入真实 token，数据在 `.temp/e2e/<ts>/publish/Data`）

Result：见"最后一跑"（本文件底部回填）。已 Verified 的过程事实：
- 插件 v2 产物被宿主远程加载（`index.js` + `style.css` 均 200）；
- 建项目 → 生成：`generate/preview` 返回 seed=6a37f62dc6fcec5f、共享层 227 条、light/dark 各 23 条，且**预览未写库**（tokenCount 保持 0）；
- `② 确认写入` 后后端返回"令牌 273 / 审计 total 179 / critical 0 / warning 1"；
- 令牌工作台一行显示 `5.28:1（aa）`+`oklch-ramp` → 对比度是后端真算的（此前是写死 -1）。

前两跑的失败与修复（都是真失败，不是环境问题）：
1. 项目列表"令牌数 0"（`TokenCount` 缓存列无人维护）→ 改为读取时现算 + 新增回归测试；
2. 编辑器 Enter 不保存（只有"保存"按钮有处理器）→ 补 `@keyup.enter` + e2e 改点按钮。

## Screenshots（e2e 产物，逐张读图核对）

`ForgeSelf.Web/screenshots/e2e/design-system/`：`01-shell-loaded` / `02-generated` / `03-token-studio` / `04-colorlab-dark` / `05-audit` / `06-export` / `07-release` / `08-icons` / `09-components` / `10-showcase` / `11-archived` + `design-system-v2.log`（网络/控制台证据）。
读图结论见 `06-review.md` 的 Findings（最后一跑完成后回填）。

## AC 对照（哪些有证据、哪些没有）

| AC | 状态 | 证据来源 |
|---|---|---|
| AC1 表结构 + xcode 无漂移 | ✅ Verified | 两次 `xcode Model.xml` 产物 `diff -r` 逐字节一致；`BindColumn` 集合比对无漂移 |
| AC2 冷启建表 12 张、不吞异常 | ✅ Verified | 本轮 e2e 宿主日志：`创建数据表：DesignAsset,…,DesignToken`（12 张）+ 失败路径记 Error |
| AC3 CRUD + 批量 + 唯一冲突 409 | ✅ Verified | xUnit `DesignSystemStoreTests` + e2e 走 UI 建项目后 API 复核 |
| AC4 环/悬空 → 400 且零行落库 | ✅ Verified | xUnit 事务回滚断言 count 不变 |
| AC5 确定性同参数同输出 | ✅ Verified | xUnit 幂等重生 + 种子写入每行 `GeneratorSeed` |
| AC6 oklch 色阶（L 单调/中调峰值/暗端不灰） | ✅ Verified | `ColorGenerationTests`（含种子逐位锚定） |
| AC7 对比度定向选 tone | ✅ Verified | 6 个色相 × light/dark 全角色达标断言 |
| AC8 非色令牌随参数变化 | ✅ Verified | 差集断言（不同比例/密度值集合不相等） |
| AC9 排版模块化+fluid+motion reduced+shadow 分层一致 | ✅ Verified | `GenerationAuditTests` 层配对 + 导出展开测试 |
| AC10 WCAG 比率数学与判级 | ✅ Verified | `ContrastMathTests` + OKLab 参考值钉死 |
| AC11 审计分类齐 + critical 拦发布 409 | ✅ Verified | `ReleaseSnapshotTests.存在未通过的critical审计_禁止发布` |
| AC12 投影格式齐 + 合法 data.sql + 复合展开真值 | ✅ Verified | `ExportProjectionTests` 12 格式 + 新增"不得导出空声明" |
| AC13 参考物形状一致且不暴露物理表名 | ✅ Verified | 形状断言 + 表/列名白名单断言 |
| AC14 快照不可变 + diff 五类 | ✅ Verified | `ReleaseSnapshotTests` 12 项（含改值/改 hex/改别名/新增/下架/主题集合） |
| AC15 控制器鉴权 + 无 token 401 | ✅ Verified | `DesignSystemAuthTests` 反射断言 |
| AC16 section 库驱动、无 fixture、无死按钮 | ✅ Verified | 13 section + grep 无 SRE fixture + 硬编码样式 grep 空；console/marketing 演示页整体下线 |
| AC17 主题微调保存后刷新仍生效 | ⚠️ 部分 | **口径已改**：无"未落库草稿"态（微调=参数重生成，落库即事实）；reload 持久化由 e2e 步 16 覆盖。ThemeLab 不做 L-C-H 写面板（决策见 03-plan 偏差） |
| AC18 手改保护 + 冲突回报 | ✅ Verified | xUnit + `skippedProtected/conflicts` 现已回给界面（此前静默跳过不可见） |
| AC19 无硬编码设计值 / BrandLogo 取项目名 / 无 example.com | ✅ Verified | grep（本轮跑过，样式硬编码 0 命中）+ plugin.json 复核 |
| AC20 前端 check/test/build 全绿 | ✅ Verified | 见上（"TS 与 C# 黄金值同表"一项按 not-taken 010 取消） |
| AC21 dotnet 构建+测试全绿且跨插件无串扰 | ✅ Verified（DesignSystem 范围） | 带 verbose logger 实跑 **164/164**（= `--list-tests` 发现数）；**全量 `dotnet test` 仍不稳定**（测试主机崩溃 + 4 项非本任务失败）→ 记 TODO，未当作已修 |
| AC22 插件层 e2e 全链路 | ✅ Verified | 连跑两跑全绿（17 组断言 / 45~85s），见下"最终跑" |
| AC23 本地 zip 发布 + 页面自动更新 | ✅ Verified（包侧） | `release-local.ps1 -Version v2.3.0 -UpdateDir artifacts/update` 385s 完成；哈希自洽 + 产物内容探针 FOUND（见下"AC23"节）。**页面点「重启并更新」属用户动作，agent 未停/启宿主** |
| AC24 文档回写一致 | ✅ Verified | README/ROADMAP 矛盾项重写、`docs/02-features/036-design-system.md` 新建、skill 登记 |
| AC25 走查截图 + 测试数据清理（软删） | ✅ Verified | 11 张截图 + 读图（见"读图结论"）；数据用唯一项目码 + 归档软删，不删库 |

## 最终跑（2026-09-29 M5 收口）

### 插件层 e2e（真实宿主 + 真实库，零 mock）

命令：`pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system`（cwd `ForgeSelf.Web`）
结果：**连续两跑 `1 passed`**（1.3m / 1.4m），来源等级 **Verified**。真实输出摘要（用例自己写的证据行）：

```text
project=e2e-… id=1
plugin.json Version=2.0.0 meta.modelVersion=2.0.0
light.surface-bg=#f6f6f9 dark.surface-bg=#211f25
light.space.4=8px compact.space.4=6px
release=1.0.0 hash=866a713ab856… tokens=1181
内置图标=40 导出CSS长度=12870
预览容器背景 浅色=rgb(246, 246, 249)（期望 rgb(246, 246, 249)）深色=rgb(33, 31, 37)（期望 rgb(33, 31, 37)）
```

换肤这一环本轮改成**逐位比对**：预览容器计算样式底色 == 后端 `semantic.surface-bg` 的 hex→rgb，
且切主题时先确认页脚主题码已变、再确认注入的 CSS 头部注释变成 `theme=light|dark`——
不再用"背景色看起来变了"这种弱判据。

### 读图结论（`ForgeSelf.Web/screenshots/e2e/design-system/`，11 张）

| 截图 | 结论 |
|------|------|
| 01-shell-loaded | 外壳中性主题、导航三组齐备、版本徽标与 meta 一致；无破图 |
| 02-generated / 03-token-studio | 生成结果与令牌表可读；改值后回读一致 |
| 04-colorlab-dark | 暗色档色阶条带 11 档连续，无档位缺口 |
| 05-audit / 06-export / 07-release | 审计分类、导出预览、快照哈希均显示真实数字 |
| 08-icons | 内置图标 40 枚线稿一致（同 stroke 规格），无占位方框 |
| 09-components | 6 个组件样张 + TOKENREFS 清单；首轮读图发现按钮样张文字重复（"按钮 按钮"）→ 已修并重跑 |
| 10-showcase | 暗色档品牌页：hero + 色阶 + 令牌计数，无溢出（步 14 断言 ≤2px） |
| 11-archived | 归档后列表状态正确（取消路径未改后端，确认路径才改） |

### 本轮由"真跑"新揪出并修掉的缺陷

1. **生成不落组件目录**：`generate` 只写 `component.*` 令牌，`DesignComponent` 表 0 行 → "组件库"对刚生成的项目永远显示"0 个组件"。
   修：`DesignGenerator.SeedComponentCatalog`（清单取本次真实生成的路径，缺令牌就不建目录）+ 控制器接入 + xUnit 断言"目录引用的每条令牌在库里真存在"。
2. **取色器空值告警**：`<input type="color">` 在种子色不是 hex 时被塞空串，浏览器每帧告警（e2e 控制台里 8 条）。
   修：形状不合就不渲染该输入框（虚线占位），并把"不得再出现该告警"写成 e2e 断言。
3. **换肤别名可指向未定义变量**：后端 CSS 未到位时别名块仍写出 `var(--ds-semantic-…)`，整条声明在 computed-value 阶段失效 → 预览容器全透明。
   修：别名只引用扫出来的真实变量（`definedVars`），CSS 为空则两段都不注入；`skin.test.ts` 加回归用例。
4. **宿主 SQLite 并发 BUSY 冒泡成 500**（见 `06-review.md` Risk 与 TODO）：`code = Busy (5) / database is locked`
   命中过 `AuditRepository.Record→Entity.Update`、`DesignProjectService.Find`（只读）、`tokens/effective`。
   已做：派生连接串补 `Busy Timeout=5000`（宿主日志确已带上；库头两字节=2 确认 WAL）；插件只读请求退避重试（写请求不重试）；
   e2e 客户端同样只对 Busy 重试。**根因在宿主 DAL，未由本插件闭合，已记 P1 TODO。**

### 后端门禁（计数口径已查明：quiet logger 会假绿）

同一命令、同一程序集，换 logger 就换结论：

| 命令 | 报告 | 真相 |
|---|---|---|
| `dotnet test --filter "FullyQualifiedName~Plugins.DesignSystemTests"`（默认 logger） | 失败 0 / 通过 **85** | **假绿**：测试主机中途崩溃，已跑条数被当总数报 |
| `dotnet test --filter "FullyQualifiedName~DesignSystem"`（默认 logger） | 失败 0 / 通过 **154**，日志夹 `测试主机进程崩溃` | 同上，结果被截断 |
| 同命令 + `--logger "console;verbosity=normal"` | **测试总数 164 / 通过 164**，无崩溃 | 与 `--list-tests` 发现数一致 → 这才是真数 |

规则已沉淀（`agent-workflow.md` B4 + `design-system-verify` §一）：本仓跑 `dotnet test` 一律带 verbose console logger，
并核对"总数 == 发现数"，不等就是事故、不得当通过。

插件前端：`pnpm run check` 0 error、`pnpm run test` 23/23、`pnpm run build` → `dist/index.js 249.28 kB`、`dist/style.css 52.28 kB`（Verified）。

### AC23 本地 zip 发布（2026-09-29 18:41，Verified）

命令：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/release-local.ps1 -Version v2.3.0 -UpdateDir artifacts/update`
结果：`release-local: ALL DONE in 385s`，**未打 tag、未停/启任何用户宿主进程**。

| 校验项 | 判据 | 结果 |
|---|---|---|
| 产物齐备 | zip + SHA256SUMS + RELEASE-NOTES 同时落 `artifacts/release` 与 `artifacts/update` | ✅ 71.8 MB / 99 B / 3.1 KB |
| 哈希自洽 | `sha256sum` 实测 == `SHA256SUMS.txt` 内记录 | ✅ `3e10fb42…edadfa`（`-c` 因该文件是 CRLF 而报读取错，逐字比对通过） |
| 插件新鲜度 | zip 内 `Plugins/DesignSystem/{DesignSystem.dll,plugin.json,web/dist/index.js,style.css}` 时间戳为本次构建 | ✅ 18:41 / 18:38 |
| 新实现真进包 | `scripts/probe-dll-string.cjs` 探 `SeedComponentCatalog` | ✅ FOUND（DesignSystem.dll） |
| 前端同源修复真进包 | 打包后的 `index.js` 里含 `definedVars` 的变量扫描正则 | ✅ FOUND（`--ds-[\w-]+`） |
| 宿主那一行真进包 | 探 `ForgeSelf.dll` 里 `Busy Timeout=5000` | ✅ FOUND |

> 探针与解包全部在仓库外 `$TEMP/dsz` 完成，用完即删；结论本身由**打包产物内容**支撑，不靠"脚本跑成功"。
> 页面侧「检查更新 → 下载 → 重启并更新」是用户动作（agent 不得代停宿主），本轮只交付到"可更新的包 + 校验和"。



## Known Limitations

1. 导出体积（大项目 `bundle` / `stardust-sql`）未实测 → T302/U1 仍开放。
2. 插件 src 无 eslint 入口（工具链归属待决策）。
3. `plugin.json` entry 缓存键无 content-hash（宿主加载器职责，同版本改动需硬刷）。
4. 全量 `dotnet test` 在本仓当前不稳定（测试主机崩溃 + 4 项既有失败）；本交付以 DesignSystem 过滤集为准，且**不声称全量绿**。
5. **矩阵厚度受令牌面限制**：格子只能由真实 `component.*` 令牌反推。已补尺寸轴（sm/md/lg）与 focus/disabled 态令牌；
   仍无对应令牌的组合（如 ghost/link 角色）界面如实不显示，不塞假行。
6. **e2e 的绿是"带只读重试的绿"**：宿主 SQLite 并发 BUSY 未根治（G8），去掉重试仍可能出现 500。
   本轮另加两件根治性缓解：审计/目录**整批一个事务**、发布按项目**串行化**。
7. ~~测试计数口径未统一~~ **已查明**：默认 logger 在测试主机崩溃时把"已跑条数"当总数报（假绿）；
   带 `--logger "console;verbosity=normal"` 后为 164/164，与 `--list-tests` 发现数一致。规则已入 B4/技能。

## Unresolved Issues

- 宿主 DAL 并发锁治理方案（写串行化 / 统一重试 / 逐库 PRAGMA）需人拍板 → TODO P1。
- `POST /api/design-system/projects` 同码被投递两次的现象：与"两个并发写互相撞锁"高度相关，
  发布路径已按项目串行化；投递来源（代理重放 / 前端双发）仍未定位 → TODO P3，不猜结论。

## v2.1.0 增量（2026-09-29 12:5x，组件规格与变体矩阵）

| 门禁 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 后端 | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"` | **测试总数 164 / 通过 164 / 失败 0** | Verified |
| 插件前端 | `pnpm run check` / `test` / `build` | 0 error / 23/23 / `index.js 251.82 kB`、`style.css 53.55 kB` | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system` | `1 passed (1.1m)`，含新断言：`.cg__card>=10`、`.cg__vars li>=20`、矩阵文本含 `size=(sm|md|lg)`、`GET components/button/variants` 回读到 `hover` 且每格挂真令牌清单 | Verified |

新增的真实内容（不是重排既有东西）：

- 组件目录 6 → **10**：新增对话框 / 提示浮层 / 选项卡 / 选择器，每个都带 a11yNotes（2.1.2 焦点陷阱、1.4.13 可键盘触达等）。
- 组件层令牌：尺寸轴 `button/input.{sm,md,lg}.{padding-block,padding-inline,radius,min-height}`
  （padding/radius 全部别名到既有 `space.*`/`radius.*`，只有 min-height 是字面值 28/34/42px，且 ≥ WCAG 2.5.8 的 24px）
  + `input.border-focus`、`select.border-focus`、`button.<role>.{background,foreground}-disabled`、`nav/table` 的 active 态。
- 变体矩阵：`SeedComponentCatalog` 落 `DesignComponentVariant`，格子 = (命名空间段 → 轴, 叶子后缀 → 状态)，
  轴键由 `AxisOf()` 分 `size / role / variant`，认不出不猜；无令牌支撑的组合不落格。
  测试判据：格数 ≥30、`sm<md<lg` 单调、按钮含 default/hover/active/disabled 且**不含** focus（按钮没有焦点背景令牌，造出来就是假的）、输入框含 focus。
- 写路径根治：审计落库与组件目录**整批一个事务**；`ReleaseService.Create` 按项目串行化（`ProjectGates`）。
- 版本：`2.0.0 → 2.1.0`（plugin.json + 三常量），并把测试里钉死版本号的字面量断言换成"清单 == 后端模型版本"的自洽判据。
- 交付包：**v2.3.1**（`release-local.ps1 -Version v2.3.1 -UpdateDir artifacts/update`，367s，不打 tag、不动宿主）。
  包内容实测：`Plugins/DesignSystem/DesignSystem.dll` 探到 `SeedComponentCatalog` 与 `2.1.0`；
  `ForgeSelf.dll` 探到 `Busy Timeout=5000`；zip 实测 sha256 `6e1f275a…920a` 与 `SHA256SUMS.txt` 逐字一致（71.8 MB）。

## v2.2.0 增量（2026-09-29 21:3x–23:5x，M7 品牌三表可写 + 取数竞态 + M8 交付闭环）

### 门禁（只列真跑过的）

| 门禁 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 后端（DesignSystem 范围） | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"` | **170 / 170 / 失败 0**（日志 `.temp/ds-m8c.log`；中间一跑是 169/1，红的那条正是本轮新用例） | Verified |
| 全量后端 | `dotnet test ForgeSelf.Api.Tests --logger "console;verbosity=normal"` | **总数 1653 / 通过 1641 / 失败 12**（日志 `.temp/dotnet-test-full-215329.log`）；其中 **2 项本轮造成**（已修并转绿）、8 项非本任务（根因已定位，见下） | Verified |
| 插件前端 | `pnpm run check` / `test` / `build` | 0 error / **46/46** / `index.js 272.18 kB`、`style.css 56.31 kB` | Verified |
| 宿主前端 | `pnpm run check`（`vue-tsc -b` + eslint）/ `pnpm run test` | eslint **0 error**（81 warning 为存量）/ vitest **515/515** | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system` | 竞态修复前 **1 failed**（换肤那步）→ 修复后**连跑两跑 passed（1.1m/1.1m）**；M8 改完 CSS 投影后再跑 **1 passed (1.3m)** | Verified |
| 读图 | `screenshots/e2e/design-system/` | 新增 `11-brand-assets.png`（表单控件已带样式、页面清单 4 行真数据）、`10b-skin-rapid-switch.png`（连点三次后停在浅色且样张跟随）；读图查出 1 条 P3（图标 code 被 `uppercase` 改大小写）→ TODO | Verified |

### 这一轮新增的真实内容（不是重排既有东西）

- **品牌三表从"只有 GET"变成可写 + 有种子**：`SeedBrandCatalog` 落字体（逐字体栈成员，许可证写明"系统提供、不随产物分发"）、
  起手屏（按行业倾向，描述写明"建议不是产品事实"）、logo + 母题两枚原创 SVG；新增 `POST projects/{id}/assets|screens|fonts`
  与第 14 个 section「品牌资产」（写完立即回读自证）。e2e 断言"读得到 + 写得进"两条：界面登记一条页面 → API 回读到 `/e2e-brand`。
- **重跑生成只补空不覆盖**（跑起来才暴露的缺陷）：原先 `SeedBrandCatalog` 无条件 upsert，会把用户换过的 logo / 改过的路由 /
  登记的可分发字体许可证写回去 = 删用户数据。改为按自然键跳过已存在行，用例 `重跑生成只补空_不得覆盖用户改过的品牌行` 钉住。
- **样式类完整性守卫** `web/src/design/classes.test.ts`（19 条）：守 `ds-*` 共享词汇表类必须有定义。
  它抓到的是真缺陷：`BrandAssets.vue` 用了 `ds-btn--ghost`/`ds-btn`/`ds-input`，而这三个类只存在于别的 section 的 `<style scoped>` 里
  ——`vue-tsc` 与 `vite build` 全绿，页面却渲染成浏览器默认控件。守卫自带"反例自证"用例，防止自己空转。
  踩坑记录：vitest 默认 `css:false` 会把 `.css` 的 `import.meta.glob('?raw')` stub 成空串 → 一片假红，故改为读盘。
- **手改保护回执接上**（自查表第 10 条问的那件事）：`skippedProtected`/`conflicts` 后端一直回、类型注释写着"必须显示"，界面从未渲染 → 已渲染。
- **生成结果的目录条数可见**：后端早已回 `components/variants/fonts/screens/assets`，但 TS 类型没这几个字段、界面也没显示 → 补显示并如实标注"库里现存条数"。
- **取数竞态守卫**（本轮唯一的"一红一绿"根因）：`loadSkin`/`loadEffective` 是"await 完直接赋值"，
  快速切主题时**后到的旧主题响应会覆盖新主题** → 界面档位已到 light、预览仍是 dark。
  先写 `state.test.ts` 用受控 promise 复现（修复前 3 failed：`expected 'css-light' to be 'css-dark'`），再加"只有最新一次调用可写回 ref"的序号守卫 + `reset()` 作废旧请求。
  e2e 侧另加真实环境守卫：连点 浅色→深色→浅色 不等待，断言最终投影 = `theme=light` 且底色逐位等于 light 令牌。
- **M8：品牌三表进导出投影**：CSS 只对登记了文件的字体出 `@font-face`（系统栈成员以注释列出 + 写明不分发）；
  bundle 落 `brand/logo.svg`、`brand/motif-grid.svg`（补 `viewBox="0 0 24 24"` 外壳，品牌图形与图标库共用 24 网格）、`brand/fonts.json`、`brand/screens.json`；
  DESIGN.md 增「品牌与资产」段（三表全空则不开这一节）；包内 README 加目录行 + 来自库里的计数行。
  跑测试才暴露的缺陷：默认 JSON 序列化把中文许可证转义成 `\uXXXX`，等于把合规答案锁在编码里 → 改 `UnsafeRelaxedJsonEscaping`。

### 全量测试 12 项红的拆分（不许混着报）

- **本轮造成 2 项**：`XCodeConfigTests` 两条逐字断言连接串，被 `Busy Timeout=5000` 撑长 → 改成"以绝对数据根路径开头 + 必须含 `Busy Timeout=`"
  （保住原语义：路径绝对性不许松，同时把宿主修复本身钉住），隔离跑已 17/17 绿。
- **非本任务 8 项，根因已定位**（详见 TODO.md，未擅动别人的测试基建）：
  ① 7 项 404 = 测试项目 staging 只拷 `*.dll` 不拷 `plugin.json`/`deps.json` → 插件控制器没进路由表（`c7941a0` 引入的回归）；
     顺带查出**一条假绿**：`GetExecutionStatus_NonExistentId_ShouldReturnNotFound` 之所以过，是因为路由缺失也返回 404。
  ② 1 项 = 断言大小写脆（守卫本身 `IgnoreCase` 正确，reason 里嵌的是解码原文）。
  ③ `ForgeConfigTests` 只在并发跑时红 = `DAL.ConnStrs` / `Config<T>.Provider` 等进程级静态被多个测试类各自注册（顺序依赖）。

### 未做 / 未核验（不粉饰）

- 整包 `bundle` 在**浏览器侧**仍取不到 zip：`page.evaluate(fetch)` → `Failed to fetch`；改点 `<a download>` → 事件到了但 `download.path: canceled`。
  已排除"太慢"（后端构建 605ms）。用户指示先不搞，续做入口写在 TODO.md（绕开 vite 代理直连宿主端口比对；再看是否该流式返回）。
  ⇒ 因此**"用户点下载"这条路径目前没有任何证据**，README 里记为缺口 G10。

### 交付包 v2.3.2（2026-09-29 23:58，`release-local.ps1 -Version v2.3.2 -UpdateDir artifacts/update`，110s，不打 tag、不动宿主）

- 产物：`artifacts/release/OpenForgeSelf-2.3.2-win-x64.zip` 75,312,603 字节（同时落在 `artifacts/update/`）。
- **SHA256 逐字一致**：`669f54327cc6634f2dbf39cf6553f4059a2b167c6c134d491c76767fc3fcb527`
  与包旁 `SHA256SUMS.txt` 完全相同（注意：该文件是 CRLF，`sha256sum -c` 会假报错，比对用逐字法）。
- **包内容实测**（解到仓库外临时目录，探针用仓内 `scripts/probe-dll-string.cjs`，用完即删）：
  - `Plugins\DesignSystem\DesignSystem.dll`（495,104 B）→ `SeedBrandCatalog` / `AppendFontFaces` / `SaveAsset` / `2.2.0` 全部 FOUND；
  - `Plugins\DesignSystem\plugin.json`（887 B）→ `"Version": "2.2.0"`；
  - `Plugins\DesignSystem\web\dist\index.js`（272,178 B）→ `ds-btn--ghost`、`组件目录`、`库里现存条数`、`品牌资产`×3 均在（M7 品牌段 + 竞态修复后的构建产物）；
  - `ForgeSelf.dll`（1,004,032 B）→ `Busy Timeout=5000` FOUND（宿主那一行仍在包里，等用户复核）。
- 踩坑记录（可复用）：这个 zip 的条目名用 **反斜杠** 分隔，`unzip` 会警告 "appears to use backslashes as path separators" 且通配匹配失败；
  改用 PowerShell `System.IO.Compression.ZipFile` 按 `FullName` 精确取条目即可（本次验证就是这么做的）。

## v2.3.0 增量（2026-09-30 00:1x–00:3x，M9：版本化覆盖品牌与组件）

| 门禁 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 后端（DesignSystem 范围） | `dotnet test --filter ~DesignSystem --logger "console;verbosity=normal"` | **175 / 175 / 失败 0**（`.temp/m9-all.log`；版本升到 2.3.0 后复跑仍 175/175，`.temp/m9-ver.log`） | Verified |
| 插件前端 | `pnpm run check` / `test` / `build` | 0 error / **46/46** / `index.js 274.95 kB`、`style.css 56.58 kB` | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system` | **1 passed (57.2s)**，含 M9 新断言：界面写入的页面在两版 diff 的 `specsAdded` 里、且对比面板 `.rb__specs` 显示 `screen <code> · 新增` | Verified |
| 读图 | `11b-release-spec-diff.png` | 两版哈希不同（`cfad0960ee26` vs `d226a1d6bfe9`）而令牌数都是 1476 → **规格确实进了哈希**；面板显示 合计 1 / 品牌与组件规格 +1 | Verified |
| 交付包 | `release-local.ps1 -Version v2.3.3 -UpdateDir artifacts/update`（110s） | SHA256 `06c2734846cb…26f796` 与 `SHA256SUMS.txt` 逐字一致；包内 `DesignSystem.dll` FOUND `ReleaseSpec` / `SpecsComparable` / `BuildSpecs` / `2.3.0`，`plugin.json` = `2.3.0` | Verified |

新增能力（不是重排）：快照 schema **1 → 2**，`specs` 一节覆盖 `component/variant/asset/screen/font`（`kind + key + 字段表`）；
**规格进哈希**（否则只换 logo 会被判"与上一版一致"而幂等放行）；schema 1 旧快照比对回 `specsComparable=false`，
界面显示"不可比"而不是凭空"新增 N 条"；内置图标库（ProjectId=0）明确不进快照。
后端 5 条新用例：快照含五类规格 / 换 logo 同版本必须被拒 + 新版本 diff 报到 `asset/logo.svgBody` /
未变时零假变更 / 旧快照不可比 / 加一条起手屏要报成新增。

踩坑记录（本轮真实付出）：
- 用 Python 改版本号时以 `utf-8-sig` 写回，给 `plugin.json` 与 `web/package.json` **加了 UTF-8 BOM** —— 已剥回；
  教训：批量改文本要用 Edit 工具或显式 `encoding='utf-8'`，BOM 会污染清单解析路径（e2e 里那句"剥 BOM"的防御代码就是为这类事写的）。
- e2e 里 `getAttribute('value')` 取不到 Vue `:value` 绑定的选项值（那是 DOM 属性不是 attribute）→ 选不中版本、按钮一直禁用、点击超时；
  改成 `evaluate(el => el.value)` 并加 `toBeEnabled` 前置断言，失败信息直接指向"版本没选上"。
- 按钮文案是「对比」不是「逐项比对」——按文案找控件前先 grep 模板，别凭记忆。

## v2.4.0 增量（2026-09-30 01:0x–01:2x，M10：组件规格供给进机器可读产物）

| 门禁 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 后端（DesignSystem 范围） | `dotnet test --filter ~DesignSystem --logger "console;verbosity=normal"` | **178 / 178 / 失败 0**（三元组升 2.4.0 后复跑仍 178/178；日志 `$TEMP/ds_m10c.log`、`ds_m10d.log`，逐条数与"测试总数"核对一致） | Verified |
| 后端全量 | `dotnet test ForgeSelf.Api.Tests` | 1665 总数 / **1655 通过 / 10 失败**，10 项**全在非本任务**（见下"全量红项拆分"） | Verified |
| 插件前端 | `pnpm -C Plugins/DesignSystem/web run check` / `test` / `build` | 0 error / **46/46** / `index.js 274.95 kB`、`style.css 56.58 kB` | Verified |
| 宿主前端 | `pnpm -C ForgeSelf.Web run check` | 0 error / 81 warning（既有），含新改的 e2e spec 类型检查 | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system` | **1 passed (2.2m)**（最终码 2.4.0 重跑；日志 `$TEMP/ds_e2e_m10b.log`） | Verified |
| 读图 | `09-components.png` | 版本徽标显示 `模型 2.4.0 · 生成器 2.4.0 · 投影 2.4.0`；组件库标题"10 个组件 · 数据来自后端库"；每卡有 TOKENREFS 芯片与"变体 × 状态矩阵 (n)"（card=1、input=6）；无溢出/无空态 | Verified |
| 交付包 | `release-local.ps1 -Version v2.4.0 -UpdateDir artifacts/update`（131s，不打 tag、不动宿主） | `OpenForgeSelf-2.4.0-win-x64.zip` 71.8 MB；SHA256 `102fa2773f2172f3dcb17ab39a6732854b37e466fb4ccfd28d63b20191d37bf9` 与 `SHA256SUMS.txt` 逐字一致 | Verified |
| 包内容实测 | 解到仓库外临时目录 + `scripts/probe-dll-string.cjs`（用完即删，已清） | `DesignSystem.dll`（524,288 B）→ `EntityRows` / `ToStardustEntity` / `AppendComponentSpecs` / `unresolvedTokenRefs` / `2.4.0` 全 FOUND；`plugin.json`（869 B）= `"Version": "2.4.0"` 且无 BOM；`web/dist/index.js`（274,953 B）与 `style.css`（56,581 B）为当轮构建；`ForgeSelf.dll` → `Busy Timeout=5000` 仍在（等用户复核） | Verified |

### 这一轮真正修掉的三类"声明与供给分叉"

1. **registry 只有令牌、没有规格** → 条目键集改为「组件目录 ∪ `component.*` 分组」，`meta` 带
   `anatomy` / `a11yNotes` / `states` / `variants[]`（每格 `axes` + 令牌清单）/ `unresolvedTokenRefs`。
   实测（真实宿主）：`button` 解剖=3、状态=active/default/disabled/hover、格子=13、尺寸轴 sm/md/lg 都在。
2. **DESIGN.md 的"组件"节只有两句通用提醒** → 增 `### 组件规格`，逐蓝本列解剖/状态/变体轴/令牌条数/可达性要求；目录为空不开节。
3. **spec FR13 从没实现**：清单里十类实体的 `url` 指向的路由不存在，且 `CountFor` 另算一套 ——
   `design-component` 数的是组件层**令牌**条数、`design-icon`/`design-screen`/`design-font-face` 写死 0（库里明明有行）。
   现在 `GET api/design-system/{id}/{entity}.json` 真存在，且清单 `total` 与明细 `data[]` 同出 `EntityRows`。
   实测十类行数：`design-color=141 design-shadow=4 design-motion=13 design-type-role=17 design-spacing=10 design-radius=8 design-icon=40 design-component=10 design-screen=5 design-font-face=14`。

后端新增 4 条用例：`Registry_组件条目把规格带出来_且令牌引用逐条可核对` / `Stardust清单声明的行数_明细端点必须真给得出同样多` /
`实体明细不泄漏物理表形状_未知实体如实报错` / `DESIGN_md_组件节按蓝本列出解剖状态轴与可达性要求`；
并把 `ExportProjectionTests` 夹具补上 `SeedComponentCatalog` + 组件/变体/图标三张表的 `Cache.Expire=0`（不种数据就无从验证"供给得出来"）。

### 全量红项拆分（10 项，全部非本任务，根因都已在 TODO 有条目）

- 7 项 = **插件控制器 404**（`WorkflowPlanningIntegrationTests` 6 + `ScriptRunnerDiIntegrationTests.GetRuntimes_ShouldResolvePluginControllerThroughCordisContext`）：
  测试 staging glob `Plugins\**\*.dll` 不拷 `plugin.json` → `PluginManager` 跳过无清单目录 → 路由没进表。隔离重跑仍红 ⇒ 与本轮改动无关（属测试基建，放宽 glob 需用户点头）。
- 1 项 = `ForgeConfigTests.ForgeSetting_配置文件归一到数据根Config目录`：**隔离重跑通过**，只在并发全量里红（`FileConfigProvider` 进程级静态被先跑的测试定值）。
- 1 项 = `TerminalCommandGuardTests.Check_EncodedCommand_DestructivePayload_Rejected`：断言脆（大小写），判定本身没错。
- 1 项 = `ScriptRunnerTests.CancelAsync_ForRunningScript_ShouldCancelExecution`：既有计时依赖红。
- DesignSystem 范围 178/178 全绿。

### 本轮踩的坑（可复用）

- **`ParseStringArray` 用 `GetValue<String>()` 会崩**：调用方把 `componentIdsJson` 塞成 `[123]`（数字）时抛 `InvalidOperationException`（不是 `JsonException`），
  整个导出被一条元数据带崩。改用 `n?.ToString()`——"能读成什么就是什么"。
- **单测夹具里没有插件生命周期 → 内置图标库是空的**：`design-icon` 断言 `>0` 假红。
  修法不是在投影里放水，而是在用例里真登记一条图标（`SaveIcon`）再断言，顺带证明"有行就出得去"。
- **Edit 工具替换整块时把相邻方法的注释首行吃掉了**（e2e 里 M9 那段）：改完必须回读 diff，别只看编译过不过。

## v2.5.0 增量（2026-09-30 01:3x–01:5x，M11：Element Plus 换肤接缝，收 spec U3 / 缺口 G13）

| 门禁 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 后端（DesignSystem 范围） | `dotnet test --filter ~DesignSystem --logger "console;verbosity=normal"` | **181 / 181 / 失败 0**（新增 3 条用例；日志 `$TEMP/ds_250_be.log`，逐条数==测试总数） | Verified |
| 后端全量 | `dotnet test ForgeSelf.Api.Tests` | 1668 总数 / **1659 通过 / 9 失败**，9 项全在非本任务（`$TEMP/full_250.log`）：WorkflowPlanning 6 + `ScriptRunnerDiIntegrationTests`（同"插件控制器 404"家族）+ `ForgeConfigTests`（隔离重跑即绿，并发才红）+ `TerminalCommandGuardTests`（断言大小写脆）。上一跑是 10 红，多的那条是 `ScriptRunnerTests.CancelAsync`（既有计时依赖，本轮自己过了）；总数 1665→1668 正是本轮新增 3 条 | Verified |
| 插件前端 | `pnpm -C Plugins/DesignSystem/web run check` / `test` / `build` | 0 error / **46/46** / `index.js 274.95 kB`、`style.css 56.58 kB`（本轮未改前端源码，产物同上一轮） | Verified |
| 宿主前端 | `pnpm -C ForgeSelf.Web run check` | 0 error / 81 warning（既有），含新改的 e2e spec | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system` | **1 passed (1.4m)**；证据行 `element-plus 接缝 102 个变量 / 引用 132 条令牌（全部由 tokens.css 定义）`，并断言导出中心出现 `element-plus` 卡片 | Verified |
| 读图 | `06-export.png` | 主题 `dark`、`投影版本 2.5.0 · 格式 13`、徽标 `模型/生成器/投影 2.5.0`；卡片预览文本头部即 `projection 2.5.0｜generator 2.5.0`；无溢出 | Verified |
| 交付包 | `release-local.ps1 -Version v2.5.0 -UpdateDir artifacts/update` | `OpenForgeSelf-2.5.0-win-x64.zip`；SHA256 `f4f4e9729e312b38d83bf30070e021b7b9c685b13e61ec6ebed04d810620e758` 与 `SHA256SUMS.txt` 逐字一致 | Verified |
| 包内容实测 | 解到仓库外临时目录 + `scripts/probe-dll-string.cjs`（用完即删，已清） | `DesignSystem.dll`（534,016 B）→ `ToElementPlus` / `ElColorBase` / `DefinedCssVars` / `element-plus` / `2.5.0` 全 FOUND；`plugin.json`（897 B）= `"Version": "2.5.0"` | Verified |

新增能力：**第 13 个导出格式 `element-plus`**（+ bundle 内逐主题 `element-plus/theme.<theme>.css`）。
它把 `--el-*` 接到本设计系统的 `--ds-*` 上 —— 宿主界面就是 Element Plus 写的，这是"为系统进行设计"第一次能直接落到被服务的系统身上（U3 从"留个接口位"变成有产物）。
四条口径：右侧零字面色值；引用必须能在**同主题** `tokens.css` 里解析（否则如实列"未映射 N 项"并指名缺哪条令牌）；
`light-N`/`dark-2` 照 EP 的比例但把混合目标换成本页底色 / 正文墨色；`--el-color-white/black`、`--el-index-*`、`--el-transition-all/-fade*` 有意不映射并写明理由。

### 本轮被测试抓到的真缺陷（不是重构，是修错）

`color-mix(in oklab, var(--ds-semantic-brand) 70%, --ds-semantic-surface-bg)` —— 第二个"颜色"写成了**裸自定义属性名**，
它不是 `<color>`，整条声明在 computed-value 阶段失效（表现：EP 组件静默掉回默认色，与换肤预期相反）。
本仓在预览换肤上已踩过同族坑（别名指向未定义变量 → 预览全透明）。修法：混合目标一律包成 `var(--ds-…)`；
并加断言 `NotMatchRegex(@"color-mix\([^;]*,\s*--ds-")` 把这条形状钉住。

### 本轮踩的坑（可复用）

- **Edit 整块替换会吃掉相邻行的内容**：本轮两次（一次把 `AppendFocusVisible` 尾部三行删掉，一次把 README 的 G10 行首删掉）。
  教训：`old_string` 只圈住要改的最小片段；改完必须看回读结果，而不是只看编译过不过。
- **断言要区分"声明行"与"注释"**：`NotContain("--el-index-")` 被"有意不映射"说明段里的键名假红；
  改成先按行取出 `--el-*:` 声明再断言，语义才准。

## v2.6.0 增量（2026-09-30 02:0x–02:2x，M12：门禁必须对"用户手改之后"成立，缺口 G14）

| 门禁 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 后端（DesignSystem 范围） | `dotnet test --filter ~DesignSystem --logger "console;verbosity=normal"` | **185 / 185 / 失败 0**（新增 4 条；`$TEMP/ds_260_be.log`；中途一次 183/185 是两条新维度抓到我自己的错，见下） | Verified |
| 插件前端 | `pnpm -C Plugins/DesignSystem/web run check` / `test` / `build` | 0 error / **46/46** / `index.js 275.87 kB`（+0.92 kB = 四类新类别说明）、`style.css 56.58 kB` | Verified |
| 宿主前端 | `pnpm -C ForgeSelf.Web run check` | 0 error / 81 warning（既有），含新改的 e2e spec | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system` | **1 passed (1.5m)**；新步 9b：把 `component.button.sm.min-height` 改 18px → 跑审计必须出现 target-size warning；改回 28px → 该 warning 必须消失 | Verified |
| 读图 | `05b-audit-target-size.png` / `05-audit.png` | 改坏时"共 177 条，critical 0 / **warning 2** / info 116，通过 59"；改回后"**warning 1** / 通过 60" —— 唯一变化就是那一条 target-size（余下 1 条 warning 与本轮无关，两跑之间它没动）；门禁口径表 11 类齐全且带中文说明 | Verified |
| 交付包 | `release-local.ps1 -Version v2.6.0 -UpdateDir artifacts/update` | `OpenForgeSelf-2.6.0-win-x64.zip`；SHA256 `99eee8ee0e13b54b582d1ea926f3d908788d7a72bc2debc5caf65fbad976ab45` 与 `SHA256SUMS.txt` 逐字一致 | Verified |
| 包内容实测 | 解到仓库外临时目录 + `scripts/probe-dll-string.cjs`（用完即删，已清） | `DesignSystem.dll`（539,648 B）FOUND `CheckTargetSize` / `CheckRamp` / `CheckLifecycleRefs` / `ramp-monotonic` / `lifecycle-ref` / `2.6.0`；`plugin.json`（897 B）= 2.6.0；`web/dist/index.js`（275,869 B）含新类别文案 | Verified |

新增能力：审计从 7 类扩到 **11 类**，四类新维度盯"库里的现值"而不是"生成器算得对不对"：
`target-size`（`*.min-height` ≥24px，WCAG 2.2 2.5.8）、`ramp-monotonic`（space/radius/duration 按声明序必须递增）、
`naming`（path 点分 kebab）、`lifecycle-ref`（仍被引用的 deprecated/removed）。**四类一律 warning 不拦发布**：
2.5.8 自带例外条款，升成 critical 会误伤，而"假警报一次就废掉整套门禁的信任"是本仓既有纪律（036 §审计门禁口径）。
档序不另列一份 —— 直接读 `ScaleGenerators.SpaceSteps/RadiusSteps/DurationSteps`（本轮把三张表从 private 提为 public）。
密度轴主题会覆盖尺度值，所以尺度单调性在 density 主题上单独复查；尺度消息报完整路径（`space.5=9px → space.6=2px`）而不是只报档名。

### 新维度一上线就抓到的两处自己的错（先红后绿）

1. **命名正则把生成器自己的 `z-index.1` 判成不合形**：第一版写成 `^[a-z0-9]+(\.[a-z0-9][a-z0-9-]*)*$`，
   首段不允许连字符 → `z-index` 不合法。教训已写进技能：**新加判据必须先断言"生成产物自己必须过"**，
   否则新维度第一次上线就是一片假红，之后没人再看它。
2. **尺度消息只报档名**：`space. 族在 6 处不再递增（5=9px → 6=2px）` 读起来要用户自己拼路径；
   改成报完整 `space.5=9px → space.6=2px`，断言随之收紧为 `Contain("space.5")`。

### 本轮踩的坑（可复用）

- **`OnlyContain` 对空集合会失败**：预检查"生成产物全部 info"时用了 `passed=false` 过滤，结果集为空 → 假红。
  要断言"全部通过"必须用 `passed=null`（不过滤）再 `NotBeEmpty + OnlyContain`。
- **Edit 整块替换第三次吃掉相邻声明行**（这次是 `[Fact] public void 复合阴影令牌的展开层与真源成对落库()`）：
  编译立刻炸，说明"改完必回读 diff"仍是必须动作，而不是可选项。

### v2.6.1 补记（自查抓到的一条"静默空跑"门禁）

`ramp-monotonic` 的数值解析第一版只认 `dimension + px`，而 `duration.*` 是 `duration` 类型、值带 `ms` →
时长族那条判据**从来没跑过**，界面上的门禁口径却写着"space / radius / duration 三类"。
- 先写反例用例 `尺度单调性对时长族也必须真的生效_不能是静默空跑`（把 `duration.macro` 压到 100ms 必须报）→ **红**（`Expected hit not to be <null>`），确认缺陷真实存在；
- 修：`ValueOf` 同时支持 dimension/duration/number，并把原文一并带回做显示（`duration.base=200ms → duration.macro=100ms`）→ **绿**；
- 门禁重跑：后端 DesignSystem **186/186**（`$TEMP/ds_261_be.log`）；后端全量 1673 / **1664 通过 / 9 失败**（`$TEMP/full_261.log`，9 项仍全在非本任务：插件控制器 404 家族 7 + `ForgeConfigTests` 并发才红 + `TerminalCommandGuardTests` 断言脆；总数 1672→1673 正是新增那条时长反例用例）、插件 check/test/build（`index.js 275.87 kB`）、宿主 check 0 error、e2e **1 passed (1.6m)**；
- 重打包：`OpenForgeSelf-2.6.1-win-x64.zip` SHA256 `826db91ce4217f57e010630704c3eb745a71c84a02cece847ff23acc745e1f31` 与 `SHA256SUMS.txt` 逐字一致；
  包内 `DesignSystem.dll` FOUND `ValueOf`/`CheckRamp`/`2.6.1`，`plugin.json` = 2.6.1。
  **v2.6.0 的包作废**（同版本换内容会砸"同版本同输出"的复现契约），`artifacts/release` 里两份都在，更新源按最高版本选 2.6.1。
- 教训入册：新判据不能只断言"生成产物通过"，**必须再造一个反例证明它真的会响**（技能第 24 条 ④）。

## 补验：EP 接缝的**功能**证据（2026-09-30 03:0x，未改产物 → 不升版本号）

v2.5.0 只证明了"接缝文件写得对"（正则 + 引用可解析）。本轮补一条能量化的因果证明：在真实宿主页面里放一个
`.el-button--primary` 探针，注入 `tokens.light.css` + `element-plus.light.css`，读 EP 自己用来上色的那条变量。

| 门禁 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 宿主前端 | `pnpm -C ForgeSelf.Web run check` | 0 error / 81 warning（既有） | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system` | **1 passed (1.0m)**（新增步 10c） | Verified |

实测数字（e2e 证据行原文）：
`EP 接缝实测 --el-color-primary：#F59E0B → #6d28d9（目标 #6d28d9）；组件层 --el-button-bg-color：#F59E0B → #6d28d9；最终 background-color=rgb(245, 158, 11)`

- **成立的部分（断言钉住）**：注入后 `:root` 的 `--el-color-primary` 与 EP 组件层的 `--el-button-bg-color` 都从**宿主自带主题的琥珀色**变成我们的品牌色，
  且注入前必须是琥珀（有对照才成立）。⇒ 接缝确实流到了 EP 的组件层，并盖过了宿主自己的主题文件。
- **不成立的部分（如实记录，不假装通过）**：探针最终 `background-color` 仍是琥珀。诊断枚举（递归进 `@layer`/`@media`）只抓到
  `.el-button{...}` 与 Tailwind preflight 的 `button, input, select, optgroup, textarea, ::file-selector-button{...}` 两条命中规则（均被截断），
  未定位到具体那条硬写背景的规则。结论：**宿主存在不读变量的按钮背景规则，任何主题（含宿主自己那份）都盖不过它** ——
  属宿主侧样式，已记 TODO P3；本产物的承诺边界改写在 README/036 里（"把变量喂进 EP 组件层"，不是"覆盖宿主所有硬写背景"）。
- 过程记录：这条断言第一版写的是"最终像素必须等于品牌色"，跑两次都红；**没有把断言悄悄删弱**，
  而是改成"变量层断言 + 像素层记录"，并把为什么写进注释与文档（技能第 23 条加 ④）。

## v2.6.2 增量（2026-09-30 03:2x–04:0x，M12c：对比度门禁的覆盖面 · 豁免 · 结论新鲜度）

### 改了什么（依据：WCAG 2.2 1.4.3 原文 + 本仓"宁少勿假"纪律）

1. `contrast` 的判定对象从**写死的八对清单**改为**按命名约定从库里推导**（`AuditEngine.ComponentPairsOf`）：
   `component.<ns>.foreground[-状态]` → 同命名空间 `.background[-状态]` → `.background` → `.tint[-状态]` → `.tint`，再补推不出来的显式对（placeholder / nav / tabs 的 hover 底）。
2. 禁用态按 **WCAG 1.4.3「非活动界面构件」豁免**：`AddContrast(…, exempt)` 未达 AA 时 `rule=wcag22-1.4.3-exempt`、
   判级退回 1.4.11 的 3.0（≥3.0 = info / <3.0 = warning），**永不 critical**，消息写明豁免依据。
3. `AuditRepository.Record` 整批写入后**清掉本轮未产出的旧行**（同 scope 内），使"一次 Run 的产出 = 该 scope 的全部结论"。

### 三条缺陷都是这套改动自己抓到的（不是用户报的）

| # | 缺陷 | 怎么抓到的 | 处置 |
|---|---|---|---|
| 1 | 生成器自己产出的 `dialog` / `tooltip` / `select` 前景从未被审计查过；用户新增组件直接绕过门禁 | 新用例断言"同命名约定的对必须一起被查到" → **红**（`Expected checkedPaths … to contain "component.dialog.foreground"…`） | 改为按约定推导 → **绿** |
| 2 | 推导一上线就在自家生成产物里报 **9 条 critical**（按钮禁用态实测 3.41:1），砸掉 8 条 ReleaseSnapshot 前置 + 1 条门禁用例 | 全量跑 DesignSystem 用例即红 | 按规范原文豁免（只报读数不拦发布），**没有把断言改弱**：新增反例证明 3.0 兜底线仍会响 |
| 3 | `Record` 只覆盖本轮产出的键 → 某条对不再被判定时，上一轮 `Passed=true` 留在库里，而 `HasBlocking` 读的就是它 | 写反例时 `Single(...)` 抛 `Sequence contains no matching element`（因为库里是**上一轮**的行） | 清旧行 + 用例 `这一轮没判成的对象_不得留着上一轮的通过行冒充查过`（v2.6.3 扩成 `声明成color却解析不出颜色的令牌_必须自己报无法判定_也不许留旧通过行`，见下节） |

过程记录（教训已入技能第 25 条）：反例第一版把禁用态前景别名到 `component.button.primary.background-disabled`，
`UpsertBatch` 返回 `Updated=0` —— 组件层令牌按主题分层存储，dark 下那是一条 **Created** 而不是 Updated；
断言因此改成 `Created + Updated == 1` 并把 `Diagnostics` 打进 because 文案：**反例必须先证明前提真的发生了**，否则测的是没发生的事。

### 门禁实测（全部 Verified，实跑；日志逐条核对，不看 exit code）

| 门禁 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 后端 DesignSystem | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem"` | **190/190 通过、失败 0**（落盘日志 `已通过` 行数 190 == 汇总"通过数: 190"） | Verified |
| 后端全量 | `dotnet test ForgeSelf.Api.Tests` | 1677 总计 / **1667 通过 / 10 失败**（上一轮基线 1673/1664/9，+4 全是本任务新用例） | Verified |
| 插件前端 | `pnpm -C Plugins/DesignSystem/web run check` | 0 error | Verified |
| 插件单测 | `pnpm -C Plugins/DesignSystem/web run test` | **46 passed**（4 files） | Verified |
| 插件构建 | `pnpm -C Plugins/DesignSystem/web run build` | `dist/index.js 275.94 kB`、`style.css 56.58 kB` | Verified |
| 宿主前端 | `pnpm -C ForgeSelf.Web run check` | 0 error / 81 warning（既有） | Verified |
| 插件层 e2e | `pnpm -C ForgeSelf.Web exec playwright test e2e/plugins/design-system` | **1 passed (1.2m)**，新增步 9c | Verified |

全量那 10 项红的拆分：**9 项与上一轮同一批**（插件控制器 404 家族 7 = `WorkflowPlanningIntegrationTests` 6 + `ScriptRunnerDiIntegrationTests` 1；`ForgeConfigTests` 1；`TerminalCommandGuardTests` 1），
**新增 1 项** `ScriptRunnerTests.CancelAsync_ForRunningScript_ShouldCancelExecution`：`Expected Status to be Cancelled {4}, but found Timeout {5}` —— 属宿主 ScriptRunner 插件，本任务未触碰该面。
根因（只读定位，Verified）：`Plugins/ScriptRunner/Services/ScriptExecutor.cs:146-152` 的 `CancelAsync` 已把行写成 Cancelled，
但 `:316-335` 的后台收尾按**唯一判据 `externalCancellationToken.IsCancellationRequested`** 再写一次同一行；走 `CancelAsync` 时该 token 是 `None`，于是恒判 Timeout
（`ExecuteCodeAsync` 的 `TimeoutSeconds=300`（`:107`）让"真超时"在本用例里不可能发生 ⇒ Timeout 只能是误标），
且两次写之间没有"已终态不覆写"保护 → 谁最后落库谁赢，只有负载高时才翻。已记 TODO P2（另立任务修，不在本任务范围内顺手改宿主插件）。

### e2e 现场证据（真实宿主 + 真实后端，零 mock）

- 覆盖面：`对比度结论 58 行 / 前景对覆盖 29 个 / 禁用态豁免 6 行（无 critical）` —— 改之前这一维只有写死的八对；
- 豁免不拦发布：审计板 `总计 197 / 通过 76 / CRITICAL 0 / WARNING 5 / INFO 116 / BLOCKING 可发布`，同一次 run 里 `release=1.0.0 hash=d226a1d6bfe9… tokens=1476` **发布成功**
  （= 禁用态那 6 条确实没参与阻断，是"读数在案但放行"，不是被删掉的通过）；
- 界面口径同步：门禁表 `contrast` 行显示"…未达 = critical；禁用态按 1.4.3「非活动构件」豁免，只报读数不拦发布"（e2e 断言该行必须含 `1.4.3`）；
- 版本自洽：`plugin.json Version=2.6.2 == meta.modelVersion=2.6.2`，投影头部 `projection 2.6.2｜generator 2.6.2`；
- 截图读图（`05-audit.png`）：徽标三值一致、门禁口径 11 行无截断、汇总卡与 contrast 分组表头对齐，无溢出/遮挡。

### 发布产物（本地 zip，按用户指令不打 tag）

| 项 | 结果 | 等级 |
|---|---|---|
| 打包 | `scripts/release/release-local.ps1 -Version v2.6.2 -UpdateDir artifacts/update` → `OpenForgeSelf-2.6.2-win-x64.zip`（71.8 MB）+ `SHA256SUMS.txt` + `RELEASE-NOTES-2.6.2.md` | Verified |
| 哈希自洽 | `SHA256SUMS.txt` 记 `3540d57aab7b1ee688883900597b719281e78b8fcfd8ff110b8297645f588708`，`sha256sum` 实算**逐字一致** | Verified |
| 包内代码 | `Plugins/DesignSystem/DesignSystem.dll` FOUND `ComponentPairsOf`、FOUND `-exempt`、FOUND `1.4.3 豁免非活动构件`、FOUND `2.6.2`（`scripts/probe-dll-string.cjs`，UTF-16LE 探针） | Verified |
| 包内清单 | `Plugins/DesignSystem/plugin.json` → `"Version": "2.6.2"` | Verified |
| 包内界面 | `Plugins/DesignSystem/web/dist/index.js`（275,944 B = 构建产物 275.94 kB）FOUND `非活动构件` ⇒ 门禁口径的新文案确实随包发出 | Verified |

探针教训（照旧记一条）：`wcag22-1.4.3-exempt` 在二进制里 **ABSENT** 不是"没发出去"，而是该串由插值 `rule + "-exempt"` 拼装 —— 二进制字符串探针只能命中**字面量**，改探 `-exempt` 即 FOUND。
解包与探针都在仓库外临时目录跑，跑完已删除（`%TEMP%/ds262probe` 不存在）。

## v2.6.3 增量（2026-09-30 04:0x–04:2x，M12c 的第二半：判不成的对象必须自己报"无法判定"）

v2.6.2 让 `Record` 清掉本轮未产出的旧行，堵住了"旧绿灯冒充今天查过"。但当天自查就把这条记进了 TODO（P3）：
**对象还在库里、只是这轮判不成**（声明为 color 却解析不出颜色，例如别名指到 `space.4`）时，它会从审计里静默消失 ——
界面上既不见红也不见黄，与"查过且没问题"仍然一模一样。按「登记过的规则要补做不是排队」，本轮不当队列，当场补做。

- **先红**：把 `这一轮没判成的对象…` 改写成 `声明成color却解析不出颜色的令牌_必须自己报无法判定_也不许留旧通过行`，
  跑单条 → **红**（`Expected row not to be <null> because 判不成就静默消失 = 界面上看不出这里有个没判成的对象`），缺陷真实存在；
- **后绿**：`AuditEngine.CheckColorResolvable` —— 遍历本主题图里 `$type=color` 的节点，`Resolve` 成功但 `SeedColorOrNull` 解析不出颜色的，
  落一条 `kind=contrast / rule=wcag22-1.4.3-unresolved / severity=warning / passed=false`，带可执行建议；
  **别名本身就失败的（Missing/Cycle/…）不在这里报**，仍由 `alias` 报 critical，避免同一对象两处记账互相矛盾。
  颜色解析复用 `ColorRampGenerator.SeedColorOrNull`，不另写一份 hex/oklch 判定。
- **新判据先断言"生成产物自己过"**（技能 24①）：同一用例里先断言刚生成的系统 `…-unresolved` 行为 0 条，再造反例。
- **界面口径同步**：`AuditBoard` 的 `contrast` 门禁口径补"声明为 color 却解析不出颜色 = warning（无法判定）"；
  README / ROADMAP P1.14 / `036` / 技能 25③ 同步；文档里指向已改名用例的引用一并修（旧名 `这一轮没判成的对象…` 已不存在）。
- **e2e 步 9c 扩三段**：`component.card.foreground` 别名到 `space.4` → 跑审计必须出现 `unresolved`；改回 `semantic.text-1` → 必须消失。

### v2.6.3 门禁实测（全部 Verified，实跑 + 读落盘日志）

| 门禁 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 后端 DesignSystem | `dotnet test --filter "FullyQualifiedName~DesignSystem"` | **190/190**（先红后绿：红项为 `Expected row not to be <null>`） | Verified |
| 后端全量 | `dotnet test ForgeSelf.Api.Tests` | 1677 总计 / **1667 通过 / 10 失败**，与 v2.6.2 基线同一批；`grep -i designsystem` 命中失败行 **0** 条 | Verified |
| 插件前端 | `pnpm -C Plugins/DesignSystem/web run check` / `test` / `build` | 0 error / **46 passed** / `index.js 276.01 kB`（口径文案变长所致） | Verified |
| 宿主前端 | `pnpm -C ForgeSelf.Web run check` | 0 error / 81 warning（既有） | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system` | **1 passed (1.4m)**；步 9c 三段（`audit?kind=contrast&passed=false` 轮询三次：出现 → 消失） | Verified |

同一轮 e2e 证据行不变：`对比度结论 58 行 / 前景对覆盖 29 个 / 禁用态豁免 6 行（无 critical）`
⇒ 新判据在**干净生成产物**上是 0 条（没有制造噪声），只有真把令牌写成非颜色时才响。

### v2.6.3 发布产物（本地 zip，不打 tag）

| 项 | 结果 | 等级 |
|---|---|---|
| 打包 | `release-local.ps1 -Version v2.6.3 -UpdateDir artifacts/update` → `OpenForgeSelf-2.6.3-win-x64.zip`（71.8 MB）+ `SHA256SUMS.txt` + `RELEASE-NOTES-2.6.3.md` | Verified |
| 哈希自洽 | `SHA256SUMS.txt` = `db28e2ddcdde9f4ddf2bbac192282dbe479df77118e94f620ad410a4b66bd215`，`sha256sum` 实算**逐字一致** | Verified |
| 包内代码 | `DesignSystem.dll` FOUND `CheckColorResolvable` / `-unresolved` / `对比度无法判定` / `2.6.3` | Verified |
| 包内清单 | `plugin.json` → `"Version": "2.6.3"` | Verified |
| 包内界面 | `web/dist/index.js` 276,012 B（= 构建 276.01 kB）FOUND `无法判定` | Verified |

解包与探针在仓库外 `%TEMP%/ds263probe` 跑完已删除。v2.6.2 的包**不作废**（它是当时的完整内容），更新源按最高版本取 v2.6.3。

## v2.6.4 增量（2026-09-30 04:3x–05:0x，M12e：顺序也收成一份真相）

TODO 里那条 P3（"DESIGN.md / registry 里轴值与状态按字母序"）与 v2.6.2/2.6.3 同族：**声明了序，却没有序的真源**。本轮闭合。

- 新增 `DesignSystemConstants.VariantAxes`：`size` xs→xl、`role` primary→link、`state` default→hover→active→focus-visible→focus→disabled→pressed；
  `OrderOf / Has / Rank / Sort` 四个入口，表外的值一律退回字母序（没证据的顺序不编造）。
- 消费方全部改读它：`DesignGenerator.StateSuffixes`（由词表派生，故矩阵生成序不变形）、`AxisOf`（认轴）、
  矩阵 `SortOrder` 编号、`ExportService` 的 DESIGN.md 状态串 / registry `meta.states` / Stardust 实体 `states` / `AxisSummary` 轴值与轴键。
- `GET /meta` 增 `stateOrder` / `sizeOrder`：前端 `ComponentGallery.statesOf` 改按后端词表排，**删掉 `.sort()`**；
  读路径 `CatalogRepository.ListVariants` 不再按 `State` 字母序重排，改按落库 `SortOrder`。

### 这一轮是 e2e 当裁判（断言没有被改弱）

新断言"界面状态序 == DESIGN.md 状态序"第一次跑就红：
`Expected: "default、disabled、hover、active"`（界面，来自变体优先枚举的首现序）
`Received: "default、hover、active、disabled"`（产物，来自我刚加的词表排序）。
处置不是把断言改成"集合相同即可"，而是把**另外两处**（读路径、前端）也拉回同一张表 —— 三方一致才是这条声明成立的样子。

### 门禁实测（Verified 项已实跑；全量与 zip 见本节后补）

| 门禁 | 结果 | 等级 |
|---|---|---|
| 后端 DesignSystem | **192/192**（最终代码重跑；新增 `产物里的档位与状态必须按档位序_不是字母序凑的` 与 `矩阵读序按变体分组_组内按状态档位序_用户补的格子不许跳到最前`；落盘日志 `已通过` 行数 192 == 汇总通过数 192，失败 0） | Verified |
| 插件前端 | check 0 error / vitest 46 passed / build `index.js 276.29 kB` | Verified |
| 宿主前端 | check 0 error / 81 warning（既有） | Verified |
| 插件层 e2e | **1 passed (1.1m)**：`meta.stateOrder[0] == default`、界面 chips 序 == DESIGN.md 序 == 词表序 | Verified |
| 后端全量 | `dotnet test ForgeSelf.Api.Tests`（改完 `ListVariants` 后**重跑一次**） | 1679 总计 / **1670 通过 / 9 失败**；9 项全是历史红（控制器 404 家族 7 + `ForgeConfigTests` + `TerminalCommandGuardTests`），`grep -i designsystem` 命中失败行 **0** 条；上一轮偶发的 `ScriptRunnerTests.CancelAsync…` 本轮通过（与"负载相关"的根因一致） | Verified |
| 本地 zip | `release-local.ps1 -Version v2.6.4 -UpdateDir artifacts/update` → `OpenForgeSelf-2.6.4-win-x64.zip`（71.8 MB）；SHA256 `c7e4c536122e1243d87b137b80d034929d0e2f4ee794f90b38532ece2f60f969` 与 `SHA256SUMS.txt` 逐字一致；包内 FOUND `VariantAxes` / `stateOrder` / `focus-visible` / `2.6.4`，`plugin.json` = 2.6.4，`web/dist/index.js`（276,292 B = 构建 276.29 kB）FOUND `stateOrder` | Verified |

解包与探针在仓库外 `%TEMP%/ds264probe` 跑完已删除。v2.6.4 是**顺序一份真相**这一改动的首个包（v2.6.3 的包仍是当时内容的有效版本，不作废）。

## v2.6.5 增量（2026-09-30 05:3x–06:1x，M12f：尺度档位序也交给后端词表）

v2.6.4 把状态/变体轴收成一份真相后，自查发现同族还有一处漏网：`DensityScales` 自己列了 `NAME_ORDER`
（`hairline、thin、xs、sm、md、lg、xl、2xl、pill、full、thick`）—— 里面 `thin`/`pill` 之类后端根本不产出，而真源 `ScaleGenerators.RadiusSteps` 改档序时它不会跟着动。

- 后端：`ScaleGenerators` 新增 `SpaceOrder` / `RadiusOrder` / `DurationOrder`。**`RadiusOrder` 必须含 `pill`/`full`**：
  这两档是 `Radius()` 追加的绝对值档（999px / 9999px），不在倍率表 `RadiusSteps` 里 —— 只把倍率表当词表就会漏掉库里真实存在的档位。
  `GET /meta` 出 `scaleOrders`。
- 前端：`DensityScales` 删掉 `NAME_ORDER`，改读 `meta.scaleOrders`；`ShadowMotion` 的阴影列表从"路径字典序"改走同一个 `compareSteps`。
- 顺带修掉潜伏陷阱：`stepOf` 用 `parseInt` 取末段 ⇒ `radius.2xl` 被读成"第 2 档"，于是 `2xl` 被插进命名档中间、`pill`/`full` 反而排在它前面。
  现在只有**整段是数字**才算数值档（`space.4` ✓ / `radius.2xl` ✗）。

### e2e 断言被证伪三次，改的是精度不是结论（过程如实记录）

1. 第一版用 `startsWith('radius.')` 收 `td.ds-mono` → 红：值列写成 `radius.xs = 2.1px` 也被收进来了；
2. 第二版改锚定正则 `^radius\.\S+$` → 仍红：`td.ds-mono` 只覆盖表格，而圆角那一块是**卡片网格**（`span.ds-mono`），压根没选中；
3. 第三版按 `.ds-stack` 块 + 标题定位到"那一张表"、块内选择器放宽到 `.ds-mono` → 绿。
   全程没有把结论改弱（例如退化成"档位存在即可"），改的只是**测的是不是那一张表**。

### 门禁实测（Verified 项已实跑；全量与 zip 见下）

| 门禁 | 结果 | 等级 |
|---|---|---|
| 插件单测 | vitest **53 passed**（derive.test.ts 17 → 21，含 2xl 陷阱、shadow.10 数值序、stepOf 语义、词表外退字母序） | Verified |
| 插件前端 | check 0 error / build `index.js 276.44 kB` | Verified |
| 后端 DesignSystem | **192/192** | Verified |
| 宿主前端 | check 0 error / 81 warning（既有） | Verified |
| 插件层 e2e | **1 passed (1.1m)**，新增步 8b：界面 `space.` / `radius.` 顺序 == `meta.scaleOrders` | Verified |
| 后端全量 | `dotnet test ForgeSelf.Api.Tests` | 1679 总计 / **1670 通过 / 9 失败**；9 项全是历史红（控制器 404 家族 7 + `ForgeConfigTests` + `TerminalCommandGuardTests`），`grep -i designsystem` 命中失败行 **0** 条 | Verified |
| 本地 zip | `release-local.ps1 -Version v2.6.5 -UpdateDir artifacts/update` → `OpenForgeSelf-2.6.5-win-x64.zip`（71.8 MB）；SHA256 `a106c44f216e111c1340d814425a6fff49461609f28e33cd4d6543fb74cc3146` 与 `SHA256SUMS.txt` 逐字一致；包内 `DesignSystem.dll` FOUND `SpaceOrder`/`RadiusOrder`/`scaleOrders`/`2.6.5`，`plugin.json` = 2.6.5，`web/dist/index.js`（276,435 B = 构建 276.44 kB）FOUND `scaleOrders` 且 **ABSENT `hairline`**（反向探针：被删掉的前端镜像档名表确实不在产物里） | Verified |

解包与探针在仓库外 `%TEMP%/ds265probe` 跑完已删除。

### 出包后的补验（只改测试与文档 → 不升版本号）

读 `05-density-order.png` 时发现截图上"后端说明"整列是 `—`：那一列取自原行 `description`，截图时机抢在取回之前。
给步 8b 补一条断言：**首行的后端说明必须真的填上**（`poll(...).not.toBe('—')`），并把截图挪到断言之后。
重跑 e2e **1 passed (1.5m)**，新截图里说明列已显示 `1 号间距 = 基准 4px × 0.5` … `6 号间距 = 基准 4px × 4`，与 `SpaceSteps` 的倍率一致。
（页面上方仍有一行"正在读取密度对比所需的两个主题…"——那是对比区的加载提示，表格本身已渲染完，不是缺数据。）
本轮只动 e2e 与文档，插件产物未变 ⇒ **v2.6.5 的包仍然有效**，不升版本、不重打包。

## v2.6.6 增量（2026-09-30 06:5x–07:2x，M12g：界面词表族收尾）

需求：把"顺序/词表只有一份真相"这条做到底 —— 界面侧最后三份手抄词表（层级序、色族序、审计类别序）清零，
并证明后端新出的两张表**是实现本身**而不是换个地方抄。

| 门禁 | 命令 | 结果 | 级别 |
|---|---|---|---|
| DesignSystem 后端 | `dotnet test --filter "FullyQualifiedName~DesignSystemTests"` | **194 通过 / 0 失败**（10 类：Oklch 36 · ColorGeneration 31 · ContrastMath 25 · GenerationAudit 22 · ExportProjection 24 · Store 17 · ReleaseSnapshot 17 · TokenGraph 12 · BuiltinIcon 7 · Auth 3），比 v2.6.5 多 2 条（本轮新增） | Verified |
| 全量后端 | `dotnet test ForgeSelf.Api.Tests` | 1681 总计 / **1672 通过 / 9 失败**；9 项全是历史红（`WorkflowPlanningIntegrationTests` 6 + `ScriptRunnerDiIntegrationTests.GetRuntimes…` 1 = 插件控制器 404 家族、`ForgeConfigTests` 1、`TerminalCommandGuardTests` 1），DesignSystem 红 **0**；上轮偶发的 `ScriptRunner…CancelAsync` 本轮通过 | Verified |
| 插件前端 | `pnpm run check` / `test` / `build` | check 0 error；vitest **60 passed**（新守卫 `vocabulary.test.ts` 6 条 + `derive.test.ts` 词表签名改造）；`dist/index.js` **278.19 kB** | Verified |
| 宿主前端 | `pnpm run check` | 0 error / 81 warning（历史遗留，未新增） | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system/design-system.spec.ts` | **1 passed (1.3m)**（最终代码，含截图前的数据到位断言）；步 8c 实跑输出 `色阶条带族序：brand → accent → neutral → success → warning → danger → info`、`四张词表：3 层级 / 7 色族 / 11 审计类别 / 7 状态档位` | Verified |
| 本地 zip | `release-local.ps1 -Version v2.6.6 -UpdateDir artifacts/update` → `OpenForgeSelf-2.6.6-win-x64.zip`（75,334,764 B）；SHA256 `f7a6e84556d36cd83980ada1d4ddaa9dfebb329272698d8fec4b3eedce66a17d`，与 `SHA256SUMS.txt` 及 `artifacts/update/` 副本逐字一致 | Verified |
| 包内探针 | 解包（仓库外 `%TEMP%/ds266probe`，用完已删）：`plugin.json` Version=2.6.6；`DesignSystem.dll` FOUND `ColorFamilies`/`auditKinds`/`colorFamilies`/`roleOrder`/`2.6.6`；`web/dist/index.js`（278,189 B = 构建 278.19 kB）FOUND `auditKinds`/`colorFamilies`，**ABSENT** `'primitive','semantic','component'`、`'brand','accent','neutral'`、`'contrast','alias','tier-violation'` 三份镜像字面量 | Verified |

### 词表"被消费"的证据（不是又一份自证式断言）

- **色族表**：`DesignGenerator` 现在 `foreach (var family in ColorFamilies.All)` 逐族产阶，`RampFor` 没有规则 → 抛；
  用例 `色族序由ColorFamilies供给_生成器逐族产阶_一族不多也不少` 断言"产出的族集合与顺序 == 表"+ 每族 ≥10 阶（表上有名、产物里没货 = 红）。
- **审计表**：用例 `审计词表与引擎产出必须双向对上_每一类都真能产出` 一次凑齐 11 类的触发条件，
  断言 `产出 ⊆ 表` 且 `表 ⊆ 产出`（后者就是"空声明"判据），再用反射核对 `AuditKinds` 的 const 与 `All` 一一对应。
- **前提成立性**：同一条用例里两批写入分别断言 `Created+Updated == 3 / == 5`，失败信息带 `Diagnostics`。
  这不是装饰：第一版就是因为 `UpsertBatch` 的"全图校验、破损整批拒绝"守卫把整批回滚，测试在测空气（红字面：四类没产出）。
- **界面侧反向探针**：`vocabulary.test.ts` 临时塞入 `__probe.ts`（含 `['primitive','semantic','component']` 与四类 kind）→ 守卫红 2 条；删探针 → 60 passed。探针文件未留存。
- **产物内探针**（`web/dist/index.js`）：FOUND `auditKinds`(1) / `colorFamilies`(2)；**ABSENT** `'primitive','semantic','component'` 与 `'brand','accent','neutral'` 字面量清单。

### 读图（Level 3）

- `05-audit.png`：门禁说明清单按后端序完整渲染 11 类（contrast→…→unused），文案与判据一致，无版式异常。
- `05c-vocabulary-order.png` 第一版**不合格**：拍在 `组件库 0 个组件 / 正在读取设计系统库…` 的半加载状态（与 v2.6.5 同一课）。
  补 `expect(.cg__card).toBeVisible()` 后重跑通过，新图显示 10 个组件与真实 tokenRefs；再补 `scrollIntoViewIfNeeded()` 让截图拍到被断言的状态下拉本身。

### 未升版本 / 未做的事

- `roleOrder` 本轮只出到 `/meta`，**没有界面消费者**（如实记，不算已交付；见 TODO 与 README 同名条目）。

## v2.6.7 增量（2026-09-30 07:2x–08:5x，M12h：契约可见 + 变体表单收口）

需求：把 v2.6.6 留下的三件"看得见但没用上 / 用得上但看得见不真"收掉 —— 十类实体 url 在界面上可见且**读回真实行数**、
`roleOrder` 拿到真消费者（顺带修掉矩阵**分组序**的字母序缺陷）、变体轴改成"先选后拼"；外加图标 code 的显示缺陷。

| 门禁 | 命令 | 结果 | 级别 |
|---|---|---|---|
| DesignSystem 后端 | `dotnet test --filter "FullyQualifiedName~DesignSystemTests"` | **195 通过 / 0 失败**（Oklch 36 · ColorGeneration 31 · ContrastMath 25 · ExportProjection 24 · GenerationAudit 23 · ReleaseSnapshot 17 · Store 17 · TokenGraph 12 · BuiltinIcon 7 · Auth 3），比 v2.6.6 多 1 条 | Verified |
| 全量后端 | `dotnet test ForgeSelf.Api.Tests` | 1682 总计 / **1662 通过 / 20 失败**；DesignSystem 红 **0**；20 项 = 9 项历史红 + **11 项 `AgentRegistryServiceTests`（新红，非本任务）** | Verified |
| 插件前端 | `pnpm run check` / `test` / `build` | check 0 error；vitest **62 passed**（`derive.test.ts` +2 条：轴清单取值、按轴拼 JSON 的转义与空值）；`dist/index.js` **287.32 kB** | Verified |
| 宿主前端 | `pnpm run check` | 0 error / 81 warning（历史，未新增） | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system/design-system.spec.ts` | **1 passed (2.9m，用例本体 1.1m)**（最终代码 + 截图前滚到被断言的表），实跑输出见下 | Verified |
| 本地 zip | `release-local.ps1 -Version v2.6.7 -UpdateDir artifacts/update` → `OpenForgeSelf-2.6.7-win-x64.zip`（75,338,411 B）；SHA256 `e0a4b49835e05087ec73a73a0e69d5a097788e58cd748a76460e0636b0bd83f4`，与 `SHA256SUMS.txt` 及 `artifacts/update/` 副本逐字一致 | Verified |
| 包内探针 | 解包（仓库外 `%TEMP%/ds267probe`，用完已删）：`plugin.json`=2.6.7；`DesignSystem.dll` FOUND `variantAxes`/`entities`/`AxisSortKey`/`ColorFamilies`/`2.6.7`；`web/dist/index.js`（287,320 B = 构建 287.32 kB）FOUND `variantAxes`/`entities`，**ABSENT `stateOrder`**（反向探针：被合并掉的那三条并列字段确实不在交付物里） | Verified |

### 契约可见的真实读数（不是"链接摆在那儿"）

- 步 10a 十类实体**逐类读回** `total`：`design-color=159 · design-shadow=9 · design-motion=13 · design-type-role=17 · design-spacing=10 ·
  design-radius=8 · design-icon=40 · design-component=10 · design-screen=4 · design-font-face=14`；断言"行数必须是数字"（卡在读取中/报错都算不合格），
  并核对界面列出的实体名 == `meta.entities`、每条 url 都含当前项目 id。
- 步 8d 变体表单：轴下拉 == `meta.variantAxes` 声明序；选 `role` 后档位下拉 == 该轴词表；拼出的 JSON 是 `{"role":"primary"}`；
  切「直接写 JSON」再切回来，值不丢。
- 步 10 矩阵分组序（v2.6.7 的新证据）：`role` 轴 `primary → secondary → danger`、`size` 轴 `sm → md → lg` —— 此前是 JSON 文本字典序（`danger` 会排到 `primary` 前）。
- 步 13 图标 code：界面显示 `dashboard`，与库里逐字一致（此前被父级 `.ds-micro` 的 `text-transform: uppercase` 显示成 `DASHBOARD`）。

### 先红后绿的三条（判据都没削弱）

1. **版本自洽断言抓到"测试打在旧产物上"**：e2e 期望 2.6.7、实际 2.6.6 —— 端口 7102 上还有一个我先前误拉起 184 项套件后残留的宿主实例在答，
   globalSetup 新起的宿主没抢到端口但健康检查通过。用户手动关掉旧实例后重跑即绿。
   基建缺口已记 TODO（起宿主前不探端口 = 会把测试悄悄打在旧构建上）。
2. **`AxisSortKey` 第一版把档位放在轴之前** → `size=sm` 与 `role=primary` 交错（都是 0001），新用例与 v2.6.4 那条老用例同时红。
   改成"轴位在前、档位在内"；老用例的期望从"按 `VariantKey` 字母序"升级为"按词表序"（**更强的判据**，不是迁就实现）。
3. **`Equal(...)` 把 because 当成期望元素**（FluentAssertions params 重载）→ 期望多出 1 项。改成先组 `string[]` 再传，判据本身没动。

### 非本任务的 11 项红：根因定位（Verified，单跑对照）

`AgentRegistryServiceTests` 在全量跑里 11 项红、**单跑 18/18 全绿**。机制：`AgentRegistryService` 构造函数从**数据库**加载内置 agent
（`Plugins/AIAgent/Services/AgentRegistryService.cs:28-45`），而测试自己 `new` 出来不建数据；全量跑里连接串/数据根由先跑的 fixture 决定
（`DAL.ConnStrs` / `Config<T>.Provider` 那组进程级静态）⇒ "库里有没有 coordinator"取决于调度顺序。本轮我加了用例改变了排序，把它暴露出来。
属既有的"静态状态隔离"缺口（TODO 同名条目已补这条证据），不在本任务内修。

### 读图（Level 3）与一条没解释完的观察

- `08c-entity-sockets.png`（补 `scrollIntoViewIfNeeded` 后重拍）：十行实体、真实 url（`?theme=dark`）、真实行数 159/9/13/17/10/8/40/10/4/14，版式无溢出。
  第一版拍在格式卡片那一屏（下载按钮一排），**看不到被断言的表** → 补的是断言/滚动，不是换一张图。
- `05c-vocabulary-order.png`：`组件 *=button`（回填生效，不再停在占位符）、`STATE=default`、`轴=role`、`档位=primary`、
  `VARIANTJSON（按轴选，自动拼好；只读）= {"role":"primary"}`，右下「多轴？直接写 JSON」贴底对齐 ✓。
- **未解释完的一条**：同一步骤的 `05c` 在 v2.6.6 那次是**暗色画布**、这次是**浅色画布**。机制已定位（`loadSkin()` 只在挂载与 `nav.skin` 页触发，
  `skinCss` 一旦加载就留在根上 ⇒ 画布明暗取决于本次会话到过哪些页），**但"哪一次到过预览页"没在日志里对上**，属 Unknown；
  已记 TODO（要么让切主题直接触发 `loadSkin()` 并断言画布跟随，要么在界面上写清"画布皮肤只在预览页生效"）。
  数据面的主题是真的：`tokens/effective?theme=dark` 有请求、步 12 实测 `底色=rgb(33,31,37)`。

## v2.6.8 增量（2026-09-30 11:2x–13:1x，M12i：画布投影身份收成状态 + 导出读路径去 N+1）

### 改了什么（一句话）

`skinTheme`/`skinApplied` 进状态 + 主题条四态 + 画布角标（档位与字面颜色值）；`cssVar` 修好并新增 `resolveCssVar`；
导出/快照的变体读改一次批量（`VariantsByComponent`）+ 导出页读回限流 `mapLimit(≤3)`；新增后端门禁「每格式 × 每主题」。
版本三元组与 `plugin.json` 同步到 **2.6.8**。

### 门禁（串行，实测输出）

| 门禁 | 命令 | 结果 | 等级 |
|------|------|------|------|
| DesignSystem 后端 | `dotnet test … --filter FullyQualifiedName~DesignSystemTests` | **197 通过 / 0 失败**（+2：`每种格式在每个主题下都能导出_不只跑浅色档`、`批量取变体与逐组件取_行序逐位一致`） | Verified |
| 插件前端 check | `pnpm run check`（宿主 vue-tsc） | 0 error | Verified |
| 插件前端 test | `pnpm run test`（宿主 vitest） | **76 passed**（62 → 76，+14：`skinTheme`/`skinApplied` 5 + `cssVar` 3 + `resolveCssVar` 3 + `mapLimit` 3） | Verified |
| 插件前端 build | `pnpm run build` | `dist/index.js 290.03 kB` / `style.css 57.94 kB` | Verified |
| 宿主前端 check | `ForgeSelf.Web && pnpm run check` | 0 error（81 warning 历史） | Verified |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/design-system/design-system.spec.ts` | **1 passed (5.6m)**；本轮网络日志仍有 **3 次只读 500**（`variants` / `export?format=css` / `fonts`，被 `fetchRead` 退避重试兜住 ⇒ 用例绿） | Verified |
| 全量后端 | `dotnet test ForgeSelf.Api.Tests` | 1684 项 / **1673 通过 / 11 失败**（23m36s）；**DesignSystem 红 0** | Verified |
| 发布 zip | `scripts/release/release-local.ps1 -Version v2.6.8 -UpdateDir artifacts/update` | `OpenForgeSelf-2.6.8-win-x64.zip` **75,340,460 B**，SHA256 `f686776902cd37dfe754287345545d3424b0a004691797094251610e3113e164`；与 `SHA256SUMS.txt` 记录的哈希逐字一致、`artifacts/update/` 副本 `cmp` 逐字节一致 | Verified |

### 先红后绿（判据都没削弱）

1. **`skinTheme` 不存在**：新写的 5 条用例先红（`expect(skinTheme.value).toBe('dark')` 取到 undefined）→ 实现后该文件 9/9 绿。
2. **`cssVar` 一条也取不到**：`expected '' to be '#211f25'`、`expected '' to be '20px'` → 修 `--` 前缀拼接 + 允许末条无分号。
   反向证据：这个函数**在本次之前全仓零调用点**（`grep cssVar(` 只有定义），注释却写着"界面各处取色不再自己算"。
3. **e2e 抓到状态机少一态**：我在非预览页切档处断言 `pending`，实测 `none` → 真相是"本次会话从没进过预览页 = 压根没取过"，
   与"取过但已过期"是两件事 ⇒ 状态机扩成 `applied`/`pending`/`unloaded`/`unavailable`，文案分开。
4. **e2e 抓到角标值不是颜色**：断言 `hexToRgb(surface-bg)` 时报
   `角标里的 surface-bg「var(--ds-color-neutral-950)」必须是可解析的颜色` → 语义层是别名，加 `resolveCssVar` 顺链到原语字面值。
5. **我自己的两处写法错**（判据未动）：`ContainKeys(a,b,c,"because")` 命中 `params TKey[]` 重载编译错；
   `Equal("…","…","…", because)` 同族陷阱第二次踩。

### 三方对齐的实测数字（本轮核心证据）

- 进入预览页（选 dark 之后）：`[skin] 进入组件库：画布投影=dark，角标 surface-bg=#211f25，画布 computed=rgb(33, 31, 37)`，
  与后端 `dark.surface-bg=#211f25` 逐位一致（`rgb(33,31,37)` 即 `#211f25`）。
- 浅色档：角标 `surface-bg #f6f6f9`、`brand #6d28d9`（后者正是步 7 用户手改 `color.brand.600` 的值 —— 角标顺带证明了"手改进了投影"）。
- 跨页链路：`[skin] 令牌工作台切 dark：主题条状态=画布投影未取（进入「看效果」页时按 dark 取）`；
  预览页应用后再次切档 → `[skin] 外壳页切 dark：画布投影待重取 light → dark（进入「看效果」页时应用）`。
- 版本自洽：`plugin.json Version=2.6.8 meta.modelVersion=2.6.8`（e2e 断言 + 页角标 `模型 2.6.8 · 生成器 2.6.8 · 投影 2.6.8`）。
- 十类实体行数（限流后仍全部读回）：`design-color=159 design-shadow=9 design-motion=13 design-type-role=17 design-spacing=10
  design-radius=8 design-icon=40 design-component=10 design-screen=4 design-font-face=14`。

### 并发这条为什么值得记（以及它没被"根治"）

- 现象：改之前那轮 e2e 网络日志里有 2 条 500，落在**不同端点**（一次 `export?format=less&theme=dark`、一次 `audit?kind=contrast`）
  ⇒ 不是某个格式坏了，是并发撞锁。宿主日志栈实证（`.temp/e2e/<ts>/backend.log`）：
  `code = Busy (5) / SQLiteException: database is locked`，位置 `CatalogRepository.ListVariants` ← `ExportService.Load`（**只读路径**）。
- 插件侧收敛两处：`ExportService.Load` / `ReleaseService.BuildSpecs` 的逐组件 N+1 → 一次 `VariantsByComponent`；
  导出页读回 `mapLimit(≤3)`。**但 500 没被消除**：连跑三轮 e2e 的只读 500 次数是 **2 / 0 / 3**，落点每轮不同
  （`export?format=less` → `audit?kind=contrast` → `components/tooltip/variants`+`export?format=css`+`fonts`）。
  我一度把"某一轮计数为 0"当成收敛证据 —— 那是**单次运行的运气**，不是效果证明；判据应改成"跨轮看分布"。
- **诚实边界**：`http.ts` 的只读退避重试（最多 4 次尝试）本来就会把这类 500 兜住，所以"界面上没红"并不证明"没发生"；
  本轮做的是减少争抢本身，**宿主跨请求写锁仍未根治**（TODO P1，需人拍板）。

### 全量后端 11 项红的归属（DesignSystem 红 = 0，逐条拆）

- **9 项历史红（已登记的既有问题）**：`ScriptRunnerDiIntegrationTests.GetRuntimes…` + `WorkflowPlanningIntegrationTests` 6 项
  （= 测试 staging 只拷 DLL 不拷 `plugin.json` → 插件控制器 404 家族，TODO P1 待拍板）、
  `ForgeConfigTests.ForgeSetting_配置文件归一到数据根Config目录`、`TerminalCommandGuardTests.Check_EncodedCommand_…`。
- **`ScriptRunnerTests.CancelAsync_ForRunningScript_ShouldCancelExecution`**：本轮断言原文
  `Expected Status to be Cancelled{4}, but found Running{1}`（上一轮记录的是 `Timeout{5}`）⇒
  与 TODO 已登记的那条 ScriptRunner 收尾覆写缺陷同族，且**表现不稳定**（停在 Running / 被改成 Timeout）。非本任务。
- **`Sems.RunnerServiceTests.StopExternal_Kills_ExternalProcess`**（`RunnerServiceTests.cs:156` `Assert.True() Failure`）：
  单独跑该类时**更差**（18 项里 7 红，含 `Launch_Then_Stop_…`、`ExecuteCodeAsync_…` 等）⇒
  不是顺序依赖，是"真起外部进程再杀掉"这类用例在本机上的时序/环境脆弱。**新增登记进 TODO**（本轮定位到此，未越界去修别的插件）。
- **上一轮的 11 项 `AgentRegistryServiceTests` 本轮全绿** ⇒ 进一步印证那条 TODO 的机制判断（结果取决于全量跑的调度顺序）。

### 包内探针（解包在仓库外 `%TEMP%/ds268pkg`，用完已删）

- `Plugins/DesignSystem/plugin.json` → `"Version": "2.6.8"`（打包进去的就是本轮清单）
- `DesignSystem.dll` → FOUND `VariantsByComponent`（批量读方法真在程序集里）、FOUND `2.6.8`
- `web/dist/index.js` → FOUND `data-skin-theme` / `画布投影` / `surface-bg` 共 14 处（角标与主题条在产物里）；
  **ABSENT `^ds-`**（反向探针：`cssVar` 那条把 `--` 前缀拼错的旧正则确实不在了）
- 一条环境注意：`SHA256SUMS.txt` 是 **CRLF**，Git Bash 下 `sha256sum -c` 会因文件名尾部 `` 报
  `No such file or directory`（不是哈希不符）。核对方式改成"直接 `sha256sum` 与文件内记录逐字比对"。
