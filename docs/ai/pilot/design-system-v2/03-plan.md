# Plan — DesignSystem 插件 v2

> 阶段：Stage 3｜具体到真实文件路径。Task ID：PILOT-ds-v2
> 里程碑 M1→M5 顺序推进；每个里程碑结束即可独立验证（不攒到最后）。

## Files To Change

### M1 后端地基（数据层 + 门面 + 控制器）
- file: `Plugins/DesignSystem/Data/Model.xml`（新建）
  reason: 11 表列/索引/默认值唯一真源（铁律 9）；`ConnName=DesignSystem`、`Namespace=ForgeSelf.Api.Plugins.DesignSystem.Entities`、`Output=Entities`，结构照 `Plugins/Scheduler/Data/Model.xml`
- file: `Plugins/DesignSystem/Data/Entities/*.cs` + `*.Biz.cs`（`xcode Model.xml` 生成 / 人工）
  reason: 生成件勿手改；自定义查询与校验（直查 DB，铁律 11）写 `.Biz.cs`
- file: `Plugins/DesignSystem/Data/DesignSystemTables.cs`（新建）
  reason: 建表唯一真源；`DAL.Create(conn).Db.ServerVersion` 先开库 → `TableItem.Create(typeof(X)).DataTable` → `dal.SetTables(...)`；异常抛出（G1）
- file: `Plugins/DesignSystem/DesignSystemPlugin.cs`（改写，现 20 行 no-op）
  reason: `Apply()` 调 `EnsureCreated()` + `ctx.EnsurePluginDataDirectory()` 定库位 + 注册服务 + 内置图标库首植
- file: `Plugins/DesignSystem/Services/{Oklch,ContrastMath}.cs`（新建）
  reason: 纯函数色彩/对比度数学，无 IO，可黄金值单测；后续所有生成与审计的地基
- file: `Plugins/DesignSystem/Services/TokenRepository.cs`（新建）
  reason: 令牌/主题/项目读写、批量 upsert 事务、`ResolveEffective`（G4）、别名图解析 + 环检测（FR4/G17）
- file: `Plugins/DesignSystem/Services/{DesignProjectService,ComponentService,IconService,ScreenService}.cs`（新建）
  reason: 各自实体的门面；变体入库前规范化 JSON（G5）；内置库只读守卫（G7）
- file: `Plugins/DesignSystem/Controllers/DesignSystemController.cs`（新建）
  reason: 端点全在此；类级 `[Authorize("ApiKeyPolicy")]`（铁律 17）；`meta` 能力清单（FR18）
- file: `Plugins/DesignSystem/DesignSystem.csproj`（改）
  reason: 清死引用（`:12-19` 未用的 AspNetCore.Http 相关如确无必要则删）；确认 `Data/**` 与 `web/**` 编译项正确
- file: `Plugins/DesignSystem/plugin.json`（改）
  reason: `Version→2.0.0`、`IconUrl` 去 `example.com` 占位、`Permissions` 声明（对照 AIAgent）
- file: `ForgeSelf.Api.Tests/Plugins/DesignSystem/*.cs`（新建）
  reason: 色彩/对比度黄金值、别名环 + 事务回滚、唯一性 409、鉴权反射（仿 `McpAdminAuthTests`）、`EnsureCreated` 表齐
- file: `ForgeSelf.Api/…`（**只读参考，不改**）：`Controllers/PluginController.cs`、`ForgeSelf.Core/IContext.cs:13-34`、`XCodeConfig`
  reason: 接线方式与 Testing 环境 early-return 闸门（铁律 10）以现有实现为准

### M2 生成与审计引擎
- file: `Plugins/DesignSystem/Services/{ColorRampGenerator,SemanticResolver,TypographyGenerator,ScaleGenerators,DesignGenerator,AuditEngine}.cs`（新建）
  reason: 替换 `web/src/design/colorScale.ts:29-49` 的写死曲线与 `generate.ts:23-61 ≡ presets.ts:68-93` 的常量；对比度定向选 tone；reduced-motion 派生；审计落库（FR5–FR9）
- file: `ForgeSelf.Api.Tests/Plugins/DesignSystem/{ColorRamp,SemanticContrast,NonColorScales,Determinism}Tests.cs`（新建）
  reason: AC5–AC11 的黄金值/差集/单调性断言

