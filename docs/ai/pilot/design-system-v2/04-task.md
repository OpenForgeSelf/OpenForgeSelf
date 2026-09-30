# Agent Task — M1 后端地基（含任务索引）

> 阶段：Stage 4｜工作单元，零自我决策空间。前序：00 / 01 / research / design / 02-spec / 03-plan 齐备。
> **闸门1**：本文件与 `02-spec.md` 的 Acceptance Criteria 经用户确认后，方可写实现代码（数据库结构变更属高风险，AGENTS.md §3）。

## Task Index（design §8 缺口 → 任务）

| ID | 内容 | 里程碑 | 状态（2026-09-29） |
|---|---|---|---|
| T101 | `Data/Model.xml` 11 表 + `xcode` 生成 + `DesignSystemTables.cs`（`OklchH` 等索引预留） | M1 | ✅ 12 表；`xcode` 二次生成逐字节一致、`BindColumn` 无漂移 |
| T102 | `TokenRepository`：批量事务（全或无）+ `Generator=manual` 跳过 + 冲突计数 | M1 | ✅ `EntityTransaction` + 手改保护 + 版本先行校验 |
| T103 | `ResolveEffective`（共享层+主题覆盖）+ 别名图解析/环检测 + 悬空置空 + 复合真源一致 | M1 | ✅ 复合 `ValueJson` 视为有值；`effective` 端点已改为真算 hex/对比度/判级 |
| T104 | `Oklch.cs` / `ContrastMath.cs` + C#/TS 同表黄金值 | M1 | ⚠️ C# 侧完成（矩阵按 CSS Color 4 参考值钉死）；**TS 镜像按 not-taken-decisions 010 取消**，前端不再算色 |
| T105 | `DesignSystemController`（类级 `ApiKeyPolicy`）+ `meta` 能力清单 | M1 | ✅ 能力含 `releases`/`releases.diff`，界面按能力灰化入口 |
| T106 | `DesignComponent`/`Variant` 服务（变体 JSON 规范化） | M1 | ✅ canonical 排序键 JSON，幂等 upsert |
| T107 | 图标库首植 + 内置库只读守卫（`BuiltinProjectId`） | M1 | ✅ 补完：`BuiltinIcons` 40 枚原创 24grid/1.5stroke，`Apply` 幂等首植 + 7 项测试 |
| T108 | 项目/主题服务 + `dirty` 派生 + `gen-tokens-css.ts` 职责边界 | M1/M3 | ✅ 项目/主题（modeKind 白名单校验）；**`gen-tokens-css.ts` 已下线**（`tokens.css` 转手工维护） |
| T109 | `ReleaseService` 不可变快照 + diff | M3 | ✅ 快照按主题自足展开 + SHA256 + 门禁拒发 + 令牌级 diff；12 项测试 |
| T110 | 归档=软删 + 二次确认编排纯函数 | M4 | 🔄 `Archive` 已是软删；界面二次确认落在 Projects.vue |
| T111 | `http.ts` 分级空态 + "未落库"标注 | M4 | 🔄 `http.ts`+`PanelState`（401/错误/空/加载四态）已就位 |
| T201 | 生成引擎（色阶/对比度定向/排版/尺度/motion-reduced）+ 预览期派生策略 | M2 | ✅ 非色彩尺度随参数变化；种子逐位锚定；填充/标签成对达标 |
| T301 | 投影层 11 个 + golden-file + `ProjectionVersion` + 合法 `data.sql` | M3 | ✅ 11 格式 + bundle；**修掉复合令牌导出空声明的缺陷**（shadow/transition 现展开成真值） |
| T302 | 导出体积实测（U1）→ 必要时分页/流式 | M3 | ⬜ **未做**：未实测导出文本体积，无证据即不宣称达标 |
| T4xx | 前端 12 section 库驱动 + 去硬编码 + 补 `check/test` | M4 | 🔄 13 section（新增 ReleaseBoard）+ `check/test` 委托宿主工具链；eslint 仍覆盖不到插件 src（已记 TODO） |
| T5xx | 门禁四步 + e2e 全链路 + 发布 + 走查 + 文档/技能回写 | M5 | ⬜ 未开始 |

---

## Task ID
`PILOT-ds-v2 / M1`

## Objective
`Plugins/DesignSystem` 拥有可用后端：插件冷启动自建 `DesignSystem` 库（11 表），经 HTTP 完成项目/主题/令牌/组件/图标/页面的 CRUD 与批量 upsert，别名成环或悬空可被拒绝且**零行落库**，色彩与对比度数学有黄金值单测，所有控制器无 token 一律 401 —— `dotnet build` 0 error、`dotnet test --filter DesignSystem` 全绿。

## Scope

### Allowed
- 新建：`Plugins/DesignSystem/Data/**`（`Model.xml`、`Entities/*.cs` 仅由 `xcode` 生成、`*.Biz.cs` 人工、`DesignSystemTables.cs`）、`Plugins/DesignSystem/Services/{Oklch,ContrastMath,TokenRepository,DesignProjectService,ComponentService,IconService,ScreenService}.cs`、`Plugins/DesignSystem/Controllers/DesignSystemController.cs`、`ForgeSelf.Api.Tests/Plugins/DesignSystem/**`
- 改写：`Plugins/DesignSystem/DesignSystemPlugin.cs`（现 no-op）、`DesignSystem.csproj`（仅清死引用/编译项）、`plugin.json`（`Version→2.0.0`、`IconUrl` 去占位、`Permissions` 声明）
- 只读参考：`Plugins/Scheduler/Data/Model.xml`（结构样板）、`ForgeSelf.Core/IContext.cs`、`ForgeSelf.Api/Controllers/AIProviderController.cs`（鉴权样板）、`ForgeSelf.Api.Tests/**/McpAdminAuthTests`（反射鉴权断言样板）
- 对应文档同步：`docs/ai/pilot/design-system-v2/03-plan.md` 偏差表、当天工作日记

