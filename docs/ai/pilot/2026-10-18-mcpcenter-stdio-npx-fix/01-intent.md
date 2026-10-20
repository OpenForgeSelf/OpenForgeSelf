# 01-intent（stdio 外部 MCP npx 三层修复批，2026-10-18）

## 用户意图

原话：「又发现个问题，mcp配置npx @playwright/mcp@latest这个，报错，你先复现问题，增加对应测试复现，修复后再跑测试」。
澄清补充（AskUserQuestion）：操作路径 = MCP 中心插件·网关配置·新增外部 MCP（stdio, command=npx, args=@playwright/mcp@latest）→ 点「测试」失败；报错原文（:51888 实例，宿主 2.4.9.2610180248）：`请求失败(400): 无法启动外部 MCP 进程 'npx': An error occurred trying to start process 'npx' with working directory 'D:\src\tools\OpenForgeSelf\versions\2.4.9.2610180248'. 系统找不到指定的文件。`

即：**红灯测试先行复现 → 修复 → 回归测试**，并按缺陷类任务走 problem-class-closure + plugin-development 六步门禁。

## 成功判据（可验证）

1. 红灯锚：`McpStdioWindowsShimTests` 真实 npx 集成测，修复前失败输出与用户报错**逐字同款**，修复后绿；
2. 定向 dotnet test 全绿（新守卫/新测试文件 + 既有 McpCenter 套件）；
3. 插件 e2e mcp-center 套全绿，含新「stdio npx 真实世界」**零容忍**用例（必须 connected，禁止降级分支）+ UI 截图两张读图核验；
4. 门禁③：dev-stack 源码栈按**用户原文配置**（npx @playwright/mcp@latest，无 -y）API 实测 connected + /test success；
5. PC 归档：check-problem-classes 绿 + ProblemClassRegistryTests 绿；
6. 发布：release-local -Sign 产物含 McpCenter 2.5.1，签名/哈希校验过。

## 非目标

- 不做前端「帧型」下拉 UI（config 字段已预留，出现 auto 判不了的现场再加）；
- 不改宿主版本线（并行会话资产）；不动无关的 Chat/Multimodal/Workflow 域红灯（归因他批，登记不修）；
- 不停/启/杀用户 :51888 实例；门禁⑤只读复验待用户自行更新后发起。
