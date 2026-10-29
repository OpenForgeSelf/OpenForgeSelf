# 01 · Intent — 工具命名空间统一与归属重估

## Problem
1. **工具名无命名空间，存在 5 处实锤重名**：注册表按 `Name` 先到先得（ToolRegistry.cs:60），后注册者静默失效——`execute_workflow`/`get_workflow_status`/`list_workflows`、`format_json`/`compute_hash` 五个名字上，AIAgent/WorkflowEngine、DevTools/TextTools 各有两套实现抢一个名字，实际生效哪份取决于插件加载顺序，行为不可预期。
2. **归属错位**：AIAgent 插件注册了文件三件套（list_files/read_file/write_file），与 FileTools 插件的职责重叠；AIAgent 还注册了与 WorkflowEngine 完全重复的 3 个工作流工具（正是重名冲突源头）。能力工具散落在编排插件里，插件边界失真。
3. **项目目录硬绑定**：文件三工具强制依赖「当前选中的项目根」（未选目录即报错），路径参数只能是相对路径——无法操作登记项目之外的文件，与 FileTools 已有的「任意绝对路径」模式割裂。

## Why（用户指令 2026-10-28 输入1）
> 「各个插件的注册名，都加上自己插件的前缀，避免冲突。第二个是重新评估导出工具的归属，比如AIAgent导出这么多文件相关的，应该迁出，还有就是不应该绑定项目目录，依赖项目目录的应该改成参数或者参数传递绝对路径」

三层诉求 = **命名空间确定性**（消除加载顺序决定行为）、**插件职责单一**（能力归能力插件）、**参数化解耦**（工具不隐式依赖会话状态）。

## Expected Outcome
- 每个工具名都带插件前缀（平台保留名白名单除外），注册表零重名，且由机器守卫强制（新增工具忘带前缀 = 测试红）。
- 文件三工具归 FileTools（绝对路径参数化）；AIAgent 删除与 WorkflowEngine 重复的 3 个工具；每个工具只有一个归属插件。
- 旧工具名对外不立即断：注册表别名层兜底 + 存量 Agent 配置一次性迁移。
- 全量门禁绿（中档：宿主全量 dotnet test + 涉改插件冒烟 e2e）。

## Constraints
- 不改数据库结构（ConfigJson/PlanJson 是 JSON 字段内容迁移，非 schema 变更）。
- 不动外部 MCP 协议面（`universal_tool`/`mcp.<id>.<tool>` 命名空间、tools/list 契约）。
- 不动 ToolBridge 内部词表（独立词汇，不在注册表）。
- 不动 run_terminal_command 的 CWD 项目根门（用户未点名，记 not-taken 候选）。
- 高风险变更：闸门1 批准后才 Implement；分批落地，每批独立可验证、可回滚（git）。

## Success Criteria
1. 守卫测试断言：注册表内全部工具名 ∈（`<该插件 toolPrefix>_` 前缀名 ∪ 平台保留名白名单），且**零重名**。
2. 旧名经别名调用行为与迁移前一致（单测锚定）。
3. `file_read`/`file_write`/`file_list`（迁入 FileTools）以绝对路径参数工作，**不依赖项目根选择**（未选项目也能用，单测 + e2e 锚定）。
4. AIAgent 注册表贡献中不再有 execute_workflow/get_workflow_status/list_workflows。
5. 中档门禁全绿：宿主 `dotnet test` 无新增红 + 涉改插件冒烟 e2e 绿 + 前端 check/test 绿。
