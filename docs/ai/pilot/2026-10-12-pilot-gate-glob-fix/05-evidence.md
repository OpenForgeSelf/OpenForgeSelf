# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。

## Task

PILOT-2026-10-12-pilot-gate-glob-fix（轻量 mini-task）

## Changed Files

- scripts/verify-pilot-gate.ps1（+24 / -2；唯一改动文件）

## 门禁自测（AC1/AC2）

Command:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-gate.ps1 -Base a00e45d
```

Result: FAIL（预期行为，来源等级：Verified）

```text
FAIL: pilot 闸门未通过（47 项）
EXIT=1
# 完整输出落盘 .temp/gate-run-2026-10-12.log：47 项全部为「越界 diff」；
# 工件链 PASS（2026-10-11-design-overhaul）；无 trailer FAIL；无 3b FAIL
```

- 修复前基线（Edit 工具 LF 中间版，同样输出）：`FAIL: pilot 闸门未通过（47 项）`（Verified）
- 47 项按远程 L2 分类逐行核对（Verified）：后端契约与宿主 5（PluginMetadata.cs/PluginSurfaces.cs/PluginFrontendManifestTests.cs/PluginController.cs/PluginManager.cs）、前端非 vue 源码与单测 15、e2e 8、静态资产 11（6 背景图 + 5 品牌 svg）、构建与生成物 2（components.d.ts/package.json）、插件内非 vue 3（CostScope http.ts/DesignSystem base.css/tokens.css）、治理与基线 3（pre-commit + 两个 baseline json）= 5+15+8+11+2+3+3 = 47
- 21 项误报（src/ 根 .vue 如 App.vue、Plugins/*/web/src/*.vue）在日志中 0 命中（Verified）
- ConvertTo-GlobRegex 语义样例（Inferred，按实现推导，与实测输出一致）：
  - `src/**/*.vue` → `^src/(?:.*/)?[^/]*\.vue(?:/.*)?$`：`src/App.vue` 命中（旧逻辑误判）、`src/views/X.vue` 命中
  - `ForgeSelf.Api/**` → `^ForgeSelf\.Api/.*(?:/.*)?$`：`ForgeSelf.Api/Plugins/PluginManager.cs` 命中
  - `Plugins/*/web/src/*.vue` → `^Plugins/[^/]*/web/src/[^/]*\.vue(?:/.*)?$`：单个 `*` 不跨目录

## 文件保真验证（AC3/AC4）

- BOM：`[IO.File]::ReadAllBytes` 前 3 字节 = EF BB BF → True（Verified）
- EOL：HEAD blob（git cat-file 二进制导出）CR=75、LF=225（混合行尾，BOM=True）；修复后文件 CR=75（原 75 行 CRLF 未动）、LF=247（新增行沿用锚点行 LF）（Verified）
- diff 范围：`git diff --stat scripts/verify-pilot-gate.ps1` → `24 insertions(+), 2 deletions(-)`（Verified）；`git diff` 全文 = 函数插入 + 两行合一，无其他改动（Verified）
- 无其他文件改动：git status 前后比对，脚本之外的工作树改动均为既有未提交项（7 个 .vue/css + 未跟踪 pilot 目录与 scripts/ci/，非本任务引入，未触碰）（Verified）

## 3b 回写检查正则核对（远程疑点，只核对未修改）

- 本地 3b 正则实际字节：`(?s)^\s*---\s*\r?` + LF + `(.*?)` + `\r?` + LF + `---`（脚本文件 LF 行尾，`\r?` 为转义序列，非真实 CR）（Verified，字节转储）
- 合成样本：CRLF 文档 `---\r\nkey: v\r\n---` → match=True；LF 文档 → match=True（Verified）⇒ 该换行形式对两种 EOL 均可匹配，远程「CRLF 文件会匹配失败」的具体机制在本文件不成立
- 真实 04 文件 34 个：matched=8，全部为文件首字节即 `---` 者；NO-MATCH 26 个，文件头均为 `# Agent Task ...` 等标题行（其中 1 个还带 BOM）（Verified，逐文件首 24 字节转储）
- 结论：26/34 个 04 静默跳过 3b 检查的真因是 `^` 锚点 + `(?s)` 未开 `(?m)`（front-matter 不在字符串开头即不匹配），而非换行；verify-pilot-artifacts.ps1:72 同款单行正则受同一锚点限制（Inferred，正则等价推演）

## Unit Test / Integration Test / E2E / Static Analysis

Result: N/A（本次为 PowerShell 门禁脚本修复，无前端/后端代码改动；未跑 §5.6 中档后端全量——协调人明确「下一步只有一个」只跑一条命令，且 ForgeSelf.Api.Tests 存在预存编译阻断（WorkflowEnginePluginRegistrationTests.cs:63 引用不存在类型，见 TODO），全量当前不可跑）

## Screenshots

N/A

## Known Limitations

- 单个 `*` 语义收紧（不再跨目录）：历史任务若依赖旧行为，重跑 `-Base` 会多红；CI 只比新区间，不影响上线（协调人已确认）
- 3b 锚点问题未修（只核对报告；修复需另行批准）

## Unresolved Issues

- 无阻塞。AC1-AC5 全部达成（Verified）。
