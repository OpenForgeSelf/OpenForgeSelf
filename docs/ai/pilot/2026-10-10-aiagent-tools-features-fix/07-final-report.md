# AI-Native Pilot Result（最终汇报）

> Task ID：2026-10-10-aiagent-tools-features-fix｜状态：代码与验证完成，未提交（等用户授权 commit）

## 1. Repository Understanding
见 00（插件服务双通道、工具注册链、usageStats 数据底座）。

## 2. Selected Task
用户输入2 的五项缺陷/需求。

## 3. Changed Files
13 文件（见 03-plan 表）+ 新增测试 2 个；版本 1.7.4/1.0.1 已 stage `publish/Plugins/{ai-agent,workflow-engine}/versions/`。

## 4. Validation
vue-tsc 0 err；eslint 0 err；vitest 765/765；dotnet filter 119/119（6 例基线红经 stash 对照确认与本次无关）；新回归 1/1；e2e all-features 5/5 + ai-agent 7/7。

## 5. Evidence
05-evidence（含 4 张走查截图与实测值）。

## 6. Review
APPROVED（06-review，2 条 Minor 遗留入 TODO）。

## 7. Risk
低：模型侧工具 +1（universal_tool 进默认作用域，031 设计本意）；显式勾选才放开全注册表。

## 8. Problems Found
- WorkflowPlanningIntegrationTests 6 例基线红（存量）；
- 031 真实 LLM e2e 依赖 51888；
- WorkflowEngine 工具类每次 new 服务（存量）。

## 9. Process Evaluation
根因先行→单问确认→实现→三域验证（单测/e2e/MCP 走查）；出树构建 + Tailwind 类覆盖检查沿用既有 SOP。

## 10. 最重要的问题
**工作流「服务没激活」的根因是跨插件契约只进 IServiceCollection 不进共享表**——同类隐患（插件间 ctx.Get 依赖）值得在 plugin-development 技能中固化为检查项。

## 11. 下一步建议
① 51888 实例启动后做只读复验 + 031 场景 2-5 复跑；② 授权后分两批提交（宿主导航 / AIAgent+WorkflowEngine）；③ plugin-development 技能补「兄弟插件契约须 ctx.Register」铁律。
