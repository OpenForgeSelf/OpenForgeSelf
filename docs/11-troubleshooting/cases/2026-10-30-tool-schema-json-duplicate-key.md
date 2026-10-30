---
id: 2026-10-30-tool-schema-json-duplicate-key
title: 插件工具 ParametersJsonSchema 重复键少闭合括号，该工具经网关的任何调用全挂
pc: PC-017
severity: P1
evidence: Verified
---
# Case：2026-10-30 万能工具 write_file 三连挂（schema 重复键）

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC-017，不复述。

## 1 现象

用户要求实测 fs 万能工具（mcp__fs__universal_tool）。`write_file` 三次调用（不同参数：多行中文内容/单行内容/不同路径）全部返回同一错误：
`{"Code":"INVALID_PARAMETERS","Message":"参数验证失败: 参数JSON解析失败: Expected depth to be zero at the end of the JSON payload. There is an open JSON object or array that should be closed. LineNumber: 26 | BytePositionInLine: 1."}`（:51888 实例，11:30-11:38 三次，时间戳见日记）。
同批对照：`read_file`、`list_files`、`run_terminal_command`（git status / pwsh Set-Content / Remove-Item 护栏负例）全部正常——问题被隔离到 write_file 单工具。

## 2 复现红灯

- **现场层（升级前实例）**：万能工具任意 write_file 调用必挂（上节原文，11:30/11:33/11:38 三次实测）。
- **测试层（红灯先行）**：新增守卫 `ForgeSelf.Api.Tests/Architecture/ToolSchemaJsonGuardTests.cs`，`dotnet test --filter FullyQualifiedName~ToolSchemaJsonGuardTests` → **3 failed / 0 passed**（trx：`.temp/results-schema-guard/schema-guard-red.trx`）。失败信息与生产逐字节一致：
  `System.Text.Json.JsonReaderException : Expected depth to be zero at the end of the JSON payload. There is an open JSON object or array that should be closed. LineNumber: 26 | BytePositionInLine: 1.`
  扫查守卫定位输出：`FileTools\Services\FileToolFunctions.cs:235 → schema 不是合法 JSON`（行 235 = ParametersJsonSchema 起始行）。

## 3 根因（5 Whys + 第 6 问）

1. 为什么 write_file 调用全挂？→ 网关在调用前解析该工具的参数模板（ParametersJsonSchema）失败，抛 INVALID_PARAMETERS。
2. 为什么解析失败？→ schema 字符串少一个闭合括号，读完整段 payload 时 depth≠0（恰在第 26 行）。
3. 为什么少一个闭合括号？→ `Plugins/FileTools/Services/FileToolFunctions.cs` 原 242-243 行 `"path": {` 键连写两行，第二个 `{` 多吃一层闭合。
4. 为什么会连写？→ 2026-10-29 两个会话并行改该文件发生坏合并（TODO 协作区登记过「write_file schema required 重复/缺逗号」），当天的「最小语法修复以解锁构建」修掉了其余重复/缺失括号，**漏掉这一处**。
5. 为什么构建和测试没拦？→ schema 是 verbatim 字符串，编译器只验字符串语法不验 JSON 内容；仓内没有「工具 schema 必须可解析」的任何测试（Sems/FinishTool 只各自验自家工具）。
- **第 6 问：为什么现有流程/守卫没让我一开始就写对？** 归因：①缺类级守卫——schema 内容从未被当契约测试（本批补 G2）；②修复撞车批的验收只跑到「构建绿」就停，缺一条「被修文件所在工具域重跑 schema 守卫」的收口动作（已写入 PC-017 lens 第 3 问）。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|-----------------|----------|-------------------------------|
| Plugins/FileTools/Services/FileToolFunctions.cs:242-243（修复前） | 是（本体） | 已修（删 243 重复行） |
| 其余 73 个 verbatim schema（全 Plugins/**/*.cs 枚举，2026-10-30 实测 total=74/fails=1） | 否 | 全部可解析，由 G2 守卫持续覆盖 |
| Plugins/DesignSystem/Agent/DesignToolIndex.cs（拼接型 schema，源码提取不到） | 形态同类、未验证 | TODO：守卫扩展实例级枚举（DesignTools 8 工具 + DesignToolKit 可构造性确认） |

## 5 修复与被否方案

修法 = PC-017 canonical fix：删 243 行重复键（`"path": {` 仅保留一个），`Plugins/FileTools/plugin.json` 1.2.0→1.2.1。被否方案见 PC-017 §3（网关降级放行会静默扩散坏契约）。

## 6 重构提案与结论

**R0（不重构）+ 一条 R1 级提案记 TODO**：ToolRegistry 注册期 eager 解析全部工具 schema，坏 schema 在宿主启动/插件加载即报错并点名工具（把失败从「调用期」提前到「注册期」，错误信息带工具名）。不动公共契约，独立小任务，不混入本批。

## 7 守卫落地

新增 G2 守卫 `ForgeSelf.Api.Tests/Architecture/ToolSchemaJsonGuardTests.cs`（三用例：全插件源码枚举解析 + file-tools 实例级 + write_file 五参锁），红灯→绿灯日志 `.temp/results-schema-guard/`（red-run3.log 3 failed → green-run.log 32 passed/0 failed，含 FileTools 域回归）。首见无逃逸。

## 8 逃逸分析（二次复发必做，首见可写「N/A 首次」）

N/A 首次。

## 9 基建改进

①全插件 schema 守卫入库（插件任务快档新增必跑项）；②TODO 登记：DesignSystem 拼接型 schema 实例级守卫扩展、ToolRegistry 注册期 eager 解析提案、file-tools 主链路 e2e 缺失补建（本批以「升级实例 fs 端到端复测」替代，正式 e2e 另立）。

## 10 复用赋能

新插件注册工具、宿主/插件间迁移工具、任何改 ParametersJsonSchema 的批——**必跑 `dotnet test --filter ToolSchemaJsonGuardTests`**（已入 PC-017 lens 与本 case §9）；新增 schema 若为拼接型，须同步扩展实例级枚举。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| write_file 三连挂且错误恒定 | Verified | 万能工具 11:30/11:33/11:38 实测返回体（日记 输入1） |
| 缺陷位置与形态 | Verified | FileToolFunctions.cs:242-243（修复前，git clean＝已提交源码） |
| 全仓扫查 74 个 schema 唯一命中 | Verified | pwsh 枚举探针输出 total=74 fails=1（case §4） |
| 红灯 3 failed，错误与生产逐字节一致 | Verified | .temp/results-schema-guard/schema-guard-red.trx + red-run3.log |
| 修复后 32/32 绿（守卫 3 + FileTools 域 29） | Verified | .temp/results-schema-guard/schema-guard-green.trx + green-run.log，EXIT=0 |
| fs 网关桥接 :51888 宿主（PID 34728） | Verified | run_terminal_command 父进程回溯（pwsh $PID → PPID=34728=netstat :51888 监听进程） |
