# Mini Task（轻量工件：Intent/Spec/Plan/Task 合并）

> Task ID：2026-10-19-aiagent-line-file-tools ｜ 日期：2026-10-19 ｜ 类型：feature（非缺陷，不走问题类闭环）
> 宿主版本载体：AIAgent 插件 1.9.1 → 1.9.2 → 1.9.3（plugin.json 单点，csproj 无 Version）

## Intent

用户原话（输入1）：「用最小路径实现读取两个工具的改造，读取可支持指定行数，即起始行，结果返回读取的内容以及总行数，写的话也是如此支持指定起始行数直接覆盖，两个工具都必须指定起始行，否则报错，描述里面说明用法以及参数格式。」
后续收拢（输入3/4）：read 的 end_line 选填（缺省到行尾，返回内容+总行数）；**write 必须带开始+结束行**（先读后写，行号必知）。
末段追加（输入6）：「万能工具不是可以搜索工具吗，将搜索说明放到万能工具描述里面，避免每次都直接 list_tool 而忽略了搜索」。

## Spec

范围仅 AIAgent 插件三个文件 + 对应单测：

1. **read_file**（`aiagent.read_file`）：`path`+`start_line` 必填（1 基，缺失/非正整数报错）；`end_line` 选填（含端点，缺省=文末，不得小于 start_line，超出钳到文末）；返回 `{success,path,startLine,endLine,content,totalLines,usedRoot}`。行口径：CRLF/CR 归一 LF 后按 `\n` 切分，尾换行产生的空段计入行数。
2. **write_file**（`aiagent.write_file`）：`path`+`content`+`start_line`+`end_line` 全必填（schema required 含 end_line）；只替换 start..end 闭区间，区间外行原样保留；文件不存在时新建（限 start_line=1）；start_line > 现有行数+1 报错；Description 写明「必须先 read_file 拿行号——先读后写」。
3. **universal_tool**：Description 与 `tool` 参数 schema 内置「找工具先搜索、后枚举」动线（search_tools {keyword,limit?} 打分检索 → 无果才 list_tools 兜底），与既有未知工具封套 ToolSearchHints 口径一致。
4. 不做兼容层（用户明示「先不考虑兼容，直接改」）；不动 `IProjectWorkspaceService` 契约；不动其他工具。

### 验收标准

- AC1 read_file 缺 start_line / 非正整数 → 参数错误封套（中文）。
- AC2 read_file 指定 start（+可选 end）→ 返回对应区间 content + totalLines + 生效 startLine/endLine；end 超文末钳制；end<start 报错。
- AC3 write_file 缺 start_line 或缺 end_line → 报错；区间替换保留区间外行（头部+尾部）；新建文件限 start=1；start 越界报错。
- AC4 universal_tool 描述含「先 search_tools 检索、后 list_tools 枚举」引导文案（Description + tool 参数 schema 两处）。
- AC5 定向单测全绿；AIAgent plugin.json 版本与 Description 同步（1.9.3）。

## Plan / Task

| 步骤 | 文件 | 动作 | 判定 |
|------|------|------|------|
| T1 | `Plugins/AIAgent/Services/ProjectFileToolFunctions.cs` | 两工具按行改造 + 新增 internal `FileToolLineOps`（SplitLines/TryReadStartLine/TryReadEndLine(required 参数化)） | 编译 0 错 |
| T2 | `ForgeSelf.Api.Tests/Plugins/AIAgentTests/ProjectWorkspaceDefaultRootTests.cs` | 更新既有用例 + 新增边界用例（缺参、end<start、钳制、区间替换保尾、缺 end_line） | dotnet test --filter ProjectWorkspaceDefaultRootTests 全绿 |
| T3 | `Plugins/AIAgent/Services/ToolFunctions/UniversalTool.cs` | Description + tool schema 文案（输入6） | 定向测试 UniversalTool|SearchTools 绿 |
| T4 | `Plugins/AIAgent/plugin.json` | 版本 1.9.3 + Description 追加各版说明 | JSON 合法 |
| T5 | 发布 | 按 plugin-publish-verify 技能：release-local -Sign -UpdateDir 发 2.4.12（含 1.9.1）、2.4.13（含 1.9.2），产物验真（SHA256/exe 版本/签名/L1/probe 串） | 验真全绿 |

Allowed：上述 4 文件 + 对接文档 `fs工具集HTTP-MCP调用文档-1.9.2.md`（仓根，用户要求落盘）+ 本工件目录。
Forbidden：宿主内核、其他插件、IProjectWorkspaceService 契约、兼容旧无 start_line 调用。
Rollback：git revert 本提交；发布产物不回滚（版本前进取新版）。
