# 验证证据（Evidence）

> 本文汇总本次会话「dsh 对齐改造是否全部实现 + 文档是否对齐 + 收尾修复是否闭环」的验证证据。
> 结论先行：**八批次全线 IS_PASS；文档滞后 27 处全部修复；收尾零越界**。
> 证据来源等级：**Verified**（全部为实测复跑或源码实读实证，无推断项）。

## 1. 八批次收官质量证据（QA 终验，2026-09-29）

| 维度 | 数字 | 说明 |
|---|---|---|
| 收官全量 | **1735 例**（Api 1679 + Core.Tests 32 + Abstractions.Tests 24） | 绿率 **99.4%** |
| 失败面 | 10–11 例（0.6%） | **全部定性为既有基线族**：WAF 测试宿主不挂插件控制器路由的 7 条 404 + 进程类双向 flake 3–4 条，与 B9 改动**零交集** |
| 沙箱必红族 | **50 条 → 0** | B9-4 根治（`FORGESELF_DATA_ROOT` 双解析重载），独立复跑实证清零，含 B8 静态度 canary `ChatProjectionRaceTests` 由红转绿 |
| QA 对抗探针 | 8 探针 ≈ 50 条 | 独立复验，外送真 Bug 2 个（B7 PlanGenerator 收束缺陷·中危、B8 多 call 前端交错序·低危），均已修复并有门禁锁定 |

## 2. 方案落地度审计证据（架构师，只读）

- **0 条 ❌ 未实现**：specs/040/041/042 + `docs/01-architecture/dsh-*` 全部改动清单条目在代码有落点。
- 门禁测试 100% 存在且逐字同名：`ToolPipelineTests` 12、`ReactLoopAgentTests` 7、`ReactLoopInboxGateTests` 3、等。
- 8 处语义偏差全部有代码注释或 §2.7 勘误背书；其中 4 处（LLM 面走 `IAIProvider` 直连 / claim 前置 / ad-hoc 豁免 / spill 文本格式）经收尾修复已补记文档。

## 3. pilot 六目录审计证据（工程师，只读）

| pilot | 结论 | 关键证据 |
|---|---|---|
| 027 插件本地更新源 | ✅ 全落地 | `PluginVersionService.cs:333/341/416` 本地包扫描；`PluginController:791/807` |
| 028 打包升级备份 | ✅ 已提交 | 批次2+输入36-39 已随 **`81b9609`** 提交（原报告「未提交」状态滞后，已勘误） |
| batch-a 菜单路由 | ✅ 闭环 | `PluginController.cs:269` T1；移交项1/2 已闭环 |
| batch-b 供应商目录 | ❌ 未开工（与草案状态自洽） | `vendor_code`/目录文件 0 命中；`AiProvidersPanel.vue:543-545` 仍硬编码三值；E-1~E-4 勘误行号全准 |
| batch-c 目录大小插件 | ✅ 落地（1.1.2 演进覆盖） | `FolderScanService`+`FolderSnapshotService`+测试 17 例；`.trash` 归档实物丢失（见 `06-review.md`） |
| folder-scan-perf-1.1.2 | ✅ 落地 | 并行分片 `MaxParallelism=8` + 取消聚合 + 17 例测试 |

## 4. 全量文档滞后扫描证据（QA，只读）

- **漏改 21 处**（A 类 16 漏改 + B 类 5 需改写），涉及 9 个文档；**覆盖缺口 6 处**（新实体在 features/reference/glossary 未同步）。
- 合规命中 30+ 处（dsh 三部曲历史语境、SOP），AGENTS.md 与全部 README 零残留。
- 收尾修复后 grep 复核：8 个目标文件 11 处旧组件命中**全为退役/归档语境、0 处活描述**。

## 5. 文档收尾修复证据（lead 独立核对）

- `git status --short specs/` 为空 → specs/040/041/042 偏差注记（四差异/spill 文本/UnifiedUsage/ad-hoc 豁免/SyncAsync 改写/验收勾选）**此前已随改造批次入库**，本次零重复改动。
- `dsh-alignment-plan.md` §1 体检表刷新 100%、§8 补 B9、§9 六 checkbox 勾选；`dsh-alignment-tasks-b2-b8.md` §6 待明确 1/2/4/5 裁决回填；施工总览「九批次」；`host-capability-seams.md`「方案 B 已实施」。
- pilot 报告勘误：028 七处「未提交」补 `81b9609` 勘误（11 处命中）、batch-c 归档丢失、batch-a 移交项3 排查单、027 路径更正。
- **改动边界**：收尾批仅 `.md`，零代码改动、零 git 命令（`git status` 全量复核，代码改动均为前八批次遗留）。

## 6. 文档治理收敛证据（本会话收尾阶段）

- 新建 `docs/ai/pilot/dsh-alignment-b2-b9/` 承载本次会话全部上下文（00–07）。
- 原 `docs/01-architecture/dsh-alignment-*` 三份已迁移为本目录 `02-spec.md`/`03-plan.md`/`04-task.md`，原位置清空。
- `specs/040/041/042` 维持 `.gitignore` 忽略（不入库、逐渐废弃），要点摘要进 `02-spec.md`。

## 7. 复现命令

```bash
# 收官全量（需在 FORGESELF_DATA_ROOT 已设置环境下）
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
# 文档滞后 grep 复核
grep -rn "IAgentLoop\|ToolCallContext\|StepRunLoopService\|SaveMessageAsync" docs/
# 改动边界复核
git status --short
```
