# Evidence — 2026-11-09-json-facade-unify

> 状态：**批1 已入库 / v2 未实施（闸门1 待批）**。本文件为 2026-11-12 分批归档时按工作区实况补记。

## 验收标准覆盖

- **AC1（ForgeJson 单测）**：批1 侧已达成——`ForgeJson.cs` 已入库（`ForgeSelf.Abstractions/ForgeJson.cs` 受版本控制），PC-027 转义守卫单测随 `49ec93e` 入库且过滤集绿；v2 扩展面（Parse 包装/WriterOptions/继承强制）以 v2 实施时实测为准，**当前未验证 = Unknown**。
- **AC2（守卫 v2 RED→GREEN，~800 处改写）**：**未实施**。批1 只做了 PC-027 默认转义收口（非 ASCII 转义一处类），未做全仓 JsonSerializer 直调收敛与守卫 v2。
- **AC3（build 0 error）**：批1 侧 Verified（全量 3773 用例编译运行，2026-11-11 全量日志）；v2 未实施不适用。
- **AC4（全量净新增红=0）**：批1 侧达成——全量 32 红=工作区存量/BOM（已修）/B5 红态，无批1 引入红（判责记录见 `.forgeself/memory/2026-11-11.md`）；v2 未实施不适用。
- **AC5（AC1-AC4 留 Verified 日志）**：本文件为归档补记，v2 实施时须按本节留全量日志。
- **AC6（verify-pilot-artifacts PASS）**：随 2026-11-12 归档入库时由 pre-commit 全量模式机械核验（00-07 八件 + 关键节）。

## 证据清单（实际产生）

| 项 | 等级 | 证据 |
|----|------|------|
| 批1（PC-027 转义收口 + ForgeJson）入库 | Verified | 提交 `49ec93e`（159 文件，含 ForgeSelf.Abstractions/ForgeJson.cs）；过滤集 425/425（`.temp/tmp/filtered-tests-20261111-r3.log`） |
| 全量基线判责 | Verified | 全量 3773/3741/32（17m46s，`.temp/tmp/full-tests-20261111.log`）；32 红判责=1 BOM（已修）+6 B5+1 瞬时+24 存量 |
| v2 守卫改写（~800 处） | Verified（未做） | 工作区无对应改动（git status，2026-11-12）；闸门1 未批 |

## 来源等级说明

快档（过滤集）+ 中档（全量）已跑；v2 的机械改写、守卫 RED→GREEN、全量兜底均未发生（Unknown/未实施）。
