# 07 Final Report — AW-SPLIT-20261007

## Task
把全仓最大的规则库 `docs/04-standards/agent-workflow.md`（925 行 / 136,339 B）拆分为主题分册目录，
地址（§编号）不漂移、内容零改写、指路全部可达。来源：用户 2026-10-07 指令「agent-workflow.md 太长的话要拆分文件」，闸门1 批准「按 5 份拆分，执行 E1→E7」。

## Validation（实测，全部来自命令回执）

| # | 判据 | 读数 | 级别 |
| --- | --- | --- | --- |
| V1 | 拆分零丢失（正文拼接 == 原文 14..end） | SHA256 两侧同为 `B6F363BB…21737A`，`912/912` 行 → **PASS** | Verified |
| V2 | 终态逐行回证 | `recon 765` vs `原文非空 763`，差异 **4 条 = 1 改词 + 3 新增**，逐条可指认（跨层指针修正 + §B6 一条教训） | Verified |
| V3 | 标题清单闭合 | 原文 **25 个 H1/H2** 在分册中恰好各一次 → **PASS** | Verified |
| V4 | 体量 | `15 / 52 / 209 / 244 / 317 / 148 / 27` 行（最大分册 317；旧文件只剩 15 行指路） | Verified |
| V5 | 指路可达 | 改写 **29 行**（AGENTS 18 / README 6 / packaging 3 / ai-native 2）；残留 1 行＝他人未提交 hunk 内，刻意不代改，仍由薄指路解析 | Verified |
| V6 | 相对链接 | 分册内链接全为同目录目标；唯一跨层目标（`../doc-reverse-sync-sop.md`）已修 | Verified |
| V7 | BOM 一致性 | `head -c 3` 工作区 == HEAD（`AGENTS.md`/packaging `efbbbf`；README/ai-native 无 BOM）；自误剥离已按字节补回 | Verified |
| V8 | 无自动断言被触发 | `grep -rn "agent-workflow"` 于 `ForgeSelf.Api.Tests/`、`scripts/`、`ForgeSelf.Web/package.json`、`.github/` → **零命中** | Verified |
| — | 构建 / 测试 | **本批未跑**（纯文档；宿主被他人 McpCenter 9 个编译错误挡住，中档全量此刻不可用） | Unknown（如实标注，不冒充通过） |

## Review
`06-review.md`：**Final Decision = APPROVED**。
两条轴自查：Standards——§编号即地址的约定未被破坏、"同一事实只一处承载"（维护规则只在 README 一处，别处指路）；
Spec——FR-1…FR-5 全部落地，AC1…AC7 满足，U-1/U-2 仍按 Unknown 记录。

## 交付物
- `docs/04-standards/agent-workflow/README.md`（索引 + §编号→分册地址表 + 维护规则 + 历史行号声明）
- `…/a-workflow-core.md`（A1–A5）、`…/a-workflow-docs-report.md`（A6–A10，含本会话新立的 §2.5 细则）
- `…/b-engineering-daily.md`（B1–B6）、`…/b-engineering-platform.md`（B7–B12）、`…/c-changelog.md`（Part C）
- `docs/04-standards/agent-workflow.md` → 15 行薄指路（兼容层）
- 4 份活文档的 29 行指路改写；§B6 一条新教训

## 沉淀（已回写，不排队）
- **新规则**：批量改写文档的脚本必须保留原文件 BOM（判据 `head -c 3` 前后一致；补法用字节级前置）→ `agent-workflow/b-engineering-daily.md` §B6。
- **判据教训**：AC2 首版假红源于基线含被刻意移出的 H1 ⇒ 反例先怀疑判据再怀疑被验物（记入 06 §6）。

## 未做（NOT-to-do，理由见 06 §7 与 not-taken）
- 不把一次性拆分脚本固化为 `scripts/` 资产；不回收历史 pilot 工件里的行号引用；不改 A8（已弃用节）留档原文。

## 状态
**⚠️ COMPLETED_WITH_RISK**：目标达成、判据 Verified；风险＝本批未跑任何构建/测试（纯文档，无代码路径），
且 AGENTS.md 与并行会话同文件（已用 hunk 级暂存隔离，仍需提交后由对方复核其 §5.0 hunk 完好）。
