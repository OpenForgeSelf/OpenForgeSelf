# 05 Evidence（证据分级：Verified / Inferred / Unknown）

## 验证矩阵

| # | 验证项 | 命令/出处 | 结果 | 等级 |
|---|--------|-----------|------|------|
| 1 | 红灯复现（旧判据下拒绝语误判失败） | `dotnet test --filter AIProviderTestConnectionOkGuardTests`（改矩阵后）→ `.temp/test-red-provider-relax.log` | 3 失败 / 5 通过；`SaysOk("我无法按要求只回复指定词语。")=False` 即缺陷本体 | Verified |
| 2 | 绿灯（新判据矩阵） | `dotnet test --filter AIProviderTestConnectionReplyGuardTests` → `.temp/test-green-provider-relax.log` | 9/9 通过 | Verified |
| 3 | 构建 | `dotnet build ForgeSelf.Api.Tests.csproj` → `.temp/build-provider-relax.log` | 0 error（warning 均存量 CS8618/CS8614，非本批文件） | Verified |
| 4 | 中档全量后端测试（§5.6：改宿主源码触发） | `dotnet test`（全量）→ `.temp/test-full-provider-relax.log` | 3778 总 / 3753 过 / 25 失败；判责见下表 | Verified |
| 5 | 问题类机器校验 | `pwsh scripts/check-problem-classes.ps1` → `.temp/pc-check-provider-relax-r2.log` | OK：34 个 PC（含 PC-034）签名/allowlist/guards 全过 | Verified |
| 6 | 守卫的守卫（PC-034 落盘后） | `dotnet test --filter ProblemClassRegistryTests\|ProblemClassEnumerationTests` → `.temp/test-pcregistry-relax.log` | 8/8 通过 | Verified |
| 7 | 两条件外失败隔离复跑 | `dotnet test --filter MafApprovalLoopTests\|DesignAgentToolContractTests` → `.temp/test-isolate-2.log` | 10/10 通过（类序/共享态污染，非确定性红） | Verified |
| 8 | e2e ai-providers.spec.ts | 未跑（未触发深档：未改 e2e 基建/global-setup/fixtures；仅标题与注释两处文字，断言未动；mock `chatContent:'ok'` 在新判据下仍成功） | — | Unknown（明示未跑） |
| 9 | 真实模型走查（运行实例点「测试连接」） | 未做（不启停/不碰运行实例铁律 + 本地 dev-stack 走查属发布验收批） | — | Unknown（明示未跑） |

## 全量 25 失败判责表（对表 TODO 已登记基线）

| 失败族 | 数量 | 归属 | 依据 |
|---|---|---|---|
| ProjectWorkspaceDefaultRootTests | 15 | 既有基线（TODO 登记「ProjectWorkspace×15」） | 逐条同名对表 |
| WorkflowPlanningIntegrationTests | 6 | 既有基线（TODO 登记「WorkflowPlanning×6」） | 同上 |
| ScriptRunnerDiIntegrationTests | 1 | 既有基线（TODO 登记「ScriptRunnerDi×1」） | 同上 |
| JsonNonAsciiEscapingGuardTests | 1 | 在途批（TODO：仅剩 smart-terminal 批 2 个在途文件待清） | TODO P2 遗留条目① |
| MafApprovalLoopTests | 1 | 类序/共享态污染（隔离 10/10 绿） | `.temp/test-isolate-2.log` |
| DesignAgentToolContractTests | 1 | 同上 | 同上 |
| **归本批** | **0** | — | AIProvider 相关用例 0 失败；失败族与本批改动面零交集 |

## 改动文件清单（实测 git status）

- `ForgeSelf.Api/Services/AIProviderService.cs`（判据 + 注释，+16/-11 内含）
- `ForgeSelf.Api/Controllers/AIProviderController.cs`（注释 ×2）
- `ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionOkGuardTests.cs` → 删除；`AIProviderTestConnectionReplyGuardTests.cs`（新，9 例矩阵）
- `ForgeSelf.Web/e2e/ai-providers.spec.ts`（标题 + 注释 2 处，断言 0 改）
- `docs/02-features/001-ai-provider-config.md`（§3 判据/提取/守卫三 bullet 真源回写 + ExtractContent 陈旧描述勘误）
- `docs/11-troubleshooting/`：PC-034（新）+ case（新）+ problem-classes/README、cases/README、index.md 三处索引
- `docs/ai/pilot/2026-11-14-provider-test-relax/`（mini-task + 05/06/07）
- TODO.md / `.forgeself/memory/2026-11-14.md`（不入库文件）