### M3 投影导出
- file: `Plugins/DesignSystem/Services/ExportService.cs` + `Services/Projections/{DtcgTree,CssVariables,TailwindV4Theme,Scss,Less,TypeScript,TokensStudio,DesignMd,RegistryJson,StardustJson,StardustSql}.cs`（新建）
  reason: 行 → DTCG 中间表示 → 各目标；Stardust 兼容端点（FR12/FR13）；zip + SHA256
- file: `ForgeSelf.Api.Tests/Plugins/DesignSystem/Goldens/*.{json,css,scss,md}`（新建）
  reason: 每格式 golden-file（AC12/AC13）
- file: `Plugins/DesignSystem/web/scripts/gen-tokens-css.ts`（改）
  reason: 明确"后端导出覆盖宿主预览基线"的职责边界（G3）

### M4 前端重构（12 section 库驱动）
- file: `Plugins/DesignSystem/web/src/http.ts`（新建）
  reason: 直连后端 + `localStorage['forge_api_token']`；**响应已是包装体，按项目铁律 15 只解一层 `data`**；分级空态（G19/G20）
- file: `Plugins/DesignSystem/web/src/design/{oklch.ts,contrast.ts,goldens.ts}`（新建）、`{generate.ts,exporters.ts,presets.ts,storage.ts}`（改造）
  reason: TS 侧只保留预览用色彩数学镜像 + 与 C# 同一组黄金值（G2）；一次性生成器/导出器废弃并迁到后端；`storage.ts` 降级为"离线草稿 + 未落库标注"，首启一次性迁移为草稿项目
- file: `Plugins/DesignSystem/web/src/sections/{Projects,TokenStudio,ColorLab,TypeScale,DensityScales,ShadowMotion,IconLibrary,ThemeLab,AuditBoard,ExportCenter}.vue`（新建）
  reason: FR15 的库驱动工作台
- file: `Plugins/DesignSystem/web/src/sections/{ComponentGallery,TokenShowcase}.vue`（重写）、`console/*.vue`（5，改造）、`marketing/MarketingSite.vue`（改造）
  reason: 删硬编码 fixture（`ComponentGallery.vue:38-58`、`TokenShowcase.vue:6-8/:93/:102/:181`）、接上或删除无处理器按钮（`ConsoleServices.vue:22`、`ConsoleConfig.vue:25-26`、`ConsoleDeployments.vue:19`、`MarketingSite.vue:45-46/:86`）
- file: `Plugins/DesignSystem/web/src/components/{DsButton,BrandLogo,StatusBadge,MetricCard,TableRow,NavItem,DsInput,CodeBlock}.vue`（改）
  reason: 去硬编码色值（`DsButton.vue:74/:113/:117` 等），一律 `--ds-*`；`BrandLogo.vue:53` 字标取项目名（FR16）
- file: `Plugins/DesignSystem/web/src/DesignSystemView.vue`（改）
  reason: 项目/主题状态提升到后端；`:102` 去硬编码；根视图版本徽标（铁律 13）；导航桥降级链（铁律 5）
- file: `Plugins/DesignSystem/web/src/index.ts`（微调）
  reason: 导出名必须仍等于 `plugin.json.views[0]`（`DesignSystemView`）
- file: `Plugins/DesignSystem/web/package.json`（改）
  reason: **补 `check`（vue-tsc --noEmit + eslint）与 `test`（vitest）脚本** —— 现在只有 `gen:tokens/build/dev`，而 ROADMAP 验收要求跑它们
- file: `Plugins/DesignSystem/web/src/**/*.test.ts`（新建）
  reason: 色彩数学黄金值 + 确认编排纯函数（宿主 `vitest.config.ts:33` 已 include `../Plugins/*/web/src/**/*.test.ts`）

### M5 验证 / 发布 / 文档
- file: `ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts`（改 + 拆多个 spec）
  reason: `:141` 版本断言同步 2.0.0；全链路新用例（AC22）；**禁止一次性临时脚本代替**
- file: `Plugins/DesignSystem/README.md`、`ROADMAP.md`（改）
  reason: 消除自评矛盾（`README.md:191-192` vs `:199`）、行业键错（`:166` `devops` ↔ 代码 `devtools` `generate.ts:92`）、写 v2 契约
- file: `docs/02-features/0XX-design-system.md`（新建/改）
  reason: 新端点与契约入库（§四维护闭环第 5 步）
