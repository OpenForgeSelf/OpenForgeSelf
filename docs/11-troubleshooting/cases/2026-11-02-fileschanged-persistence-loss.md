---
id: 2026-11-02-fileschanged-persistence-loss
title: agent 执行回写记录 filesChanged 恒空——结构化文件清单在快照提取与自动留痕两段丢失
pc: PC-023
severity: P2
evidence: Verified
---

# Case：执行记录 filesChanged 断链（TODO 233）

- **日期**：2026-11-02
- **发现渠道**：用户 TODO.md P3 回报（「执行记录 filesChanged 结构化字段未持久化」）+ 战役批 B2① 复现测试（战役 `2026-11-02-maf-runtime-hardening`）。
- **现象**：待办一键交给内置 Agent，agent 真实调用 write_file 写了文件、任务推进到待验收；执行记录里 5 条「agent 执行回写」记录的 `filesChanged` 全部为空——「改了什么」从台账上消失，验收人无法从记录判断改动面。
- **根因（两段独立断点，叠加后恒空）**：
  1. **快照提取段**：`BuiltInAgentExecutionService.ExtractFilePaths` 用裸正则 `path["']?\s*[:=]\s*["']…` 直接扫 `AgentStepRun.ToolCallsJson`。而 ToolCallsJson 的落库形态是「轨迹列表序列化后再套一层 JSON 字符串」（每个 `StepToolCallTrace.ArgumentsSummary` 存原始参数字符串），双编码后内层引号全部转义成 `\"`，字节层面是 `path\\\":`——正则永不命中，静默返回空。
  2. **自动留痕段**：`TodoDispatchService.AppendAdvanceRecordAsync`（agent 执行回写）调用 `ITaskExecutionService.AppendSystemAsync` 时根本没有文件清单参数可传——手动路径 `AppendAsync` 一直支持 `filesChanged`，系统留痕路径签名里就没有这个字段，状态快照（`AgentStatusDto.FilesChanged`）里的数据被丢弃。
- **修复（B2① 三点）**：
  1. 新增 `Plugins/AIAgent/Services/ToolCallFileExtractor.cs`：先按 `List<StepToolCallTrace>` 结构化反序列化逐轨迹解析 ArgumentsSummary（主路径），失败再反转义跑宽松正则（兜底旧数据/截断形态）；
  2. `TaskExecutionService.AppendSystemAsync` + 接口补 `filesChanged` 参数，经 `FileChangeList.ToStorage` 归一落库（与手动路径同一条不变量）；
  3. `AppendAdvanceRecordAsync` 透传 `status.FilesChanged`。
- **复现红灯（先红后绿）**：`ForgeSelf.Api.Tests/Plugins/TodoTracker/BuiltInFilesChangedPersistenceTests.cs`（[Collection("XCode")] 真库，2 用例：快照提取 + 自动回写落库）。还原修复态实测红：2 失败/0 通过，失败原文 = `Expected snapshot.FilesChanged not to be empty`（日志 `.temp/tmp/b2-red-evidence.log`）；修复后 2/2 绿。
- **守卫（G1）**：`BuiltInFilesChangedPersistenceTests.cs`（active，进快档过滤集）。
- **同类扫查**：① `AppendSystemAsync` 全部 19 个调用点逐个过——仅「agent 执行回写」一个点携带文件数据（其余为状态流转留痕，本就无文件可丢），已修；② Plugins + 宿主侧全部 `new Regex/Regex.Matches` 逐个过——其余均为纯文本解析（分词/统计/命令守卫/设计扫描），非「双编码 JSON 提取」形态，无同类。
- **教训**：凡从落库 JSON 提取结构化字段，必须先反序列化再取值，禁止裸正则扫「序列化后再套字符串」的形态（双编码让引号全变 `\"`，正则必哑）；手动留痕与系统留痕是同一条数据不变量，接口参数面必须一并对齐，否则自动路径就是静默丢弃点。关联：B2②③（Run 收据/验收）复用同一提取器取证——`RunReceiptTests`/`RunVerifyGateTests` 一并守住了提取器。
