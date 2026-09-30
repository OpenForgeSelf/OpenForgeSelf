# Plan — M13 设计令牌导入 / 回流（design-system 插件）

> 阶段：Stage 3｜依据 `01-intent.md`（成功判据 7 条）与 `02-spec.md`（FR-I1…I10）
> **闸门 1：已过**（2026-09-30 用户拍板「按推荐开工：DTCG·只进已有项目·默认不覆盖·带预览」；提交口径＝「先不提交，继续做」）
> Task ID：PILOT-design-system-import ｜ 目标版本：**2.7.0**（新端点 + 新能力声明 ⇒ minor）

## 一、两处对 Spec 的偏离（先记录再修正，不偷偷改）

| # | Spec 原文 | 计划改成 | 为什么 |
|---|---|---|---|
| D1 | FR-I6 导入行写 `Generator="import:dtcg"` | 写 `Generator=TokenGenerators.Imported`（常量值 `imported`），格式与内容哈希写进 `Extensions` 的 `forgeself.import.*` | `TokenGenerators` 里**已经有** `Imported`，且 `IsProtected()` 与 `UpsertBatch` 的保护判定都认它。再发明一个 `"import:dtcg"` 字面量 = 同一件事两份真相，且**手改保护会当场失效**（`ProtectedGenerators` 不认它）——这是会直接砸掉 FR-I5 的 |
| D2 | U3「复合令牌首期一律拒写」 | **接受** object/array 形状（存 `ValueJson`），只拒未知 `$type` 与解析失败 | 我们**自己的** DTCG 导出里 shadow / typography / transition / border / gradient 就是复合 `$value`（`ExportService.BuildToken` 直接写 `ValueJson`）。拒写复合值 ⇒ FR-I10「export → import → export 逐字一致」根本不可能成立，round-trip 判据自相矛盾。外部未知形状按原样存 + 行标 `imported` 可追溯；投影不认的形状不会崩，只是不产出该条（审计仍会报它） |

U1/U5/U6 按推荐口径落定：首期只 DTCG；上限 5000 条目 / 4 MB（超限显式 400）；**不新建主题**（未知 theme code → 400）。

## 二、落点（真实文件）

| 动作 | 文件 | 内容 |
|---|---|---|
| 新增 | `Plugins/DesignSystem/Services/DtcgImporter.cs` | 纯函数式：`ImportPlan Parse(JsonElement doc, IReadOnlyList<String> themeCodes, String? themeCode)` → `{ Patches, Rejected[], Counts, DocumentMeta }`。**不落库、不读库**（可单测、可被 preview 与 import 共用同一份解析） |
| 改 | `Plugins/DesignSystem/Services/DesignSystemConstants.cs` | 版本三元组 → `2.7.0`；新增 `ImportFormats { Dtcg, All }` 与 `ImportLimits { MaxEntries = 5000, MaxBytes = 4_194_304 }`（上限只在这里定义一次） |
| 改 | `Plugins/DesignSystem/Controllers/DesignSystemController.cs` | `[HttpPost("projects/{id}/import/preview")]`、`[HttpPost("projects/{id}/import")]`；`/meta` 增 `importFormats` + `importLimits`，`capabilities` 追加 `"import"` |
| 改 | `Plugins/DesignSystem/plugin.json` | `Version` → `2.7.0` |
| 改 | `Plugins/DesignSystem/web/src/api.ts` | `importPreview(id, doc, q)` / `importTokens(id, doc, q)`；`MetaInfo` 增 `importFormats` / `importLimits` |
| 改 | `Plugins/DesignSystem/web/src/sections/ExportCenter.vue` | 新增「导入 / 回流」区：`<input type=file>` → 本地解析成 JSON 文本 → preview 表（将写入 / 冲突 / 被拒 三张清单 + 计数）→「确认写入」按钮（`overwrite` 显式勾选）→ 结果行 + 「导入后请重跑审计」提示 |
| 改 | `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/`（新文件 `ImportTests.cs`） | FR-I1…I10 的判据用例（见 §四） |
| 改 | `ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts` | 新步骤：真宿主里「选文件 → 预览 → 写入 → 库里读得到 → 再导出对得上」 |

**不碰**：`Data/Model.xml` 与实体（无表结构变更）、`ExportService`（导出侧已能出 DTCG；round-trip 用现成的）、宿主任何文件、依赖清单。

## 三、解析规则（写死在计划里，实现照此，不留临场判断）

