# Specification — M13 设计令牌导入 / 回流（design-system 插件）

> 阶段：Stage 2｜从 `00/01` 与真实代码推导（本会话实测过的端点与行为在下文标注 Verified；未证实的一律进 §Unknown）
> Task ID：PILOT-design-system-import ｜ 日期：2026-09-30
> **状态：待闸门 1。本文件是"要建成什么样"的草案，未动任何实现代码。**

## Functional Requirements

| # | 需求 | 判据（可测） |
|---|---|---|
| FR-I1 | `POST api/design-system/projects/{id}/import?format=dtcg&theme=<code>` 接收一份 DTCG JSON，解析成一批 `TokenPatch`，**只经 `TokenRepository.UpsertBatch` 落库** | 用例：导入真样本后库里出现预期 tier/别名行；断言写入口只有一个（不新增第二条落库路径） |
| FR-I2 | `POST .../import/preview` 同载荷但**不落库**，返回"将写入 / 冲突 / 被拒"三张清单（与既有 `generate/preview` 同形） | 用例：preview 后 `DesignToken.FindCount` 不变 |
| FR-I3 | 层级判定规则明确且写进报告：路径首段命中 `semantic.` / `component.` → 该层；否则 `primitive` | 用例：`colors.primary.500` 落 primitive；`semantic.text-1` 落 semantic |
| FR-I4 | `$value` 形如 `{a.b}` → `AliasPath`；字面值 → `Value`；`$type` 不在 `TokenTypes.All` 内 → **整条拒写并回诊断**（不静默丢、不降级成字符串） | 用例：未知 `$type` 的条目出现在 `rejected[]` 且库里无该行 |
| FR-I5 | 手改保护不被绕过：`Generator=manual` 的行默认不覆盖，`overwrite=true` 才覆盖，冲突条数回显（沿用 `UpsertResult.SkippedProtected/Conflicts`） | 用例：先手改一行再导入 → `SkippedProtected ≥ 1` 且值未变 |
| FR-I6 | 来源可追溯：导入行写 `Generator="import:dtcg"`、`GeneratorSeed=sha256(上传字节)` | 用例：导入后读回两列均非空且 seed 等于对同一文件实算的哈希 |
| FR-I7 | 主题归属显式：`theme=<code>` 决定写到哪个主题，缺省写共享层（`ThemeId=0`）；不存在的主题 code → 400 | 用例：非法 code 报 400 且不写库 |
| FR-I8 | 全图校验沿用：成环 / 悬空 / 逆向引用 → **整批不写**，逐条返回诊断（现有 `UpsertBatch` 行为） | 用例：含环样本导入后库里行数不变、诊断条数 ≥1 |
| FR-I9 | 界面入口：导出交付页加「导入」区（选文件 → preview 差异 → 确认写入 → 结果 + 审计重跑提示）；`capabilities` 增 `import`（后端没声明就灰掉） | e2e：真实宿主里走完四步并能在库里读回 |
| FR-I10 | **round-trip 稳定**：对**我们自己**导出的 DTCG，`export → import → export` 必须逐字一致 | 用例：两次 `ToDtcg` 输出字符串相等；不一致则必须把差异列成可解释清单 |

## Input

- 媒体类型：`application/json`，W3C DTCG 形状（组嵌套 + `$value` / `$type` / `$description` / `$extensions`，`$type` 沿树继承 —— 与我方导出一致，Verified：`ExportService.ToDtcg`）。
- 查询参数：`format`（首期只 `dtcg`）、`theme`（可选主题 code）、`overwrite`（默认 `false`）。
- 上限：单次条目数与字节数上限见 §Unknown U5（先按现有导出的实测规模 1476 令牌量级设计）。

## Output

- `preview`：`{ willWrite: [{path, tier, type, alias|value, themeCode}], conflicts: [path], rejected: [{path, reason}], counts: {...} }`
- `import`：`{ created, updated, skippedProtected, conflicts[], diagnostics[{path,status,message}], auditHint }`（`auditHint` 提示"导入后需重跑审计"，不替用户静默跑）。
- 失败：400 + `{error, diagnostics[]}`；**不出现部分写入**。

## Business Rules

