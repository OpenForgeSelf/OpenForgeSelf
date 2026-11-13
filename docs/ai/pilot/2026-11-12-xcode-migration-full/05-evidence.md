# 05 Evidence — 宿主入口强制 XCode Migration=Full

> 任务：`2026-11-12-xcode-migration-full`｜记录时间：2026-11-12｜证据只记实际发生，分级 Verified/Inferred/Unknown。

## E1 构建（AC1）

- 命令：`cd ForgeSelf.Api && dotnet build`（TEMP 指 `.temp\tmp`，日志 `.temp/logs/xcode-migration-build.log`）。
- 结果（日志正文）：**`0 个错误`**，1446 警告（全部为存量基线告警，LlmProxyHttp/UpdateService 等，与本变更无关）；耗时 14.45s。
- 判定：`XCodeSetting.Current.Migration` setter 与 `XCode.DataAccessLayer.Migration.Full` 编译通过——API 名称由编译器裁决，非推测。**Verified**。

## E2 时序与落点核对（AC3）

- 回读 `ForgeSelf.Api/Program.cs:41-62`：强制块位于 `ConfigUnifier.UnifyAllConfigFiles(...)`（:41-42，FileName 已重定向到 `{数据根}/config/`）之后、`--tray` 分支（:67）与 `AppBuilder.CreateWebApplication` 之前；`XCodeSetting.Current.Migration = XCode.DataAccessLayer.Migration.Full`（:53）→ 降级 `Save()`（:54-62，B9-5 风格 try/catch 告警）。**Verified**。
- testhost 早退分支（Program.cs:13-21）在本代码之前 return → WebApplicationFactory/测试夹具不经过，PC-014「Migration 保持 On」约定不受影响。**Verified（代码路径核对）**。
- RunTrayMode 由同一顶层语句流覆盖（顶层先于 `--tray` 分支执行）。**Verified（代码路径核对）**。

## E3 API 语义实证（Spec Business Rules）

- 来源①：宿主运行实例自写 `C:\Users\12504\.forgeself\config\XCode.config` 注释（NewLife.XCode 12.0.2026.701 渲染）：「反向工程。Off 关闭；ReadOnly 只读不执行；On 打开，仅新建；Full 完全，修改删除」。**Verified**。
- 来源②：nupkg `newlife.xcode.12.0.2026.701` → `lib/netstandard2.1/XCode.xml`：`P:XCode.XCodeSetting.Migration`（同文案）、`T:XCode.DataAccessLayer.Migration`（成员 Off/ReadOnly/On/Full）、`SQLiteMetaData.CheckTable(IDataTable,IDataTable,Migration,XCodeSetting)`（SQLite 重建表入口）、`DbMetaData.ResolveMigration`。**Verified**。探针目录用后已清理。
- 反射路径尝试失败（NewLife.Core 依赖加载），以官方 XML 文档为凭——已记入日记，不影响结论（XML 与 DLL 同构建产物）。

## E4 后端全量测试（AC2，中档门禁）

- 命令：`cd ForgeSelf.Api.Tests && dotnet test --no-build -m:1 --blame-hang --blame-hang-timeout 8m --blame-hang-dump-type mini`（TEMP=`.temp\tmp-t2`，日志 `.temp/logs/xcode-migration-fulltest4.log`）。
- 结果（日志正文）：**总 3738，通过 3712，失败 26，17m10s**（`失败! - 失败: 26，通过: 3712，…`）。blame 报「所有测试都已运行完毕」= 真实跑完非挂死。**Verified**。
- 基线对表（`​.temp/full-test1.log`，2026-11-07 16:13，总 3737 / 失败 29）：
  - 本次红 26 中 **23 个 ⊆ 基线红**（存量）。
  - **新增红 = 0 归属本批**：名单差集 3 个（`JsonNonAsciiEscapingGuardTests.生产代码_JSON写出路径…`、`McpStdioWindowsShimTests.StdioTransport_WindowsNpxShim…`、`OpenAIResponsesOutboundSchemaTests.Production_OutputTextDelta_Sites_DeclareItemId`）经核实全部归属**今晨未提交的 LLM 代理批**（工作区大量该批改动；守卫报错文本自证指认 `AnthropicProtocolBridge.cs:151`/`LlmProxyHttp.cs:101`——均 LLM 批文件；三者分别为静态源码扫描×2 + 子进程测试，与 Program.cs 启动配置零因果路径）。已记 TODO 转交。**Verified（名单差集 + 守卫指认文本）**。
  - 基线红消失 6 个（时序型波动，串行 vs 并行轮次差异），非本批引入。
- 判定：**AC2 ✅**（相对基线无本批新增红）。

## E5 启动冒烟（运行时实证）

- 命令：`pwsh scripts/dev-stack.ps1 -SkipBuild`（起源码宿主 :7301，数据根 `.temp\dev-stack\data`），完成后 `-Stop` 清理。
- 证据① XTrace 日志 `.temp/dev-stack/data/log/2026_11_12.log`：`18:05:10.977 02 N - XCode 反向工程强制为 Full（Migration=Full，新建+修改+删除）`——强制块真实执行。**Verified**。
- 证据② 同数据根 `config/XCode.config`：`<Migration>Full</Migration>`——Save 持久化真实落盘。**Verified**。
- 附注：第一次全量（多节点）MSBuild worker 集群崩溃 MSB4166×17（环境问题，非测试红）；清理 23 个空闲 MSBuild 节点 + `-m:1` 后复现正常。第 2/3 轮被墙钟误判误杀（testhost CPU 实测持续增长，实为正常推进），已记日记教训。
