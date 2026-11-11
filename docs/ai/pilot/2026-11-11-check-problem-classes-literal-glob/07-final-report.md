# Final Report — check-problem-classes 字面路径 glob 短路（lite）

> 收口：2026-11-11

## 状态

**✅ COMPLETED**

## 任务

修复 `check-problem-classes.ps1` 对字面路径签名 glob 误走全仓 `-Recurse` 遍历导致单类卡 10min+ 的性能缺陷（本次分批提交任务执行中暴露；pre-commit 对 docs/11-troubleshooting 提交同样触发）。

## 完成内容

- `Get-FilesByGlob` 头部加字面路径短路（无 `*`/`{` → `Get-Item` 单文件），通配路径逻辑不动。

## Validation

- 修复前基线：log 停在 PC-022 完成 25min+、CPU 近零 ✅ Verified（`.temp/tmp/check-problem-classes-final.log`）。
- 修复后全量：**38.6s，27 个 PC 全部 OK** ✅ Verified（`.temp/tmp/check-problem-classes-fixed.log`）。
- 中档全量 dotnet test（含 scripts 守卫 RepositoryScriptTests）：随 2026-11-11 批次执行（见当日日记）⚠️/✅ 以日志为准。

## Review 结论

见 `06-review.md`：**APPROVED**。

## 问题类归档 / 重构提案 / 悬空教训

- **问题类归档**：一次性脚本性能缺陷，非复发类，不立 PC（台账不变）；证据留本目录。
- **重构提案**：无（8 行内修复，无重构价值）。
- **悬空教训**：教训已落脚本注释（「字面路径短路，避免 -Recurse 全仓遍历」）；「守卫工具自身的耗时也是守卫成本」记入日记，无悬空项。

## 风险与结论

风险：无已知。可交付，随本批第 5 个提交（fix(scripts)）归档。