1. 导入不是新真相来源：它只是"外部文件 → TokenPatch → 既有写入口"，校验、事务、手改保护全部沿用（`UpsertBatch` 整批事务，Verified）。
2. 层级/类型只能从**证据**推：路径前缀 + `$type`；推不出来的拒写，不猜。
3. 导入必须可复现、可追溯：同一文件 + 同一参数 → 同一结果（`GeneratorSeed` 记内容哈希）。
4. 导入不得改变既有生成器行为：`Generator=generator*` 的行按现有"重跑只补空"规则处理，不由导入越权覆盖。

## Boundary Conditions

- 空对象 `{}` / 只有组没有叶子 → `created=0`，如实返回 0，不报错也不假装成功。
- 同一批里重复 path → 现有 `UpsertBatch` 已按"载荷内自重复"整批拒（Verified）。
- 复合值（shadow 的数组、typography 对象）→ 首期**拒写并回诊断**（形状映射未定，见 U3）。
- 文件里带 `$extensions.forgeself.*`（我方 tint/css 扩展）→ 原样保留还是丢弃：见 U4。
- 主题 code 大小写、路径含空格 → 走现有 `NormalizePath` + `naming` 审计，导入不另开宽松通道。

## Error Handling

- 解析失败（非 JSON）→ 400 + 位置信息；未知 `$type`/复合值 → 条目级 `rejected[]`；图校验失败 → 整批拒 + 诊断清单。
- 任何"跳过"都必须在响应里计数并可见；**静默丢条目 = 缺陷**（本仓既有纪律）。

## Compatibility

- 不改表结构、不加 NuGet/npm 依赖（用现有 `System.Text.Json`）。
- `GET /meta` 增 `importFormats: ["dtcg"]`（前端据此显式降级）；既有端点行为不变。
- 版本三元组递增到 **2.7.0**（新端点 + 新能力声明）；`plugin.json` 同步。
- 旧项目/旧快照不受影响；导入写库后草稿态审计需重跑（`Record` 已会清掉本轮未产出的旧行，v2.6.2）。

## Non-functional Requirements

- 一次 1500 条目级导入耗时与锁窗口可接受（整批一个事务，与 `AuditRepository.Record` 同理）；实测数字写进 evidence。
- 失败不留半成品；SQLite 撞锁沿用现有止痛（`Busy Timeout`），根治仍属宿主待拍板项。

## Acceptance Criteria（闸门 1 要确认的就是这张表）

1. FR-I1…I10 各有对应用例且全绿（后端 `dotnet test`）。
2. round-trip：`export?format=dtcg` → `import` → `export` 逐字一致（FR-I10）。
3. 反例必须被拒：含环 / 未知 `$type` 两份样本 → 整批不写 + 诊断可读（FR-I4/I8）。
4. 手改保护仍生效（FR-I5）。
5. 导入后跑审计，新令牌进入判定（对比度/分层/命名有结论行）（FR-I9 的后端侧）。
6. 插件层 e2e 在真实宿主走完"选文件→预览→写入→库里读得到→再导出对得上"，并留证据行。
7. 四层门禁全绿 + 本地 zip 重打包 + 包内探针命中 + 文档回写（README / ROADMAP / `036` / 技能）。

## Unknown

| # | 不确定点 | 影响 | 处理方式 |
|---|---|---|---|
| U1 | Tokens Studio 的 `$themes` / `$sets` 与我方"主题轴（color/density/brand）"是否一一对应 | 决定首期是否只承诺 DTCG | **询问**（Intent Q1/Q5）；未定前不进 Spec 正文 |
| U2 | Figma Variables 原生 JSON（非 DTCG）要不要直接吃 | 多做一种解析面 | 搁置（等 U1 结论；Figma 也能导出 DTCG） |
| U3 | 复合令牌（shadow 展开层、typography）的导入形状：吃 `$value` 数组还是只吃我方 `forgeself` 扩展 | 影响 FR-I4 的拒绝范围 | 保守假设并标注：**首期一律拒写**，只收标量与别名 |
| U4 | 导入是否保留文件里已有的 `$extensions`（可能与我方 tint 语义撞名） | 影响 round-trip 与污染风险 | 保守假设并标注：只保留 `forgeself.*` 白名单前缀，其余丢弃并计入报告 |
| U5 | 单次导入的条目/字节上限 | 影响锁窗口与超时 | 询问（默认先按 5000 条目 / 4 MB 设计并在实现里显式报错） |
| U6 | 导入能否**新建主题**（文件里带 mode 而我方没有） | 影响 FR-I7 与主题治理 | 保守假设并标注：**不允许**，只写已存在的主题或共享层 |
