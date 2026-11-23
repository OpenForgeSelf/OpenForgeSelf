# 05 Evidence — update-restart-socket-10013

- **task**: 2026-11-21-update-restart-socket-10013
- **date**: 2026-11-21（2026-11-23 灾后重建批同口径复验）

## AC 覆盖表

| AC | 结论 | 证据（Verified 除非注明） | 等级 |
| --- | --- | --- | --- |
| AC1 AccessDenied 有界重试吸收 | ✅ `AccessDenied_前几次失败_有界重试后成功`：Attempts=3，OK 返回 | dotnet test 过滤集 | Verified |
| AC2 耗尽中文诊断/透传/深层识别 | ✅ 三用例：6 次耗尽抛含 `net stop winnat`；TimedOut Attempts=1；嵌套链 AccessDenied 走满 6 次 | 同上 | Verified |
| AC3 wiring 自愈层在管线 | ✅ `默认client管线_首层为自愈层且终级handler仍挂ModelProxy`（字段遍历手法同 HttpClientProxyWiringTests） | 同上 | Verified |
| AC4 进程内全流程 E2E | ✅ `RunAsync_全流程_落位切指针清理并重启`：落位/合并覆盖/旧版本目录不动/current/保留 current+上一版/扁平清理/staged+zip 清理/exit 0 | 同上 | Verified |
| AC5 同版本重装/嵌套拒绝/缺 versions | ✅ 三用例全绿（robocopy 覆盖不炸；`*逐代嵌套*`；`*versions*` 且不落位） | 同上 | Verified |
| AC6 PC-041 三处同源 + 排序语义 | ✅ RepositoryScriptTests 守卫 + ToSortKey/Compare/SortNewestFirst/null 空串共 6 用例 | 同上 | Verified |
| AC7 ps1 随包守卫 + 文档同步 | ✅ 守卫断言 ps1 在位；packaging §1.2/§1.8.2/§3/变更记录、scripts/README×3 行、index :53、PC-041、case、cases/problem-classes 台账行 | 文件盘点 | Verified |
| 中档全量对基线 | ✅ 首批 2026-11-21 全量 3956 总/3924 过/32 败（15 PWD+6 WF+3 Oar+7 单发），失败名集合与晨跑基线一致，DesignSystem 1 红属并行流 | `.temp/fulltest-20261121-2242.log` | Verified（首批）；重建批=过滤集复验 + 全量待跑 | Verified/部分 |
| 构建 | ✅ `dotnet build ForgeSelf.slnx` 零警告新增 | 构建日志 | Verified |

## 未覆盖（Unknown）

- 真机 exe 全流程更新（用户下版动线）——Inferred 可行（进程内 E2E 全链 + 模拟布局），实机待验证；
- 10013 用户机根治操作（`net stop winnat` / 挪动态端口段）——用户机操作，agent 不可执行。
