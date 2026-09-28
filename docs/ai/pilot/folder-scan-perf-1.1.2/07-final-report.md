# AI-Native Pilot Result（最终汇报）

> 模板：`docs/18-templates/ai-pilot/07-final-report.tpl.md`｜task-id: `folder-scan-perf-1.1.2`｜2026-09-29

## 1. Repository Understanding

我确认了：本任务仓库基线与批次C 一致（`docs/ai/pilot/batch-c-folder-size-plugin/00-repository-understanding.md`），本任务只做增量核实——`FolderScanService.cs`（1.1.1 单线程 + 每文件 2 次元数据调用）、`FolderScanJobStore.cs`（非并发字典 + `job.Sync` 锁约定）、既有测试 12 用例、版本号两处。全部有当次读文件依据，未做常识推测（见 00）。

## 2. Selected Task

用户输入16（T34）：「当前实现与XCoder码神工具的实现有何差别？为什么码神工具统计文件夹大小这么快，当前实现这么慢？」→ A/B 实测定位两根因（per-file 2 次 stat、单线程），立项性能补丁 1.1.2；方案按输入17（T37-41）确立的格式规则落盘 `mini-task.md`，闸门1 经 T42 批复（并行度 min(核数,8)、绝对计时阈值、先提交 1.1.1 再开 1.1.2）。

## 3. Changed Files

`FolderScanService.cs`（流式枚举 + 并行分片 + 取消聚合修复）、`FolderScanServiceTests.cs`（+3 用例 + ITestOutputHelper）、`plugin.json`/`web/package.json`（1.1.2）、`docs/02-features/036…`（算法节）、`e2e/plugins/file-tools/file-tools.spec.ts`（D-3 夹具适配）、`docs/ai/pilot/folder-scan-perf-1.1.2/00~07`。

## 4. Validation

Build: `dotnet build`（插件）0 错误 ✅ Verified｜`pnpm run build`（插件 web）绿 ✅ Verified
Unit Test: `dotnet test --filter FolderScanServiceTests` **17/17**（计时用例实测 **32 ms** / 阈值 1500 ms）✅ Verified｜宿主 `pnpm run test` **482/482** ✅ Verified｜`pnpm run check` 0 error ✅ Verified
E2E: `npx playwright test e2e/plugins/file-tools --output=.pw-out-perf` **6/6** ✅ Verified｜截图读图 ✅ Verified（v1.1.2 徽标、耗时 10 ms、Cancelled 中间态 31.20 GB/76,393 文件、占比 ≤100、无告警行）

## 5. Evidence

`05-evidence.md`：全部条目 Verified；红灯→修复 2 例（并行取消被吞成 Completed → 令牌聚合修复；e2e 夹具窗口消失 → D-3 改扫真实大盘）；探针合规（仓库外、用完即删）。

## 6. Review

`06-review.md`：八问全 PASS｜Risk L1｜Minor×3（规范裁剪 vs 钩子冲突 D-1、绝对阈值环境敏感、一次瞬时 testhost 崩溃未定位）｜**Final Decision: APPROVED**（附条件：运行实例复验待用户启用 1.1.2）。

## 7. Risk

L1。代码层风险被精确值断言守卫；残留项 = 线上锚点树性能（Inferred，未直接实测）+ 运行实例复验未做，均由用户启用 1.1.2 闭合。

## 8. Problems Found

1. **流程工具冲突**：规范 §4 轻量裁剪（mini-task 单文件）被 pre-commit 钩子「00~07 八件齐全」强制否决——规范与工具打架，需裁定（记 TODO）。
2. **性能补丁的测试时序债**：提速 10-30× 后，所有依赖「扫描中窗口」的测试（e2e 取消用例）全部失效——性能优化必须同步审查**依赖慢行为的测试**。
3. 并行化把取消检查的盲区从「无」变成「分片 <256 条目时检查点不触发」——这类「粒度假设失效」是并行改造的典型暗坑，靠真实断言（非 mock）才抓到。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（偏差 D-1/D-2/D-3 均记录后修正 Plan） |
| Code → Test | PASS（真实红灯 2 例均走「分析→修根因→重跑」） |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 最重要的问题

**规范与工具的冲突无人裁定**：§4 说 mini-task 可以只写一个文件，钩子却要求八件齐全才能提交——两条规则各自「正确」，合起来逼着 agent 双轨落盘（同一内容写两遍）。规则体系缺一个「规则冲突时谁赢」的终审入口，这次是我临场规避，下次别人可能就地违反其一。

## 11. 下一步建议

只提一个：做一次「规则冲突清障」专项——全量 grep AGENTS.md/docs/04-standards 与 pre-commit 钩子/CI 脚本的实际强制，输出一张「声明 vs 强制」对照表，冲突处逐条拍板（本例：钩子豁免 mini-task，或废除 §4 裁剪），避免规范继续靠 agent 临场裁量维持一致。