1. **组 vs 叶子**：对象里含 `$value` 即叶子；其余键是组。`$schema` / `$extensions`（根级）/ `$description` / `$type` 等 `$` 前缀键不算组。
2. **路径** = 从根到叶的键名以 `.` 连接（我们导出的 `Insert()` 就是这个规则的逆）。路径段含点号无法表达 ⇒ 与导出同侧的既有限制，不另开通道。
3. **`$type` 继承**：沿树向下携带（对应 `ApplyTypeInheritance`）；叶子无 `$type` 且父链也没有 → 该条 `rejected`（reason=`缺 $type`），不猜成 color。
4. **值映射**：
   - `$value` 是字符串且匹配 `^\{(.+)\}$` → `AliasPath = 捕获组`、`Value = ""`；
   - `$value` 是 string/number/boolean → `Value = 文本`、`AliasPath = ""`；
   - `$value` 是 object/array → `ValueJson = 原样 JSON 文本`、`Value = ""`、`AliasPath = ""`；
   - `$type` ∉ `TokenTypes.All` → `rejected`（reason=`未知 $type: xxx`），**不降级成 string**。
5. **层级**（FR-I3）：首段 `semantic` → semantic；`component` → component；其余 → primitive。
6. **主题**（FR-I7）：`theme` 缺省 → `SharedThemeId`；给了 code 但不在该项目主题清单 → 整个请求 400（不是条目级拒写，因为它是"参数错"）。
7. **来源**（FR-I6/D1）：`Generator=imported`、`GeneratorSeed=sha256(上传字节 hex 小写)`、`Extensions` 里并上 `{"forgeself":{"import":{"format":"dtcg","seed":"<sha>","at":"<iso>"}}}`（文件自带的 `$extensions` 原样保留，`forgeself` 键下我们自己的字段覆盖同名）。
8. **上限**：条目数 > `ImportLimits.MaxEntries` 或字节 > `MaxBytes` → 400 + 明确文案（不静默截断）。
9. **空文档** `{}` / 只有组 → `counts.entries = 0`，preview 返回三张空清单，import 返回 `created=0/updated=0`，**不报错也不假装成功**。

## 四、判据 → 用例映射（每条 FR 至少一条，全在 `dotnet test` 里）

| FR | 用例名（`ImportTests`） | 断言的**事实** |
|---|---|---|
| I1 | 导入DTCG_走唯一写入口_库里出现预期层级与别名行 | 行数/tier/AliasPath；并断言 `UpsertBatch` 是唯一落库路径（读码级：`DtcgImporter` 不引用任何实体 Save） |
| I2 | preview不落库_计数与import一致 | preview 前后 `DesignToken.FindCount` 相等，且 preview.willWrite 计数 == import 的 created+updated |
| I3 | 层级由路径首段判定_不猜 | `colors.primary.500`→primitive、`semantic.text-1`→semantic、`component.card.background`→component |
| I4 | 未知type整条拒写并回诊断 | `rejected[].reason` 含类型名；库里无该行 |
| I5 | 手改保护默认生效_显式覆盖才改 | 先 `UpsertOne(Generator=manual)` 改值 → 导入 → `SkippedProtected ≥ 1` 且值未变；`overwrite=true` 才变 |
| I6 | 来源可追溯_generator为imported且seed是内容哈希 | 读回 `Generator=="imported"`、`GeneratorSeed==sha256(文件字节)`（测试里自己算一遍） |
| I7 | 非法主题码400_缺省写共享层 | 两分支各一条 |
| I8 | 成环样本整批不写 | 库里行数不变 + 诊断 ≥1 |
| I9 | 导入后审计能看见新令牌 | 跑审计 → `audit?kind=naming`（或 contrast）里出现新路径的结论行 |
| I10 | round-trip逐字一致 | `export?format=dtcg` → import → 再 export → 两次字符串 `Equal`（不一致就把差异列进断言消息） |
| 边界 | 空文档返回0不报错 / 超限400 | 两条 |

## 五、实现顺序（每步改完即可验）

1. 常量 + `DtcgImporter`（纯函数）→ 先跑 `ImportTests` 的 I3/I4/I9 解析面（此时还没有 HTTP）；
2. 控制器两端点 + `/meta` 能力 → 跑 I1/I2/I5/I7/I8/I10 + 边界；
3. 插件前端（api + ExportCenter）→ `pnpm run check` + vitest（解析/预览表的数据形状用单测钉，不依赖挂载）；
4. 版本三元组 + `plugin.json` → 2.7.0；
5. 四层门禁串行 → zip v2.7.0 → 包内探针（DLL FOUND `DtcgImporter`/`importFormats`；UI FOUND `导入`）→ 文档回写。

## 六、Forbidden（本轮不许做）

- 不新增 NuGet/npm 依赖；不改 `Model.xml`/实体生成物；不给导入开第二条落库路径；
- 不做 Tokens Studio / Figma 原生 JSON 解析（Q5 另立）；不新建主题；不静默跑审计（只提示）；
- 不停/启/杀任何宿主进程；不 commit（用户口径：先不提交）；
- 不把 preview 做成"前端自己算差异"——preview 与 import 必须共用同一个 `Parse()`，否则两份真相。