- file: `.agents/skills/plugin-development/SKILL.md` 或新建 `design-system-workflow` 技能 + `AGENTS.md` §2.4
  reason: 兑现 ROADMAP P2.3（`forge-design-system-verify` 类技能）并登记（不登记 = 等于不存在）

## Implementation Steps
1. **M1a 数据层**：写 `Data/Model.xml`（11 表，逐列 `DisplayName/Description/DefaultValue/Nullable`）→ `cd Plugins/DesignSystem/Data && xcode Model.xml` → `diff` 无字段漂移 → 补 `*.Biz.cs`（`FindUnique`、`FindByPath` 等直查 DB）→ 写 `DesignSystemTables.cs`（`ConnName` 常量 + `EntityTypes` + `EnsureCreated`）→ `DesignSystemPlugin.Apply()` 接线 → `dotnet build`。
2. **M1b 色彩数学**：`Oklch.cs`（sRGB↔linear↔OKLab↔OKLCH、gamut clamp、hue-cycling）+ `ContrastMath.cs`（相对亮度、比率、AA/AAA/非文本判级）→ xUnit 黄金值（含 `L=0`/`C=0` 边界）。
3. **M1c 门面 + 控制器**：`TokenRepository`（含 `ResolveEffective` 两层合并、别名解析 + 环检测、批量事务 upsert、乐观并发）→ 项目/主题/组件/图标/页面服务 → `DesignSystemController`（类级鉴权）→ xUnit（409/400/回滚）+ 反射鉴权测试 → `dotnet test --filter DesignSystem`。
4. **M2 生成引擎**：色阶（tone 目标 + 彩度峰值 + hue-cycling）→ 语义角色对比度定向选 tone → 排版（比例 + fluid clamp）→ spacing/radius/border/shadow 分层/motion(+reduced) → `DesignGenerator` 组装（确定性、`Generator=manual` 跳过、冲突计数）→ `AuditEngine` 落库（5 类）→ xUnit（AC5–AC11）。
5. **M3 投影**：`DtcgTree`（合成点分路径、`$type` 继承、别名串、`$extensions`）→ 各投影 → zip + SHA256 → `api/<entity>.json` + `index.json` + 合法 `data.sql` → golden-file 测试。
6. **M4 前端**：`http.ts` + 类型 + 空态分级 → 10 个新 section → 2 个重写 + 6 个改造 → 组件去硬编码 → `package.json` 补 `check/test` → vitest（含与 C# 同表黄金值）→ 构建（沙箱内用 §3.2 出树兜底）。
7. **M5 闭环**：门禁四件套 → e2e 全链路（版本断言同步）→ 请示用户后 commit + 打 tag `v2.0.0`（或 `release-local.ps1 -UpdateDir`）→ 通知用户在设置页点自动更新 → 走查（截图读图 + §3.4 清单 + 软删测试数据）→ 文档与技能回写 + 工作日记。

## Test Plan
1. **xUnit 纯函数**：`OklchTests`（往返误差 <1e-4）、`ContrastMathTests`（≥6 组黄金色对）、`ColorRampTests`（L 单调、中调彩度峰值、纯灰边界、gamut clamp）、`DeterminismTests`（两次生成 sha256 相同）、`NonColorScalesTests`（两项目值集合不相等）。
2. **xUnit 数据/契约**：`TokenRepositoryTests`（别名环 + 零行落库、悬空置空 + critical、`ThemeId=0` 覆盖合并、唯一冲突 409、乐观并发 409、manual 不被覆盖）、`DesignSystemTablesTests`（11 表齐、二次调用幂等）、`ExportProjectionTests`（golden-file 逐格式、DTCG 结构校验、Stardust 形状与列名白名单、`data.sql` 可执行）、`DesignSystemAuditReleaseTests`（Critical 未清 → 409）、`DesignSystemAuthTests`（反射断言每个控制器带 `ApiKeyPolicy`）。
3. **vitest（插件 web）**：色彩镜像与 C# 黄金值同表、确认编排纯函数（取消 → 零请求）、空态分级函数、别名解析提示函数。
4. **Playwright e2e（零 mock）**：建项目 → 生成 → 断言色阶/审计渲染 → 改一个令牌（点即保存）→ reload 仍在 → 主题微调保存生效 → 导出下载并读真实文件内容 → 建 Release → Critical 门禁负例 → 无 fatal console error → 窄视口不破版（1280×720）→ 截图读图。
5. **隔离与安全**：测试库落 `TestDataDir/{类名}_{随机串}/design-system.db`，**只创建不删除**；`env.IsEnvironment("Testing")` 不绕早期返回闸门。

