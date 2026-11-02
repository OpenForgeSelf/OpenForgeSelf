# AI-Native Pilot Result（最终汇报）

> PILOT-076 ｜ 2026-10-30 ｜ 任务：版本行落 csproj + release-local 缺省读 csproj（2026-10-30 输入3，闸门1 已批版本行 2.4.22）

## 1. Repository Understanding

我确认了：版本链真源 = `docs/04-standards/packaging-upgrade-backup.md` §1.1/§4-R10（发行串 = 三段号 + 10 位时间码，tag/versions/zip/exe FileVersion/设置页同串）；发行串由 `release-local.ps1` 单点生成、经 `FORGESELF_RELEASE_VERSION` 环境变量注入两个 exe csproj（禁止 `-p:Version`，NU1105/NETSDK1018）；csproj 旧兜底 `2.3 + 0.<时间码>` 已与真实发行线 2.4.22 脱节；`0.0.0-local` 为旧本地缺省；PC-006（.ps1 BOM）守卫在位。

## 2. Selected Task

输入3 三条：① 最新版本号进 csproj；② release-local 不传版本时缺省读 csproj 版本、解析固定部分；③ 文档同步「以后发布 zip 不需指定版本，且不得自行改 csproj 版本（除非得到允许）」。

## 3. Changed Files

- `ForgeSelf.Api/ForgeSelf.Api.csproj`（VersionPrefix **2.4.22** + 纯时间码后缀 + 注释块）
- `scripts/release/release-lib.ps1`（新增 `Get-CsprojReleaseVersion`）
- `scripts/release/release-local.ps1`（缺省三分支；`0.0.0-local` 废止）
- `ForgeSelf.Api.Tests/RepositoryReleaseVersionTests.cs`（新增 9 守卫用例）
- 文档 5 份：真源 §1.1/R10/变更记录、`AGENTS.md` §2.3、`scripts/README.md`、`plugin-development/SKILL.md`×3、`b-engineering-daily.md` B6 扩展（BOM 沉淀）
- 数据操作：Sems 命令 6 去 `-Version`（fs 网关，已回读核验）

## 4. Validation

Build: PASS（两 .ps1 AST parseErrors=0；全量构建 0 error CS）— Verified

Unit Test: 守卫集红 9 失败/0 通过 → 绿 **9/9（exit 0）** — Verified；中档全量（scripts/** 触发）**3324 例 / 65 红 / 3259 绿**，65 红逐条对表 10-29 基线 `test-full-7.log`：**0 条归本批**（50 基线既有 + 15 他会话 68df669 新批 + 1 flake 单跑复绿）— Verified

发布链端到端（2026-10-30 19:14，用户输入7 授权）：`release-local.ps1 -Sign` **不传 -Version 真跑缺省链** → 发行串 **2.4.22.2610301914**；zip 161.8MB 落 `D:\src\my-proj\OpenForgeSelf\updates`，SHA256 对表 MATCH，19 插件落位，**两 exe FileVersion 同串**，Authenticode Valid+时间戳 — Verified

E2E: N/A（零 UI 改动，02-spec「无界面交互」节申报）— Verified（边界真实）

## 5. Evidence

`docs/ai/pilot/2026-10-30-release-version-from-csproj/05-evidence.md`：AC1-AC10 逐条映射（AC2 已随发布实跑回填 **端到端 Verified**；AC5 CI 实跑留待下次 tag）；事故与处置记录 4 条（edit 剥 BOM→补回；孤儿 testhost 锁 DLL→清理重跑；RepoRoot 必填规格微调；发布首跑 MSB6006→build-server shutdown 复跑过）；函数冒烟 `Get-CsprojReleaseVersion` → `2.4.22` 实测输出。

## 6. Review

`06-review.md` Final Decision = **APPROVED**（八问全过；Minor 2 条均为他批归属，已入 TODO 移交）。闸门2 待用户验收；通过前不提交 git。

## 7. Risk

L1。~~残余风险：完整发布链未实跑~~ **已闭环**（2026-10-30 19:14 用户授权真跑：缺省链日志行实出 + 2.4.22.2610301914 验包全绿）。现残余：CI 打 tag 通道待下次 tag 自然覆盖（AC5 保持代码级 Verified）。

## 8. Problems Found

1. edit 工具剥 .ps1 UTF-8 BOM（B6 规则扩展已沉淀：任何工具改 .ps1 后字节复核）。
2. job_kill 留孤儿 testhost 锁 DLL + 「job 命令末语句为字符串 → exit 0 假象」——再次实证 §5.0「看日志正文，不拿 exit code 当证据」。
3. 基线三类他批遗留：`backfill-feature-ids.ps1` 缺 BOM（30b076f）、AIAgent ProjectWorkspaceDefaultRootTests 15 红（68df669）、CancelAsync 竞速 flake。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（1 处红灯驱动微调，已申报） |
| Code → Test | PASS（TDD 红灯先于实现，9 红→9 绿） |
| Test → Evidence | PASS（中档全量对表 + 事故留痕） |
| Evidence → Review | PASS |

## 10. 问题类归档

无新建产品问题类 + 理由：本批为功能增强（非缺陷/回归/返工），不触发 problem-class-closure 门禁。执行期两起流程事故的沉淀归属：①「工具剥 .ps1 BOM」并入既有 B6 BOM 类（`b-engineering-daily.md` §B6 2026-10-30 扩展条，非 docs/11-troubleshooting 产品缺陷类）；②「孤儿 testhost / exit code 假象」属 §5.0 既有判则的再实证（05-evidence 事故记录 ②，不另立类）。

## 11. 重构提案

R1（局部，不排期，仅披露）：`ForgeSelf.Bootstrapper.csproj` 的兜底版本串仍是旧形（`2.3 + 0.<时间码>`）——发布态经注入不受影响，仅独立 dev 构建走兜底；待下次触及 Bootstrapper 构建面时把兜底改为「读 Api csproj 或同形纯时间码」，消除双形。按 problem-class-closure 分档属 R1：07 披露即可，未列入闸门范围。

## 12. 悬空教训清单（必须为空）

空——本报告每条遗留均已落成可检索条目：

- 「backfill-feature-ids.ps1 补 BOM」→ **已修**（2026-10-30 输入7 用户批准代修：字节级前置 +3 字节，守卫 26/26 绿 `pilot076-bom-guard.log`；未提交，随闸门3 入库）
- 「AIAgent ProjectWorkspaceDefaultRootTests 15 红（68df669 批）」→ TODO.md ⬜ 条目（P2，来源:输入3）
- 「ScriptRunnerTests.CancelAsync flake」→ TODO.md ⬜ 条目（P3，来源:输入3）
- 「Bootstrapper csproj 兜底串 R1」→ 本节 §11 载体 + TODO.md ⬜ 条目（P3，来源:输入3）
