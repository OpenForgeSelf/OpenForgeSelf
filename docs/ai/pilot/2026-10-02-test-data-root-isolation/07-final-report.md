# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜状态只报事实，禁止模糊表述。

## 1. Repository Understanding

宿主 + 插件架构；数据根由 `DataLocationService` 统一解析（`FORGESELF_DATA_ROOT` → Development `{BaseDirectory}/data` → 生产 `%UserProfile%/.forgeself`）；NewLife `Config<T>` 默认把配置文件放程序目录 `Config\{Name}.config`，由 `ConfigUnifier` 统一重定向。测试项目此前无进程级环境隔离入口。

## 2. Selected Task

`2026-10-02-test-data-root-isolation`（轻量档）：测试侧自动隔离，杜绝 `dotnet test` 污染真实宿主根 `~/.forgeself`。

## 3. Changed Files

- `ForgeSelf.Api.Tests/TestDataRootIsolation.cs`（新增）
- `ForgeSelf.Api.Tests/TestDataRootIsolationGuardTests.cs`（新增）
- `docs/04-standards/agent-workflow.md`（§B12 第 830 行 + Part C）
- `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/_ProbeToCssNoDb.cs`（删除）
- bin `Config/` 残留产物（清理，未入库）

## 4. Validation

- 守卫测试：`失败 0 / 通过 4`（Verified）
- 隔离实证：隔离根 `.temp\dotnet-test\20261002-120925-73848`，`Core.config` 的 `LogPath` 指向隔离根（Verified）
- bin 两处 `Config/` 为空，不再生成（Verified）
- 回归过滤集：`失败 6 / 通过 21`，失败集与修复前基线**完全一致**（无新增红）（Verified）

## 5. Evidence

详见 `05-evidence.md`：根因证据链 4 条 + 修复验证矩阵 + 回归对表（均 Verified）。

## 6. Review

`06-review.md` Final Decision = **APPROVED**（八问全 PASS；Major 项=宿主侧顺序缺陷，范围外已记 TODO）。

## 7. Risk

L1：测试基建改动，无生产代码变更；宿主侧顺序缺陷（`Program.cs:38` / `AppBuilder.cs:89` 的 `Save()` 早于 `ConfigUnifier`）仍在——已列 Known Limitations 并记入 TODO。

## 8. Problems Found

- 宿主侧顺序缺陷（关联，范围外）
- 隔离目录每次运行新建、不自动清理（Minor）

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（Plan 偏差已记录：单设数据根不足，需三件套） |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 最重要的问题

「只设 `FORGESELF_DATA_ROOT`」不足以隔离——程序目录残留一份固化宿主 `LogPath` 的 Core.config 即可绕过隔离；且 `XTrace` 与 `ConfigUnifier` 的**执行顺序敏感**。二者均为运行期实证才发现，是本次最有价值的工程结论（已沉淀 agent-workflow.md §B12）。

## 11. 下一步建议

1. 用户验收（闸门2）后提交（闸门3）。
2. 另立单处理宿主侧顺序缺陷（`Program.cs:38` / `AppBuilder.cs:89` 的 `Save()` 调到 `ConfigUnifier` 之后），使生产 publish 程序目录不再残留 Core.config。
3. 恢复 M2（showroom-wizard）推进。