## Verification

### Build
```bash
cd ForgeSelf.Api && dotnet build 2>&1 | tail -20
cd Plugins/DesignSystem/Data && xcode Model.xml
diff <(grep -oE 'BindColumn\("[A-Za-z]+"' Plugins/DesignSystem/Data/Entities/DesignToken.cs) \
     <(grep -oE 'BindColumn\("[A-Za-z]+"' /tmp/DesignToken.before.cs)   # 无字段漂移
node ForgeSelf.Web/node_modules/vite/bin/vite.js build --config Plugins/DesignSystem/web/vite.wrapper.config.ts \
     --outDir "$PWD/Plugins/DesignSystem/web/dist" --emptyOutDir        # 沙箱出树兜底
```

### Unit Test
```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" 2>&1 | tail -20
cd ForgeSelf.Web && pnpm run check && pnpm run test
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test            # 本里程碑新交付的脚本
```

### Integration Test
```bash
# 真实宿主 + 真实库的 HTTP 冒烟（同一 bash 调用内 启动→curl→kill，不碰用户实例）
dotnet build ForgeSelf.Api && dotnet run --project ForgeSelf.Api --urls http://127.0.0.1:7102 &
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:7102/api/design-system/meta           # 期望 401（鉴权生效）
curl -s -H "Authorization: Bearer $TOKEN" http://127.0.0.1:7102/api/design-system/meta | head
```

### E2E
```bash
cd ForgeSelf.Web && bash node_modules/.bin/playwright test e2e/plugins/design-system --reporter=line
# globalSetup 自动构建宿主 + 起 publish 实例（隔离，不杀用户进程）
```

### Other Checks
```bash
# AC19 硬编码色值
grep -RInE '#[0-9a-fA-F]{3,6}\(|rgba?\(' Plugins/DesignSystem/web/src --include='*.vue' | grep -v styles/tokens.css
# 产物契约（裸导入证明 external 生效）
grep -c 'from "vue"' Plugins/DesignSystem/web/dist/index.js
# 四步门禁：见 plugin-development §四；发布判据 = Release 资产可下载且与 SHA256SUMS 一致
```

## Plan 偏差记录
| 时间 | 偏差点 | 原 Plan | 修正后 |
|---|---|---|---|
| 2026-09-28 | 插件目录 | 技能 §3.1 写 `ForgeSelf.Api/Plugins/<X>` | 实际为仓库根 `Plugins/<X>`（commit `c7941a0` 已迁）→ 本 Plan 全用根路径 |
| 2026-09-29 | M3 收尾 | `ReleaseService` 快照含所有生命周期状态 | `removed` 令牌不进快照（`TokenNode` 加 `Lifecycle` 透传）；否则软删永不产生 diff 的"删除"，AC14 名不副实 |
| 2026-09-29 | M4 色彩镜像 | 新建 `web/src/design/{oklch,contrast,goldens}.ts`（TS 与 C# 同一组黄金值） | **不做**：色彩/对比度数学只留 C# 一份，界面读 `tokens/effective`、试算走 `generate/preview`、交付走 `export?format=css`。v1 的玩具感正来自前端第二套实现必然漂移（见 not-taken-decisions 010） |
| 2026-09-29 | M4 换肤 | `storage.ts` 降级为离线草稿 + 首启迁移为草稿项目 | **改为**：预览容器复用后端 CSS（`:root`→`.ds-skin` 收窄 + 变量别名表）；localStorage 不再是任何数据源，v1 草稿链（presets/schema/tokensToCss/generate/exporters/storage/colorScale + gen-tokens-css.ts）整体下线进 `.trash/`，`styles/tokens.css` 转手工维护 |
| 2026-09-29 | M4 工具链 | `check` = vue-tsc + eslint，`test` = vitest（插件自带脚本） | 插件不装工具链（仓库既有约定）：脚本改为委托宿主 `pnpm -C ../../../ForgeSelf.Web exec vue-tsc/vitest`；**eslint 仍覆盖不到插件 src**（宿主 flat config 的 `files` 不能越出 `src/**`），已记 TODO，不谎称已达成 |
| （待实现中回填） | | | |
