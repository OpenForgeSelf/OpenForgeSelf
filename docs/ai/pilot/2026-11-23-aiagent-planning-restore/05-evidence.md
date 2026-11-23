# 05 Evidence（2026-11-23 · 全部 Verified 除标注外）

## 一、命令实录（快档门禁）
| 命令 | 结果 |
|------|------|
| `dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj` | exit=0，error 0（首跑 1 error CS0246 缺 Entities using，补 using 后绿） |
| `dotnet test --filter AIAgentPluginCompositionTests` | **4/4 通过**（组合根 arity 断言 / 真规划回写 / 无规划器回退+note / 确认后拒收） |
| `pnpm install && pnpm run build`（Plugins/AIAgent/web） | exit=0（vite build 绿，插件产物可构建） |
| `pnpm run test`（Plugins/AIAgent/web，本批新增 vitest 入口） | `src/assistant/session.logic.test.ts` **15/15 绿**（含新增 note 透传契约用例）；全套件 153 用例中 15 失败 + 10 套件加载失败，**归属=基线测试基建债**（见 §四） |
| `pwsh scripts/check-problem-classes.ps1` | 见 §三 |
| `pwsh scripts/check-commit-hygiene.ps1 -Staged` | 见 §三 |
| `pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-23-aiagent-planning-restore -EnforceHeader -EnforceAcCoverage -EnforceInteraction` | 见 §三 |

## 二、AC 核对
- **AC1**（planned 触发 LLM 规划回合、真计划回写）：✅ Verified — 测试 `StartPlanningAsync_WithGenerator_PublishesRealPlan` 绿（fake 规划器 2 步真计划回写，Note=null）；facade `PostMessage` planned 分支经 `SafeStart` 触发（源码读核）。
- **AC2**（失败回退+标注+拒收）：✅ Verified — 测试 `StartPlanningAsync_WithoutGenerator_FallsBackToDraftWithNote` 绿（note=「规划失败，已用回退计划」）+ `UpdatePlannedPlan_RejectedAfterConfirm` 绿；Warn 三路径（不可用/未产出/异常）+ 超时路径源码读核。
- **AC3**（组合根全显式 + DbAdmin 同形态）：✅ Verified — 测试 `CompositionRoot_ConstructsExecutor_WithExplicitOptionalArgs` 绿（源码断言 registry:/planGenerator:）；DbAdminPlugin `systemConnNames: DbConnectionService.DefaultSystemConnNames` 显式传参（源码读核）。
- **AC4**（note 前端贯通）：✅ Verified — vitest 契约用例 `passes through plan note (planning-fallback marker)` 绿；AgentWorkspace 计划卡 note 渲染 + build 绿（Inferred 级：实际页面渲染未走浏览器走查，见 §四）。

## 三、机器门禁结果（提交前实测补录）
- `check-problem-classes.ps1`：PASS（40 PC OK；PC-040 守卫改 guarded-partial 后仍绿）。
- `check-commit-hygiene.ps1 -Staged`：PASS（files/lines/hits 见提交时输出）。
- `verify-pilot-artifacts.ps1`（本目录）：PASS；`verify-pilot-feature-writeback.ps1 -TaskId 2026-11-23-aiagent-planning-restore`：PASS。

## 四、风险与未覆盖（Inferred / Unknown 分级）
- ⚠️ 插件 web 全套件首次在本 clone 可跑（本批补 test 脚本+vitest devDep），暴露存量红：12 断言失败（http.session/http/sessionArchive——会话归档与 SSE 契约漂移，疑丢失批相关面）+ 10 套件 vue 解析失败（缺 vitest 别名/依赖配置）。**归属=灾前基线/测试基建债，非本批引入**（本批未触碰上述文件）；已记 TODO 另批处理。
- ⚠️ 浏览器级走查（起源码起栈实点计划卡）未跑（Unknown）——note 渲染经 build+契约测试间接验证；插件六步闭环的 ②③⑤（冒烟 e2e/源码走查/运行实例复验）本批未执行，需用户安排。
- LLM 真规划的端到端实跑（真实模型出 submit_plan）未验证（Unknown）——规划回路依赖模型服务，单测用 fake 规划器锁行为；PlanGeneratorService 本体为基线存量代码未改。
