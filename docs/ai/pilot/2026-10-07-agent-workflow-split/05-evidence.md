# 05 Evidence — AW-SPLIT-20261007

分级：**Verified**（本会话有真实命令回执）/ **Inferred**（推断未实测）/ **Unknown**（没证据）。三档不混用。

## 1. 变更清单（实际发生的）

| 动作 | 文件 | 读数 |
| --- | --- | --- |
| 新建 | `docs/04-standards/agent-workflow/README.md` | 52 行（索引 + §编号地址表 + 维护规则 + 历史行号声明） |
| 新建 | `…/a-workflow-core.md` | 209 行（原文 14–216 + 4 行分册头） |
| 新建 | `…/a-workflow-docs-report.md` | 244 行（原文 217–454） |
| 新建 | `…/b-engineering-daily.md` | 317 行（原文 455–763 的分册头 **+ 本批新增 §B6 一条 2 行**） |
| 新建 | `…/b-engineering-platform.md` | 148 行（原文 764–905） |
| 新建 | `…/c-changelog.md` | 27 行（原文 906–925） |
| 覆写 | `docs/04-standards/agent-workflow.md` | 925 行 → **15 行**薄指路 |
| 改指路 | `AGENTS.md` 18 行 / `docs/README.md` 6 / `packaging-upgrade-backup.md` 3 / `ai-native-engineering-workflow.md` 2 | 脚本自报"改写行"数；`git diff --numstat` 对账见 §3 |

## 2. Verified（判据读数）

- **V1 AC1 拆分零丢失**：`.temp/tmp/split-agent-workflow.ps1` 正文拼接 SHA256 == 原文 `14..end` SHA256
  `B6F363BB34F2E7B97F4A8A81AA03DF542F7CC08EE3284F0D33B06A036421737A`（两侧同串），`reconLines=912 expectedLines=912` → **PASS**（日志 `.temp/tmp/awsplit-check.log`）。
  ⚠️ 口径：**这条证的是"拆分动作本身"**（E2 当时），后续两处有意改动不再由它覆盖，改由 V2 逐条列账。
- **V2 AC2 逐行回证（终态）**：`.temp/tmp/diff-recon.ps1` → `recon 正文行=765`、`原文件 14..end 非空行=763`、
  **差异 4 条 = `<=`1 + `=>`3**，且这 4 条**恰好且只是**本批两处有意改动：
  ① `a-workflow-docs-report.md:133` 反向同步 SOP 的跨层指针（`doc-reverse-sync-sop.md（同目录）` → `../doc-reverse-sync-sop.md`）；
  ② `b-engineering-daily.md` §B6 末新增一条 BOM 教训（2 行正文）。 ⇒ 除这两处外与原文逐行一致。
- **V3 AC2′ 标题清单闭合**：原文 14..end 的 **25 个 H1/H2** 在分册中**恰好各出现一次**（`AC2 PASS`）。
- **V4 AC3 体量**：`wc -l` 终态实测 **15 / 52 / 209 / 244 / 317 / 148 / 27**（合计 1012 行含新增头与索引）；最大分册 **317 行**（B1–B6），
  原 925 行单文件已只剩 15 行指路 ⇒ 单次阅读量下降约 **2/3 ~ 6/7**（按 §编号只取一分册计）。
- **V5 AC4 指路可达**：`repoint-agent-workflow.ps1` 改写 **AGENTS.md 18 / docs/README 6 / packaging 3 / ai-native 2 = 29 行**；
  改写后四文件中残留 `agent-workflow.md` 字样的行 = **AGENTS.md 1 行（L195）**，其余三份 **0**。
  那 1 行**刻意不改**：它位于并行会话未提交的 §5.0 hunk 内（`§B2` 指向），改它等于代对方提交内容；该路径由薄指路文件仍可解析。
- **V6 AC5 链接**：分册内全部 markdown 相对链接实测为 `](README.md)`/`](a-workflow-*.md)`/`](b-engineering-*.md)`/`](c-changelog.md)` 六类，均为**同目录**目标 → 可解析；唯一跨层目标（V2①）已修。
- **V7 BOM 一致性**：`head -c 3` 实测 `AGENTS.md`/`packaging-upgrade-backup.md` 工作区与 HEAD **同为 `efbbbf`**；`docs/README.md`/`ai-native…md` 同为 `2320…`（无 BOM）。分册按规格写成无 BOM。
- **V8 无自动断言**：`grep -rn "agent-workflow" ForgeSelf.Api.Tests/ scripts/ ForgeSelf.Web/package.json .github/` → **零命中** ⇒ 拆分不触碰任何守卫/CI 假设（范围限定于所列路径，不含 `docs/`）。

## 3. 范围与偏差

- `git diff --numstat` 四份被改文档：AGENTS.md **25/25**（= 我的 18 行改写 + 他人 §5.0 移位的 7/7，二者同文件不同 hunk）、docs/README **6/6**、packaging **3/3**、ai-native **2/2**。
- 偏差 D1：**04-task 首次写错目录**（落到 `docs/ai/pilot/2026-10-11-placeholder/`）→ `mv` 回本任务目录 + `rmdir` 空壳，无残渣（实测 `ls docs/ai/pilot | grep placeholder` 空）。
- 偏差 D2：**repoint 脚本用 `UTF8Encoding($false)` 写回，剥掉了 AGENTS.md 与 packaging 的 BOM**（当场 `od` 实证 `23204f` vs HEAD `efbbbf`）→ 用字节级前置补回并复验（V7）。教训已按 §2.5 回写进 §B6（V2②），**这是本批唯一新增的规则文字**。
- 偏差 D3：AC1 的判据脚本 AC2 首版把"原文 H1（第 1 行）"也算进清单 → 假红一次；修正为只对 `14..end` 取清单后 PASS。

## 4. Unknown / 未做

- U1 **未跑任何构建或测试**：纯文档改动，无代码路径受影响；且宿主 `dotnet build` 当前仍被并行会话 McpCenter 的 9 个编译错误挡着（非本批），中档全量此刻跑不了——**本批未验证该档，如实标注**。
- U2 历史 pilot 工件中 `agent-workflow.md:551` 式**行号引用**在新布局下不再对应同一位置（Inferred，按定义即失效）；处置＝薄指路 + README 历史说明，不回改。
- U3 是否有仓外（用户笔记/其它仓库）按行号引用该文件 ⇒ Unknown，由旧路径继续存在兜住。

## 5. 一次性脚本处置

`.temp/tmp/` 下 `split-agent-workflow.ps1` / `repoint-agent-workflow.ps1` / `diff-recon.ps1` 均为本次操作脚本（`.temp` 在 gitignore 内，未入库）。
**复核方式不依赖它们**：任何人可用 `git show 507044a:docs/04-standards/agent-workflow.md` 取回原件，与本目录分册正文按 §编号逐行对照（V2 的判据即此）。
