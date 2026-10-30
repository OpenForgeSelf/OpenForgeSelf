# 05 Evidence：filetools-writefile-schema（2026-10-30）

> 全部验证为实跑实录；来源等级逐条标注。任务闭环 = PC-017（schema 可解析）+ PC-018（成功回报 ≠ 副作用落定）双缺陷。

## 验证矩阵

| # | 验证项 | 命令/动作 | 结果 | 等级 | 证据 |
|---|--------|-----------|------|------|------|
| 1 | 红灯（PC-017） | `dotnet test --filter ToolSchemaJsonGuardTests`（修复前） | 3 failed / 0 passed；`Expected depth to be zero…LineNumber 26` 与 :51888 生产报错逐字节一致 | Verified | `.temp/results-schema-guard/schema-guard-red.trx`、red-run3.log |
| 2 | 扫查唯一命中 | pwsh 全插件 verbatim schema 枚举 | total=74，fails=1（FileToolFunctions.cs:235 起始） | Verified | case PC-017 §4 |
| 3 | 绿灯（修复①后） | 同 #1 + FileTools 域过滤 | 32 passed / 0 failed，EXIT=0 | Verified | schema-guard-green.trx、green-run.log |
| 4 | 红灯（PC-018） | `FileToolsWritePersistTests`（stash 落盘修复后） | 3 failed / 1 passed（三个磁盘回读断言全红） | Verified | persist-red 日志 |
| 5 | 绿灯（修复②后） | 同 #3 过滤集 | 36 passed / 0 failed（含 4 个持久化用例），EXIT=0 | Verified | green-run3c.log（OOM 一次后重跑） |
| 6 | 发布 2.4.20 | `release-local.ps1 -Version 2.4.20 -Sign` | ALL DONE 125s；签名 失败 0；SHA256 `676a448b…` 一致；包内 FileTools 1.2.1 | Verified | release-2420.log、SHA256SUMS.txt、zip-probe-2420 |
| 7 | 升级→2.4.20 | 浏览器代点：检查更新→下载→重启并更新（含确认框） | 新宿主 PID 82960 = `versions\2.4.20.2610301232`；设置页徽标 v2.4.20.2610301232 | Verified | screenshots/live-51888/upgrade-2420-after-badge.png |
| 8 | fs 复测揭出零落盘 | fs write_file → read_file | success:true + writtenLength 29，但 read_file 文件不存在；pwsh 递归全仓零命中 | Verified | 本会话调用记录 + case PC-018 §1 |
| 9 | 源码缺写盘调用 | grep + `git show HEAD` | 工作区与 HEAD 均无 `WriteAllText`；文件仅 68df669 一个提交 | Verified | grep/git 输出 |
| 10 | 发布 2.4.21 | `release-local.ps1 -Version 2.4.21 -Sign` | ALL DONE 151s；签名 失败 0；SHA256 `e23fd9a8…` 一致；包内 FileTools 1.2.2 | Verified | release-2421.log、SHA256SUMS.txt、zip-probe-2421 |
| 11 | 升级→2.4.21 | 浏览器代点同 #7 | 新宿主 PID 43580 = `versions\2.4.21.2610301259`；徽标 v2.4.21.2610301259 | Verified | screenshots/live-51888/upgrade-2421-after-badge.png |
| 12 | fs 终验·新建+回读 | fs write_file(3 行) → read_file | 回读与写入逐字符一致；pwsh `Get-Content` 独立复核 127 字节内容一致 | Verified | 本会话调用 + pwsh 输出 |
| 13 | fs 终验·区间替换 | fs write_file start_line=2 end_line=2 → read_file | 第二行被替换、其余行原样保留 | Verified | 本会话调用记录 |
| 14 | fs 终验·必填校验 | fs read_file 漏 cwd/start_line | `缺少必填参数: cwd; 缺少必填参数: start_line`（schema 解析与校验生效的旁证） | Verified | 本会话调用记录 |
| 15 | fs 终验·exec 正负例 | `git status --short -- Plugins/FileTools` / `Remove-Item` | 正例 exit 0 真实 stdout（显示本批 2 个改动文件）；负例被 allowlist 正确拒绝 | Verified | 本会话调用记录 |
| 16 | 台账机器校验 | `ProblemClassRegistryTests` + `check-problem-classes.ps1` | 5/5 绿；18 PC 签名/allowlist/guards 全过 | Verified | 本会话输出 |
| 17 | 插件冒烟 e2e | playwright e2e/plugins/file-tools | 5 passed / 1 failed；失败=取消流（Running→Failed 应为 Cancelled），stash 对照实验证明为**既有红**非本批引入 | Verified | filetools-e2e*.log + 基线对照日志 |

## 未覆盖/风险

- file-tools 取消流 e2e 既有红未修（异根因，TODO 已登记 + 对照实验定责）。
- DesignSystem 拼接型 schema、ToolRegistry 注册期 eager 解析、FileTools 其余副作用工具扩面——均记 TODO 不阻塞。
- 快档门禁（过滤集 + 插件 e2e）；未跑中档（未碰宿主源码 ForgeSelf.Api/**）与深档（未碰 e2e 基建），符合 §5.6 触发表。
- 前端 pnpm check/test/design:check 未跑：本批零 ForgeSelf.Web/src 与 Plugins/*/web 改动（无前端变更面）。
