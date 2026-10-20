# 02 同类扫查 Playbook（P3）

> 扫查是本任务的**必做环节**，不是可选优化。产出一张命中表进 case §4，没有命中表 = 任务未完成。

## 扫描范围矩阵（铸己匣实测定稿，别少扫）

| 域 | 路径 | 备注 |
|---|---|---|
| 插件后端 | `Plugins/**/*.cs` **和** `ForgeSelf.Api/Plugins/**/*.cs` | ⚠️ 本仓有**两棵插件树**，只扫一棵必漏（PC-001 G2 测试即扫两棵） |
| 宿主 | `ForgeSelf.Api/**/*.cs`、`ForgeSelf.Core/**`、`ForgeSelf.Abstractions/**` | 契约/seed 清单在 Abstractions 与 PluginManager |
| 宿主前端 | `ForgeSelf.Web/src/**` | fetch/路由/组件 |
| 插件前端 | `Plugins/*/web/src/**` | http.ts 封装是否被绕过 |
| 脚本 | `scripts/**/*.ps1`（+ 技能内 scripts/） | 编码/BOM/调用方式类问题 |
| 测试与 e2e | `ForgeSelf.Api.Tests/**`、`ForgeSelf.Web/e2e/**`（含 `e2e/plugins/**`） | 守卫本身也可能同根因失效 |
| 文档 | `docs/04-standards/**`、`AGENTS.md`、`.agents/skills/**` | 规则/流程类问题（弃用词残留、散文教训） |

## 签名写法（按优先级）

1. **枚举式（首选，对应 G2）**：能写出"遍历全部 X 断言 Y"的就不写 grep。参考实现：
   - `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（契约消费点、裸 fetch、连接名三个枚举测试）
   - `ForgeSelf.Api.Tests/RepositoryScriptTests.cs`（*.ps1 BOM 逐文件 + 阳性对照）
   - 要点：扫描集合必须带**阳性对照**（`scanned.Should().BeGreaterThan(N)`），防空转假绿。
2. **grep 签名（G3，跨语言/文档场景）**：front-matter 一行 flow-map：
   `{id: S1, kind: grep, glob: "Plugins/**/*.cs", regex: "...", expect: no-match}`
   - `expect` 语义要写明确：`no-match`（命中即红）/ `any-match-with-file-level-token-check`（自定义语义须在 PC §2 解释判据）。
   - 正则天然脆弱：落签名前先在本地真跑一遍，把**存量合规命中**（如带 token 的 authFetch 封装）设计进判据或 allowlist，别指望"以后再说"。
3. **禁用的写法**：把"注意/建议/小心"当签名（不可执行）；用一次性 `temp/*.cjs` 当扫查手段（AGENTS 红线，结论必须沉淀为可重复测试）。

## 命中表格式（进 case §4）

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|---|---|---|
| Plugins/X/...:123 | 是 | 已修（本批，canonical fix） |
| ForgeSelf.Web/...:45 | 是 | 已守卫（allowlist 入 PC-NNN，expires=…） |
| scripts/....ps1:8 | 否（异根因） | TODO.md 新条目（来源:输入N） |

**处置规则**（铁律 3）：同根因命中 = 本任务范围，用 canonical fix 统一修；异根因才进 TODO。拿不准"同不同根因"时，按不变量逐字对照判断，不靠表象。

## 扫查超限（>10 处或触硬约束）

修当前实例 → 建 G3 守卫 **baseline 模式**（存量全部入 allowlist，每项带 `reason` + `expires`，过期即红）→ 余项列 TODO（标题带 PC id）→ 在 07「问题类归档」节披露超限原因。allowlist 不是豁免，是**带利息的债**。
