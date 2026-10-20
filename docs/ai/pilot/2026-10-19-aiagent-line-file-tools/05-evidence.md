# Evidence

> 阶段：Stage 7（Execute → Verify）。只记录实际发生的结果。
> 每条证据标注出处等级：Verified（真实跑过命令）/ Inferred / Unknown。

## Task

PILOT 2026-10-19-aiagent-line-file-tools（AIAgent 1.9.1–1.9.3：read_file/write_file 按行区间改造 + universal_tool 内置先搜索后枚举动线）

## Changed Files

- `Plugins/AIAgent/Services/ProjectFileToolFunctions.cs` — read_file/write_file 按行语义（详见 mini-task.md Spec）；新增 internal `FileToolLineOps`（SplitLines / TryReadStartLine / TryReadEndLine，end 的 required 参数化：read=false、write=true）
- `ForgeSelf.Api.Tests/Plugins/AIAgentTests/ProjectWorkspaceDefaultRootTests.cs` — 既有用例补 start_line/end_line 实参；新增 ReadFile_MissingStartLine_Fails、ReadFile_WithEndLine_ReturnsClosedRangeAndClamps、ReadFile_EndLineBeforeStartLine_Fails、WriteFile_MissingStartLine_Fails、WriteFile_MissingEndLine_Fails、WriteFile_WithEndLine_ReplacesRangeAndKeepsTail 等
- `Plugins/AIAgent/Services/ToolFunctions/UniversalTool.cs` — Description 末尾 + tool 参数 schema description 内置「先 search_tools 检索、后 list_tools 枚举」
- `Plugins/AIAgent/plugin.json` — 1.9.0→1.9.1→1.9.2→1.9.3，Description 逐版追加
- `fs工具集HTTP-MCP调用文档-1.9.2.md`（仓根）— 对接方文档按新契约补写

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~ProjectWorkspaceDefaultRootTests"
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~UniversalTool|FullyQualifiedName~SearchTools"
```

Result: **ProjectWorkspaceDefaultRootTests 32/32 绿**（1.9.2 收拢后终跑）；**UniversalTool|SearchTools 定向全绿**（1.9.3）。Verified。

```text
过程如实记录：① 首轮 28/28 → 补 end_line 后 31/31 → write end 改必填后 32/32；
② 一处断言修正（"L2\nL3" → "L2\nL3\n"，Join 不剪尾行空段，实现正确、断言写错）；
③ 构建/测试曾被自家 dev-stack 宿主锁 DLL（MSB3027）→ dev-stack.ps1 -Stop 解除，测后恢复 :7301。
```

## AC 逐项判定

- AC1 **Verified**（ReadFile_MissingStartLine_Fails；TryReadStartLine 对缺失/非正整数返回中文错误封套）
- AC2 **Verified**（ReadFile_FromStartLine2_ReturnsTailAndTotalLines + ReadFile_WithEndLine_ReturnsClosedRangeAndClamps（含 end=99 钳到文末）+ ReadFile_EndLineBeforeStartLine_Fails）
- AC3 **Verified**（WriteFile_MissingStartLine_Fails + WriteFile_MissingEndLine_Fails + WriteFile_WithEndLine_ReplacesRangeAndKeepsTail（A1..E5 换 2..4 → A1/X/Y/E5, totalLines=4）+ WriteFile_FromStartLine2_KeepsHeadAndOverwritesTail + 新建限 start=1 分支）
- AC4 **Verified**（UniversalTool.cs:32-33 Description 与 :41 tool schema 两处文案，读文件核实）
- AC5 **Verified**（上节两条 dotnet test 全绿；plugin.json Version=1.9.3 + Description 含 v1.9.1/1.9.2/1.9.3 三段说明）

## 发布与产物验真（T5）

- 2.4.12（含 AIAgent 1.9.1）：`release-local.ps1 -Version 2.4.12 -Sign -UpdateDir D:\src\my-proj\OpenForgeSelf\updates`，ALL DONE 168s。验真：SHA256 8ad58c…d642 逐字对、两 exe FileVersion=2.4.12.2610191517、签名 Valid + Sectigo 时间戳、L1=包内 20 个 plugin.json、probe-dll-string 实到 start_line 串。Verified。
- 2.4.13（含 AIAgent 1.9.2）：同流程，ALL DONE 163s。验真：SHA256 72106c…1789 逐字对、exe FileVersion=2.4.13.2610191553、签名 Valid、L1=20、包内 AIAgent plugin.json=1.9.2、probe 实到 end_line 串。Verified。
- 1.9.3（universal_tool 文案）**未打包**——本地更新源最新仍 2.4.13；待用户拍板随下一版发布。

## Known Limitations

- `:18890` 运行实例未更新到 2.4.13 前，对接文档中新参数成功路径标【待验】（契约按 1.9.2 实现源撰写）。
- 行口径含尾换行空段（"L1\n" 计 2 行），已写入文档 §4，对接方以 totalLines 为准。