### Forbidden
- **改任何既有表的 schema**：宿主库、其他插件库、`ForgeSelf.Abstractions` 契约（新 `$type` 封闭枚举属契约变更，须先出 ADR）
- **删除/清空任何数据文件或目录**（铁律 10）；测试夹具不得挂 `ProcessExit`/`Dispose` 做清理
- 手改 `Entities/*.cs`（会被 `xcode` 覆写）；自定义查询写进 `.Biz.cs`
- 新依赖（NuGet/npm）、构建配置变更、CI workflow 改动
- 停/启/杀用户运行中的宿主进程；手工 `Copy-Item` 发布产物
- 改宿主 `ForgeSelf.Web/src/**`（本里程碑不动宿主前端）；改 `ForgeSelf.Web/e2e/**`（留到 M5）
- 顺手重构无关代码、超出 `03-plan.md` Files To Change 清单的文件
- 在本里程碑写 M2–M4 的生成引擎 / 投影 / 前端 section（避免半成品）

## Acceptance Criteria（本单元）
- [ ] `Data/Model.xml` 含 11 表，逐列 `DisplayName/Description`，`ConnName=DesignSystem`、`Namespace=ForgeSelf.Api.Plugins.DesignSystem.Entities`、`Output=Entities`（AC1）
- [ ] `cd Plugins/DesignSystem/Data && xcode Model.xml` 生成成功；连跑两次产物字节一致（幂等）；`BindColumn` diff 无字段漂移
- [ ] `DesignSystemTables.cs`：先 `DAL.Create(conn).Db.ServerVersion` 开库再 `dal.SetTables(...)`；异常抛出（G1）；`Apply()` 首启建表，二次启动不报错（AC2）
- [ ] `(ProjectId,ThemeId,Path)` 唯一冲突 → 409 + 冲突 Path；唯一性校验直查 DB（`FindAll/FindCount`），不读 `Meta.Cache`（AC3，铁律 11）
- [ ] 别名 `a→a`、`a→b→a`、链长 17、`semantic→component` 逆向：各自 400 + 明确原因 + **落库行数不变**；悬空别名：值置空 + 记 critical（AC4/G17）
- [ ] `ResolveEffective`：`ThemeId=0` 共享层 + 主题覆盖按 `Path` 合并，单测覆盖"主题未覆盖某 Path"回落共享（G4）
- [ ] `Generator=manual` 行在批量重新 upsert 默认不被覆盖，回报 `conflicts`；`overwrite=true` 才全量（AC18）
- [ ] 复合 shadow：`ValueJson` 为真源，`DesignShadowLayer` 展开一致；单测锁死一致性与 `(TokenId,Layer)` 唯一（G6）
- [ ] `DesignComponentVariant` 入库前 JSON 键序规范化；同载荷重复提交幂等（G5）
- [ ] 内置图标库首植成功且**只读**（写内置库 → 403/400）（G7）
- [ ] `Oklch` 往返误差 <1e-4；`ContrastMath` ≥6 组黄金色对（白/黑=21、`#767676`/白≈4.54 等）+ AA/AAA/非文本判级（AC10）
- [ ] `GET /api/design-system/meta` 返回 `ModelVersion/GeneratorVersion/ProjectionVersion/能力面清单`；无 token 的任意 design-system 端点 → 401（AC15/FR18）
- [ ] `dotnet build` 0 error 0 warning；`dotnet test --filter DesignSystem` 全绿；跨插件测试无串扰（AC21）
- [ ] `plugin.json` `Version=2.0.0`、无 `example.com`、`frontend` 契约四项未变（AC24 前置）

## Expected Files
- `Plugins/DesignSystem/Data/Model.xml`
- `Plugins/DesignSystem/Data/Entities/{DesignProject,DesignTheme,DesignToken,DesignShadowLayer,DesignComponent,DesignComponentVariant,DesignIcon,DesignScreen,DesignFontFace,DesignAudit,DesignRelease}.cs`（+ `.Biz.cs`）
- `Plugins/DesignSystem/Data/DesignSystemTables.cs`
- `Plugins/DesignSystem/DesignSystemPlugin.cs`
- `Plugins/DesignSystem/Services/{Oklch,ContrastMath,TokenRepository,DesignProjectService,ComponentService,IconService,ScreenService}.cs`
- `Plugins/DesignSystem/Controllers/DesignSystemController.cs`
- `Plugins/DesignSystem/plugin.json`
- `ForgeSelf.Api.Tests/Plugins/DesignSystem/*.cs`
- `docs/ai/pilot/design-system-v2/{03-plan,05-evidence}.md`、`.forgeself/memory/2026-09-28.md`

## Verification Commands
```bash
cd Plugins/DesignSystem/Data && xcode Model.xml                 # 生成实体
cd Plugins/DesignSystem/Data && xcode Model.xml && xcode Model.xml  # 幂等（产物一致）
cd ForgeSelf.Api && dotnet build 2>&1 | tail -20
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" 2>&1 | tail -20
# 鉴权冒烟（同一 bash 调用内启动→curl→kill，用 7102 隔离实例，不碰用户宿主）
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:7102/api/design-system/meta   # 401
grep -n '"Version"' Plugins/DesignSystem/plugin.json
grep -n 'example.com' Plugins/DesignSystem/plugin.json || echo "OK no placeholder"
```
