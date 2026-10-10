# Intent

> 阶段：Stage 1｜Task ID：2026-10-10-aiagent-tools-features-fix｜日期：2026-10-10
> 闸门1 确认方式：用户报障清单即任务指令；根因定位后经对话向用户汇报（含第 4 项方案选择问题，用户选「常驻标签并入标签栏」），随后以「继续」两次驱动实现——视为已批。

## Problem
用户报 5 项缺陷/需求（输入2）：
1. AIAgent composer 工具列表缺 universal_tool（万能工具）、run_terminal_command（执行命令）、todo 工具；
2. Agent 编辑对话框「工具」是纯文本输入框，无候选下拉；
3. AIAgent 调用工作流工具报「服务没激活」类错误；
4. 「所有功能」页排序需与首页一致（频次高在前、钉住最前），卡片增加钉住操作，钉住项固定显示在顶部导航栏；
5. 右上角「所有功能」左侧增加插件图标直达插件管理页。

## Why
①②③ 是功能缺陷：工具类已写好但从未注册进 ToolRegistry / 无候选数据源 / 跨插件契约解析断链；④⑤ 是导航一致性与入口效率改进。

## Expected Outcome
见 02-spec 各需求验收；五项全部可验证。

## Constraints
- 0 自定义 token（--el-* + color-mix）；导航统一 openPage；不定义新 WS 事件词。
- 不停/启用户运行实例（51888 当前本就未运行）；默认挂载作用域不扩大（防小模型 prompt 撑爆）。

## Success Criteria
- 工具列表含 universal_tool / run_terminal_command / todo_*（实测 112 个全插件工具）；
- Agent 编辑工具为下拉多选且可选保存；
- WorkflowEnginePlugin.Apply 后 ctx 可解析 IWorkflowService/Executor/Scheduler（回归测试）；
- 所有功能页排序=钉住→热度→原顺序，钉住项进顶部导航栏；顶栏有插件入口；
- 全部门禁绿（vue-tsc/eslint/vitest/dotnet test/相关 e2e）。
